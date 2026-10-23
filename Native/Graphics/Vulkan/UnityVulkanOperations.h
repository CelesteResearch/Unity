#pragma once
#include "CelesteGraphicsVulkan.h"
#include "IUnityGraphics.h"

struct CelesteUnityVulkanOperation;
struct CelesteUnityVulkanOutputCache;

// Opaque native requests. Create returns caller loan. PrepareEvent adds one event
// loan immediately before enqueue; callback releases it. CancelEvent is legal only
// when caller proves event was never issued. ReleaseOperation drops caller loan.
extern "C"
{
    UNITY_INTERFACE_EXPORT UnityRenderingEventAndData UNITY_INTERFACE_API CelesteUnityVulkanGetOperationEvent();
    UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityVulkanGetOperationEventId();
    UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
    CelesteUnityVulkanCreateAcquireDeviceRequest();
    UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
    CelesteUnityVulkanCreateOutputRequest(void* nativeTexture, uint64_t generation);
    UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
    CelesteUnityVulkanCreatePacketRequest(CelesteGraphicsPacket* packet, CelesteUnityVulkanOutputCache* output,
                                          uint64_t generation, int32_t waitForCompletion);
    UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
    CelesteUnityVulkanCreateDrainRequest(uint64_t generation, int32_t releaseSession);
    UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
    CelesteUnityVulkanPrepareEvent(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API CelesteUnityVulkanCancelEvent(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
    CelesteUnityVulkanGetOperationStatus(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API
    CelesteUnityVulkanGetOperationError(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
    CelesteUnityVulkanWaitOperation(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
    CelesteUnityVulkanReleaseOperation(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT CelesteGraphicsDevice* UNITY_INTERFACE_API
    CelesteUnityVulkanGetOperationDevice(CelesteUnityVulkanOperation* request);
    UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API
    CelesteUnityVulkanGetOperationGeneration(CelesteUnityVulkanOperation* request);
    // Transfers one cache loan from successful output request to caller, at most once.
    UNITY_INTERFACE_EXPORT CelesteUnityVulkanOutputCache* UNITY_INTERFACE_API
    CelesteUnityVulkanTakeOutputCache(CelesteUnityVulkanOperation* request);
    // No Unity API call; descriptor was captured on render thread. One loan per Present.
    UNITY_INTERFACE_EXPORT int32_t UNITY_INTERFACE_API
    CelesteUnityVulkanAcquireOutputLoan(CelesteUnityVulkanOutputCache* cache, CelesteGraphicsOutput** output);
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
    CelesteUnityVulkanReleaseOutputCache(CelesteUnityVulkanOutputCache* cache);
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API
CelesteUnityVulkanGetOperationCompletionEventId(CelesteUnityVulkanOperation* request);

extern "C" UNITY_INTERFACE_EXPORT CelesteUnityVulkanOperation* UNITY_INTERFACE_API
CelesteUnityVulkanCreatePacketBatchRequest(CelesteGraphicsPacket* packet, CelesteUnityVulkanOutputCache* const* outputs,
                                           int32_t count, uint64_t generation, int32_t waitForCompletion);