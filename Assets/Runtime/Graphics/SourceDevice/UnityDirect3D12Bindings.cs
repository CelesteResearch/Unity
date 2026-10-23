using System;
using System.Runtime.InteropServices;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal static class UnityDirect3D12Bindings
    {
        private const string Library = "GfxPluginCelesteGraphicsUnityDirect3D12";

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityDirect3D12GetRenderEvent();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12GetEventId();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityDirect3D12GetLastError();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12CreateDevice(IntPtr adapter, out IntPtr device);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12AcquireOutput(IntPtr adapter, IntPtr device,
            IntPtr renderBuffer,
            out IntPtr output);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12ExecutePacket(IntPtr adapter, IntPtr packet, out ulong value,
            out int executed);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityDirect3D12RetainCompletionFence(IntPtr adapter);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12CompletePacket(IntPtr adapter, IntPtr packet, ulong value,
            out IntPtr submission);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern ulong CelesteUnityDirect3D12GetDeviceGeneration();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12CreateTextureCache(IntPtr adapter, IntPtr device,
            IntPtr texture, out IntPtr cache);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityDirect3D12AcquireTextureOutputLoan(IntPtr cache, out IntPtr output);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern void CelesteUnityDirect3D12ReleaseTextureCache(IntPtr cache);

        internal static void Require(int result)
        {
            if (result != 1)
                throw new InvalidOperationException(Marshal.PtrToStringUTF8(CelesteUnityDirect3D12GetLastError()) ??
                                                    "Unity Direct3D12 bridge failed.");
        }
    }
}
