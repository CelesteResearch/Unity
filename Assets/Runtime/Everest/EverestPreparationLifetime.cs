using System;
using System.Threading;

namespace Celeste.Unity
{
    // A preparation remains active through its asynchronous cancellation cleanup.
    // This is only one part of the idle content-replacement barrier: the caller
    // must also prevent new preparations and release every scene/resource owner.
    internal sealed class EverestPreparationLifetime : IDisposable
    {
        private static int active;
        private int released;
        public static bool IsQuiescent => Volatile.Read(ref active) == 0;

        public static EverestPreparationLifetime Begin()
        {
            Interlocked.Increment(ref active);
            return new EverestPreparationLifetime();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
                Interlocked.Decrement(ref active);
        }
    }
}
