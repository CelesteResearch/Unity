#include "UnityVulkanOperations.h"
#include "UnityVulkanBridgeState.h"
#include "CelesteUnityVulkanOperation.h"
#include <exception>
#include <new>
#include <cstdio>

using namespace CelesteUnityVulkanBridgeState;
namespace
{
    void Release(CelesteUnityVulkanOperation* request)
    {
        if (!request || request->references.fetch_sub(1, std::memory_order_acq_rel) != 1)
            return;
        if (request->packet)
            CelesteVulkanGetDeviceApi()->ReleasePacket(request->packet);
        if (request->output)
            CelesteUnityVulkanReleaseOutputCache(request->output);
        for (auto output : request->batchOutputs)
            CelesteUnityVulkanReleaseOutputCache(output);
        delete request;
    }
    void Finish(CelesteUnityVulkanOperation* request, int32_t status)
    {
        {
            std::lock_guard<std::mutex> lock(request->completionMutex);
            request->status.store(status, std::memory_order_release);
        }
        request->completion.notify_all();
        Release(request); // Render event loan, retained through an queue submission event.
    }
    void Fail(CelesteUnityVulkanOperation* request, const char* error)
    {
        Error(request->error, error);
        Finish(request, -1);
    }
    template <typename T> T DeviceProc(const CelesteVulkanNativeDevice& device, const char* name)
    {
        return reinterpret_cast<T>(device.getDeviceProcAddr(device.device, name));
    }
    void UNITY_INTERFACE_API CompleteQueue(int, void* data)
    {
        auto request = static_cast<CelesteUnityVulkanOperation*>(data);
        if (request->device != backendDevice ||
            request->generation != publishedGeneration.load(std::memory_order_acquire))
        {
            Fail(request, "Vulkan device changed before queued completion callback.");
            return;
        }
        const auto device = request->completionDevice;
        VkFence fence = VK_NULL_HANDLE;
        const auto create = DeviceProc<PFN_vkCreateFence>(device, "vkCreateFence");
        const auto submit = DeviceProc<PFN_vkQueueSubmit>(device, "vkQueueSubmit");
        const auto wait = DeviceProc<PFN_vkWaitForFences>(device, "vkWaitForFences");
        const auto destroy = DeviceProc<PFN_vkDestroyFence>(device, "vkDestroyFence");
        if (!create || !submit || !wait || !destroy)
        {
            Fail(request, "Vulkan fence completion dispatch is incomplete.");
            return;
        }
        VkFenceCreateInfo info{};
        info.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
        const char* failedCall = "vkCreateFence";
        VkResult result = create(device.device, &info, nullptr, &fence);
        if (result == VK_SUCCESS)
        {
            failedCall = "vkQueueSubmit";
            result = submit(device.graphicsQueue, 0, nullptr, fence);
        }
        if (result == VK_SUCCESS)
        {
            failedCall = "vkWaitForFences";
            result = wait(device.device, 1, &fence, VK_TRUE, UINT64_MAX);
        }
        if (fence)
            destroy(device.device, fence, nullptr);
        if (result != VK_SUCCESS)
        {
            char message[128];
            std::snprintf(message, sizeof(message), "%s failed with VkResult %d; stop this session.", failedCall,
                          static_cast<int>(result));
            Fail(request, message);
            return;
        }

        if (request->releaseSession)
        {
            if (!CelesteVulkanReleaseDevice(backendDevice))
            {
                Fail(request, CelesteVulkanGetLastError());
                return;
            }
            backendDevice = nullptr;

            publishedGeneration.store(0, std::memory_order_release);
        }
        Finish(request, 1);
    }
    void SubmitPacket(CelesteUnityVulkanOperation* request)
    {
        if (request->device != backendDevice ||
            request->generation != publishedGeneration.load(std::memory_order_acquire))
        {
            Fail(request, "Vulkan device changed before packet queue submission.");
            return;
        }
        const auto api = CelesteVulkanGetDeviceApi();
        CelesteGraphicsSubmission* submission = nullptr;
        if (!api->Submit(request->packet, &submission))
        {
            Fail(request, api->GetLastError());
            return;
        }
        const bool completed = !request->wait || api->Wait(submission) != 0;
        if (!completed)
            Error(request->error, api->GetLastError());
        api->ReleaseSubmission(submission);
        Finish(request, completed ? 1 : -1);
    }
    void UNITY_INTERFACE_API Execute(int id, void* data)
    {
        auto request = static_cast<CelesteUnityVulkanOperation*>(data);
        if (!request)
            return;
        if (id == eventId + 1)
        {
            if (request->status.load(std::memory_order_acquire) != 0)
            {
                Release(request);
                return;
            }
            if (!request->recorded.load(std::memory_order_acquire))
            {
                Fail(request, "Completion event ran before Vulkan recording event.");
                return;
            }
            if (request->kind == CelesteUnityVulkanOperation::Kind::Packet)
                SubmitPacket(request);
            else
                CompleteQueue(id, request);
            return;
        }
        if (id != eventId || !vulkan)
        {
            Fail(request, "Unity Vulkan operation event/device unavailable.");
            return;
        }

        try
        {
            if (request->kind == CelesteUnityVulkanOperation::Kind::AcquireDevice)
            {
                if (!backendDevice)
                {
                    if (!nativeDevice.device || !nativeDevice.apiVersion)
                    {
                        Fail(request, CelesteUnityVulkanGetLastError()[0]
                                          ? CelesteUnityVulkanGetLastError()
                                          : "Preloaded Vulkan device identity is unavailable.");
                        return;
                    }
                    CelesteVulkanBorrowedDeviceInfo info{};
                    info.nativeDevice = nativeDevice;
                    if (!CelesteVulkanCreateBorrowedDevice(&info, &backendDevice))
                    {
                        Fail(request, CelesteVulkanGetLastError());
                        return;
                    }
                    publishedGeneration.store(++nextGeneration, std::memory_order_release);
                }
                request->device = backendDevice;
                request->generation = publishedGeneration.load(std::memory_order_acquire);
                if (!request->generation)
                {
                    Fail(request, "A failed Vulkan shutdown still retains its device.");
                    return;
                }
                Finish(request, 1);
                return;
            }
            if (!backendDevice || !request->generation ||
                request->generation != publishedGeneration.load(std::memory_order_acquire))
            {
                Fail(request, "Vulkan operation belongs to a retired device generation.");
                return;
            }
            request->device = backendDevice;
            request->completionDevice = nativeDevice;
            if (request->kind == CelesteUnityVulkanOperation::Kind::ConfigureOutput)
            {
                UnityVulkanImage image{};
                VkImageSubresource sub{VK_IMAGE_ASPECT_COLOR_BIT, 0, 0};
                if (!request->nativeTexture ||
                    !vulkan->AccessTexture(request->nativeTexture, &sub, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                                           VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, VK_ACCESS_SHADER_READ_BIT,
                                           kUnityVulkanResourceAccess_PipelineBarrier, &image))
                {
                    Fail(request, "Unity output image acquisition failed.");
                    return;
                }
                if (image.type != VK_IMAGE_TYPE_2D || image.samples != VK_SAMPLE_COUNT_1_BIT)
                {
                    Fail(request, "Unity output must be a single-sample 2D image.");
                    return;
                }
                auto cache = new CelesteUnityVulkanOutputCache{};
                cache->device = backendDevice;
                cache->generation = request->generation;
                cache->nativeTexture = request->nativeTexture;
                cache->output.image = image.image;
                cache->output.format = image.format;
                cache->output.extent = {image.extent.width, image.extent.height};
                cache->output.incomingLayout = cache->output.outgoingLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
                cache->output.incomingStage = cache->output.outgoingStage = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
                cache->output.incomingAccess = cache->output.outgoingAccess = VK_ACCESS_SHADER_READ_BIT;
                cache->output.incomingQueueFamily = cache->output.outgoingQueueFamily =
                    nativeDevice.graphicsQueueFamily;
                request->output = cache;
                Finish(request, 1);
                return;
            }
            if (request->kind == CelesteUnityVulkanOperation::Kind::Drain)
            {

                request->recorded.store(true, std::memory_order_release);
                Release(request);
                return;
            }
            const auto accessOutput = [&](CelesteUnityVulkanOutputCache* output) {
                if (output->generation != request->generation || output->device != request->device)
                {
                    Error(request->error, "Unity output cache belongs to another device generation.");
                    return false;
                }
                UnityVulkanImage image{};
                VkImageSubresource sub{VK_IMAGE_ASPECT_COLOR_BIT, 0, 0};
                if (!vulkan->AccessTexture(output->nativeTexture, &sub, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                                           VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, VK_ACCESS_SHADER_READ_BIT,
                                           kUnityVulkanResourceAccess_PipelineBarrier, &image))
                {
                    Error(request->error, "Unity output shader-read access failed.");
                    return false;
                }
                if (image.image != output->output.image)
                {
                    Error(request->error, "Unity output image changed; refresh output after resize before submitting.");
                    return false;
                }
                return true;
            };
            if (request->output && !accessOutput(request->output))
            {
                Finish(request, -1);
                return;
            }
            for (auto output : request->batchOutputs)
                if (!accessOutput(output))
                {
                    Finish(request, -1);
                    return;
                }
            // Unity may supply a secondary command buffer. Only validate resources
            // here; the following Allow+Flush event owns queue submission of the
            // backend's primary buffer, with shader-read layout before and after.
            request->recorded.store(true, std::memory_order_release);
            Release(request);
        }
        catch (const std::exception& error)
        {
            Fail(request, error.what());
        }
        catch (...)
        {
            Fail(request, "Native Unity Vulkan operation threw an unknown exception.");
        }
    }
}
extern "C" UNITY_INTERFACE_EXPORT UnityRenderingEventAndData UNITY_INTERFACE_API CelesteUnityVulkanGetOperationEvent()
{
    return Execute;
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityVulkanGetOperationEventId()
{
    return eventId < 0 ? -1 : eventId;
}
extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
CelesteUnityVulkanCreateAcquireDeviceRequest()
{
    return new (std::nothrow) CelesteUnityVulkanOperation(CelesteUnityVulkanOperation::Kind::AcquireDevice);
}
extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
CelesteUnityVulkanCreateOutputRequest(void* texture, uint64_t generation)
{
    auto request = new (std::nothrow) CelesteUnityVulkanOperation(CelesteUnityVulkanOperation::Kind::ConfigureOutput);
    if (request)
    {
        request->nativeTexture = texture;
        request->generation = generation;
    }
    return request;
}
extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
CelesteUnityVulkanCreatePacketRequest(CelesteGraphicsPacket* packet, CelesteUnityVulkanOutputCache* output,
                                      uint64_t generation, int32_t wait)
{
    if (!packet || !CelesteVulkanRetainPacket(packet))
        return nullptr;
    auto request = new (std::nothrow) CelesteUnityVulkanOperation(CelesteUnityVulkanOperation::Kind::Packet);
    if (!request)
    {
        CelesteVulkanGetDeviceApi()->ReleasePacket(packet);
        return nullptr;
    }
    request->packet = packet;
    request->output = output;
    request->generation = generation;
    request->wait = wait != 0;
    if (output)
        output->references.fetch_add(1, std::memory_order_relaxed);
    return request;
}
extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
CelesteUnityVulkanCreateDrainRequest(uint64_t generation, int32_t release)
{
    auto request = new (std::nothrow) CelesteUnityVulkanOperation(CelesteUnityVulkanOperation::Kind::Drain);
    if (request)
    {
        request->generation = generation;
        request->releaseSession = release != 0;
    }
    return request;
}
extern "C" UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
CelesteUnityVulkanPrepareEvent(CelesteUnityVulkanOperation* request)
{
    if (!request || request->queued.exchange(true))
        return 0;
    request->references.fetch_add(request->kind == CelesteUnityVulkanOperation::Kind::Packet ||
                                          request->kind == CelesteUnityVulkanOperation::Kind::Drain
                                      ? 2
                                      : 1,
                                  std::memory_order_relaxed);
    return 1;
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
CelesteUnityVulkanCancelEvent(CelesteUnityVulkanOperation* request)
{
    if (!request || !request->queued.exchange(false))
        return;
    const bool second = request->kind == CelesteUnityVulkanOperation::Kind::Packet ||
                        request->kind == CelesteUnityVulkanOperation::Kind::Drain;
    if (second)
        Release(request);
    Release(request);
}
extern "C" UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
CelesteUnityVulkanGetOperationStatus(CelesteUnityVulkanOperation* request)
{
    return request ? request->status.load(std::memory_order_acquire) : -1;
}
extern "C" UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API
CelesteUnityVulkanGetOperationError(CelesteUnityVulkanOperation* request)
{
    return request ? (request->status.load(std::memory_order_acquire) ? request->error : "Native operation is pending.")
                   : "Native operation allocation/packet retention failed.";
}
extern "C" UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
CelesteUnityVulkanWaitOperation(CelesteUnityVulkanOperation* request)
{
    if (!request)
        return -1;
    std::unique_lock<std::mutex> lock(request->completionMutex);
    request->completion.wait(lock, [request] { return request->status.load(std::memory_order_acquire) != 0; });
    return request->status.load(std::memory_order_acquire);
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
CelesteUnityVulkanReleaseOperation(CelesteUnityVulkanOperation* request)
{
    Release(request);
}
extern "C" UNITY_INTERFACE_EXPORT CelesteGraphicsDevice* UNITY_INTERFACE_API
CelesteUnityVulkanGetOperationDevice(CelesteUnityVulkanOperation* request)
{
    return request && request->status.load(std::memory_order_acquire) == 1 ? request->device : nullptr;
}
extern "C" UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API
CelesteUnityVulkanGetOperationGeneration(CelesteUnityVulkanOperation* request)
{
    return request && request->status.load(std::memory_order_acquire) == 1 ? request->generation : 0;
}
extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOutputCache* UNITY_INTERFACE_API
CelesteUnityVulkanTakeOutputCache(CelesteUnityVulkanOperation* request)
{
    if (!request || request->status.load(std::memory_order_acquire) != 1)
        return nullptr;
    auto output = request->output;
    request->output = nullptr;
    return output;
}
extern "C" UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
CelesteUnityVulkanAcquireOutputLoan(CelesteUnityVulkanOutputCache* output, CelesteGraphicsOutput** loan)
{
    if (!output || !loan || output->generation != publishedGeneration.load(std::memory_order_acquire))
        return 0;
    return CelesteVulkanAcquireOutput(output->device, &output->output, loan);
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
CelesteUnityVulkanReleaseOutputCache(CelesteUnityVulkanOutputCache* output)
{
    if (output && output->references.fetch_sub(1, std::memory_order_acq_rel) == 1)
        delete output;
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API
CelesteUnityVulkanGetOperationCompletionEventId(CelesteUnityVulkanOperation* request)
{
    return request &&
                   (request->kind == CelesteUnityVulkanOperation::Kind::Packet ||
                    request->kind == CelesteUnityVulkanOperation::Kind::Drain) &&
                   eventId >= 0
               ? eventId + 1
               : -1;
}
extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
CelesteUnityVulkanCreatePacketBatchRequest(CelesteGraphicsPacket* packet, CelesteUnityVulkanOutputCache* const* outputs,
                                           int32_t count, uint64_t generation, int32_t wait)
{
    if (count < 0 || (count && !outputs))
        return nullptr;
    auto request = CelesteUnityVulkanCreatePacketRequest(packet, nullptr, generation, wait);
    if (!request)
        return nullptr;
    try
    {
        request->batchOutputs.reserve(static_cast<size_t>(count));
        for (int32_t index = 0; index < count; ++index)
        {
            auto output = outputs[index];
            if (!output || output->generation != generation)
            {
                Release(request);
                return nullptr;
            }
            request->batchOutputs.push_back(output);
            output->references.fetch_add(1, std::memory_order_relaxed);
        }
        return request;
    }
    catch (...)
    {
        Release(request);
        return nullptr;
    }
}