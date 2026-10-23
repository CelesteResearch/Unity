using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AOT;

namespace Celeste.Unity.Audio.Fmod.Html5
{
    public sealed partial class FmodStudioHtml5DeviceIO
    {
        private sealed class FilePreparationRequest
        {
            public readonly int Module;
            public readonly TaskCompletionSource<bool> Completion = new();

            public FilePreparationRequest(int module) => Module = module;
        }

        private static readonly ConcurrentDictionary<int, FilePreparationRequest> FilePreparations = new();

        private static readonly FmodHtml5NativeMethods.PrepareFileCallback FilePreparedCallback =
            CompleteFilePreparation;

        private static int nextFileRequestId;

        // Only strings cross the Unity heap. The verified complete bank remains owned by FMOD's JS VFS.
        public async Task PrepareBankFileAsync(string path, string url, long size, string sha256,
            CancellationToken cancellationToken = default)
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
                throw new InvalidOperationException("FMOD bank preparation requires its Unity owner thread.");
            var module = ModuleHandle;
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A bank file path is required.", nameof(path));
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("A bank resource URL is required.", nameof(url));
            if (size < 0 || size > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(size));
            if (sha256 == null || sha256.Length != 64)
                throw new ArgumentException("A SHA-256 hexadecimal digest is required.", nameof(sha256));
            cancellationToken.ThrowIfCancellationRequested();
            var ownerContext = SynchronizationContext.Current ??
                               throw new InvalidOperationException(
                                   "FMOD bank preparation requires the Unity owner context.");
            var requestId = Interlocked.Increment(ref nextFileRequestId);
            if (requestId <= 0) throw new InvalidOperationException("FMOD file request identifiers are exhausted.");
            var request = new FilePreparationRequest(module);
            if (!FilePreparations.TryAdd(requestId, request))
                throw new InvalidOperationException("FMOD file request already exists.");
            try
            {
                FmodHtml5NativeMethods.PrepareBankFile(requestId, module, Encoding.UTF8.GetBytes(path + "\0"),
                    Encoding.UTF8.GetBytes(url + "\0"), (int)size, Encoding.UTF8.GetBytes(sha256 + "\0"),
                    FilePreparedCallback);
                using var registration = cancellationToken.Register(() => CancelFilePreparation(requestId,
                    cancellationToken, ownerContext));
                await request.Completion.Task;
            }
            finally
            {
                FilePreparations.TryRemove(requestId, out _);
            }
        }

        private static void CancelFilePreparation(int requestId, CancellationToken token,
            SynchronizationContext ownerContext)
        {
            if (!FilePreparations.TryRemove(requestId, out var request)) return;
            if (SynchronizationContext.Current == ownerContext) CancelOnOwner();
            else
            {
                try
                {
                    ownerContext.Post(_ => CancelOnOwner(), null);
                }
                catch (Exception exception)
                {
                    request.Completion.TrySetException(exception);
                }
            }

            void CancelOnOwner()
            {
                try
                {
                    FmodHtml5NativeMethods.CancelPrepareBankFile(requestId);
                    request.Completion.TrySetCanceled(token);
                }
                catch (Exception exception)
                {
                    request.Completion.TrySetException(exception);
                }
            }
        }

        private static void CancelFilePreparations(int module)
        {
            Exception failure = null;
            foreach (var pair in FilePreparations)
            {
                if (pair.Value.Module != module || !FilePreparations.TryRemove(pair.Key, out var request)) continue;
                try
                {
                    FmodHtml5NativeMethods.CancelPrepareBankFile(pair.Key);
                }
                catch (Exception exception)
                {
                    failure = failure == null ? exception : new AggregateException(failure, exception);
                }
                finally
                {
                    request.Completion.TrySetException(new ObjectDisposedException(nameof(FmodStudioHtml5DeviceIO)));
                }
            }

            if (failure != null) throw failure;
        }

        [MonoPInvokeCallback(typeof(FmodHtml5NativeMethods.PrepareFileCallback))]
        private static void CompleteFilePreparation(int requestId, IntPtr error)
        {
            FilePreparations.TryRemove(requestId, out var request);
            Exception failure = null;
            try
            {
                if (error != IntPtr.Zero) failure = new InvalidOperationException(ReadUtf8(error, true));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (error != IntPtr.Zero)
                {
                    try
                    {
                        FmodHtml5NativeMethods.Free(0, error);
                    }
                    catch (Exception exception)
                    {
                        failure = failure == null ? exception : new AggregateException(failure, exception);
                    }
                }
            }

            if (request != null)
            {
                if (failure != null) request.Completion.TrySetException(failure);
                else request.Completion.TrySetResult(true);
            }
            else if (failure != null)
            {
                try
                {
                    UnityEngine.Debug.LogException(failure);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
