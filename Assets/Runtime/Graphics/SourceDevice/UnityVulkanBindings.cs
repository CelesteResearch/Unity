using System;
using System.Runtime.InteropServices;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal static class UnityVulkanBindings
    {
        private const string Library = "GfxPluginCelesteGraphicsUnityVulkan";

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanGetOperationEvent();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityVulkanGetOperationEventId();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityVulkanGetOperationCompletionEventId(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanCreateAcquireDeviceRequest();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanCreateOutputRequest(IntPtr nativeTexture, ulong generation);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanCreatePacketRequest(IntPtr packet, IntPtr output,
            ulong generation, int waitForCompletion);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanCreateDrainRequest(ulong generation, int releaseSession);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityVulkanPrepareEvent(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern void CelesteUnityVulkanCancelEvent(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityVulkanGetOperationStatus(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanGetOperationError(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityVulkanWaitOperation(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern void CelesteUnityVulkanReleaseOperation(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanGetOperationDevice(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern ulong CelesteUnityVulkanGetOperationGeneration(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanTakeOutputCache(IntPtr request);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern int CelesteUnityVulkanAcquireOutputLoan(IntPtr cache, out IntPtr output);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern void CelesteUnityVulkanReleaseOutputCache(IntPtr cache);

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern IntPtr CelesteUnityVulkanGetLastError();

        [DllImport(Library, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        internal static extern ulong CelesteUnityVulkanGetDeviceGeneration();
    }
}
