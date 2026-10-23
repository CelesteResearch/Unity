#include "UnityDirect3D12Adapter.h"
#include <limits>
#include <stdexcept>
UnityDirect3D12Adapter::UnityDirect3D12Adapter(IUnityGraphicsD3D12v8& value) : host(value)
{
    HRESULT result = host.GetDevice()->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&completionFence));
    if (FAILED(result))
        throw std::runtime_error("Create completion fence failed");
}
int32_t UnityDirect3D12Adapter::CreateBorrowedDevice(CelesteGraphicsDevice** output)
{
    CelesteDirect3D12BorrowedDeviceInfo info{host.GetDevice()};
    return CelesteDirect3D12CreateBorrowedDevice(&info, output);
}
int32_t UnityDirect3D12Adapter::AcquireOutput(CelesteGraphicsDevice* device, UnityRenderBuffer renderBuffer,
                                              CelesteGraphicsOutput** output)
{
    if (output)
        *output = nullptr;
    if (!renderBuffer || !output)
        throw std::invalid_argument("RenderTexture output is required");
    auto* resource = host.TextureFromRenderBuffer(renderBuffer);
    if (!resource)
        throw std::runtime_error("Unity RenderTexture has no Direct3D12 resource");
    const auto desc = resource->GetDesc();
    if (desc.Dimension != D3D12_RESOURCE_DIMENSION_TEXTURE2D || desc.DepthOrArraySize != 1 ||
        desc.SampleDesc.Count != 1 || desc.Width > UINT32_MAX)
        throw std::invalid_argument("Output requires a single-sample 2D RenderTexture");
    CelesteDirect3D12OutputInfo info{};
    info.resource = resource;
    info.width = static_cast<uint32_t>(desc.Width);
    info.height = desc.Height;
    info.format = desc.Format;
    // ExecuteCommandList's expected/current records make Unity insert the actual
    // incoming transition and learn this outgoing state. No guessed current state.
    info.incomingState = D3D12_RESOURCE_STATE_COMMON;
    info.outgoingState = D3D12_RESOURCE_STATE_COMMON;
    return CelesteDirect3D12AcquireOutput(device, &info, output);
}
int32_t UnityDirect3D12Adapter::ExecutePacket(CelesteGraphicsPacket* packet, uint64_t* actualFenceValue,
                                              int32_t* executed)
{
    if (actualFenceValue)
        *actualFenceValue = 0;
    if (executed)
        *executed = 0;
    if (!packet || !actualFenceValue || !executed)
        throw std::invalid_argument("Missing submission arguments");
    CelesteDirect3D12HostRecording recording{};
    if (!CelesteDirect3D12PrepareHostPacket(packet, &recording))
        return 0;
    try
    {
        if (!recording.commands || recording.resourceStateCount > INT_MAX ||
            (recording.resourceStateCount && !recording.resourceStates))
            throw std::runtime_error("Backend returned invalid host recording");
        states.resize(recording.resourceStateCount);
        for (uint32_t i = 0; i < recording.resourceStateCount; ++i)
        {
            const auto& source = recording.resourceStates[i];
            states[i] = {source.resource, source.expectedState, source.currentState};
        }
    }
    catch (...)
    {
        CelesteDirect3D12CancelPreparedHostPacket(packet);
        throw;
    }
    *executed = 1;
    *actualFenceValue = ++nextCompletion;
    host.ExecuteCommandList(recording.commands, static_cast<int>(states.size()), states.data());
    return 1;
}
int32_t UnityDirect3D12Adapter::CompletePacket(CelesteGraphicsPacket* packet, uint64_t value,
                                               CelesteGraphicsSubmission** output)
{
    if (!value || !packet || !output)
        throw std::invalid_argument("Missing completion arguments");
    // This method runs ONLY in the Allow event on Unity's submission thread,
    // after FlushCommandBuffers has emitted the preceding ExecuteCommandList.
    if (value > lastSignaled)
    {
        auto* queue = host.GetCommandQueue();
        if (!queue)
            throw std::runtime_error("Unity Direct3D12 submission queue unavailable");
        if (FAILED(queue->Signal(completionFence.Get(), value)))
            throw std::runtime_error("Queue completion signal failed");
        lastSignaled = value;
    }
    return CelesteDirect3D12CommitHostPacket(packet, completionFence.Get(), value, output);
}
