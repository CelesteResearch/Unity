using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.Scheduling;

namespace Celeste.Unity
{
    public sealed class NativeSourceMainThreadDispatcher : ISourceEngineMainThreadDispatcher
    {
        private interface IWork
        {
            void Run();
        }

        private sealed class Work<T> : IWork
        {
            private readonly Func<T> action;

            private readonly TaskCompletionSource<T> completed =
                new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Work(Func<T> action)
            {
                this.action = action;
            }

            public void Run()
            {
                try
                {
                    completed.SetResult(action());
                }
                catch (Exception exception)
                {
                    completed.SetException(exception);
                }
            }

            public T Wait()
            {
                return completed.Task.GetAwaiter().GetResult();
            }
        }

        private readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        private readonly ConcurrentQueue<IWork> work = new ConcurrentQueue<IWork>();

        public void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Invoke(() =>
            {
                action();
                return true;
            });
        }

        public T Invoke<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread.ManagedThreadId == mainThread) return action();
            var request = new Work<T>(action);
            work.Enqueue(request);
            return request.Wait();
        }

        public void Pump()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("Native operations must run on the owning main thread.");
            int pending = work.Count;
            for (int index = 0; index < pending && work.TryDequeue(out var request); index++) request.Run();
        }
    }
}
