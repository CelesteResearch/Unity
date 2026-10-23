using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AOT;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    public sealed partial class FmodStudioHtml5DeviceIO
    {
        private static readonly ConcurrentDictionary<int, TaskCompletionSource<FmodStudioHtml5DeviceIO>> Loads = new();
        private static readonly FmodHtml5NativeMethods.LoadCallback ReadyCallback = CompleteLoad;
        private static readonly Action<int, IntPtr> FreeCallbackMemory = FmodHtml5NativeMethods.Free;
        private static readonly Action<int> ReleaseCallbackModule = FmodHtml5NativeMethods.DisposeModule;
        private static readonly Action<Exception> ReportCallbackError = UnityEngine.Debug.LogException;
        private static int nextRequestId;

        public static async Task<FmodStudioHtml5DeviceIO> LoadAsync(string scriptUrl, string wasmUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                throw new ArgumentException("The FMOD script URL is required.", nameof(scriptUrl));
            if (string.IsNullOrEmpty(wasmUrl))
                throw new ArgumentException("The FMOD Wasm URL is required.", nameof(wasmUrl));
            if (IntPtr.Size != 4)
                throw new PlatformNotSupportedException("FMOD HTML5 requires the Unity Wasm32 heap ABI.");
            cancellationToken.ThrowIfCancellationRequested();
            var ownerContext = SynchronizationContext.Current ??
                               throw new InvalidOperationException(
                                   "FMOD HTML5 loading requires the Unity owner synchronization context.");
            var requestId = Interlocked.Increment(ref nextRequestId);
            if (requestId <= 0) throw new InvalidOperationException("FMOD load request identifiers are exhausted.");
            var completion = new TaskCompletionSource<FmodStudioHtml5DeviceIO>();
            if (!Loads.TryAdd(requestId, completion))
                throw new InvalidOperationException("FMOD load request already exists.");
            try
            {
                FmodHtml5NativeMethods.Load(requestId, Encoding.UTF8.GetBytes(scriptUrl + "\0"),
                    Encoding.UTF8.GetBytes(wasmUrl + "\0"), ReadyCallback);
                using var registration =
                    cancellationToken.Register(() => CancelLoad(requestId, cancellationToken, ownerContext));
                return await completion.Task;
            }
            finally
            {
                Loads.TryRemove(requestId, out _);
            }
        }

        private static void CancelLoad(int requestId, CancellationToken token, SynchronizationContext ownerContext)
        {
            if (!Loads.TryRemove(requestId, out var completion)) return;
            if (SynchronizationContext.Current == ownerContext) CancelOnOwner();
            else
            {
                try
                {
                    ownerContext.Post(_ => CancelOnOwner(), null);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }

            void CancelOnOwner()
            {
                try
                {
                    FmodHtml5NativeMethods.CancelLoad(requestId);
                    completion.TrySetCanceled(token);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }
        }

        [MonoPInvokeCallback(typeof(FmodHtml5NativeMethods.LoadCallback))]
        private static void CompleteLoad(int requestId, int moduleHandle, IntPtr error)
        {
            Loads.TryRemove(requestId, out var completion);
            FinishLoad(completion, moduleHandle, error, FreeCallbackMemory, ReleaseCallbackModule, ReportCallbackError);
        }

        internal static void FinishLoad(TaskCompletionSource<FmodStudioHtml5DeviceIO> completion, int moduleHandle,
            IntPtr error,
            Action<int, IntPtr> freeMemory, Action<int> releaseModule, Action<Exception> reportError)
        {
            FmodStudioHtml5DeviceIO device = null;
            Exception failure = null;
            try
            {
                if (completion != null)
                {
                    if (moduleHandle <= 0 || error != IntPtr.Zero)
                        throw new InvalidOperationException(error == IntPtr.Zero
                            ? "FMOD HTML5 initialization failed."
                            : ReadUtf8(error, true));
                    device = new FmodStudioHtml5DeviceIO(moduleHandle);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (error != IntPtr.Zero)
            {
                try
                {
                    freeMemory(moduleHandle, error);
                }
                catch (Exception exception)
                {
                    RecordFailure(exception);
                }
            }

            if (moduleHandle > 0 && (device == null || failure != null))
            {
                try
                {
                    releaseModule(moduleHandle);
                }
                catch (Exception exception)
                {
                    RecordFailure(exception);
                }
            }

            if (completion != null)
            {
                if (failure != null) completion.TrySetException(failure);
                else completion.TrySetResult(device);
            }
            else if (failure != null)
            {
                // Cancellation already completed its task. Cleanup errors still reach Unity's console.
                // Logging itself cannot be allowed to unwind a reverse P/Invoke callback.
                try
                {
                    reportError(failure);
                }
                catch (Exception)
                {
                }
            }

            void RecordFailure(Exception exception)
            {
                failure = failure == null ? exception : new AggregateException(failure, exception);
            }
        }
    }
}
