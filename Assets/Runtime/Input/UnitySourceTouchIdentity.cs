using System;

namespace Celeste.Unity.Input
{
    internal static class UnitySourceTouchIdentity
    {
        internal static long PhysicalFingerId(int deviceId, int touchId)
        {
            if (deviceId < 0 || deviceId > 32767 || touchId < 0 || touchId > 65535)
                throw new NotSupportedException(
                    "Unity touch identity exceeds the source signed-32 physical transport.");
            return ((long)deviceId << 16) | (uint)touchId;
        }
    }
}
