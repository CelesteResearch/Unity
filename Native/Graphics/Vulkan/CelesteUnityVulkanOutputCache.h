#pragma once
#include "CelesteGraphicsVulkan.h"
#include <atomic>
struct CelesteUnityVulkanOutputCache
{
    std::atomic<uint32_t> references{1};
    CelesteGraphicsDevice* device;
    uint64_t generation;
    void* nativeTexture;
    CelesteVulkanOutputInfo output;
};
