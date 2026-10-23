using System;
using System.Runtime.InteropServices;
using Celeste.Audio.Fmod.Interop;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    internal static partial class FmodHtml5NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void LoadCallback(int requestId, int moduleHandle, IntPtr error);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_Load", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Load(int requestId, byte[] scriptUrl, byte[] wasmUrl, LoadCallback callback);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_CancelLoad", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void CancelLoad(int requestId);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_DisposeModule", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void DisposeModule(int moduleHandle);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_Free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Free(int moduleHandle, IntPtr pointer);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemLoadBankMemory",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemLoadBankMemory(int moduleHandle, IntPtr studiosystem,
            IntPtr buffer,
            int length, out IntPtr bank);
    }
}
