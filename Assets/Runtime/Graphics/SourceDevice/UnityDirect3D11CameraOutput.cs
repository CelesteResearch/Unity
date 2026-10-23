using Celeste.Graphics.Direct3D11.Submission;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
// One physical Unity output. It is not a source texture, source scene or SpriteBatch.
    internal sealed class UnityDirect3D11CameraOutput : IDisposable
    {
        private readonly Camera activeGameCamera;
        private readonly int mainThread;
        private RenderTexture output;
        private CommandBuffer finalCopy;
        private IntPtr copyLoan;
        private bool disposed;
        private bool cameraCopyStopped;
        internal ComputeBuffer SynchronizationBuffer { get; }
        internal RenderTexture ExistingOutput => disposed || cameraCopyStopped ? null : output;

        internal UnityDirect3D11CameraOutput(Camera activeGameCamera,
            int unityMainThreadId)
        {
            this.activeGameCamera = activeGameCamera ?? throw new ArgumentNullException(nameof(activeGameCamera));
            mainThread = unityMainThreadId;
            RequireMainThread();
            SynchronizationBuffer = new ComputeBuffer(1, sizeof(uint));
            try
            {
                if (UnityDirect3D11Bindings.SourceGpuAcquireUnityDevice() == IntPtr.Zero) ThrowPhysicalError();
                EnsureOutput();
            }
            catch
            {
                SynchronizationBuffer.Dispose();
                throw;
            }
        }

        internal RenderTargetIdentifier BorrowOutput()
        {
            EnsureOutput();
            return new RenderTargetIdentifier(output);
        }

        // Called on the existing Unity main context, before submitting source GPU work.
        // Resize drains existing physical events before releasing their borrowed texture.
        internal void EnsureOutput()
        {
            RequireMainThread();
            if (disposed) throw new ObjectDisposedException(nameof(UnityDirect3D11CameraOutput));
            if (GraphicsSettings.currentRenderPipeline != null ||
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new NotSupportedException("The actual camera output requires Unity Builtin D3D11.");
            if (!activeGameCamera.isActiveAndEnabled || activeGameCamera.targetTexture != null ||
                activeGameCamera.targetDisplay != 0 || activeGameCamera.allowHDR || activeGameCamera.allowMSAA ||
                activeGameCamera.rect != new Rect(0, 0, 1, 1))
                throw new InvalidOperationException("Supply the active full-window LDR, non-MSAA game camera.");
            int width = activeGameCamera.pixelWidth;
            int height = activeGameCamera.pixelHeight;
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The actual Unity camera output has no drawable size.");
            if (output != null && output.width == width && output.height == height)
            {
                if (UnityDirect3D11Bindings.SourceGpuHostCopyCompletion(copyLoan) < 0) ThrowPhysicalError();
                return;
            }

            GraphicsFormat format =
                GraphicsFormatUtility.GetLinearFormat(SystemInfo.GetGraphicsFormat(DefaultFormat.LDR));
            if (format != GraphicsFormat.R8G8B8A8_UNorm && format != GraphicsFormat.B8G8R8A8_UNorm)
                throw new NotSupportedException("The host must expose a byte-preserving RGBA8/BGRA8 output.");
            ReleaseOutput();
            output = new RenderTexture(new RenderTextureDescriptor(width, height, format, 0)
                { msaaSamples = 1, useMipMap = false, autoGenerateMips = false });
            output.name = "Celeste Source Physical Output";
            if (!output.Create())
            {
                DestroyOutput(output);
                output = null;
                throw new InvalidOperationException("Unity output allocation failed.");
            }

            try
            {
                copyLoan = UnityDirect3D11Bindings.SourceGpuCreateHostCopyLoan(output.GetNativeTexturePtr());
                if (copyLoan == IntPtr.Zero) ThrowPhysicalError();
                if (!cameraCopyStopped)
                {
                    finalCopy = new CommandBuffer { name = "Celeste Source Physical Camera Copy" };
                    finalCopy.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    finalCopy.IssuePluginEventAndData(UnityDirect3D11Bindings.SourceGpuGetHostCopyEvent(), 0, copyLoan);
                    activeGameCamera.AddCommandBuffer(CameraEvent.AfterEverything, finalCopy);
                }
            }
            catch
            {
                ReleaseOutput();
                throw;
            }
        }

        private void RequireMainThread()
        {
            if (mainThread <= 0 || Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("Camera output operations require the existing Unity main thread.");
        }

        internal void StopCameraCopy()
        {
            RequireMainThread();
            cameraCopyStopped = true;
            ReleaseCameraCopy();
        }

        private void ReleaseCameraCopy()
        {
            if (finalCopy != null)
            {
                activeGameCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, finalCopy);
                // Flush already-enqueued reusable camera callbacks before freeing their loan.
                using (var barrierCommands = new CommandBuffer())
                    global::UnityEngine.Graphics.ExecuteCommandBuffer(barrierCommands);
                var barrier = AsyncGPUReadback.Request(SynchronizationBuffer);
                barrier.WaitForCompletion();
                if (barrier.hasError)
                    throw new InvalidOperationException(
                        "Camera output release synchronization failed; its resource loan remains retained.");
                finalCopy.Dispose();
                finalCopy = null;
            }
        }

        private void ReleaseOutput()
        {
            ReleaseCameraCopy();

            if (copyLoan != IntPtr.Zero)
            {
                UnityDirect3D11Bindings.SourceGpuReleaseHostCopyLoan(copyLoan);
                copyLoan = IntPtr.Zero;
            }

            if (output != null)
            {
                output.Release();
                DestroyOutput(output);
                output = null;
            }
        }

        private static void DestroyOutput(RenderTexture texture)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }

        public void Dispose()
        {
            RequireMainThread();
            if (disposed) return;
            ReleaseOutput();
            SynchronizationBuffer.Dispose();
            disposed = true;
        }

        private static void ThrowPhysicalError()
            => throw new InvalidOperationException(
                Direct3D11GraphicsPackets.LastError ?? "Actual Unity camera copy failed.");
    }
}
