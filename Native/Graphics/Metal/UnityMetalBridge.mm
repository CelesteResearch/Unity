#include "CelesteGraphicsVulkan.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsMetal.h"
#import <Metal/Metal.h>
#include <vulkan/vulkan_metal.h>
#include <atomic>
#include <condition_variable>
#include <cstring>
#include <mutex>
#include <memory>
#include <stdexcept>
#include <string>
#include <unordered_set>

namespace
{
    IUnityInterfaces* interfaces = nullptr;
    IUnityGraphicsMetalV2* metal = nullptr;
    IUnityGraphics* graphics = nullptr;
    CelesteGraphicsDevice* device = nullptr;
    CelesteVulkanNativeDevice native{};
    uint64_t generation = 0;
    uint64_t unityDeviceRegistryId = 0;
    bool retired = false;
    std::recursive_mutex lifecycle;
    thread_local std::string error;
    struct Output
    {
        VkImage image = VK_NULL_HANDLE;
        id<MTLTexture> texture = nil;
        CelesteGraphicsDevice* owner = nullptr;
        uint64_t generation = 0;
    };
    std::unordered_set<Output*> outputs;
    struct Fence
    {
        std::atomic<int> references{2};
        std::mutex gate;
        std::condition_variable completed;
        int status = 0;
    };
    void Release(Fence* fence)
    {
        if (fence->references.fetch_sub(1, std::memory_order_acq_rel) == 1)
            delete fence;
    }
    void Complete(Fence* fence, int status)
    {
        {
            std::lock_guard<std::mutex> lock(fence->gate);
            fence->status = status;
        }
        fence->completed.notify_all();
        Release(fence);
    }
    void Require(int result)
    {
        if (!result)
            throw std::runtime_error(CelesteVulkanGetLastError());
    }
    template <class T> T Proc(const char* name)
    {
        T result = reinterpret_cast<T>(native.getDeviceProcAddr(native.device, name));
        if (!result)
            throw std::runtime_error(std::string("Missing Vulkan Metal entry point: ") + name);
        return result;
    }
    void InitializeImage(VkImage image)
    {
        VkCommandPool pool = VK_NULL_HANDLE;
        VkCommandPoolCreateInfo poolInfo{VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO};
        poolInfo.queueFamilyIndex = native.graphicsQueueFamily;
        auto check = [](VkResult result) {
            if (result != VK_SUCCESS)
                throw std::runtime_error("Vulkan Metal image initialization failed.");
        };
        check(Proc<PFN_vkCreateCommandPool>("vkCreateCommandPool")(native.device, &poolInfo, nullptr, &pool));
        try
        {
            VkCommandBufferAllocateInfo allocate{VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO};
            allocate.commandPool = pool;
            allocate.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
            allocate.commandBufferCount = 1;
            VkCommandBuffer commands = VK_NULL_HANDLE;
            check(Proc<PFN_vkAllocateCommandBuffers>("vkAllocateCommandBuffers")(native.device, &allocate, &commands));
            VkCommandBufferBeginInfo begin{VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO};
            begin.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
            check(Proc<PFN_vkBeginCommandBuffer>("vkBeginCommandBuffer")(commands, &begin));
            VkImageMemoryBarrier barrier{VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER};
            barrier.oldLayout = VK_IMAGE_LAYOUT_UNDEFINED;
            barrier.newLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
            barrier.dstAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
            barrier.image = image;
            barrier.subresourceRange = {VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
            auto pipelineBarrier = Proc<PFN_vkCmdPipelineBarrier>("vkCmdPipelineBarrier");
            pipelineBarrier(commands, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr,
                            0, nullptr, 1, &barrier);
            const VkClearColorValue black{{0, 0, 0, 1}};
            Proc<PFN_vkCmdClearColorImage>("vkCmdClearColorImage")(
                commands, image, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &black, 1, &barrier.subresourceRange);
            barrier.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            barrier.newLayout = VK_IMAGE_LAYOUT_GENERAL;
            barrier.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
            barrier.dstAccessMask = VK_ACCESS_MEMORY_READ_BIT | VK_ACCESS_MEMORY_WRITE_BIT;
            pipelineBarrier(commands, VK_PIPELINE_STAGE_TRANSFER_BIT, VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, 0, 0, nullptr,
                            0, nullptr, 1, &barrier);
            check(Proc<PFN_vkEndCommandBuffer>("vkEndCommandBuffer")(commands));
            VkSubmitInfo submit{VK_STRUCTURE_TYPE_SUBMIT_INFO};
            submit.commandBufferCount = 1;
            submit.pCommandBuffers = &commands;
            check(Proc<PFN_vkQueueSubmit>("vkQueueSubmit")(native.graphicsQueue, 1, &submit, VK_NULL_HANDLE));
            check(Proc<PFN_vkQueueWaitIdle>("vkQueueWaitIdle")(native.graphicsQueue));
        }
        catch (...)
        {
            Proc<PFN_vkDestroyCommandPool>("vkDestroyCommandPool")(native.device, pool, nullptr);
            throw;
        }
        Proc<PFN_vkDestroyCommandPool>("vkDestroyCommandPool")(native.device, pool, nullptr);
    }
    void UNITY_INTERFACE_API FenceEvent(int, void* data)
    {
        auto fence = static_cast<Fence*>(data);
        if (!metal)
        {
            Complete(fence, -1);
            return;
        }
        metal->EndCurrentCommandEncoder();
        id<MTLCommandBuffer> commands = metal->CommitCurrentCommandBuffer();
        if (!commands)
        {
            Complete(fence, -1);
            return;
        }
        // Unity V2 commits through the engine's own bookkeeping and returns the submitted buffer.
        // Its completion includes earlier camera/Scene reads of imported textures on this queue.
        [commands waitUntilCompleted];
        Complete(fence, commands.status == MTLCommandBufferStatusCompleted ? 1 : -1);
    }
    void RetireUnityReads()
    {
        if (!metal)
            return;
        metal->EndCurrentCommandEncoder();
        id<MTLCommandBuffer> commands = metal->CommitCurrentCommandBuffer();
        if (commands)
            [commands waitUntilCompleted];
    }
    void CloseDevice()
    {
        if (!device)
            return;
        auto wait = Proc<PFN_vkDeviceWaitIdle>("vkDeviceWaitIdle");
        wait(native.device);
        auto destroy = Proc<PFN_vkDestroyImage>("vkDestroyImage");
        for (auto output : outputs)
        {
            if (output->owner != device)
                continue;
            if (output->image)
                destroy(native.device, output->image, nullptr);
            [output->texture release];
            output->image = VK_NULL_HANDLE;
            output->texture = nil;
            output->owner = nullptr;
        }
        Require(CelesteVulkanReleaseDevice(device));
        device = nullptr;
        native = {};
        retired = false;
        ++generation;
    }
    void UNITY_INTERFACE_API DeviceEvent(UnityGfxDeviceEventType event)
    {
        std::lock_guard<std::recursive_mutex> lock(lifecycle);
        if (event == kUnityGfxDeviceEventInitialize)
        {
            metal = interfaces->Get<IUnityGraphicsMetalV2>();
            unityDeviceRegistryId = metal && metal->MetalDevice() ? metal->MetalDevice().registryID : 0;
            retired = device != nullptr;
        }
        else if (event == kUnityGfxDeviceEventShutdown)
        {
            RetireUnityReads();
            if (device)
                Proc<PFN_vkDeviceWaitIdle>("vkDeviceWaitIdle")(native.device);
            // Keep the owned device alive until managed resources have released their native objects.
            // A retired session can only dispose; it cannot draw or become a new session.
            retired = true;
            ++generation;
            metal = nullptr;
            unityDeviceRegistryId = 0;
        }
    }
}

extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* value)
{
    interfaces = value;
    graphics = interfaces->Get<IUnityGraphics>();
    graphics->RegisterDeviceEventCallback(DeviceEvent);
    DeviceEvent(kUnityGfxDeviceEventInitialize);
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginUnload()
{
    std::lock_guard<std::recursive_mutex> lock(lifecycle);
    if (graphics)
        graphics->UnregisterDeviceEventCallback(DeviceEvent);
    RetireUnityReads();
    try
    {
        CloseDevice();
    }
    catch (const std::exception& failure)
    {
        error = failure.what();
    }
    for (auto output : outputs)
        delete output;
    outputs.clear();
    graphics = nullptr;
    interfaces = nullptr;
    metal = nullptr;
}
extern "C" UNITY_INTERFACE_EXPORT const char* CelesteUnityMetalGetLastError()
{
    return error.c_str();
}
extern "C" UNITY_INTERFACE_EXPORT uint64_t CelesteUnityMetalGetGeneration()
{
    std::lock_guard<std::recursive_mutex> lock(lifecycle);
    return generation;
}
extern "C" UNITY_INTERFACE_EXPORT UnityRenderingEventAndData CelesteUnityMetalFenceEvent()
{
    return FenceEvent;
}
extern "C" UNITY_INTERFACE_EXPORT void* CelesteUnityMetalCreateFence()
{
    return new Fence();
}
extern "C" UNITY_INTERFACE_EXPORT int CelesteUnityMetalWaitFence(void* value)
{
    auto fence = static_cast<Fence*>(value);
    std::unique_lock<std::mutex> lock(fence->gate);
    fence->completed.wait(lock, [&] { return fence->status != 0; });
    return fence->status;
}
extern "C" UNITY_INTERFACE_EXPORT void CelesteUnityMetalReleaseFence(void* value)
{
    Release(static_cast<Fence*>(value));
}

extern "C" UNITY_INTERFACE_EXPORT int CelesteUnityMetalAcquireDevice(CelesteGraphicsDevice** result)
{
    try
    {
        std::lock_guard<std::recursive_mutex> lock(lifecycle);
        if (!unityDeviceRegistryId)
            throw std::runtime_error("Unity has no live Metal device.");
        if (retired && device)
            throw std::runtime_error("Dispose the retired Metal session before creating another one.");
        if (device)
        {
            *result = device;
            return 1;
        }
        VkExportMetalObjectCreateInfoEXT exported{VK_STRUCTURE_TYPE_EXPORT_METAL_OBJECT_CREATE_INFO_EXT};
        exported.exportObjectType = VK_EXPORT_METAL_OBJECT_TYPE_METAL_DEVICE_BIT_EXT;
        const char* extensions[] = {VK_EXT_METAL_OBJECTS_EXTENSION_NAME};
        uint32_t physicalDeviceCount = 32;
        for (uint32_t index = 0; index < physicalDeviceCount; ++index)
        {
            CelesteVulkanOwnedDeviceInfo info{};
            info.physicalDeviceIndex = index;
            info.deviceExtensionCount = 1;
            info.deviceExtensions = extensions;
            info.instanceCreateNext = &exported;
            CelesteGraphicsDevice* candidate = nullptr;
            if (!CelesteVulkanCreateOwnedDevice(&info, &candidate))
                continue;
            CelesteVulkanNativeDevice candidateNative{};
            if (!CelesteVulkanGetNativeDevice(candidate, &candidateNative))
            {
                CelesteVulkanReleaseDevice(candidate);
                continue;
            }
            auto enumerate = reinterpret_cast<PFN_vkEnumeratePhysicalDevices>(
                candidateNative.getInstanceProcAddr(candidateNative.instance, "vkEnumeratePhysicalDevices"));
            if (enumerate)
                enumerate(candidateNative.instance, &physicalDeviceCount, nullptr);
            auto exportObjects = reinterpret_cast<PFN_vkExportMetalObjectsEXT>(
                candidateNative.getDeviceProcAddr(candidateNative.device, "vkExportMetalObjectsEXT"));
            VkExportMetalDeviceInfoEXT exportedDevice{VK_STRUCTURE_TYPE_EXPORT_METAL_DEVICE_INFO_EXT};
            VkExportMetalObjectsInfoEXT objects{VK_STRUCTURE_TYPE_EXPORT_METAL_OBJECTS_INFO_EXT, &exportedDevice};
            if (exportObjects)
                exportObjects(candidateNative.device, &objects);
            const bool sameDevice =
                exportedDevice.mtlDevice && exportedDevice.mtlDevice.registryID == unityDeviceRegistryId;
            if (!sameDevice)
            {
                CelesteVulkanReleaseDevice(candidate);
                continue;
            }
            device = candidate;
            retired = false;
            native = candidateNative;
            ++generation;
            *result = device;
            return 1;
        }
        throw std::runtime_error(
            "MoltenVK cannot create a Vulkan device on Unity's Metal GPU with Metal object sharing.");
    }
    catch (const std::exception& failure)
    {
        error = failure.what();
        return 0;
    }
}

extern "C" UNITY_INTERFACE_EXPORT int CelesteUnityMetalReleaseDevice(CelesteGraphicsDevice* value)
{
    try
    {
        std::lock_guard<std::recursive_mutex> lock(lifecycle);
        if (value != device)
            throw std::runtime_error("The Metal device belongs to another session.");
        CloseDevice();
        return 1;
    }
    catch (const std::exception& failure)
    {
        error = failure.what();
        return 0;
    }
}

extern "C" UNITY_INTERFACE_EXPORT void* CelesteUnityMetalImportTexture(void* value)
{
    try
    {
        std::lock_guard<std::recursive_mutex> lock(lifecycle);
        id<MTLTexture> texture = (__bridge id<MTLTexture>)value;
        if (retired || !device || !texture || texture.device.registryID != unityDeviceRegistryId ||
            texture.textureType != MTLTextureType2D || texture.pixelFormat != MTLPixelFormatRGBA8Unorm ||
            texture.sampleCount != 1 || texture.mipmapLevelCount != 1)
            throw std::runtime_error("Metal sharing requires a same-GPU RGBA8 UNorm, 2D, non-MSAA texture.");
        VkImportMetalTextureInfoEXT imported{VK_STRUCTURE_TYPE_IMPORT_METAL_TEXTURE_INFO_EXT};
        imported.plane = VK_IMAGE_ASPECT_COLOR_BIT;
        imported.mtlTexture = texture;
        VkImageCreateInfo info{VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO};
        info.pNext = &imported;
        info.imageType = VK_IMAGE_TYPE_2D;
        info.format = VK_FORMAT_R8G8B8A8_UNORM;
        info.extent = {static_cast<uint32_t>(texture.width), static_cast<uint32_t>(texture.height), 1};
        info.mipLevels = info.arrayLayers = 1;
        info.samples = VK_SAMPLE_COUNT_1_BIT;
        info.tiling = VK_IMAGE_TILING_OPTIMAL;
        info.usage = VK_IMAGE_USAGE_TRANSFER_DST_BIT;
        info.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
        info.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
        auto output = std::make_unique<Output>();
        VkImage image = VK_NULL_HANDLE;
        const VkResult result = Proc<PFN_vkCreateImage>("vkCreateImage")(native.device, &info, nullptr, &image);
        if (result != VK_SUCCESS)
            throw std::runtime_error("MoltenVK failed to import Unity's Metal texture.");
        try
        {
            InitializeImage(image);
        }
        catch (...)
        {
            Proc<PFN_vkDestroyImage>("vkDestroyImage")(native.device, image, nullptr);
            throw;
        }
        try
        {
            outputs.insert(output.get());
        }
        catch (...)
        {
            Proc<PFN_vkDestroyImage>("vkDestroyImage")(native.device, image, nullptr);
            throw;
        }
        output->image = image;
        output->texture = [texture retain];
        output->generation = generation;
        output->owner = device;
        return output.release();
    }
    catch (const std::exception& failure)
    {
        error = failure.what();
        return nullptr;
    }
}
extern "C" UNITY_INTERFACE_EXPORT int CelesteUnityMetalAcquireOutput(void* value, CelesteGraphicsOutput** result)
{
    try
    {
        std::lock_guard<std::recursive_mutex> lock(lifecycle);
        auto output = static_cast<Output*>(value);
        if (retired || !output || outputs.find(output) == outputs.end() || output->generation != generation ||
            output->owner != device)
            throw std::runtime_error("The shared Metal image belongs to a retired session.");
        CelesteVulkanOutputInfo info{};
        info.image = output->image;
        info.format = VK_FORMAT_R8G8B8A8_UNORM;
        info.extent = {static_cast<uint32_t>(output->texture.width), static_cast<uint32_t>(output->texture.height)};
        // Unity's Metal access is synchronized by the host. GENERAL preserves imported storage content.
        info.incomingLayout = VK_IMAGE_LAYOUT_GENERAL;
        info.outgoingLayout = VK_IMAGE_LAYOUT_GENERAL;
        info.incomingStage = info.outgoingStage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
        info.incomingAccess = info.outgoingAccess = VK_ACCESS_MEMORY_READ_BIT | VK_ACCESS_MEMORY_WRITE_BIT;
        info.incomingQueueFamily = info.outgoingQueueFamily = native.graphicsQueueFamily;
        Require(CelesteVulkanAcquireOutput(device, &info, result));
        return 1;
    }
    catch (const std::exception& failure)
    {
        error = failure.what();
        return 0;
    }
}
extern "C" UNITY_INTERFACE_EXPORT void CelesteUnityMetalReleaseOutput(void* value)
{
    std::lock_guard<std::recursive_mutex> lock(lifecycle);
    auto output = static_cast<Output*>(value);
    if (!output || outputs.erase(output) == 0)
        return;
    if (device && output->owner == device && output->image)
        Proc<PFN_vkDestroyImage>("vkDestroyImage")(native.device, output->image, nullptr);
    [output->texture release];
    delete output;
}
extern "C" UNITY_INTERFACE_EXPORT int CelesteUnityMetalSubmit(CelesteGraphicsPacket* packet)
{
    try
    {
        std::lock_guard<std::recursive_mutex> lock(lifecycle);
        if (retired || !device)
            throw std::runtime_error("The Metal submission session has retired.");
        auto api = CelesteVulkanGetDeviceApi();
        CelesteGraphicsSubmission* submission = nullptr;
        if (!api->Submit(packet, &submission))
            throw std::runtime_error(api->GetLastError());
        const int completed = api->Wait(submission);
        api->ReleaseSubmission(submission);
        if (!completed)
            throw std::runtime_error(api->GetLastError());
        // The private Vulkan queue has completed before Unity may sample the shared texture.
        return 1;
    }
    catch (const std::exception& failure)
    {
        error = failure.what();
        return 0;
    }
}
