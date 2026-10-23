#pragma once
#include "CelesteGraphicsDirect3D12.h"
#include <wrl/client.h>
struct CelesteUnityDirect3D12TextureCache final
{
    CelesteGraphicsDevice* device;
    Microsoft::WRL::ComPtr<ID3D12Resource> resource;
    CelesteDirect3D12OutputInfo info;
};
