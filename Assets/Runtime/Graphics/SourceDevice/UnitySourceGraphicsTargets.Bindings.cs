using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsTargets
    {
        public void SetRenderTargets(SourceNativeRenderTargetBinding* targets, int count,
            IntPtr depthStencilBuffer, SourceDepthFormat depthFormat, byte preserve)
        {
            if (count < 0 || count > targetHandles.Length || count > SystemInfo.supportedRenderTargetCount)
                throw new NotSupportedException("The requested color target count is unavailable.");
            if (count == 0)
            {
                currentCount = 1;
                targetHandles[0] = backbufferColor;
                currentDepth = backbufferDepth;
                currentStencilStorage = false;
                CurrentDepthFormat = currentDepth == IntPtr.Zero
                    ? SourceDepthFormat.None
                    : GetCompatibleDepthFormat(currentDepth, out currentStencilStorage);
            }
            else
            {
                if (targets == null) throw new ArgumentNullException(nameof(targets));
                for (var index = 0; index < count; index++)
                {
                    ValidateBinding(ref targets[index]);
                    if (index > 0 && (targets[index].data1 != targets[0].data1 ||
                                      targets[index].data2 != targets[0].data2))
                        throw new ArgumentException("All color attachment dimensions must match.");
                }

                if ((depthFormat == SourceDepthFormat.None) != (depthStencilBuffer == IntPtr.Zero))
                    throw new ArgumentException("The depth format and attachment must agree.");
                var actualDepthFormat = SourceDepthFormat.None;
                var stencilStorage = false;
                if (depthStencilBuffer != IntPtr.Zero)
                {
                    textures.BorrowDepthBuffer(depthStencilBuffer);
                    var depth = textures.BorrowTexture(depthStencilBuffer);
                    if (depth.width != targets[0].data1 || depth.height != targets[0].data2)
                        throw new ArgumentException("Depth and color attachment dimensions must match.");
                    actualDepthFormat = GetCompatibleDepthFormat(depthStencilBuffer, out stencilStorage);
                    if (actualDepthFormat != depthFormat)
                        throw new ArgumentException("The declared depth format must match its actual GPU attachment.");
                }

                currentCount = count;
                for (var index = 0; index < count; index++) targetHandles[index] = targets[index].texture;
                currentDepth = depthStencilBuffer;
                CurrentDepthFormat = actualDepthFormat;
                currentStencilStorage = stencilStorage;
            }

            var first = textures.BorrowRenderTarget(targetHandles[0]);
            currentWidth = first.width;
            currentHeight = first.height;
            RecordAndExecuteBindings(preserve != 0);
        }

        public void SetRenderTargets(IntPtr targets, int count, IntPtr depthStencilBuffer,
            SourceDepthFormat depthFormat, byte preserve)
            => SetRenderTargets((SourceNativeRenderTargetBinding*)targets, count, depthStencilBuffer, depthFormat,
                preserve);

        private void RecordBindings(CommandBuffer destination, bool preserve)
        {
            var colors = colorTargets[currentCount];
            var load = preserve ? RenderBufferLoadAction.Load : RenderBufferLoadAction.DontCare;
            for (var index = 0; index < currentCount; index++)
            {
                colors.Targets[index] = new RenderTargetIdentifier(textures.BorrowRenderTarget(targetHandles[index]));
                colors.Loads[index] = load;
            }

            if (currentDepth == IntPtr.Zero && currentCount == 1)
            {
                destination.SetRenderTarget(colors.Targets[0], load, RenderBufferStoreAction.Store);
                return;
            }

            var depth = currentDepth == IntPtr.Zero
                ? new RenderTargetIdentifier(BuiltinRenderTextureType.None)
                : new RenderTargetIdentifier(textures.BorrowTexture(currentDepth));
            destination.SetRenderTarget(new RenderTargetBinding(colors.Targets, colors.Loads, colors.Stores,
                depth, currentDepth == IntPtr.Zero ? RenderBufferLoadAction.DontCare : load,
                currentDepth == IntPtr.Zero ? RenderBufferStoreAction.DontCare : RenderBufferStoreAction.Store));
        }

        public void ResolveTarget(ref SourceNativeRenderTargetBinding binding)
        {
            // Pinned source D3D11_ResolveTarget deliberately does not dereference any texture in this case.
            if (binding.multiSampleCount <= 0 && binding.levelCount <= 1) return;
            throw new NotSupportedException("Multisample resolve and mip generation need the next resource consumer.");
        }

        private void ValidateBinding(ref SourceNativeRenderTargetBinding binding)
        {
            if (binding.type != 0 || binding.levelCount != 1 || binding.multiSampleCount != 0 ||
                binding.colorBuffer != IntPtr.Zero)
                throw new NotSupportedException(
                    "This target consumer requires 2D, mip0, non-multisample texture attachments.");
            var target = textures.BorrowRenderTarget(binding.texture);
            if (!target.IsCreated() || target.width != binding.data1 || target.height != binding.data2)
                throw new ArgumentException("The source binding does not describe its actual GPU texture.");
        }

        private SourceDepthFormat GetCompatibleDepthFormat(IntPtr handle, out bool stencilStorage)
        {
            var logical = textures.GetDepthFormat(handle);
            var actual = ((RenderTexture)textures.BorrowTexture(handle)).depthStencilFormat;
            stencilStorage = actual == GraphicsFormat.D24_UNorm_S8_UInt;
            var equivalent = logical switch
            {
                SourceDepthFormat.Depth16 => actual == GraphicsFormat.D16_UNorm,
                SourceDepthFormat.Depth24 => actual is GraphicsFormat.D24_UNorm or GraphicsFormat.D24_UNorm_S8_UInt,
                SourceDepthFormat.Depth24Stencil8 => actual == GraphicsFormat.D24_UNorm_S8_UInt,
                _ => false,
            };
            if (!equivalent)
                throw new NotSupportedException(
                    "The actual depth storage does not preserve the source precision/stencil.");
            return logical;
        }
    }
}
