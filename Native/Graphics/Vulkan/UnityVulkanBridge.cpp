#include "UnityVulkanBridge.h"
#include "UnityVulkanInitialization.h"
#define UNITY_VULKAN_HEADER <vulkan/vulkan_core.h>
#include "IUnityGraphicsVulkan.h"
#include <cstring>
#include <atomic>

namespace CelesteUnityVulkanBridgeState
{
    IUnityInterfaces* interfaces = nullptr;
    IUnityGraphics* graphics = nullptr;
    IUnityGraphicsVulkanV2* vulkan = nullptr;
    CelesteVulkanNativeDevice nativeDevice{};
    CelesteGraphicsDevice* backendDevice = nullptr;
    std::atomic<uint64_t> publishedGeneration{0};
    uint64_t nextGeneration = 0;

    int eventId = -1;
    char lifecycleError[512]{};
    const char* initializationRegistrationError = nullptr;
    void Error(char* destination, const char* message)
    {
        if (!message || !*message)
            message = "Vulkan backend rejected the operation without a diagnostic.";
        const auto length = std::strlen(message);
        const auto count = length < 511 ? length : 511;
        std::memcpy(destination, message, count);
        destination[count] = 0;
    }

    void ReleaseDevice()
    {
        publishedGeneration.store(0, std::memory_order_release);
        if (backendDevice && !CelesteVulkanReleaseDevice(backendDevice))
        {
            Error(lifecycleError, CelesteVulkanGetLastError());
            return;
        }
        backendDevice = nullptr;

        nativeDevice = {};
        vulkan = nullptr;
    }

    void UNITY_INTERFACE_API DeviceEvent(UnityGfxDeviceEventType type)
    {
        if (type == kUnityGfxDeviceEventShutdown || type == kUnityGfxDeviceEventBeforeReset)
        {
            // The native recording consumer must settle/retire host submissions
            // before engine shutdown. ReleaseDevice never destroys borrowed VkDevice.
            ReleaseDevice();
            return;
        }
        if (type != kUnityGfxDeviceEventInitialize && type != kUnityGfxDeviceEventAfterReset)
            return;
        if (backendDevice)
            return;
        lifecycleError[0] = 0;
        if (!graphics || graphics->GetRenderer() != kUnityGfxRendererVulkan)
        {
            Error(lifecycleError, "Unity graphics renderer is not Vulkan.");
            return;
        }
        vulkan = interfaces->Get<IUnityGraphicsVulkanV2>();
        if (!vulkan)
        {
            Error(lifecycleError, "IUnityGraphicsVulkanV2 is unavailable.");
            return;
        }
        const auto host = vulkan->Instance();
        nativeDevice = {};
        nativeDevice.instance = host.instance;
        nativeDevice.physicalDevice = host.physicalDevice;
        nativeDevice.device = host.device;
        nativeDevice.graphicsQueue = host.graphicsQueue;
        nativeDevice.graphicsQueueFamily = host.queueFamilyIndex;
        nativeDevice.getInstanceProcAddr = host.getInstanceProcAddr;
        if (initializationRegistrationError)
        {
            Error(lifecycleError, initializationRegistrationError);
            return;
        }
        const uint32_t requested = CelesteUnityVulkanRequestedVersion(host.instance);
        if (!requested)
        {
            Error(
                lifecycleError,
                CelesteUnityVulkanInitializationWasInvoked()
                    ? "Unity initialization hook ran, but this VkInstance was not captured through vkCreateInstance."
                    : "Unity accepted the initialization hook, but has not invoked it. Preload before Vulkan initialization.");
            return;
        }
        const auto getProperties = reinterpret_cast<PFN_vkGetPhysicalDeviceProperties>(
            host.getInstanceProcAddr(host.instance, "vkGetPhysicalDeviceProperties"));
        if (!getProperties)
        {
            Error(lifecycleError, "vkGetPhysicalDeviceProperties is unavailable.");
            return;
        }
        VkPhysicalDeviceProperties properties{};
        getProperties(host.physicalDevice, &properties);
        nativeDevice.apiVersion = requested < properties.apiVersion ? requested : properties.apiVersion;
        if (!CelesteUnityVulkanEnabledFeatures(host.device, &nativeDevice.enabledFeatures))
        {
            Error(lifecycleError, "Unity Vulkan enabled features were not captured. Preload before device creation.");
            return;
        }
        if (host.getInstanceProcAddr)
            nativeDevice.getDeviceProcAddr = reinterpret_cast<PFN_vkGetDeviceProcAddr>(
                host.getInstanceProcAddr(host.instance, "vkGetDeviceProcAddr"));
        CelesteVulkanBorrowedDeviceInfo info{};
        info.nativeDevice = nativeDevice;
        if (!CelesteVulkanCreateBorrowedDevice(&info, &backendDevice))
        {
            Error(lifecycleError, CelesteVulkanGetLastError());
            ReleaseDevice();
            return;
        }
        UnityVulkanPluginEventConfig config{};
        config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
        config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
        config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                       kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
        vulkan->ConfigureEvent(eventId, &config);

        UnityVulkanPluginEventConfig completion{};
        completion.renderPassPrecondition = kUnityVulkanRenderPass_DontCare;
        completion.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_Allow;
        completion.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                           kUnityVulkanEventConfigFlag_FlushCommandBuffers |
                           kUnityVulkanEventConfigFlag_SyncWorkerThreads;
        vulkan->ConfigureEvent(eventId + 1, &completion);
        publishedGeneration.store(++nextGeneration, std::memory_order_release);
    }

}

using namespace CelesteUnityVulkanBridgeState;

extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* api)
{
    interfaces = api;
    graphics = api->Get<IUnityGraphics>();
    if (!graphics)
    {
        Error(lifecycleError, "IUnityGraphics is unavailable.");
        return;
    }
    eventId = graphics->ReserveEventIDRange(2);
    // Must be preloaded: registration after Unity creates its instance cannot recover
    // the enabled API version from the physical device's maximum supported version.
    auto initialization = api->Get<IUnityGraphicsVulkanV2>();
    initializationRegistrationError =
        !initialization
            ? "IUnityGraphicsVulkanV2 was unavailable during UnityPluginLoad; initialization hook was not registered."
        : !initialization->InterceptInitialization(CelesteUnityVulkanInterceptInitialization, nullptr)
            ? "Unity rejected InterceptInitialization registration (plugin loaded too late). Preload GfxPluginCelesteGraphicsUnityVulkan before device initialization."
            : nullptr;
    graphics->RegisterDeviceEventCallback(DeviceEvent);
    DeviceEvent(kUnityGfxDeviceEventInitialize);
}

extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginUnload()
{
    if (graphics)
        graphics->UnregisterDeviceEventCallback(DeviceEvent);
    ReleaseDevice();
    graphics = nullptr;
    interfaces = nullptr;
    eventId = -1;
}

extern "C" UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API CelesteUnityVulkanGetDeviceGeneration()
{
    return publishedGeneration.load(std::memory_order_acquire);
}
extern "C" UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API CelesteUnityVulkanGetLastError()
{
    return lifecycleError;
}
