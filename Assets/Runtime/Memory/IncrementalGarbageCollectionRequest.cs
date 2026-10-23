using System;
using System.Threading;

namespace Celeste.Unity.Memory
{
    internal sealed class IncrementalGarbageCollectionRequest
    {
        private int requested;
        private bool active;
        private bool hasEpoch;
        private int collectionEpoch;
        private int backgroundRequested;

        public void Request() => AddRequest(1);

        public void RequestSceneTransition() => AddRequest(3);

        private void AddRequest(int flags)
        {
            int value;
            do
            {
                value = Volatile.Read(ref requested);
            } while (Interlocked.CompareExchange(ref requested, value | flags, value) != value);
        }

        public void CompleteActiveRequest()
        {
            active = false;
            hasEpoch = false;
        }

        public void RequestBackground() => Interlocked.Exchange(ref backgroundRequested, 1);

        public bool Advance(Func<ulong, bool> collect, ulong budgetNanoseconds,
            Func<bool> isAutomaticCollectionEnabled, Func<int> readCollectionEpoch)
        {
            int incoming = Interlocked.Exchange(ref requested, 0);
            bool fresh = incoming != 0;
            bool sceneTransition = (incoming & 2) != 0;
            bool background = Interlocked.Exchange(ref backgroundRequested, 0) != 0;
            if (fresh)
            {
                active = true;
                hasEpoch = false;
            }

            if (!active && !background) return sceneTransition && active;

            try
            {
                bool automatic = isAutomaticCollectionEnabled();
                if (background && !automatic)
                {
                    // Manual/Disabled retain the original explicit-work path.
                    active = true;
                    hasEpoch = false;
                    fresh = true;
                }

                // Enabled hands only background work to automatic GC. It must not
                // make an old foreground request fresh or bypass its epoch check.
                if (!active) return sceneTransition && active;

                if (automatic)
                {
                    int epoch = readCollectionEpoch();
                    if (!fresh && hasEpoch && epoch != collectionEpoch)
                    {
                        // Marking advanced; automatic GC owns the remaining sweep work.
                        // This retires old manual assistance, not the automatic collector.
                        active = false;
                        hasEpoch = false;
                        return sceneTransition && active;
                    }

                    // Save the epoch BEFORE work: marking may finish inside collect
                    // while that call still reports more work.
                    collectionEpoch = epoch;
                    hasEpoch = true;
                }
                else hasEpoch = false;

                active = collect(budgetNanoseconds);
                if (!active) hasEpoch = false;
                return sceneTransition && active;
            }
            catch
            {
                // Preserve both consumed background work and foreground retry state.
                if (background) Interlocked.Exchange(ref backgroundRequested, 1);
                if (sceneTransition) AddRequest(3);
                hasEpoch = false;
                throw;
            }
        }
    }
}
