#include <d3d11.h>
#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D11.h"
#include "Direct3D11Device.h"

static IUnityInterfaces* unityInterfaces = nullptr;
static IUnityGraphics* unityGraphics = nullptr;
static ID3D11Device* borrowedDevice = nullptr;
static Direct3D11Device* sourceContext = nullptr;

Direct3D11Device* SourceUnityDeviceContext()
{
    return sourceContext;
}
static int renderEventId = 0;

static void UNITY_INTERFACE_API OnDeviceEvent(UnityGfxDeviceEventType event)
{
    if (event == kUnityGfxDeviceEventInitialize)
    {
        if (borrowedDevice || !unityGraphics || !unityInterfaces)
            return;
        // Loading the D3D11 adapter in another graphics mode is inert.
        if (unityGraphics->GetRenderer() != kUnityGfxRendererD3D11)
            return;
        auto api = unityInterfaces->Get<IUnityGraphicsD3D11>();
        if (!api)
        {
            SourceSetError("Unity did not expose IUnityGraphicsD3D11");
            return;
        }
        auto device = api->GetDevice();
        if (!sourceContext)
            sourceContext = SourceGpuCreateDeviceContext();
        if (sourceContext && SourceGpuAttachD3D11Device(sourceContext, device))
            borrowedDevice = device;
    }
    else if (event == kUnityGfxDeviceEventShutdown)
    {
        SourceGpuDestroySourceDevice(sourceContext);
        borrowedDevice = nullptr;
    }
}

SOURCE_GPU_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* interfaces)
{
    unityInterfaces = interfaces;
    unityGraphics = interfaces->Get<IUnityGraphics>();
    renderEventId = unityGraphics->ReserveEventIDRange(1);
    unityGraphics->RegisterDeviceEventCallback(OnDeviceEvent);
    OnDeviceEvent(kUnityGfxDeviceEventInitialize);
}

SOURCE_GPU_EXPORT void UNITY_INTERFACE_API UnityPluginUnload()
{
    OnDeviceEvent(kUnityGfxDeviceEventShutdown);
    if (unityGraphics)
        unityGraphics->UnregisterDeviceEventCallback(OnDeviceEvent);
    unityGraphics = nullptr;
    unityInterfaces = nullptr;
    SourceGpuReleaseDeviceContext(sourceContext);
    sourceContext = nullptr;
}

static void UNITY_INTERFACE_API RenderPhysicalPacket(int, void* data)
{
    auto packet = static_cast<Direct3D11PhysicalPacket*>(data);
    if (!packet)
        return;
    SourceGpuExecuteBorrowedPacket(packet);
    // Release the asynchronous event loan in the core that allocated the packet.
    SourceGpuReleasePacket(packet);
}

SOURCE_GPU_EXPORT int __cdecl SourceGpuRenderEventId()
{
    return renderEventId;
}
SOURCE_GPU_EXPORT Direct3D11Device* __cdecl SourceGpuAcquireUnityDevice()
{
    OnDeviceEvent(kUnityGfxDeviceEventInitialize);
    if (!borrowedDevice)
    {
        SourceSetError("Unity's D3D11 device is not attached");
        return nullptr;
    }
    // A game teardown destroys the source context while Unity keeps its device.
    // Attach is idempotent while active and recreates the context after teardown.
    return SourceGpuAttachD3D11Device(sourceContext, borrowedDevice);
}
SOURCE_GPU_EXPORT UnityRenderingEventAndData __cdecl SourceGpuGetRenderEventAndData()
{
    return RenderPhysicalPacket;
}

SOURCE_GPU_EXPORT uint64_t __cdecl SourceGpuUnityDeviceGeneration()
{
    return SourceGpuDeviceGeneration(sourceContext);
}
