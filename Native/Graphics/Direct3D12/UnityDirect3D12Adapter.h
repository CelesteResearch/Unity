#pragma once
#include "CelesteGraphicsDirect3D12.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"
#include <vector>
#include <wrl/client.h>
// All methods run exclusively inside Unity's configured rendering event.
// The backend device belongs to the caller and must close before Unity shutdown.
class UnityDirect3D12Adapter final
{
    IUnityGraphicsD3D12v8& host;
    std::vector<UnityGraphicsD3D12ResourceState> states;
    Microsoft::WRL::ComPtr<ID3D12Fence> completionFence;
    uint64_t nextCompletion = 0;
    uint64_t lastSignaled = 0; // Submission-thread only.

  public:
    explicit UnityDirect3D12Adapter(IUnityGraphicsD3D12v8& value);
    ID3D12Fence* RetainCompletionFence()
    {
        auto* fence = completionFence.Get();
        fence->AddRef();
        return fence;
    }
    int32_t CreateBorrowedDevice(CelesteGraphicsDevice** output);
    int32_t AcquireOutput(CelesteGraphicsDevice*, UnityRenderBuffer, CelesteGraphicsOutput**);
    // Failure after execution keeps the prepared packet alive. It cannot be cancelled.
    int32_t ExecutePacket(CelesteGraphicsPacket*, uint64_t* completionValue, int32_t* executed);
    int32_t CompletePacket(CelesteGraphicsPacket*, uint64_t completionValue, CelesteGraphicsSubmission**);
};
