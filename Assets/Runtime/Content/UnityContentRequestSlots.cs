#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Unity.Content
{
    internal sealed class UnityContentRequestSlots : IDisposable
    {
        private sealed class PendingRequest
        {
            internal readonly UnityContentRequestSlots Owner;
            internal readonly CancellationToken Token;
            internal readonly TaskCompletionSource<bool> Completion = new();
            internal LinkedListNode<PendingRequest>? Node;

            internal PendingRequest(UnityContentRequestSlots owner, CancellationToken token)
            {
                Owner = owner;
                Token = token;
            }
        }

        private static readonly SendOrPostCallback CancelOnOwner = state =>
        {
            var pending = (PendingRequest)state!;
            pending.Owner.CancelPending(pending);
        };

        private readonly LinkedList<PendingRequest> pendingRequests = new();
        private readonly SynchronizationContext ownerContext;
        private readonly int ownerThread;
        private readonly int maximumConcurrentRequests;
        private int occupiedSlots;
        private bool disposed;

        internal UnityContentRequestSlots(int maximumConcurrentRequests, SynchronizationContext ownerContext)
        {
            if (maximumConcurrentRequests <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumConcurrentRequests));
            this.ownerContext = ownerContext ?? throw new ArgumentNullException(nameof(ownerContext));
            this.maximumConcurrentRequests = maximumConcurrentRequests;
            ownerThread = Thread.CurrentThread.ManagedThreadId;
        }

        internal Task WaitAsync(CancellationToken token)
        {
            RequireOwnerThread();
            if (disposed) throw new ObjectDisposedException(nameof(UnityContentRequestSlots));
            token.ThrowIfCancellationRequested();
            if (occupiedSlots < maximumConcurrentRequests)
            {
                occupiedSlots++;
                return Task.CompletedTask;
            }

            var pending = new PendingRequest(this, token);
            pending.Node = pendingRequests.AddLast(pending);
            return WaitQueuedAsync(pending);
        }

        private async Task WaitQueuedAsync(PendingRequest pending)
        {
            using var cancellation = pending.Token.Register(() =>
            {
                if (Thread.CurrentThread.ManagedThreadId == ownerThread)
                    CancelPending(pending);
                else
                    ownerContext.Post(CancelOnOwner, pending);
            });
            await pending.Completion.Task;
        }

        private void CancelPending(PendingRequest pending)
        {
            RequireOwnerThread();
            if (pending.Node == null) return;
            pendingRequests.Remove(pending.Node);
            pending.Node = null;
            pending.Completion.TrySetCanceled(pending.Token);
        }

        internal void Release()
        {
            RequireOwnerThread();
            if (disposed) return;
            while (pendingRequests.First != null)
            {
                PendingRequest pending = pendingRequests.First.Value;
                pendingRequests.RemoveFirst();
                pending.Node = null;
                if (pending.Token.IsCancellationRequested)
                {
                    pending.Completion.TrySetCanceled(pending.Token);
                    continue;
                }

                pending.Completion.TrySetResult(true);
                return;
            }

            occupiedSlots--;
        }

        private void RequireOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Content request slots require their Unity owner thread.");
        }

        // Transport calls this after all active calls close, so a non-owner caller only sees a quiescent queue.
        public void Dispose()
        {
            if (disposed) return;
            if (pendingRequests.Count != 0) RequireOwnerThread();
            disposed = true;
            while (pendingRequests.First != null)
            {
                PendingRequest pending = pendingRequests.First.Value;
                pendingRequests.RemoveFirst();
                pending.Node = null;
                pending.Completion.TrySetCanceled();
            }
        }
    }
}
