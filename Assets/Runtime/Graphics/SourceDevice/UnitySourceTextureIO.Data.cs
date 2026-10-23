using System;
using Celeste.Graphics.Rendering.Textures;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceTextureIO
    {
        public void SetTextureData2D(IntPtr texture, int x, int y, int width, int height, int level,
            IntPtr data, int dataLength)
        {
            var resource = resources[texture];
            ValidateRegion(resource, x, y, width, height, level, data, dataLength);
            resource.Upload ??= CreateColorTexture(resource.Gpu.width, resource.Gpu.height);
            var full = x == 0 && y == 0 && width == resource.Gpu.width && height == resource.Gpu.height;
            if (!full && (resource.Gpu is RenderTexture || !resource.UploadCurrent))
                ReadGpu(resource, resource.Upload);

            var pixels = resource.Upload.GetPixelData<byte>(0);
            var destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(pixels);
            var source = (byte*)data;
            var rowBytes = checked(width * 4);
            var stride = checked(resource.Gpu.width * 4);
            for (var row = 0; row < height; row++)
                Buffer.MemoryCopy(source + row * rowBytes, destination + (y + row) * stride + x * 4,
                    rowBytes, rowBytes);
            resource.Upload.Apply(false, false);
            if (resource.Gpu != resource.Upload) CopyGpu(resource.Upload, resource.Gpu);
            resource.UploadCurrent = true;
            resource.ContentVersion = unchecked(resource.ContentVersion + 1);
        }

        public void GetTextureData2D(IntPtr texture, int x, int y, int width, int height, int level,
            IntPtr data, int dataLength)
        {
            var resource = resources[texture];
            ValidateRegion(resource, x, y, width, height, level, data, dataLength);
            resource.Readback ??= CreateColorTexture(resource.Gpu.width, resource.Gpu.height);
            ReadGpu(resource, resource.Readback);
            var pixels = resource.Readback.GetPixelData<byte>(0);
            var source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pixels);
            var destination = (byte*)data;
            var rowBytes = checked(width * 4);
            var stride = checked(resource.Gpu.width * 4);
            for (var row = 0; row < height; row++)
                Buffer.MemoryCopy(source + (y + row) * stride + x * 4, destination + row * rowBytes,
                    rowBytes, rowBytes);
        }

        // A real synchronous GPU read, including after draws/copies. Never trust a stale CPU upload mirror.
        private void ReadGpu(Resource resource, Texture2D destination)
        {
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.ReadPixels))
                throw new NotSupportedException("RGBA8 synchronous GPU readback is unavailable.");
            var target = resource.Gpu as RenderTexture;
            if (target == null)
            {
                resource.ReadTarget ??= CreateColorTarget(resource.Gpu.width, resource.Gpu.height);
                CopyGpu(resource.Gpu, resource.ReadTarget);
                target = resource.ReadTarget;
            }

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                destination.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                // ReadPixels already returns the Texture2D CPU byte layout; do not flip it a second time.
            }
            finally
            {
                RenderTexture.active = previous;
                SubmissionStateVersion = unchecked(SubmissionStateVersion + 1);
            }
        }

        public void CopyTexture2D(IntPtr source, IntPtr destination)
        {
            var from = resources[source];
            var to = resources[destination];
            if (from.DepthOnly || to.DepthOnly)
                throw new ArgumentException("A depth buffer cannot be copied as a color texture.");
            if (from.Gpu.width != to.Gpu.width || from.Gpu.height != to.Gpu.height)
                throw new ArgumentException("Source and destination texture dimensions must match.");
            if (to.Gpu is Texture2D cpuDestination)
            {
                var required = CopyTextureSupport.Basic;
                if (from.Gpu is RenderTexture) required |= CopyTextureSupport.RTToTexture;
                var api = SystemInfo.graphicsDeviceType;
                var desktopCopy = api == GraphicsDeviceType.Direct3D11 || api == GraphicsDeviceType.Direct3D12 ||
                                  api == GraphicsDeviceType.Vulkan;
                if (desktopCopy && !ReferenceEquals(from.Gpu, to.Gpu) &&
                    (SystemInfo.copyTextureSupport & required) == required)
                {
                    UnityEngine.Graphics.CopyTexture(from.Gpu, 0, 0, cpuDestination, 0, 0);
                    to.UploadCurrent = false;
                }
                else
                {
                    ReadGpu(from, cpuDestination);
                    cpuDestination.Apply(false, false);
                    to.UploadCurrent = true;
                }
            }
            else
            {
                CopyGpu(from.Gpu, to.Gpu);
                to.UploadCurrent = false;
            }

            to.ContentVersion = unchecked(to.ContentVersion + 1);
        }

        private void CopyGpu(Texture source, Texture destination)
        {
            if (destination is not RenderTexture target)
                throw new NotSupportedException("A direct draw copy requires a RenderTexture destination.");

            copyCommands.Clear();
            copyCommands.SetRenderTarget(target);
            copyCommands.SetViewport(new Rect(0, 0, target.width, target.height));
            copyCommands.DisableScissorRect();
            copyCommands.SetGlobalTexture(CopySource, source);
            copyCommands.SetGlobalVector(CopyDimensions, new Vector4(source.width, source.height, 0, 0));
            copyCommands.DrawProcedural(Matrix4x4.identity, copyMaterial, 0, MeshTopology.Triangles, 3);
            try
            {
                UnityEngine.Graphics.ExecuteCommandBuffer(copyCommands);
            }
            finally
            {
                // The device submitter must rebind its source targets/viewport after this temporary draw.
                SubmissionStateVersion = unchecked(SubmissionStateVersion + 1);
            }
        }

        private static void ValidateRegion(Resource resource, int x, int y, int width, int height,
            int level, IntPtr data, int dataLength)
        {
            if (resource.DepthOnly) throw new ArgumentException("Depth buffers have no RGBA8 data.");
            if (level != 0) throw new NotSupportedException("This resource candidate supports mip zero.");
            if (data == IntPtr.Zero) throw new ArgumentNullException(nameof(data));
            if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
                width > resource.Gpu.width - x || height > resource.Gpu.height - y)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (dataLength < checked(width * height * 4)) throw new ArgumentOutOfRangeException(nameof(dataLength));
        }
    }
}
