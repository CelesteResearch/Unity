#include "fmod_studio.hpp"
#include "fmod_output.h"
#include "NativeBankFileIO.h"
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <memory>
#include <unordered_map>
#include <emscripten.h>
#include <emscripten/threading.h>

namespace
{
    constexpr unsigned int Channels = 2;
    constexpr unsigned int BlockFrames = 512;
    constexpr unsigned int CapacityFrames = 2048;

    struct alignas(16) OutputRing
    {
        std::atomic<unsigned int> write{0};
        std::atomic<unsigned int> read{0};
        std::atomic<unsigned int> running{0};
        std::atomic<unsigned int> wakeEpoch{0};
        std::atomic<unsigned int> fault{0};
        unsigned int reserved = 0;
        unsigned int capacity = CapacityFrames;
        unsigned int channels = Channels;
        float samples[CapacityFrames * Channels]{};
    };
    static_assert(offsetof(OutputRing, samples) == 32);
    static_assert(sizeof(std::atomic<unsigned int>) == 4);
    static_assert(std::atomic<unsigned int>::is_always_lock_free);

    struct OutputOwner
    {
        unsigned int sampleRate;
        OutputRing ring;
        FMOD_OUTPUT_DESCRIPTION description{};
        FMOD::Studio::System* studio = nullptr;
        FMOD::System* core = nullptr;
        unsigned int plugin = 0;
        bool registered = false;
        bool initialized = false;
        bool nativeReleased = false;
        std::atomic<bool> nativeClosed{true};
    };

    // Registry and pending initialization are used only by the owning JS runtime thread.
    // Mixer callbacks borrow their own state->plugindata; no registry, lock, or allocation in Mix.
    std::unordered_map<unsigned int, std::unique_ptr<OutputOwner>> owners;
    OutputOwner* pending = nullptr;
    thread_local OutputOwner* configuring = nullptr;

    struct FileIOBinding
    {
        FMOD::Studio::System* studio = nullptr;
        bool installed = false;
        bool initialized = false;
    };
    std::unordered_map<unsigned int, FileIOBinding> fileIOBindings;
    unsigned int pendingFileIO = 0;

    void Wake(OutputRing& ring)
    {
        ring.wakeEpoch.fetch_add(1, std::memory_order_release);
        emscripten_futex_wake(&ring.wakeEpoch, 1);
    }

    void StopRing(OutputOwner* owner)
    {
        if (!owner)
            return;
        owner->ring.running.store(0, std::memory_order_release);
        Wake(owner->ring);
    }

    OutputOwner* CallbackOwner(FMOD_OUTPUT_STATE* state)
    {
        return state->plugindata ? static_cast<OutputOwner*>(state->plugindata) : configuring;
    }

    FMOD_RESULT F_CALL DriverCount(FMOD_OUTPUT_STATE*, int* count)
    {
        *count = 1;
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL DriverInfo(FMOD_OUTPUT_STATE* state, int, char* name, int length, FMOD_GUID* guid, int* rate,
                                  FMOD_SPEAKERMODE* mode, int* channels)
    {
        auto* owner = CallbackOwner(state);
        if (!owner)
            return FMOD_ERR_UNINITIALIZED;
        if (name && length > 0)
            name[0] = '\0';
        if (guid)
            *guid = {};
        if (rate)
            *rate = static_cast<int>(owner->sampleRate);
        if (mode)
            *mode = FMOD_SPEAKERMODE_STEREO;
        if (channels)
            *channels = Channels;
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL Initialize(FMOD_OUTPUT_STATE* state, int, FMOD_INITFLAGS, int* rate, FMOD_SPEAKERMODE* mode,
                                  int* channels, FMOD_SOUND_FORMAT* format, int, int*, int*, void*)
    {
        if (!configuring)
            return FMOD_ERR_UNINITIALIZED;
        state->plugindata = configuring;
        configuring->nativeClosed.store(false, std::memory_order_release);
        *rate = static_cast<int>(configuring->sampleRate);
        *mode = FMOD_SPEAKERMODE_STEREO;
        *channels = Channels;
        *format = FMOD_SOUND_FORMAT_PCMFLOAT;
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL Start(FMOD_OUTPUT_STATE* state)
    {
        auto* owner = static_cast<OutputOwner*>(state->plugindata);
        owner->ring.running.store(1, std::memory_order_release);
        Wake(owner->ring);
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL Stop(FMOD_OUTPUT_STATE* state)
    {
        StopRing(static_cast<OutputOwner*>(state->plugindata));
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL Close(FMOD_OUTPUT_STATE* state)
    {
        // Native callbacks have no authority to free a ring still borrowed by AudioWorklet.
        const auto result = Stop(state);
        auto* owner = static_cast<OutputOwner*>(state->plugindata);
        if (owner)
            owner->nativeClosed.store(true, std::memory_order_release);
        return result;
    }

    FMOD_RESULT F_CALL Mix(FMOD_OUTPUT_STATE* state)
    {
        auto& ring = static_cast<OutputOwner*>(state->plugindata)->ring;
        const auto wake = ring.wakeEpoch.load(std::memory_order_acquire);
        if (!ring.running.load(std::memory_order_acquire))
            return FMOD_OK;
        const auto write = ring.write.load(std::memory_order_relaxed);
        const auto read = ring.read.load(std::memory_order_acquire);
        if (write - read > CapacityFrames - BlockFrames)
        {
            // Stop/consumer changes the wait word, so an earlier notify cannot strand this waiter.
            emscripten_futex_wait(&ring.wakeEpoch, wake, std::numeric_limits<double>::infinity());
            return FMOD_OK;
        }
        const auto result =
            state->readfrommixer(state, ring.samples + (write & (CapacityFrames - 1)) * Channels, BlockFrames);
        if (result != FMOD_OK)
        {
            ring.fault.store(static_cast<unsigned int>(result), std::memory_order_relaxed);
            return result;
        }
        ring.write.store(write + BlockFrames, std::memory_order_release);
        return FMOD_OK;
    }

    void ConfigureDescription(OutputOwner& owner)
    {
        auto& output = owner.description;
        output.apiversion = FMOD_OUTPUT_PLUGIN_VERSION;
        output.name = "Shared PCM output";
        output.version = 0x00010000;
        output.method = FMOD_OUTPUT_METHOD_MIX_DIRECT;
        output.getnumdrivers = DriverCount;
        output.getdriverinfo = DriverInfo;
        output.init = Initialize;
        output.start = Start;
        output.stop = Stop;
        output.close = Close;
        output.mixer = Mix;
    }

    struct ConfigurationScope
    {
        OutputOwner* previous;
        explicit ConfigurationScope(OutputOwner* owner) : previous(configuring)
        {
            configuring = owner;
        }
        ~ConfigurationScope()
        {
            configuring = previous;
        }
    };
}

extern "C" FMOD_RESULT RealStudioInitialize(FMOD::Studio::System*, int, FMOD_STUDIO_INITFLAGS, FMOD_INITFLAGS,
                                            void*) __asm__("__real__ZN4FMOD6Studio6System10initializeEijjPv");
extern "C" FMOD_RESULT WrappedStudioInitialize(FMOD::Studio::System*, int, FMOD_STUDIO_INITFLAGS, FMOD_INITFLAGS,
                                               void*) __asm__("__wrap__ZN4FMOD6Studio6System10initializeEijjPv");
extern "C" FMOD_RESULT RealStudioRelease(FMOD::Studio::System*) __asm__("__real__ZN4FMOD6Studio6System7releaseEv");
extern "C" FMOD_RESULT WrappedStudioRelease(FMOD::Studio::System*) __asm__("__wrap__ZN4FMOD6Studio6System7releaseEv");

extern "C" EMSCRIPTEN_KEEPALIVE int PrepareOutput(unsigned int token, unsigned int sampleRate)
{
    if (!token || sampleRate < 8000 || sampleRate > 192000)
        return FMOD_ERR_INVALID_PARAM;
    if (pending)
        return FMOD_ERR_BADCOMMAND;
    const auto found = owners.find(token);
    if (found != owners.end())
    {
        auto* owner = found->second.get();
        if (owner->nativeReleased)
            return FMOD_ERR_INVALID_HANDLE;
        if (owner->initialized)
            return FMOD_ERR_INITIALIZED;
        if (owner->sampleRate != sampleRate)
            return FMOD_ERR_INVALID_PARAM;
        pending = owner;
        return FMOD_OK;
    }
    auto owner = std::make_unique<OutputOwner>();
    owner->sampleRate = sampleRate;
    ConfigureDescription(*owner);
    auto* created = owner.get();
    owners.emplace(token, std::move(owner));
    pending = created;
    return FMOD_OK;
}

extern "C" EMSCRIPTEN_KEEPALIVE uintptr_t GetOutputRing(unsigned int token)
{
    const auto found = owners.find(token);
    if (found == owners.end() || !found->second->initialized || found->second->nativeReleased)
        return 0;
    return reinterpret_cast<uintptr_t>(&found->second->ring);
}

extern "C" EMSCRIPTEN_KEEPALIVE int CelesteBankIO_PrepareOwner(unsigned int token)
{
    const auto found = fileIOBindings.find(token);
    const auto result = !token                                                       ? FMOD_ERR_INVALID_PARAM
                        : pendingFileIO                                              ? FMOD_ERR_BADCOMMAND
                        : found != fileIOBindings.end() && found->second.initialized ? FMOD_ERR_INITIALIZED
                                                                                     : FMOD_OK;
    if (result != FMOD_OK)
    {
        // A failed preparation is not followed by initialize; discard both unconsumed loans.
        pending = nullptr;
        pendingFileIO = 0;
        return result;
    }
    pendingFileIO = token;
    return FMOD_OK;
}

extern "C" EMSCRIPTEN_KEEPALIVE int ReleaseOutput(unsigned int token)
{
    const auto found = owners.find(token);
    if (found == owners.end())
        return FMOD_OK;
    auto* owner = found->second.get();
    if (owner->studio && !owner->nativeReleased)
        return FMOD_ERR_NOTREADY;
    if (pending == owner)
        pending = nullptr;
    // JS calls this only after the associated Worklet has stopped reading, or its context has closed.
    owners.erase(found);
    return FMOD_OK;
}

extern "C" FMOD_RESULT WrappedStudioInitialize(FMOD::Studio::System* target, int channels,
                                               FMOD_STUDIO_INITFLAGS studioFlags, FMOD_INITFLAGS flags, void* extra)
{
    auto* owner = pending;
    pending = nullptr;
    const auto fileToken = pendingFileIO;
    pendingFileIO = 0;
    if (!owner && !fileToken)
        return RealStudioInitialize(target, channels, studioFlags, flags, extra);
    if (owner)
    {
        if (owner->studio && owner->studio != target)
            return FMOD_ERR_INVALID_HANDLE;
        for (auto& entry : owners)
            if (entry.second.get() != owner && entry.second->studio == target)
                return FMOD_ERR_INITIALIZED;
    }
    FileIOBinding* fileBinding = nullptr;
    if (fileToken)
    {
        const auto existing = fileIOBindings.find(fileToken);
        if (existing != fileIOBindings.end() && existing->second.studio && existing->second.studio != target)
            return FMOD_ERR_INVALID_HANDLE;
        for (auto& entry : fileIOBindings)
            if (entry.first != fileToken && entry.second.studio == target)
                return FMOD_ERR_INITIALIZED;
        fileBinding = &fileIOBindings[fileToken];
        fileBinding->studio = target;
    }
    if (owner)
        owner->studio = target;
    ConfigurationScope scope(owner);
    FMOD::System* core = nullptr;
    auto result = target->getCoreSystem(&core);
    if (owner)
        owner->core = core;
    if (result == FMOD_OK && fileBinding && !fileBinding->installed)
    {
        result = static_cast<FMOD_RESULT>(CelesteBankIO_Install(fileToken, reinterpret_cast<FMOD_SYSTEM*>(core)));
        if (result == FMOD_OK)
            fileBinding->installed = true;
    }
    if (result == FMOD_OK && owner && !owner->registered)
    {
        result = owner->core->registerOutput(&owner->description, &owner->plugin);
        if (result == FMOD_OK)
            owner->registered = true;
    }
    if (result == FMOD_OK && owner)
        result = owner->core->setOutputByPlugin(owner->plugin);
    if (result == FMOD_OK && owner)
        result = owner->core->setDSPBufferSize(BlockFrames, 4);
    if (result == FMOD_OK && owner)
        result = owner->core->setSoftwareFormat(owner->sampleRate, FMOD_SPEAKERMODE_STEREO, 0);
    if (result == FMOD_OK)
        result = RealStudioInitialize(target, channels, studioFlags, flags, extra);
    if (owner)
        owner->initialized = result == FMOD_OK;
    if (fileBinding)
        fileBinding->initialized = result == FMOD_OK;
    if (result != FMOD_OK)
    {
        StopRing(owner);
        // A real SDK initialization failure may destroy the Studio itself. Do not require a
        // later successful release of an invalid handle, or equate an error code with cleanup.
        if (!target->isValid() && (!owner || owner->nativeClosed.load(std::memory_order_acquire)))
        {
            if (owner)
            {
                owner->nativeReleased = true;
                owner->studio = nullptr;
                owner->core = nullptr;
            }
            if (fileBinding)
            {
                if (fileBinding->installed)
                    CelesteBankIO_ReleaseOwner(fileToken);
                fileIOBindings.erase(fileToken);
            }
        }
    }
    return result;
}

extern "C" FMOD_RESULT WrappedStudioRelease(FMOD::Studio::System* target)
{
    OutputOwner* owner = nullptr;
    for (auto& entry : owners)
        if (entry.second->studio == target)
        {
            owner = entry.second.get();
            break;
        }
    StopRing(owner);
    const auto result = RealStudioRelease(target);
    if (owner && result == FMOD_OK)
    {
        owner->nativeReleased = true;
        owner->initialized = false;
        owner->studio = nullptr;
        owner->core = nullptr;
    }
    if (result == FMOD_OK)
    {
        for (auto entry = fileIOBindings.begin(); entry != fileIOBindings.end(); ++entry)
        {
            if (entry->second.studio != target)
                continue;
            if (entry->second.installed)
                CelesteBankIO_ReleaseOwner(entry->first);
            fileIOBindings.erase(entry);
            break;
        }
    }
    return result;
}
