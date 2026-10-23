using System;
using System.Runtime.InteropServices;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public static class UnityDirect3D11Bindings
    {
        private const string Library = "CelesteGraphicsUnityD3D11";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SourceGpuAcquireUnityDevice();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern ulong SourceGpuUnityDeviceGeneration();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SourceGpuGetRenderEventAndData();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int SourceGpuRenderEventId();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SourceGpuCreateHostCopyLoan(IntPtr actualUnityTexture);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void SourceGpuReleaseHostCopyLoan(IntPtr loan);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr SourceGpuGetHostCopyEvent();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int SourceGpuHostCopyCompletion(IntPtr loan);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern IntPtr SourceGpuCreateUnityTextureViewLoan(IntPtr source, ulong generation, uint width,
            uint height, out IntPtr view);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SourceGpuReleaseUnityTextureViewLoan(IntPtr loan);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern IntPtr SourceGpuGetUnityTextureViewReleaseEvent();
    }
}
