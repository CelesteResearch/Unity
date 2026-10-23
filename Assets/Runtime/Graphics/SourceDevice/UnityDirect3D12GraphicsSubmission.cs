using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Celeste.Graphics.Direct3D12.Interop;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Devices.Native.Interop;
using Celeste.Graphics.Rendering.Devices.Native.Submission;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed unsafe partial class UnityDirect3D12GraphicsSubmission : INativeGraphicsSubmission, IDisposable
    {
        private readonly SynchronizationContext mainContext;
        private readonly int mainThread;
        private readonly UnitySourceCameraOutput cameraOutput;
        private readonly UnityDirect3D12RenderEvent renderEvent;
        private readonly CelesteGraphicsDeviceApi* api;
        private readonly int previousVSync;
        private readonly List<(IntPtr Packet, IntPtr Fence, ulong Value)> failedCommits = new();
        private IntPtr device;
        private IntPtr lastSubmission;
        private ulong generation;
        private bool disposed;

        internal UnityDirect3D12GraphicsSubmission(Camera camera)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                throw new NotSupportedException("Unity Direct3D12 is required.");
            mainContext = SynchronizationContext.Current ??
                          throw new InvalidOperationException("Construct on Unity main thread.");
            mainThread = Thread.CurrentThread.ManagedThreadId;
            api = Direct3D12Native.GetDeviceApi();
            previousVSync = QualitySettings.vSyncCount;
            renderEvent = new UnityDirect3D12RenderEvent();
            cameraOutput = new UnitySourceCameraOutput(camera, FlushOutput);
        }

        internal RenderTexture GameOutput => cameraOutput.ExistingOutput;

        public IntPtr AcquireDevice() => OnMain(() =>
        {
            ThrowIfDisposed();
            if (device == IntPtr.Zero)
                renderEvent.Run(adapter =>
                {
                    UnityDirect3D12Bindings.Require(
                        UnityDirect3D12Bindings.CelesteUnityDirect3D12CreateDevice(adapter, out device));
                    generation = UnityDirect3D12Bindings.CelesteUnityDirect3D12GetDeviceGeneration();
                });
            RequireTextureGeneration();
            return device;
        });

        public void ReleaseDevice(IntPtr actual) => OnMain(() =>
        {
            renderEvent.Drain();
            if (device == IntPtr.Zero) return;
            if (device != actual) throw new ArgumentException("Wrong Direct3D12 device.", nameof(actual));
            cameraOutput.StopCameraCopy();
            renderEvent.Drain();
            ResumeCommits();
            DrainSubmittedWork();
            // All lists have already executed. The backend drains their real fence on this thread,
            // never blocking the Unity callback that must arrange that submission.
            Direct3D12Native.RequireSuccess(api->DestroyDevice(device));
            device = IntPtr.Zero;
            generation = 0;
        });

        public void PrepareDraw() => OnMain(() =>
        {
            ThrowIfDisposed();
            RequireTextureGeneration();
            cameraOutput.GetOrCreate();
        });

        public void SetPresentationInterval(SourcePresentInterval interval) => OnMain(() =>
            QualitySettings.vSyncCount = interval == SourcePresentInterval.Immediate ? 0 :
                interval == SourcePresentInterval.Two ? 2 : 1);

        public IntPtr AcquireOutput(IntPtr actual) => OnMain(() =>
        {
            ThrowIfDisposed();
            RequireTextureGeneration();
            if (actual != device || device == IntPtr.Zero)
                throw new ArgumentException("Wrong Direct3D12 device.", nameof(actual));
            IntPtr buffer = cameraOutput.GetOrCreate().colorBuffer.GetNativeRenderBufferPtr();
            IntPtr output = IntPtr.Zero;
            renderEvent.Run(adapter =>
                    UnityDirect3D12Bindings.Require(
                        UnityDirect3D12Bindings.CelesteUnityDirect3D12AcquireOutput(adapter, device, buffer,
                            out output)),
                abandonedCleanup: () =>
                {
                    if (output != IntPtr.Zero) api->ReleaseOutput(output);
                });
            return output;
        });

        public void Submit(IntPtr packet, bool waitForCompletion) => OnMain(() =>
        {
            ThrowIfDisposed();
            RequireTextureGeneration();
            ResumeCommits();
            Direct3D12Native.RequireSuccess(Direct3D12Native.CelesteDirect3D12RetainPacket(packet));
            IntPtr submission = IntPtr.Zero, fence = IntPtr.Zero;
            ulong value = 0;
            int executed = 0;
            renderEvent.Run(adapter =>
            {
                if (failedCommits.Capacity == failedCommits.Count)
                    failedCommits.Capacity = Math.Max(4, failedCommits.Count * 2);
                fence = UnityDirect3D12Bindings.CelesteUnityDirect3D12RetainCompletionFence(adapter);
                if (fence == IntPtr.Zero) throw new InvalidOperationException("Unity completion fence unavailable.");
                UnityDirect3D12Bindings.Require(
                    UnityDirect3D12Bindings.CelesteUnityDirect3D12ExecutePacket(adapter, packet, out value,
                        out executed));
            }, () => api->ReleasePacket(packet), completion: adapter =>
            {
                bool failed = false;
                try
                {
                    if (executed != 0)
                    {
                        int result =
                            UnityDirect3D12Bindings.CelesteUnityDirect3D12CompletePacket(adapter, packet, value,
                                out submission);
                        if (result != 1)
                        {
                            failedCommits.Add((packet, fence, value));
                            failed = true;
                        }

                        UnityDirect3D12Bindings.Require(result);
                        AcceptSubmission(submission);
                    }
                }
                finally
                {
                    if (!failed)
                    {
                        if (fence != IntPtr.Zero) Marshal.Release(fence);
                        api->ReleasePacket(packet);
                    }
                }
            });
            if (waitForCompletion) Direct3D12Native.RequireSuccess(api->Wait(submission));
        });

        // Transfer ownership while still in the successful completion callback. A later
        // CPU publication failure must not make Scene retirement wait an older submission.
        private void AcceptSubmission(IntPtr submission)
        {
            if (lastSubmission != IntPtr.Zero) api->ReleaseSubmission(lastSubmission);
            lastSubmission = submission;
        }

        private void ResumeCommits()
        {
            renderEvent.Drain();
            while (failedCommits.Count != 0)
            {
                var pending = failedCommits[0];
                renderEvent.Run(_ => { }, completion: adapter =>
                {
                    UnityDirect3D12Bindings.Require(UnityDirect3D12Bindings.CelesteUnityDirect3D12CompletePacket(
                        adapter, pending.Packet, pending.Value, out IntPtr submission));
                    AcceptSubmission(submission);
                    api->ReleasePacket(pending.Packet);
                    Marshal.Release(pending.Fence);
                    failedCommits.RemoveAt(0);
                });
            }
        }

        public void PresentCompleted()
        {
        }

        internal void StopCameraCopy() => OnMain(cameraOutput.StopCameraCopy);

        private void FlushOutput()
        {
            // Unity holds its own RenderTexture resource while queued copies and the backend output loan retain COM resources.
            // No extra GPU readback or queue-idle is required for an output-size change.
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityDirect3D12GraphicsSubmission));
        }

        private T OnMain<T>(Func<T> action)
        {
            if (Thread.CurrentThread.ManagedThreadId == mainThread) return action();
            T result = default;
            ExceptionDispatchInfo failure = null;
            mainContext.Send(_ =>
            {
                try
                {
                    result = action();
                }
                catch (Exception error)
                {
                    failure = ExceptionDispatchInfo.Capture(error);
                }
            }, null);
            failure?.Throw();
            return result;
        }

        private void OnMain(Action action) => OnMain(() =>
        {
            action();
            return true;
        });

        public void Dispose() => OnMain(() =>
        {
            if (disposed) return;
            renderEvent.Drain();
            if (device != IntPtr.Zero) ReleaseDevice(device);
            cameraOutput.Dispose();
            renderEvent.Dispose();
            QualitySettings.vSyncCount = previousVSync;
            disposed = true;
        });
    }
}
