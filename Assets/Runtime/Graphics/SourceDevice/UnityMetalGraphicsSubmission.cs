using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Vulkan.Devices;
using Celeste.Graphics.Vulkan.Submission;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed class UnityMetalGraphicsSubmission : IVulkanGraphicsSubmission, IUnityNativeTextureExportHost,
        IDisposable
    {
        private readonly SynchronizationContext mainContext;
        private readonly int mainThread;
        private readonly UnitySourceCameraOutput cameraOutput;
        private readonly ComputeBuffer synchronizationBuffer;
        private readonly int previousVSync;
        private IntPtr device;
        private ulong generation;
        private IntPtr outputCache;
        private RenderTexture cachedOutput;
        private int fencedFrame = -1;
        private bool disposed;

        internal UnityMetalGraphicsSubmission(Camera camera)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal)
                throw new NotSupportedException("The Metal presentation bridge requires Unity's Metal device.");
            mainContext = SynchronizationContext.Current ??
                          throw new InvalidOperationException(
                              "Construct the Metal presentation bridge on Unity's main thread.");
            mainThread = Thread.CurrentThread.ManagedThreadId;
            previousVSync = QualitySettings.vSyncCount;
            cameraOutput = new UnitySourceCameraOutput(camera, FlushOutput);
            synchronizationBuffer = new ComputeBuffer(1, sizeof(uint));
        }

        internal RenderTexture GameOutput => cameraOutput.ExistingOutput;
        private bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThread;

        public IntPtr AcquireDevice()
        {
            if (!IsMainThread) return InvokeOnMain(AcquireDevice);
            if (disposed) throw new ObjectDisposedException(nameof(UnityMetalGraphicsSubmission));
            if (device != IntPtr.Zero)
            {
                RequireDevice();
                return device;
            }

            Require(Native.CelesteUnityMetalAcquireDevice(out device));
            generation = Native.CelesteUnityMetalGetGeneration();
            return device;
        }

        public void ReleaseDevice(IntPtr value)
        {
            if (!IsMainThread)
            {
                InvokeOnMain(() => ReleaseDevice(value));
                return;
            }

            if (device == IntPtr.Zero) return;
            if (value != device)
                throw new ArgumentException("The device belongs to another Metal session.", nameof(value));
            try
            {
                cameraOutput.StopCameraCopy();
                FlushOutput();
            }
            finally
            {
                Require(Native.CelesteUnityMetalReleaseDevice(device));
                device = IntPtr.Zero;
                generation = 0;
            }
        }

        public void PrepareDraw()
        {
            if (!IsMainThread)
            {
                InvokeOnMain(PrepareDraw);
                return;
            }

            RequireDevice();
            FenceUnityReads(false);
            EnsureOutput();
        }

        public void SetPresentationInterval(SourcePresentInterval interval)
        {
            if (!IsMainThread)
            {
                InvokeOnMain(() => SetPresentationInterval(interval));
                return;
            }

            RequireDevice();
            QualitySettings.vSyncCount = interval == SourcePresentInterval.Immediate ? 0 :
                interval == SourcePresentInterval.Two ? 2 : 1;
        }

        public IntPtr AcquireOutput(IntPtr value)
        {
            if (!IsMainThread) return InvokeOnMain(() => AcquireOutput(value));
            RequireDevice();
            if (value != device)
                throw new ArgumentException("The output belongs to another Metal session.", nameof(value));
            EnsureOutput();
            Require(Native.CelesteUnityMetalAcquireOutput(outputCache, out IntPtr output));
            return output;
        }

        public void Submit(IntPtr packet, bool waitForCompletion)
        {
            if (!IsMainThread)
            {
                InvokeOnMain(() => Submit(packet, waitForCompletion));
                return;
            }

            RequireDevice();
            // Independent MoltenVK and Unity Metal queues share image storage, never command ownership.
            // The Vulkan fence completes before the camera or Scene is allowed to sample that storage.
            Require(Native.CelesteUnityMetalSubmit(packet));
        }

        public void PresentCompleted()
        {
        }

        internal void StopCameraCopy() => cameraOutput.StopCameraCopy();

        private void FenceUnityReads(bool force)
        {
            if (!force && fencedFrame == Time.frameCount) return;
            IntPtr ticket = Native.CelesteUnityMetalCreateFence();
            if (ticket == IntPtr.Zero)
                throw new InvalidOperationException("Cannot allocate the Metal completion marker.");
            try
            {
                using var commands = new CommandBuffer();
                commands.name = "Celeste Metal Image Ownership";
                commands.IssuePluginEventAndData(Native.CelesteUnityMetalFenceEvent(), 0, ticket);
                UnityEngine.Graphics.ExecuteCommandBuffer(commands);
                UnityEngine.GL.Flush();
                // This pointer query drives the render callback; it is not the GPU completion proof.
                // The native Unity Metal V2 commit/wait establishes actual image ownership.
                if (synchronizationBuffer.GetNativeBufferPtr() == IntPtr.Zero)
                    throw new InvalidOperationException("Unity Metal render synchronization failed.");
                if (Native.CelesteUnityMetalWaitFence(ticket) != 1)
                    throw new InvalidOperationException("Unity's Metal command buffer did not complete successfully.");
                fencedFrame = Time.frameCount;
            }
            finally
            {
                Native.CelesteUnityMetalReleaseFence(ticket);
            }
        }

        private void EnsureOutput()
        {
            RenderTexture output = cameraOutput.GetOrCreate();
            if (outputCache != IntPtr.Zero && output == cachedOutput) return;
            FlushOutput();
            IntPtr texture = output.GetNativeTexturePtr();
            FenceUnityReads(true);
            outputCache = Native.CelesteUnityMetalImportTexture(texture);
            if (outputCache == IntPtr.Zero) ThrowNativeError();
            cachedOutput = output;
        }

        private void FlushOutput()
        {
            if (device == IntPtr.Zero) return;
            if (generation == Native.CelesteUnityMetalGetGeneration()) FenceUnityReads(true);
            if (outputCache != IntPtr.Zero) Native.CelesteUnityMetalReleaseOutput(outputCache);
            outputCache = IntPtr.Zero;
            cachedOutput = null;
        }

        private void RequireDevice()
        {
            if (disposed || device == IntPtr.Zero || generation != Native.CelesteUnityMetalGetGeneration())
                throw new InvalidOperationException("The Metal presentation session has retired.");
        }

        private static void Require(int status)
        {
            if (status != 1) ThrowNativeError();
        }

        private static void ThrowNativeError() => throw new InvalidOperationException(
            Marshal.PtrToStringUTF8(Native.CelesteUnityMetalGetLastError()) ?? "The Metal presentation bridge failed.");

        private T InvokeOnMain<T>(Func<T> operation)
        {
            T result = default;
            ExceptionDispatchInfo failure = null;
            mainContext.Send(_ =>
            {
                try
                {
                    result = operation();
                }
                catch (Exception error)
                {
                    failure = ExceptionDispatchInfo.Capture(error);
                }
            }, null);
            failure?.Throw();
            return result;
        }

        private void InvokeOnMain(Action operation) => InvokeOnMain(() =>
        {
            operation();
            return true;
        });

        internal UnityNativeTextureBatch CreateTextureBatch(VulkanGraphicsDeviceIO source)
        {
            RequireDevice();
            return new UnityNativeTextureBatch(this, source, generation);
        }

        bool IUnityNativeTextureExportHost.IsTextureGenerationCurrent(ulong expected) => !disposed &&
            device != IntPtr.Zero && expected == generation && generation == Native.CelesteUnityMetalGetGeneration();

        IntPtr IUnityNativeTextureExportHost.CreateTextureOutput(Texture2D texture)
        {
            RequireDevice();
            IntPtr nativeTexture = texture.GetNativeTexturePtr();
            FenceUnityReads(true);
            IntPtr cache = Native.CelesteUnityMetalImportTexture(nativeTexture);
            if (cache == IntPtr.Zero) ThrowNativeError();
            return cache;
        }

        IntPtr IUnityNativeTextureExportHost.AcquireTextureOutputLoan(IntPtr cache)
        {
            Require(Native.CelesteUnityMetalAcquireOutput(cache, out IntPtr output));
            return output;
        }

        void IUnityNativeTextureExportHost.ReleaseTextureOutputCache(IntPtr cache) =>
            Native.CelesteUnityMetalReleaseOutput(cache);

        void IUnityNativeTextureExportHost.SubmitTextureBatch(IntPtr packet, IntPtr[] outputs, int count)
        {
            RequireDevice();
            // Scene repaint consumption can occur repeatedly within one Time.frameCount.
            FenceUnityReads(true);
            Require(Native.CelesteUnityMetalSubmit(packet));
        }

        void IUnityNativeTextureExportHost.DrainTextureOutputs(ulong expected)
        {
            if (expected == generation && device != IntPtr.Zero &&
                generation == Native.CelesteUnityMetalGetGeneration())
                FenceUnityReads(true);
        }

        public void Dispose()
        {
            if (!IsMainThread)
            {
                InvokeOnMain(Dispose);
                return;
            }

            if (disposed) return;
            try
            {
                if (device != IntPtr.Zero) ReleaseDevice(device);
            }
            finally
            {
                try
                {
                    cameraOutput.Dispose();
                }
                finally
                {
                    try
                    {
                        synchronizationBuffer.Dispose();
                    }
                    finally
                    {
                        QualitySettings.vSyncCount = previousVSync;
                        disposed = true;
                    }
                }
            }
        }

        private static class Native
        {
            private const string Library = "GfxPluginCelesteGraphicsUnityMetal";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr CelesteUnityMetalGetLastError();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern ulong CelesteUnityMetalGetGeneration();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr CelesteUnityMetalFenceEvent();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr CelesteUnityMetalCreateFence();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityMetalWaitFence(IntPtr fence);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void CelesteUnityMetalReleaseFence(IntPtr fence);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityMetalAcquireDevice(out IntPtr device);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityMetalReleaseDevice(IntPtr device);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr CelesteUnityMetalImportTexture(IntPtr texture);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityMetalAcquireOutput(IntPtr cache, out IntPtr output);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void CelesteUnityMetalReleaseOutput(IntPtr cache);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int CelesteUnityMetalSubmit(IntPtr packet);
        }
    }
}
