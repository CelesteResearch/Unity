#include "UnityDirect3D12Bridge.h"
#include <memory>
#include <atomic>
namespace
{
    IUnityInterfaces* interfaces;
    IUnityGraphics* graphics;
    std::unique_ptr<UnityDirect3D12Adapter> adapter;
    int eventId = -1;
    std::atomic<uint64_t> generation{0};
    uint64_t nextGeneration = 0;
    void UNITY_INTERFACE_API DeviceEvent(UnityGfxDeviceEventType type)
    {
        if (type == kUnityGfxDeviceEventShutdown || type == kUnityGfxDeviceEventBeforeReset)
        {
            generation.store(0, std::memory_order_release);
            adapter.reset();
            return;
        }
        if (type != kUnityGfxDeviceEventInitialize && type != kUnityGfxDeviceEventAfterReset)
            return;
        if (!graphics || graphics->GetRenderer() != kUnityGfxRendererD3D12)
            return;
        auto* host = interfaces->Get<IUnityGraphicsD3D12v8>();
        if (!host)
            return;
        UnityD3D12PluginEventConfig config{};
        config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
        config.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers;
        host->ConfigureEvent(eventId, &config);
        config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_Allow;
        config.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers | kUnityD3D12EventConfigFlag_SyncWorkerThreads;
        host->ConfigureEvent(eventId + 1, &config);
        try
        {
            adapter = std::make_unique<UnityDirect3D12Adapter>(*host);
            generation.store(++nextGeneration, std::memory_order_release);
        }
        catch (...)
        {
            adapter.reset();
        }
    }
    void UNITY_INTERFACE_API RenderEvent(int id, void* payload)
    {
        if ((id != eventId && id != eventId + 1) || !payload)
            return;
        auto& invocation = *static_cast<CelesteUnityDirect3D12Invocation*>(payload);
        // Null means renderer unavailable/reset: callback must report failure, not success.
        if (invocation.invoke)
            invocation.invoke(adapter.get(), invocation.context, id == eventId ? 0 : 1);
    }
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* value)
{
    interfaces = value;
    graphics = value->Get<IUnityGraphics>();
    if (!graphics)
        return;
    eventId = graphics->ReserveEventIDRange(2);
    graphics->RegisterDeviceEventCallback(DeviceEvent);
    DeviceEvent(kUnityGfxDeviceEventInitialize);
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginUnload()
{
    if (graphics)
        graphics->UnregisterDeviceEventCallback(DeviceEvent);
    generation.store(0, std::memory_order_release);
    adapter.reset();
    graphics = nullptr;
    interfaces = nullptr;
    eventId = -1;
}
extern "C" UNITY_INTERFACE_EXPORT UnityRenderingEventAndData UNITY_INTERFACE_API CelesteUnityDirect3D12GetRenderEvent()
{
    return RenderEvent;
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12GetEventId()
{
    return eventId;
}

extern "C" UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API CelesteUnityDirect3D12GetDeviceGeneration()
{
    return generation.load(std::memory_order_acquire);
}
