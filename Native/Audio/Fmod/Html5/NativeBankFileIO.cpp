#include "NativeBankFileIO.h"
#include <emscripten.h>
#include <emscripten/threading.h>
#include <atomic>
#include <algorithm>
#include <condition_variable>
#include <cstdio>
#include <cstring>
#include <deque>
#include <mutex>
#include <thread>
#include <unordered_set>
#include <vector>

namespace
{
    constexpr unsigned int PageSize = 1024 * 1024;
    enum Phase : unsigned int
    {
        Posted = 1,
        Writing = 2,
        Done = 3,
        Canceled = 4,
        Failed = 5
    };
    struct alignas(16) Exchange
    {
        std::atomic<unsigned int> token{0};
        unsigned int file = 0;
        unsigned int offset = 0;
        unsigned int length = 0;
        unsigned int total = 0;
        unsigned int priority = 0;
        std::atomic<unsigned int> ready{0};
        std::atomic<unsigned int> requested{0};
        std::atomic<unsigned int> completed{0};
        std::atomic<unsigned int> canceled{0};
        std::atomic<unsigned int> stopping{0};
        std::atomic<unsigned int> failures{0};
        std::atomic<unsigned int> owners{0};
        unsigned int owner = 0;
        unsigned int reserved = 0;
        std::atomic<unsigned int> workerFailed{0};
        unsigned char page[PageSize]{};
    } exchange;
    static_assert(offsetof(Exchange, page) == 64);
    static_assert(sizeof(std::atomic<unsigned int>) == 4);

    struct FileHandle
    {
        unsigned int owner, file;
    };
    std::vector<unsigned int> fileSizes;
    std::unordered_set<unsigned int> owners;
    std::mutex mutex;
    std::condition_variable changed;
    std::deque<FMOD_ASYNCREADINFO*> pending;
    std::deque<FMOD_ASYNCREADINFO*> canceled;
    FMOD_ASYNCREADINFO* active = nullptr;
    std::thread service;
    bool cancelActive = false;
    bool completing = false;
    bool quit = false;
    bool failed = false;
    unsigned int generation = 0;

    void Serve()
    {
        exchange.ready.store(1, std::memory_order_release);
        for (;;)
        {
            FMOD_ASYNCREADINFO* info;
            {
                std::unique_lock<std::mutex> lock(mutex);
                changed.wait(lock, [] { return quit || !pending.empty() || !canceled.empty(); });
                if (quit && pending.empty() && canceled.empty())
                    return;
                cancelActive = !canceled.empty();
                auto& queue = cancelActive ? canceled : pending;
                info = queue.front();
                queue.pop_front();
                active = info;
                completing = false;
            }
            auto* file = static_cast<FileHandle*>(info->handle);
            unsigned int size = fileSizes[file->file];
            unsigned int available = info->offset < size ? std::min(info->sizebytes, size - info->offset) : 0;
            unsigned int copied = 0;
            FMOD_RESULT result = available == info->sizebytes ? FMOD_OK : FMOD_ERR_FILE_EOF;
            while (copied < available)
            {
                unsigned int position = info->offset + copied;
                unsigned int pageOffset = position & ~(PageSize - 1);
                {
                    std::lock_guard<std::mutex> lock(mutex);
                    if (exchange.workerFailed.load(std::memory_order_acquire))
                        failed = true;
                    if (cancelActive || failed)
                        break;
                    exchange.file = file->file;
                    exchange.offset = position;
                    exchange.length = std::min(available - copied, PageSize - (position - pageOffset));
                    exchange.total = size;
                    exchange.priority = static_cast<unsigned int>(info->priority);
                    exchange.owner = file->owner;
                    exchange.token.store((++generation << 3) | Posted, std::memory_order_release);
                    emscripten_futex_wake(&exchange.token, 1);
                }
                unsigned int state;
                for (;;)
                {
                    state = exchange.token.load(std::memory_order_acquire);
                    if (exchange.workerFailed.load(std::memory_order_acquire))
                    {
                        state = Failed;
                        break;
                    }
                    unsigned int phase = state & 7;
                    if (phase == Done || phase == Canceled || phase == Failed)
                        break;
                    emscripten_futex_wait(&exchange.token, state, 100);
                }
                {
                    std::lock_guard<std::mutex> lock(mutex);
                    if (cancelActive || (state & 7) == Canceled)
                        break;
                    if ((state & 7) == Failed)
                    {
                        result = FMOD_ERR_FILE_BAD;
                        exchange.failures++;
                        break;
                    }
                    unsigned int count = exchange.length;
                    std::memcpy(static_cast<unsigned char*>(info->buffer) + copied, exchange.page, count);
                    copied += count;
                }
            }
            {
                std::lock_guard<std::mutex> lock(mutex);
                completing = true;
                if (exchange.workerFailed.load(std::memory_order_acquire))
                    failed = true;
                if (failed)
                {
                    copied = 0;
                    result = FMOD_ERR_FILE_BAD;
                }
                if (cancelActive)
                {
                    copied = 0;
                    result = FMOD_ERR_FILE_DISKEJECTED;
                    exchange.canceled++;
                }
            }
            info->bytesread = copied;
            info->done(info, result);
            exchange.completed++;
            {
                std::lock_guard<std::mutex> lock(mutex);
                active = nullptr;
                completing = false;
            }
            changed.notify_all();
        }
    }

    FMOD_RESULT F_CALL Open(const char* path, unsigned int* size, void** handle, void*)
    {
        unsigned int owner = 0;
        unsigned int file = 0;
        int consumed = 0;
        if (std::sscanf(path, "/celeste-audio/%u/%u%n", &owner, &file, &consumed) != 2 || consumed == 0 ||
            path[consumed] != '\0')
            return FMOD_ERR_FILE_NOTFOUND;
        std::lock_guard<std::mutex> lock(mutex);
        if (owners.find(owner) == owners.end() || file >= fileSizes.size() || fileSizes[file] == 0)
            return FMOD_ERR_FILE_NOTFOUND;
        *size = fileSizes[file];
        *handle = new FileHandle{owner, file};
        return FMOD_OK;
    }
    FMOD_RESULT F_CALL Close(void* handle, void*)
    {
        delete static_cast<FileHandle*>(handle);
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL Read(FMOD_ASYNCREADINFO* info, void*)
    {
        {
            std::lock_guard<std::mutex> lock(mutex);
            if (failed || exchange.workerFailed.load(std::memory_order_acquire))
                return FMOD_ERR_FILE_BAD;
            pending.push_back(info);
            exchange.requested++;
        }
        changed.notify_one();
        return FMOD_OK;
    }

    FMOD_RESULT F_CALL Cancel(FMOD_ASYNCREADINFO* info, void*)
    {
        std::unique_lock<std::mutex> lock(mutex);
        auto found = std::find(pending.begin(), pending.end(), info);
        if (found != pending.end())
        {
            pending.erase(found);
            canceled.push_back(info);
            changed.notify_one();
            changed.wait(lock, [info] {
                return active != info && std::find(canceled.begin(), canceled.end(), info) == canceled.end();
            });
            return FMOD_OK;
        }
        if (active == info)
        {
            if (!completing)
            {
                cancelActive = true;
                unsigned int token = exchange.token.load(std::memory_order_acquire);
                if ((token & 7) == Posted)
                    exchange.token.compare_exchange_strong(token, (token & ~7u) | Canceled, std::memory_order_acq_rel);
                emscripten_futex_wake(&exchange.token, 1);
            }
            changed.wait(lock, [info] { return active != info; });
        }
        return FMOD_OK;
    }
}

extern "C"
{
    EMSCRIPTEN_KEEPALIVE int CelesteBankIO_Start()
    {
        if (service.joinable())
            return FMOD_ERR_INITIALIZED;
        quit = false;
        exchange.stopping.store(0);
        exchange.ready.store(0);
        service = std::thread(Serve);
        return FMOD_OK;
    }

    EMSCRIPTEN_KEEPALIVE uintptr_t CelesteBankIO_GetExchange()
    {
        return reinterpret_cast<uintptr_t>(&exchange);
    }

    EMSCRIPTEN_KEEPALIVE int CelesteBankIO_SetFile(unsigned int bank, unsigned int size)
    {
        if (size == 0)
            return FMOD_ERR_INVALID_PARAM;
        std::lock_guard<std::mutex> lock(mutex);
        if (!owners.empty())
            return FMOD_ERR_INITIALIZED;
        if (fileSizes.size() <= bank)
            fileSizes.resize(bank + 1);
        if (fileSizes[bank] != 0 && fileSizes[bank] != size)
            return FMOD_ERR_INVALID_PARAM;
        fileSizes[bank] = size;
        return FMOD_OK;
    }

    EMSCRIPTEN_KEEPALIVE int CelesteBankIO_Install(unsigned int token, FMOD_SYSTEM* core)
    {
        if (!exchange.ready.load(std::memory_order_acquire))
            return FMOD_ERR_NOTREADY;
        if (token == 0 || core == nullptr)
            return FMOD_ERR_INVALID_PARAM;
        for (auto size : fileSizes)
            if (size == 0)
                return FMOD_ERR_NOTREADY;
        {
            std::lock_guard<std::mutex> lock(mutex);
            if (!owners.insert(token).second)
                return FMOD_ERR_INITIALIZED;
            exchange.owners++;
        }
        FMOD_RESULT result = FMOD_System_SetFileSystem(core, Open, Close, nullptr, nullptr, Read, Cancel, 2048);
        if (result != FMOD_OK)
            CelesteBankIO_ReleaseOwner(token);
        return result;
    }

    EMSCRIPTEN_KEEPALIVE int CelesteBankIO_ReleaseOwner(unsigned int token)
    {
        std::lock_guard<std::mutex> lock(mutex);
        if (owners.erase(token) != 0)
            exchange.owners--;
        return FMOD_OK;
    }

    // The caller must terminate the fetch Worker before invalidating a Writing phase.
    EMSCRIPTEN_KEEPALIVE int CelesteBankIO_Fail()
    {
        {
            std::lock_guard<std::mutex> lock(mutex);
            failed = true;
            unsigned int token = exchange.token.load(std::memory_order_acquire);
            exchange.token.store((token & ~7u) | Failed, std::memory_order_release);
        }
        emscripten_futex_wake(&exchange.token, 1);
        changed.notify_all();
        return FMOD_OK;
    }
    EMSCRIPTEN_KEEPALIVE int CelesteBankIO_Stop()
    {
        {
            std::lock_guard<std::mutex> lock(mutex);
            if (!owners.empty() || active || !pending.empty() || !canceled.empty())
                return FMOD_ERR_NOTREADY;
            quit = true;
        }
        exchange.stopping.store(1, std::memory_order_release);
        changed.notify_one();
        if (service.joinable())
            service.join();
        exchange.ready.store(0);
        return FMOD_OK;
    }
}
