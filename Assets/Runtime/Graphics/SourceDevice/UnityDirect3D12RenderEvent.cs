using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed class UnityDirect3D12RenderEvent : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void Callback(IntPtr adapter, IntPtr context, int stage);

        private static readonly Callback InvokeCallback = Invoke;
        private static readonly IntPtr CallbackPointer = Marshal.GetFunctionPointerForDelegate(InvokeCallback);
        private readonly List<Invocation> pending = new();
        private readonly ComputeBuffer synchronization = new(1, sizeof(uint));

        private sealed class Invocation
        {
            internal Action<IntPtr> Action;
            internal Action<IntPtr> Completion;
            internal ExceptionDispatchInfo Failure;
            internal readonly ManualResetEventSlim Completed = new(false);
            internal IntPtr Payload;
            internal Action AbandonedCleanup;
            internal bool Finished, Abandoned;
        }

        [MonoPInvokeCallback(typeof(Callback))]
        private static void Invoke(IntPtr adapter, IntPtr context, int stage)
        {
            var handle = GCHandle.FromIntPtr(context);
            var invocation = (Invocation)handle.Target;
            if (stage == 0)
            {
                try
                {
                    invocation.Action(adapter);
                }
                catch (Exception error)
                {
                    invocation.Failure = ExceptionDispatchInfo.Capture(error);
                }

                return;
            }

            try
            {
                invocation.Completion?.Invoke(adapter);
            }
            catch (Exception error)
            {
                invocation.Failure = ExceptionDispatchInfo.Capture(error);
            }
            finally
            {
                Marshal.FreeHGlobal(invocation.Payload);
                handle.Free();
                lock (invocation)
                {
                    invocation.Finished = true;
                    if (invocation.Abandoned) invocation.AbandonedCleanup?.Invoke();
                }

                invocation.Completed.Set();
            }
        }

        internal void Run(Action<IntPtr> action, Action cancelled = null, Action abandonedCleanup = null,
            Action<IntPtr> completion = null)
        {
            Invocation invocation = null;
            GCHandle handle = default;
            bool issued = false;
            try
            {
                IntPtr callback = UnityDirect3D12Bindings.CelesteUnityDirect3D12GetRenderEvent();
                int eventId = UnityDirect3D12Bindings.CelesteUnityDirect3D12GetEventId();
                if (callback == IntPtr.Zero || eventId < 0)
                    throw new InvalidOperationException("Unity Direct3D12 bridge is unavailable.");
                invocation = new Invocation
                    { Action = action, Completion = completion, AbandonedCleanup = abandonedCleanup };
                invocation.Payload = Marshal.AllocHGlobal(IntPtr.Size * 2);
                handle = GCHandle.Alloc(invocation);
                Marshal.WriteIntPtr(invocation.Payload, CallbackPointer);
                Marshal.WriteIntPtr(invocation.Payload, IntPtr.Size, GCHandle.ToIntPtr(handle));
                using var commands = new CommandBuffer { name = "Celeste Direct3D12 Submission" };
                commands.IssuePluginEventAndData(callback, eventId, invocation.Payload);
                commands.IssuePluginEventAndData(callback, eventId + 1, invocation.Payload);
                pending.Add(invocation);
                issued = true;
                UnityEngine.Graphics.ExecuteCommandBuffer(commands);
                synchronization.GetNativeBufferPtr();
                invocation.Completed.Wait(); // CPU callback completion only.
                pending.Remove(invocation);
                invocation.Failure?.Throw();
            }
            catch
            {
                if (issued)
                {
                    lock (invocation)
                    {
                        invocation.Abandoned = true;
                        if (invocation.Finished) invocation.AbandonedCleanup?.Invoke();
                    }
                }

                throw;
            }
            finally
            {
                if (!issued)
                {
                    if (invocation != null && invocation.Payload != IntPtr.Zero)
                        Marshal.FreeHGlobal(invocation.Payload);
                    if (handle.IsAllocated) handle.Free();
                    cancelled?.Invoke();
                }
            }
        }

        internal void Drain()
        {
            if (pending.Count == 0) return;
            synchronization.GetNativeBufferPtr();
            foreach (Invocation invocation in pending) invocation.Completed.Wait();
            pending.Clear();
        }

        public void Dispose()
        {
            Drain();
            synchronization.Dispose();
        }
    }
}
