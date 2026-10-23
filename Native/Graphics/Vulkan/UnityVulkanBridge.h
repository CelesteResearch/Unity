#pragma once
#include "CelesteGraphicsVulkan.h"
#include "IUnityGraphics.h"
extern "C" UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API CelesteUnityVulkanGetDeviceGeneration();
extern "C" UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API CelesteUnityVulkanGetLastError();