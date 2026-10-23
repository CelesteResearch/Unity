#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content.Manifests;

namespace Celeste.Unity.Content.Web
{
    internal static class UnityWebContentCacheInterop
    {
        private delegate void Completion(int request, int error, IntPtr url, IntPtr message, int cached);
        private delegate void Progress(int request, double receivedBytes, int cached);

        private sealed class PendingRequest
        {
            internal readonly int Id;
            internal readonly CancellationToken Token;
            internal readonly SynchronizationContext OwnerContext;
            internal readonly int OwnerThread;
            internal readonly Action<long, bool> Report;
            internal readonly TaskCompletionSource<UnityWebContentBlobLease> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal PendingRequest(int id, CancellationToken token, Action<long, bool> report)
            {
                Id = id;
                Token = token;
                Report = report;
                OwnerContext = SynchronizationContext.Current ??
                    throw new InvalidOperationException("Web content caching requires its Unity owner context.");
                OwnerThread = Thread.CurrentThread.ManagedThreadId;
            }
        }

        private static readonly Dictionary<int, PendingRequest> Requests = new();
        private static readonly Completion Completed = OnCompleted;
        private static readonly Progress Progressed = OnProgress;
        private static readonly SendOrPostCallback CancelOnOwner = state => CancelPending((PendingRequest)state);
        private static int sequence;

        internal static async Task<UnityWebContentBlobLease> PrepareAsync(Uri uri, ContentFileInfo file,
            Action<long, bool> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var pending = new PendingRequest(checked(++sequence), token, progress);
            Requests.Add(pending.Id, pending);
            bool transferred = false;
            try
            {
                CelesteWebPrepareContentBlob(pending.Id, uri.AbsoluteUri, file.Size, file.Sha256,
                    Completed, Progressed);
                using var registration = token.Register(() =>
                {
                    if (Thread.CurrentThread.ManagedThreadId == pending.OwnerThread) CancelPending(pending);
                    else pending.OwnerContext.Post(CancelOnOwner, pending);
                });
                UnityWebContentBlobLease lease = await pending.Completion.Task;
                transferred = true;
                return lease;
            }
            finally
            {
                Requests.Remove(pending.Id);
                if (!transferred) Release(pending.Id);
            }
        }

        private static void CancelPending(PendingRequest pending)
        {
            if (!Requests.TryGetValue(pending.Id, out PendingRequest active) || !ReferenceEquals(active, pending))
                return;
            CelesteWebCancelContentBlob(pending.Id);
            pending.Completion.TrySetCanceled(pending.Token);
        }

        [AOT.MonoPInvokeCallback(typeof(Completion))]
        private static void OnCompleted(int request, int error, IntPtr url, IntPtr message, int cached)
        {
            if (!Requests.TryGetValue(request, out PendingRequest pending))
            {
                Release(request);
                return;
            }
            if (error != 0)
            {
                pending.Completion.TrySetException(new IOException(
                    Marshal.PtrToStringUTF8(message) ?? "Web content cache preparation failed."));
                return;
            }
            var lease = new UnityWebContentBlobLease(request, Marshal.PtrToStringUTF8(url), cached != 0);
            if (!pending.Completion.TrySetResult(lease)) lease.Dispose();
        }

        [AOT.MonoPInvokeCallback(typeof(Progress))]
        private static void OnProgress(int request, double receivedBytes, int cached)
        {
            if (Requests.TryGetValue(request, out PendingRequest pending))
                pending.Report((long)receivedBytes, cached != 0);
        }

        internal static void Release(int request) => CelesteWebReleaseContentBlob(request);

        [DllImport("__Internal")]
        private static extern void CelesteWebPrepareContentBlob(int request, string url, double size,
            string sha256, Completion completion, Progress progress);

        [DllImport("__Internal")]
        private static extern void CelesteWebCancelContentBlob(int request);

        [DllImport("__Internal")]
        private static extern void CelesteWebReleaseContentBlob(int request);
    }
}
#endif
