using System;
using System.Runtime.InteropServices;

namespace Celeste.Unity.Audio.Fmod.Html5
{
    internal static partial class FmodHtml5NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void PrepareFileCallback(int requestId, IntPtr error);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_PrepareBankFile",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PrepareBankFile(int requestId, int moduleHandle, byte[] path, byte[] url,
            int size, byte[] sha256, PrepareFileCallback callback);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_CancelPrepareBankFile",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern void CancelPrepareBankFile(int requestId);
    }
}
