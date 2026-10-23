using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Vulkan.Submission;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed partial class UnityVulkanGraphicsSubmission : IVulkanGraphicsSubmission, IDisposable
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
        private bool disposed;

        internal UnityVulkanGraphicsSubmission(Camera camera)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
                throw new NotSupportedException("The Unity Vulkan submission requires Unity's Vulkan device.");
            mainContext = SynchronizationContext.Current ??
                          throw new InvalidOperationException(
                              "Construct the Vulkan submission on Unity's main thread.");
            mainThread = Thread.CurrentThread.ManagedThreadId;
            previousVSync = QualitySettings.vSyncCount;
            cameraOutput = new UnitySourceCameraOutput(camera, FlushOutput);
            synchronizationBuffer = new ComputeBuffer(1, sizeof(uint));
        }

        internal RenderTexture GameOutput => cameraOutput.ExistingOutput;

        public IntPtr AcquireDevice()
        {
            if (!IsMainThread) return InvokeOnMain(AcquireDevice);
            ThrowIfDisposed();
            if (device != IntPtr.Zero)
            {
                RequireGeneration();
                return device;
            }

            IntPtr request = RequireRequest(UnityVulkanBindings.CelesteUnityVulkanCreateAcquireDeviceRequest());
            try
            {
                RunRequest(request);
                IntPtr acquired = UnityVulkanBindings.CelesteUnityVulkanGetOperationDevice(request);
                ulong acquiredGeneration = UnityVulkanBindings.CelesteUnityVulkanGetOperationGeneration(request);
                if (acquired == IntPtr.Zero || acquiredGeneration == 0) ThrowBridgeError();
                device = acquired;
                generation = acquiredGeneration;
                return device;
            }
            finally
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOperation(request);
            }
        }

        public void ReleaseDevice(IntPtr actualDevice)
        {
            if (!IsMainThread)
            {
                InvokeOnMain(() => ReleaseDevice(actualDevice));
                return;
            }

            if (device == IntPtr.Zero) return;
            if (actualDevice != device)
                throw new ArgumentException("The device belongs to another Unity Vulkan session.",
                    nameof(actualDevice));
            RequireGeneration();
            // StopCameraCopy drains pending native use before dropping the cached image descriptor.
            cameraOutput.StopCameraCopy();
            Drain(true);
            device = IntPtr.Zero;
            generation = 0;
        }

        public void PrepareDraw()
        {
            if (!IsMainThread)
            {
                InvokeOnMain(PrepareDraw);
                return;
            }

            ThrowIfDisposed();
            RequireGeneration();
            EnsureOutput();
        }

        public void SetPresentationInterval(SourcePresentInterval interval)
        {
            if (!IsMainThread)
            {
                InvokeOnMain(() => SetPresentationInterval(interval));
                return;
            }

            ThrowIfDisposed();
            QualitySettings.vSyncCount = interval == SourcePresentInterval.Immediate ? 0 :
                interval == SourcePresentInterval.Two ? 2 : 1;
        }

        public IntPtr AcquireOutput(IntPtr actualDevice)
        {
            if (!IsMainThread) return InvokeOnMain(() => AcquireOutput(actualDevice));
            ThrowIfDisposed();
            RequireGeneration();
            if (actualDevice != device)
                throw new ArgumentException("The output belongs to another Unity Vulkan device.", nameof(actualDevice));
            EnsureOutput();
            if (UnityVulkanBindings.CelesteUnityVulkanAcquireOutputLoan(outputCache, out IntPtr output) != 1)
                ThrowBridgeError();
            return output;
        }

        public void Submit(IntPtr packet, bool waitForCompletion)
        {
            if (!IsMainThread)
            {
                InvokeOnMain(() => Submit(packet, waitForCompletion));
                return;
            }

            ThrowIfDisposed();
            RequireGeneration();
            IntPtr request = RequireRequest(UnityVulkanBindings.CelesteUnityVulkanCreatePacketRequest(
                packet, outputCache, generation, waitForCompletion ? 1 : 0));
            try
            {
                // The native thunk owns the packet loan and executes on Unity's render owner.
                // For a read the following completion event flushes host commands and waits a real queue fence.
                RunRequest(request);
            }
            finally
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOperation(request);
            }
        }

        internal void StopCameraCopy()
        {
            if (!IsMainThread)
            {
                InvokeOnMain(StopCameraCopy);
                return;
            }

            cameraOutput.StopCameraCopy();
        }

        public void PresentCompleted()
        {
            // The camera's existing command buffer presents the output already recorded by Submit.
        }

        private void EnsureOutput()
        {
            RenderTexture output = cameraOutput.GetOrCreate();
            if (outputCache != IntPtr.Zero && output == cachedOutput) return;
            IntPtr texture = output.GetNativeTexturePtr();
            FlushOutput();
            IntPtr request =
                RequireRequest(UnityVulkanBindings.CelesteUnityVulkanCreateOutputRequest(texture, generation));
            try
            {
                RunRequest(request);
                outputCache = UnityVulkanBindings.CelesteUnityVulkanTakeOutputCache(request);
                if (outputCache == IntPtr.Zero) ThrowBridgeError();
                cachedOutput = output;
            }
            finally
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOperation(request);
            }
        }

        private void FlushOutput()
        {
            if (device != IntPtr.Zero) Drain(false);
            if (outputCache != IntPtr.Zero)
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOutputCache(outputCache);
                outputCache = IntPtr.Zero;
                cachedOutput = null;
            }
        }

        internal void DrainForWindowChange() => Drain(false);

        private void Drain(bool releaseSession)
        {
            IntPtr request = RequireRequest(UnityVulkanBindings.CelesteUnityVulkanCreateDrainRequest(
                generation, releaseSession ? 1 : 0));
            try
            {
                RunRequest(request);
            }
            finally
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOperation(request);
            }
        }

        private void RunRequest(IntPtr request)
        {
            IntPtr callback = UnityVulkanBindings.CelesteUnityVulkanGetOperationEvent();
            int eventId = UnityVulkanBindings.CelesteUnityVulkanGetOperationEventId();
            if (callback == IntPtr.Zero || eventId < 0) ThrowBridgeError();
            using var commands = new CommandBuffer();
            commands.name = "Celeste Vulkan Submission";
            if (UnityVulkanBindings.CelesteUnityVulkanPrepareEvent(request) != 1) ThrowBridgeError();
            bool possiblyEnqueued = false;
            try
            {
                commands.IssuePluginEventAndData(callback, eventId, request);
                int completionEventId = UnityVulkanBindings.CelesteUnityVulkanGetOperationCompletionEventId(request);
                if (completionEventId >= 0)
                    commands.IssuePluginEventAndData(callback, completionEventId, request);
                possiblyEnqueued = true;
                UnityEngine.Graphics.ExecuteCommandBuffer(commands);
                // Split graphics jobs can defer the queue-access completion event until commands are flushed.
                UnityEngine.GL.Flush();
                // Drives the render callback. This is a CPU barrier, never proof of GPU completion.
                if (synchronizationBuffer.GetNativeBufferPtr() == IntPtr.Zero)
                    throw new InvalidOperationException("Unity Vulkan render synchronization failed.");
                // Native waits only for this request's completion condition. It never submits/polls GPU from this thread.
                int completed = UnityVulkanBindings.CelesteUnityVulkanWaitOperation(request);
                if (completed != 1 || UnityVulkanBindings.CelesteUnityVulkanGetOperationStatus(request) != 1)
                    throw new InvalidOperationException(Marshal.PtrToStringUTF8(
                                                            UnityVulkanBindings.CelesteUnityVulkanGetOperationError(
                                                                request)) ??
                                                        "Unity Vulkan operation failed.");
            }
            finally
            {
                // Once Execute starts it may have enqueued even when it throws. The native event loan
                // must survive until callback completion; only a certainly unissued event can be cancelled.
                if (!possiblyEnqueued) UnityVulkanBindings.CelesteUnityVulkanCancelEvent(request);
            }
        }

        private void RequireGeneration()
        {
            if (device == IntPtr.Zero || generation == 0 ||
                UnityVulkanBindings.CelesteUnityVulkanGetDeviceGeneration() != generation)
                throw new InvalidOperationException("The Unity Vulkan session belongs to a retired graphics device.");
        }

        private static IntPtr RequireRequest(IntPtr request)
        {
            if (request == IntPtr.Zero) ThrowBridgeError();
            return request;
        }

        private static void ThrowBridgeError()
            => throw new InvalidOperationException(Marshal.PtrToStringUTF8(
                UnityVulkanBindings.CelesteUnityVulkanGetLastError()) ?? "Unity Vulkan bridge failed.");

        private bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThread;

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

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityVulkanGraphicsSubmission));
        }

        public void Dispose()
        {
            if (!IsMainThread)
            {
                InvokeOnMain(Dispose);
                return;
            }

            if (disposed) return;
            if (device != IntPtr.Zero) ReleaseDevice(device);
            cameraOutput.Dispose();
            synchronizationBuffer.Dispose();
            QualitySettings.vSyncCount = previousVSync;
            disposed = true;
        }
    }
}
