#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "Direct3D11Device.h"
#include <atomic>
#include <windows.h>

Direct3D11Device* SourceUnityDeviceContext();

// Retains the actual Unity texture and its source context. No source resource registry or command queue.
struct SourceUnityHostCopyLoan
{
    ID3D11Texture2D* texture;
    Direct3D11Device* device;
    uint64_t generation;
    std::atomic<int> completion{0};
    ~SourceUnityHostCopyLoan()
    {
        texture->Release();
        SourceGpuReleaseDeviceContext(device);
    }
};
SOURCE_GPU_EXPORT SourceUnityHostCopyLoan* __cdecl SourceGpuCreateHostCopyLoan(void* actualUnityTexture)
{
    auto device = SourceUnityDeviceContext();
    if (!device)
        return nullptr;
    std::lock_guard<std::recursive_mutex> lock(device->physicalLock);
    if (!actualUnityTexture || !SourceRequireDevice(device))
        return nullptr;
    ID3D11Texture2D* texture = nullptr;
    if (FAILED(static_cast<IUnknown*>(actualUnityTexture)
                   ->QueryInterface(__uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&texture))))
    {
        SourceSetError("The Unity camera output is not an actual D3D11 Texture2D");
        return nullptr;
    }
    ID3D11Device* owner = nullptr;
    texture->GetDevice(&owner);
    bool same = owner == device->device;
    if (owner)
        owner->Release();
    if (!same)
    {
        texture->Release();
        SourceSetError("The Unity camera output belongs to another physical device");
        return nullptr;
    }
    auto loan = new SourceUnityHostCopyLoan;
    loan->texture = texture;
    loan->device = device;
    SourceGpuRetainDeviceContext(device);
    loan->generation = device->generation;
    return loan;
}
SOURCE_GPU_EXPORT void __cdecl SourceGpuReleaseHostCopyLoan(SourceUnityHostCopyLoan* loan)
{
    delete loan;
}
SOURCE_GPU_EXPORT int __cdecl SourceGpuHostCopyCompletion(SourceUnityHostCopyLoan* loan)
{
    return loan ? loan->completion.load(std::memory_order_acquire) : -1;
}
static DXGI_FORMAT CopyFamily(DXGI_FORMAT format)
{
    switch (format)
    {
    case DXGI_FORMAT_R8G8B8A8_TYPELESS:
    case DXGI_FORMAT_R8G8B8A8_UNORM:
    case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
        return DXGI_FORMAT_R8G8B8A8_TYPELESS;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS:
    case DXGI_FORMAT_B8G8R8A8_UNORM:
    case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
        return DXGI_FORMAT_B8G8R8A8_TYPELESS;
    default:
        return DXGI_FORMAT_UNKNOWN;
    }
}
static void UNITY_INTERFACE_API CopyToActualCameraTarget(int, void* data)
{
    auto loan = static_cast<SourceUnityHostCopyLoan*>(data);
    if (!loan)
        return;
    auto device = loan->device;
    std::lock_guard<std::recursive_mutex> lock(device->physicalLock);
    if (!SourceRequireDevice(device) || loan->generation != device->generation)
    {
        SourceSetError("Camera copy belongs to a disposed Unity device generation");
        loan->completion.store(-1);
        return;
    }
    device->renderThread = GetCurrentThreadId();
    ID3D11RenderTargetView* views[8] = {};
    ID3D11DepthStencilView* depth = nullptr;
    device->context->OMGetRenderTargets(8, views, &depth);
    ID3D11Resource* resource = nullptr;
    ID3D11Texture2D* target = nullptr;
    if (views[0])
        views[0]->GetResource(&resource);
    if (resource)
    {
        resource->QueryInterface(__uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&target));
        resource->Release();
    }
    bool valid = false;
    if (target)
    {
        D3D11_TEXTURE2D_DESC from{}, to{};
        loan->texture->GetDesc(&from);
        target->GetDesc(&to);
        valid = from.Width == to.Width && from.Height == to.Height && from.ArraySize == 1 && to.ArraySize == 1 &&
                from.MipLevels == to.MipLevels && from.SampleDesc.Count == 1 && to.SampleDesc.Count == 1 &&
                CopyFamily(from.Format) != DXGI_FORMAT_UNKNOWN && CopyFamily(from.Format) == CopyFamily(to.Format);
        if (valid)
        {
            // A byte copy, without shader, gamma transform, layout/sort or source state mutation.
            device->context->OMSetRenderTargets(0, nullptr, nullptr);
            device->context->CopyResource(target, loan->texture);
            device->context->OMSetRenderTargets(8, views, depth);
        }
        target->Release();
    }
    if (depth)
        depth->Release();
    for (auto view : views)
        if (view)
            view->Release();
    if (!valid)
        SourceSetError("The actual camera target is not size/format/MSAA compatible with the Unity host output");
    loan->completion.store(valid ? 1 : -1, std::memory_order_release);
}
SOURCE_GPU_EXPORT UnityRenderingEventAndData __cdecl SourceGpuGetHostCopyEvent()
{
    return CopyToActualCameraTarget;
}
