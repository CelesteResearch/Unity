#pragma once
#include <vulkan/vulkan_core.h>
#include "IUnityInterface.h"
PFN_vkGetInstanceProcAddr UNITY_INTERFACE_API
CelesteUnityVulkanInterceptInitialization(PFN_vkGetInstanceProcAddr original, void*);
uint32_t CelesteUnityVulkanRequestedVersion(VkInstance instance);
bool CelesteUnityVulkanEnabledFeatures(VkDevice device, VkPhysicalDeviceFeatures* features);

bool CelesteUnityVulkanInitializationWasInvoked();
