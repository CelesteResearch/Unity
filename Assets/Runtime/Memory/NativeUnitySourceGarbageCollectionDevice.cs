using System;
using Celeste.Core.Framework.GarbageCollection;
using UnityEngine.Scripting;

namespace Celeste.Unity.Memory
{
    internal sealed class NativeUnitySourceGarbageCollectionDevice : ISourceGarbageCollectionDevice
    {
        private const ulong BudgetNanoseconds = 1000000;
        private static readonly Func<ulong, bool> CollectIncremental = GarbageCollector.CollectIncremental;

        private static readonly Func<bool> IsAutomaticCollectionEnabled =
            () => GarbageCollector.GCMode == GarbageCollector.Mode.Enabled;

        private static readonly Func<int> ReadCollectionEpoch = () => GC.CollectionCount(0);
        private readonly bool incremental = GarbageCollector.isIncremental;
        private readonly IncrementalGarbageCollectionRequest request = new IncrementalGarbageCollectionRequest();

        public void CollectBackground()
        {
            if (incremental) request.RequestBackground();
            else SystemGarbageCollectionDevice.Instance.CollectBackground();
        }

        public void CollectOnRoomTransition()
        {
            if (incremental) request.Request();
            else SystemGarbageCollectionDevice.Instance.CollectOnRoomTransition();
        }

        public void CollectOnReload()
        {
            if (incremental) request.Request();
            else SystemGarbageCollectionDevice.Instance.CollectOnReload();
        }

        public void CollectOnSceneTransition()
        {
            if (incremental) request.RequestSceneTransition();
            else SystemGarbageCollectionDevice.Instance.CollectOnSceneTransition();
        }

        public void Advance(bool levelScene = false)
        {
            bool transitionWork = request.Advance(CollectIncremental, BudgetNanoseconds,
                IsAutomaticCollectionEnabled, ReadCollectionEpoch);
            if (!transitionWork || !levelScene || !incremental || !IsAutomaticCollectionEnabled()) return;

            // Finish the existing transition request before the first level frame is presented.
            // Incremental GC has indivisible stages; its per-call budget is not a hard time limit.
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int steps = 0;
            bool remaining;
            do
            {
                remaining = CollectIncremental(4000000);
                steps++;
            } while (remaining && steps < 64 && timer.ElapsedMilliseconds < 250);

            if (!remaining) request.CompleteActiveRequest();
        }
    }
}
