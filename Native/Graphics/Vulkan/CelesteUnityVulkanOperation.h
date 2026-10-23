#pragma once
#include "UnityVulkanOperations.h"
#include "CelesteUnityVulkanOutputCache.h"
#include <atomic>
#include <condition_variable>
#include <mutex>
#include <vector>
struct CelesteUnityVulkanOperation
{
    enum class Kind
    {
        AcquireDevice,
        ConfigureOutput,
        Packet,
        Drain
    };
    Kind kind;
    std::atomic<uint32_t> references{1};
    std::atomic<int32_t> status{0};
    std::atomic<bool> queued{false};
    uint64_t generation = 0;
    CelesteGraphicsDevice* device = nullptr;
    CelesteVulkanNativeDevice completionDevice{};

    CelesteGraphicsPacket* packet = nullptr;
    CelesteUnityVulkanOutputCache* output = nullptr;
    std::vector<CelesteUnityVulkanOutputCache*> batchOutputs;
    void* nativeTexture = nullptr;
    std::atomic<bool> recorded{false};
    bool wait = false;
    bool releaseSession = false;
    char error[512]{};
    std::mutex completionMutex;
    std::condition_variable completion;
    explicit CelesteUnityVulkanOperation(Kind value) : kind(value)
    {
    }
};
