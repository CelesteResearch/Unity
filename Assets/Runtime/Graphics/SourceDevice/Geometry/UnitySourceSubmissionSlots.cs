namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    internal sealed class UnitySourceSubmissionSlots
    {
        private ulong version;
        private int nextSlot;
        private int protectedSlot = -1;
        private int lastReserved;

        internal int Reserve(ulong currentVersion)
        {
            if (version != currentVersion)
            {
                version = currentVersion;
                nextSlot = 0;
                protectedSlot = -1;
            }

            if (nextSlot == protectedSlot) nextSlot++;
            lastReserved = nextSlot++;
            return lastReserved;
        }

        internal void ReleaseUnsubmitted(ulong currentVersion)
        {
            if (version != currentVersion)
            {
                version = currentVersion;
                nextSlot = 0;
                protectedSlot = -1;
            }
            else nextSlot = lastReserved;
        }

        // A flush during PrepareDraw releases the old prefix's slots. Only the
        // just-recorded draw remains pending in the new prefix, including a nonzero slot.
        internal void ObserveSubmitted(ulong currentVersion)
        {
            if (version == currentVersion) return;
            version = currentVersion;
            nextSlot = 0;
            protectedSlot = lastReserved;
        }
    }
}
