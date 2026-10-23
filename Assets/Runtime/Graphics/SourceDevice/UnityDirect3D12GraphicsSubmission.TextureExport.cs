using System;
using Celeste.Graphics.Rendering.Devices.Native;
using Celeste.Graphics.Direct3D12.Interop;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed unsafe partial class UnityDirect3D12GraphicsSubmission : IUnityNativeTextureExportHost
    {
        internal UnityNativeTextureBatch CreateTextureBatch(NativeGraphicsDeviceIO source)
        {
            ThrowIfDisposed();
            RequireTextureGeneration();
            return new UnityNativeTextureBatch(this, source, generation);
        }

        bool IUnityNativeTextureExportHost.IsTextureGenerationCurrent(ulong expected)
            => !disposed && device != IntPtr.Zero && expected != 0 && generation == expected &&
               UnityDirect3D12Bindings.CelesteUnityDirect3D12GetDeviceGeneration() == expected;

        private void RequireTextureGeneration()
        {
            if (!((IUnityNativeTextureExportHost)this).IsTextureGenerationCurrent(generation))
                throw new InvalidOperationException("The Direct3D12 texture session has retired.");
        }

        IntPtr IUnityNativeTextureExportHost.CreateTextureOutput(Texture2D texture) => OnMain(() =>
        {
            RequireTextureGeneration();
            IntPtr native = texture.GetNativeTexturePtr();
            IntPtr cache = IntPtr.Zero;
            renderEvent.Run(adapter => UnityDirect3D12Bindings.Require(
                    UnityDirect3D12Bindings.CelesteUnityDirect3D12CreateTextureCache(adapter, device, native,
                        out cache)),
                abandonedCleanup: () =>
                {
                    if (cache != IntPtr.Zero) UnityDirect3D12Bindings.CelesteUnityDirect3D12ReleaseTextureCache(cache);
                });
            return cache;
        });

        IntPtr IUnityNativeTextureExportHost.AcquireTextureOutputLoan(IntPtr cache)
        {
            RequireTextureGeneration();
            UnityDirect3D12Bindings.Require(
                UnityDirect3D12Bindings.CelesteUnityDirect3D12AcquireTextureOutputLoan(cache, out IntPtr output));
            return output;
        }

        void IUnityNativeTextureExportHost.ReleaseTextureOutputCache(IntPtr cache)
            => UnityDirect3D12Bindings.CelesteUnityDirect3D12ReleaseTextureCache(cache);

        void IUnityNativeTextureExportHost.SubmitTextureBatch(IntPtr packet, IntPtr[] outputs, int count)
        {
            RequireTextureGeneration();
            // The shared packet retains every output and describes all expected/current
            // resource states. Unity gets that whole list in one ExecuteCommandList.
            Submit(packet, false);
        }

        void IUnityNativeTextureExportHost.DrainTextureOutputs(ulong expected) => OnMain(() =>
        {
            if (generation != expected) return; // Prior session is already drained at release.
            renderEvent.Drain();
            ResumeCommits();
            DrainSubmittedWork();
        });

        private void DrainSubmittedWork()
        {
            if (lastSubmission == IntPtr.Zero) return;
            Direct3D12Native.RequireSuccess(api->Wait(lastSubmission));
            api->ReleaseSubmission(lastSubmission);
            lastSubmission = IntPtr.Zero;
        }
    }
}
