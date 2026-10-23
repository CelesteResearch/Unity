#include "UnityDirect3D12Bridge.h"
#include "CelesteUnityDirect3D12TextureCache.h"
#include <memory>
#include <stdexcept>
#include <cstring>
#include <exception>
namespace
{
    thread_local char error[512]{};
    void SetError(const char* message) noexcept
    {
        if (!message || !*message)
            message = "Direct3D12 bridge operation failed";
        const size_t size = std::strlen(message);
        const size_t count = size < sizeof(error) - 1 ? size : sizeof(error) - 1;
        std::memcpy(error, message, count);
        error[count] = 0;
    }
    template <class F> int Invoke(UnityDirect3D12Adapter* adapter, F&& operation) noexcept
    {
        error[0] = 0;
        if (!adapter)
        {
            SetError("Unity Direct3D12 renderer is unavailable");
            return 0;
        }
        try
        {
            if (operation())
                return 1;
            SetError(CelesteDirect3D12GetLastError());
        }
        catch (const std::exception& failure)
        {
            SetError(failure.what());
        }
        catch (...)
        {
            SetError("Unexpected Direct3D12 bridge failure");
        }
        return 0;
    }
}
extern "C" UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API CelesteUnityDirect3D12GetLastError()
{
    return error;
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API
CelesteUnityDirect3D12CreateDevice(UnityDirect3D12Adapter* adapter, CelesteGraphicsDevice** output)
{
    if (output)
        *output = nullptr;
    return Invoke(adapter, [&] { return adapter->CreateBorrowedDevice(output); });
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API
CelesteUnityDirect3D12AcquireOutput(UnityDirect3D12Adapter* adapter, CelesteGraphicsDevice* device,
                                    UnityRenderBuffer buffer, CelesteGraphicsOutput** output)
{
    if (output)
        *output = nullptr;
    return Invoke(adapter, [&] { return adapter->AcquireOutput(device, buffer, output); });
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12ExecutePacket(
    UnityDirect3D12Adapter* adapter, CelesteGraphicsPacket* packet, uint64_t* value, int32_t* executed)
{
    if (value)
        *value = 0;
    if (executed)
        *executed = 0;
    return Invoke(adapter, [&] { return adapter->ExecutePacket(packet, value, executed); });
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12CompletePacket(
    UnityDirect3D12Adapter* adapter, CelesteGraphicsPacket* packet, uint64_t value, CelesteGraphicsSubmission** output)
{
    if (output)
        *output = nullptr;
    return Invoke(adapter, [&] { return adapter->CompletePacket(packet, value, output); });
}
extern "C" UNITY_INTERFACE_EXPORT ID3D12Fence* UNITY_INTERFACE_API
CelesteUnityDirect3D12RetainCompletionFence(UnityDirect3D12Adapter* adapter)
{
    return adapter ? adapter->RetainCompletionFence() : nullptr;
}

extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API
CelesteUnityDirect3D12CreateTextureCache(UnityDirect3D12Adapter* adapter, CelesteGraphicsDevice* device,
                                         ID3D12Resource* resource, CelesteUnityDirect3D12TextureCache** output)
{
    if (output)
        *output = nullptr;
    return Invoke(adapter, [&] {
        if (!resource || !device || !output)
            throw std::invalid_argument("Missing Scene texture output arguments");
        const auto desc = resource->GetDesc();
        if (desc.Dimension != D3D12_RESOURCE_DIMENSION_TEXTURE2D || desc.DepthOrArraySize != 1 ||
            desc.SampleDesc.Count != 1 || desc.Width > UINT32_MAX)
            throw std::invalid_argument("Scene output requires a single-sample 2D resource");
        auto cache = std::make_unique<CelesteUnityDirect3D12TextureCache>();
        cache->device = device;
        cache->resource = resource;
        cache->info = {resource,
                       0,
                       static_cast<uint32_t>(desc.Width),
                       desc.Height,
                       desc.Format,
                       D3D12_RESOURCE_STATE_COMMON,
                       D3D12_RESOURCE_STATE_COMMON};
        // Existing backend checks physical device identity and descriptor legality.
        CelesteGraphicsOutput* validated = nullptr;
        if (!CelesteDirect3D12AcquireOutput(device, &cache->info, &validated))
            return 0;
        CelesteDirect3D12GetDeviceApi()->ReleaseOutput(validated);
        *output = cache.release();
        return 1;
    });
}
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12AcquireTextureOutputLoan(
    CelesteUnityDirect3D12TextureCache* cache, CelesteGraphicsOutput** output)
{
    if (output)
        *output = nullptr;
    error[0] = 0;
    if (!cache || !output)
    {
        SetError("Missing Scene texture cache");
        return 0;
    }
    if (CelesteDirect3D12AcquireOutput(cache->device, &cache->info, output))
        return 1;
    SetError(CelesteDirect3D12GetLastError());
    return 0;
}
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
CelesteUnityDirect3D12ReleaseTextureCache(CelesteUnityDirect3D12TextureCache* cache)
{
    delete cache;
}
