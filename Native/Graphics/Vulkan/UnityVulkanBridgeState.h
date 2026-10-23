#pragma once
#include "UnityVulkanBridge.h"
#include <atomic>
#define UNITY_VULKAN_HEADER <vulkan/vulkan_core.h>
#include "IUnityGraphicsVulkan.h"
namespace CelesteUnityVulkanBridgeState
{
    extern IUnityGraphicsVulkanV2* vulkan;
    extern CelesteVulkanNativeDevice nativeDevice;
    extern CelesteGraphicsDevice* backendDevice;
    extern std::atomic<uint64_t> publishedGeneration;
    extern uint64_t nextGeneration;

    extern int eventId;
    void Error(char* destination, const char* message);
}
