#pragma once
#include "UnityDirect3D12Adapter.h"
// Native callback payload stays alive until its Unity render event completes.
// Callback is noexcept and copies failures before another backend operation.
// Packet/output caller loans stay alive across scheduling and callback completion.
struct CelesteUnityDirect3D12Invocation
{
    void(UNITY_INTERFACE_API* invoke)(UnityDirect3D12Adapter*, void*, int32_t stage) noexcept;
    void* context;
};
extern "C" UNITY_INTERFACE_EXPORT UnityRenderingEventAndData UNITY_INTERFACE_API CelesteUnityDirect3D12GetRenderEvent();
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12GetEventId();
// These calls are valid only during the invocation above; adapter is not a persistent handle.
extern "C" UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API CelesteUnityDirect3D12GetLastError();
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12CreateDevice(UnityDirect3D12Adapter*,
                                                                                             CelesteGraphicsDevice**);
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12AcquireOutput(UnityDirect3D12Adapter*,
                                                                                              CelesteGraphicsDevice*,
                                                                                              UnityRenderBuffer,
                                                                                              CelesteGraphicsOutput**);
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12ExecutePacket(UnityDirect3D12Adapter*,
                                                                                              CelesteGraphicsPacket*,
                                                                                              uint64_t*, int32_t*);
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12CompletePacket(
    UnityDirect3D12Adapter*, CelesteGraphicsPacket*, uint64_t, CelesteGraphicsSubmission**);

extern "C" UNITY_INTERFACE_EXPORT ID3D12Fence* UNITY_INTERFACE_API
CelesteUnityDirect3D12RetainCompletionFence(UnityDirect3D12Adapter*);

struct CelesteUnityDirect3D12TextureCache;
extern "C" UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API CelesteUnityDirect3D12GetDeviceGeneration();
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API CelesteUnityDirect3D12CreateTextureCache(
    UnityDirect3D12Adapter*, CelesteGraphicsDevice*, ID3D12Resource*, CelesteUnityDirect3D12TextureCache**);
extern "C" UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API
CelesteUnityDirect3D12AcquireTextureOutputLoan(CelesteUnityDirect3D12TextureCache*, CelesteGraphicsOutput**);
extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
CelesteUnityDirect3D12ReleaseTextureCache(CelesteUnityDirect3D12TextureCache*);
