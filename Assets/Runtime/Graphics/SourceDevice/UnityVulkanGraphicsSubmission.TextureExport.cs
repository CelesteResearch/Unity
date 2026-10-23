using System;
using System.Runtime.InteropServices;
using Celeste.Graphics.Vulkan.Devices;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice
{
    internal sealed partial class UnityVulkanGraphicsSubmission : IUnityNativeTextureExportHost
    {
        internal UnityNativeTextureBatch CreateTextureBatch(VulkanGraphicsDeviceIO source)
        {
            ThrowIfDisposed();
            RequireGeneration();
            return new UnityNativeTextureBatch(this, source, generation);
        }

        internal bool IsTextureGenerationCurrent(ulong expected)
            => !disposed && device != IntPtr.Zero && generation == expected &&
               UnityVulkanBindings.CelesteUnityVulkanGetDeviceGeneration() == expected;

        internal IntPtr CreateTextureOutput(Texture2D texture)
        {
            if (!IsMainThread) throw new InvalidOperationException("Create Scene mirrors on Unity's main thread.");
            RequireGeneration();
            IntPtr request = RequireRequest(UnityVulkanBindings.CelesteUnityVulkanCreateOutputRequest(
                texture.GetNativeTexturePtr(), generation));
            try
            {
                RunRequest(request);
                IntPtr output = UnityVulkanBindings.CelesteUnityVulkanTakeOutputCache(request);
                if (output == IntPtr.Zero) ThrowBridgeError();
                return output;
            }
            finally
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOperation(request);
            }
        }

        internal void SubmitTextureBatch(IntPtr packet, IntPtr[] outputs, int count)
        {
            if (!IsMainThread) throw new InvalidOperationException("Submit Scene mirrors on Unity's main thread.");
            RequireGeneration();
            IntPtr request = RequireRequest(CreatePacketBatchRequest(packet, outputs, count, generation, 0));
            try
            {
                RunRequest(request);
            }
            finally
            {
                UnityVulkanBindings.CelesteUnityVulkanReleaseOperation(request);
            }
        }

        internal void DrainTextureOutputs(ulong expected)
        {
            if (!IsMainThread) throw new InvalidOperationException("Retire Scene mirrors on Unity's main thread.");
            if (IsTextureGenerationCurrent(expected)) Drain(false);
            // A retired session was already drained by ReleaseDevice before its generation changed.
        }

        bool IUnityNativeTextureExportHost.IsTextureGenerationCurrent(ulong expected)
            => IsTextureGenerationCurrent(expected);

        IntPtr IUnityNativeTextureExportHost.CreateTextureOutput(Texture2D texture) => CreateTextureOutput(texture);

        IntPtr IUnityNativeTextureExportHost.AcquireTextureOutputLoan(IntPtr cache)
        {
            if (UnityVulkanBindings.CelesteUnityVulkanAcquireOutputLoan(cache, out IntPtr output) != 1)
                ThrowBridgeError();
            return output;
        }

        void IUnityNativeTextureExportHost.ReleaseTextureOutputCache(IntPtr cache)
            => UnityVulkanBindings.CelesteUnityVulkanReleaseOutputCache(cache);

        void IUnityNativeTextureExportHost.SubmitTextureBatch(IntPtr packet, IntPtr[] outputs, int count)
            => SubmitTextureBatch(packet, outputs, count);

        void IUnityNativeTextureExportHost.DrainTextureOutputs(ulong expected) => DrainTextureOutputs(expected);

        [DllImport("GfxPluginCelesteGraphicsUnityVulkan", CallingConvention = CallingConvention.Winapi,
            EntryPoint = "CelesteUnityVulkanCreatePacketBatchRequest", ExactSpelling = true)]
        private static extern IntPtr CreatePacketBatchRequest(IntPtr packet,
            [In] IntPtr[] outputs, int count, ulong generation, int waitForCompletion);
    }
}
