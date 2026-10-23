using Celeste.Graphics.Direct3D11.Submission;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed class UnityDirect3D11GraphicsSubmission : IDirect3D11GraphicsSubmission
    {
        private readonly SynchronizationContext unityMainContext;
        private readonly int unityMainThreadId;
        private readonly UnityDirect3D11CameraOutput hostOutput;

        // The caller supplies Unity's actual output and its physical synchronization buffer.
        // Neither object is a source GraphicsDevice resource or a second swap chain.
        internal UnityDirect3D11GraphicsSubmission(SynchronizationContext unityMainContext,
            int unityMainThreadId, UnityDirect3D11CameraOutput hostOutput)
        {
            this.unityMainContext = unityMainContext ?? throw new ArgumentNullException(nameof(unityMainContext));
            if (unityMainThreadId <= 0) throw new ArgumentOutOfRangeException(nameof(unityMainThreadId));
            this.unityMainThreadId = unityMainThreadId;
            this.hostOutput = hostOutput ?? throw new ArgumentNullException(nameof(hostOutput));
        }

        public void SubmitAndWait(IntPtr sealedPhysicalPacket)
        {
            if (Thread.CurrentThread.ManagedThreadId == unityMainThreadId)
            {
                SubmitOnUnityMainThread(sealedPhysicalPacket);
                return;
            }

            ExceptionDispatchInfo failure = null;
            unityMainContext.Send(_ =>
            {
                try
                {
                    SubmitOnUnityMainThread(sealedPhysicalPacket);
                }
                catch (Exception error)
                {
                    failure = ExceptionDispatchInfo.Capture(error);
                }
            }, null);
            failure?.Throw();
        }

        public void SetPresentationSyncInterval(int count)
        {
            if (Thread.CurrentThread.ManagedThreadId == unityMainThreadId)
            {
                QualitySettings.vSyncCount = count;
                return;
            }

            ExceptionDispatchInfo failure = null;
            unityMainContext.Send(_ =>
            {
                try
                {
                    QualitySettings.vSyncCount = count;
                }
                catch (Exception error)
                {
                    failure = ExceptionDispatchInfo.Capture(error);
                }
            }, null);
            failure?.Throw();
        }

        public IntPtr AcquireDevice() => UnityDirect3D11Bindings.SourceGpuAcquireUnityDevice();

        public void PrepareDraw()
        {
        }

        // The existing Unity camera command buffer consumes native Present output.
        public void PresentCompleted()
        {
        }

        private void SubmitOnUnityMainThread(IntPtr sealedPhysicalPacket)
        {
            if (sealedPhysicalPacket == IntPtr.Zero)
                throw new ArgumentException("A sealed physical packet is required.", nameof(sealedPhysicalPacket));
            hostOutput.EnsureOutput();
            Direct3D11GraphicsPackets.Retain(sealedPhysicalPacket);
            bool eventLoanTransferred = false;
            Direct3D11GraphicsPackets.Retain(sealedPhysicalPacket);
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(hostOutput.BorrowOutput());
                    commands.IssuePluginEventAndData(
                        UnityDirect3D11Bindings.SourceGpuGetRenderEventAndData(),
                        UnityDirect3D11Bindings.SourceGpuRenderEventId(), sealedPhysicalPacket);
                    // Execution may enqueue before throwing. Transfer ownership before
                    // the call; a failed render submission is drained by host shutdown.
                    eventLoanTransferred = true;
                    global::UnityEngine.Graphics.ExecuteCommandBuffer(commands);
                }

                // GetNativeBufferPtr synchronizes Unity's rendering thread. The packet's
                // acquire/release completion reports native CPU execution, not GPU completion.
                if (hostOutput.SynchronizationBuffer.GetNativeBufferPtr() == IntPtr.Zero)
                    throw new InvalidOperationException("Unity render-thread submission synchronization failed.");
                int completed = Direct3D11GraphicsPackets.GetCompletion(sealedPhysicalPacket);
                if (completed != 1)
                {
                    string error = Direct3D11GraphicsPackets.LastError;
                    throw new InvalidOperationException(error ??
                                                        "Unity physical packet did not complete after the render-thread barrier.");
                }
            }
            finally
            {
                if (!eventLoanTransferred)
                    Direct3D11GraphicsPackets.Release(sealedPhysicalPacket);
                Direct3D11GraphicsPackets.Release(sealedPhysicalPacket);
            }
        }
    }
}
