using System;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceTextureIO
    {
        // This first resource consumer has no multisample resolve implementation.
        public int GetMaxMultiSampleCount(SourceSurfaceFormat format, int preferredCount)
        {
            RequireColor(format);
            return 0;
        }

        public IntPtr GenColorRenderbuffer(int width, int height, SourceSurfaceFormat format,
            int sampleCount, IntPtr texture)
        {
            throw new NotSupportedException("Multisample color renderbuffers require the next resolve consumer.");
        }

        public IntPtr GenDepthStencilRenderbuffer(int width, int height, SourceDepthFormat format, int sampleCount)
        {
            RequireDimensions(width, height);
            if (sampleCount != 0)
                throw new NotSupportedException("Multisample depth buffers require the next resolve consumer.");
            var depth = format switch
            {
                SourceDepthFormat.Depth16 => GraphicsFormat.D16_UNorm,
                SourceDepthFormat.Depth24 => GraphicsFormat.D24_UNorm,
                SourceDepthFormat.Depth24Stencil8 => GraphicsFormat.D24_UNorm_S8_UInt,
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
            // Original D3D11 uses D24S8 storage for both logical Depth24 and Depth24Stencil8.
            if (format == SourceDepthFormat.Depth24 &&
                !SystemInfo.IsFormatSupported(depth, GraphicsFormatUsage.Render))
                depth = GraphicsFormat.D24_UNorm_S8_UInt;
            if (!SystemInfo.IsFormatSupported(depth, GraphicsFormatUsage.Render))
                throw new NotSupportedException($"The GPU cannot allocate the exact requested depth format {depth}.");
            var target = CreateTarget(new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.None, depthStencilFormat = depth,
                msaaSamples = 1, mipCount = 1, useMipMap = false, autoGenerateMips = false,
            });
            return Own(new Resource { Gpu = target, DepthOnly = true, DepthFormat = format });
        }
    }
}
