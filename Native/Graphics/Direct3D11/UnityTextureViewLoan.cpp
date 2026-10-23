#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "Direct3D11Resources.h"
#include <memory>

Direct3D11Device* SourceUnityDeviceContext();

// One held COM view of the actual source texture; no copy, registry, or draw replay.
struct SourceUnityTextureViewLoan
{
    ID3D11ShaderResourceView* view;
    ~SourceUnityTextureViewLoan()
    {
        view->Release();
    }
};

SOURCE_GPU_EXPORT SourceUnityTextureViewLoan* __cdecl SourceGpuCreateUnityTextureViewLoan(
    SourceGpuTexture* texture, uint64_t generation, uint32_t width, uint32_t height, ID3D11ShaderResourceView** result)
{
    if (!result)
        return nullptr;
    *result = nullptr;
    auto device = SourceUnityDeviceContext();
    if (!device)
        return nullptr;
    std::lock_guard<std::recursive_mutex> lock(device->physicalLock);
    if (!SourceRequireDevice(device) || device->ownsPhysicalDevice || !texture || texture->device != device ||
        texture->generation != generation || generation != device->generation || !texture->resource || !texture->view ||
        texture->dimension != D3D11_RESOURCE_DIMENSION_TEXTURE2D || texture->arraySize != 1 ||
        texture->format != DXGI_FORMAT_R8G8B8A8_UNORM || texture->width != width || texture->height != height)
    {
        SourceSetError("Scene texture loan requires the current Unity device's RGBA8 Texture2D");
        return nullptr;
    }
    D3D11_SHADER_RESOURCE_VIEW_DESC description{};
    texture->view->GetDesc(&description);
    if (description.ViewDimension != D3D11_SRV_DIMENSION_TEXTURE2D ||
        description.Format != DXGI_FORMAT_R8G8B8A8_UNORM || description.Texture2D.MostDetailedMip != 0 ||
        description.Texture2D.MipLevels != texture->levels)
    {
        SourceSetError("Scene texture loan requires an actual non-MSAA Texture2D view");
        return nullptr;
    }
    auto loan = std::make_unique<SourceUnityTextureViewLoan>();
    loan->view = texture->view;
    loan->view->AddRef();
    *result = loan->view;
    return loan.release();
}

// Used only if CreateExternalTexture fails before submitting any Unity draw.
SOURCE_GPU_EXPORT void __cdecl SourceGpuReleaseUnityTextureViewLoan(SourceUnityTextureViewLoan* loan)
{
    delete loan;
}

// Normal disposal is ordered after Unity wrapper destruction and prior queued draws.
static void UNITY_INTERFACE_API ReleaseUnityTextureView(int, void* data)
{
    delete static_cast<SourceUnityTextureViewLoan*>(data);
}

SOURCE_GPU_EXPORT UnityRenderingEventAndData __cdecl SourceGpuGetUnityTextureViewReleaseEvent()
{
    return ReleaseUnityTextureView;
}
