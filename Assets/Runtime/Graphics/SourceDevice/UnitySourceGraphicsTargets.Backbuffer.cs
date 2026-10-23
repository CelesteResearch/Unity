using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsTargets
    {
        public bool HasPresented { get; private set; }

        public SourceDepthFormat GetBackbufferDepthFormat() => backbufferDepthFormat;
        public SourceSurfaceFormat GetBackbufferSurfaceFormat() => SourceSurfaceFormat.Color;

        public void GetBackbufferSize(out int width, out int height)
        {
            var target = textures.BorrowRenderTarget(backbufferColor);
            width = target.width;
            height = target.height;
        }

        // Borrowed by the eventual physical host output; this consumer still owns its source backbuffer tokens.
        public RenderTexture BorrowBackbuffer() => textures.BorrowRenderTarget(backbufferColor);

        public void ResetBackbuffer(ref SourceNativePresentationParameters parameters)
        {
            if (matchHostOutputSize)
            {
                var output = getHostOutput();
                if (output == null || !output.IsCreated() || output.width <= 0 || output.height <= 0)
                    throw new InvalidOperationException("The actual host output must have a live drawable size.");
                // The source device publishes this same ref struct to its viewport and input metrics.
                parameters.backBufferWidth = output.width;
                parameters.backBufferHeight = output.height;
            }

            // Original reset releases and replaces the source backbuffer, then physically binds the replacement.
            ReleaseBackbuffer();
            if (parameters.backBufferFormat != SourceSurfaceFormat.Color || parameters.multiSampleCount != 0)
                throw new NotSupportedException("The first backbuffer consumer requires Color without multisampling.");
            backbufferColor = textures.CreateTexture2D(parameters.backBufferFormat,
                parameters.backBufferWidth, parameters.backBufferHeight, 1, 1);
            try
            {
                if (parameters.depthStencilFormat != SourceDepthFormat.None)
                    backbufferDepth = textures.GenDepthStencilRenderbuffer(parameters.backBufferWidth,
                        parameters.backBufferHeight, parameters.depthStencilFormat, 0);
                backbufferDepthFormat = parameters.depthStencilFormat;
                hostWindow = parameters.deviceWindowHandle;
                var target = textures.BorrowRenderTarget(backbufferColor);
                // Pinned FNA faux-backbuffer presentation is linear-clamp unless the actual nearest hint is set.
                target.filterMode = nearestPresentation ? FilterMode.Point : FilterMode.Bilinear;
                target.wrapMode = TextureWrapMode.Clamp;
                viewport = new CameraViewport(0, 0, target.width, target.height);
                scissor = new IntRect(0, 0, target.width, target.height);
                SetRenderTargets(IntPtr.Zero, 0, IntPtr.Zero, SourceDepthFormat.None, 0);
                setPresentationSyncInterval?.Invoke(parameters.presentationInterval);
            }
            catch
            {
                ReleaseBackbuffer();
                throw;
            }
        }

        private void ReleaseBackbuffer()
        {
            HasPresented = false;
            if (backbufferColor != IntPtr.Zero) textures.AddDisposeTexture(backbufferColor);
            if (backbufferDepth != IntPtr.Zero) textures.AddDisposeRenderbuffer(backbufferDepth);
            backbufferColor = IntPtr.Zero;
            backbufferDepth = IntPtr.Zero;
            textures.DrainDisposals();
        }

        public void ReadBackbuffer(int x, int y, int width, int height, IntPtr data, int dataLength)
            => textures.GetTextureData2D(backbufferColor, x, y, width, height, 0, data, dataLength);

        public void SwapBuffers(IntPtr source, IntPtr destination, IntPtr window)
            => PresentRectangles((IntRect*)source, (IntRect*)destination, window);

        public void SwapBuffers(ref IntRect source, IntPtr destination, IntPtr window)
        {
            fixed (IntRect* sourcePointer = &source) PresentRectangles(sourcePointer, (IntRect*)destination, window);
        }

        public void SwapBuffers(IntPtr source, ref IntRect destination, IntPtr window)
        {
            fixed (IntRect* destinationPointer =
                       &destination) PresentRectangles((IntRect*)source, destinationPointer, window);
        }

        public void SwapBuffers(ref IntRect source, ref IntRect destination, IntPtr window)
        {
            fixed (IntRect* sourcePointer = &source)
            fixed (IntRect* destinationPointer = &destination)
                PresentRectangles(sourcePointer, destinationPointer, window);
        }

        private void PresentRectangles(IntRect* source, IntRect* destination, IntPtr window)
        {
            if (window != IntPtr.Zero && window != hostWindow)
                throw new NotSupportedException(
                    "Presenting to an alternate source window requires another host output.");
            var output = getHostOutput();
            if (output == null || !output.IsCreated() || output.graphicsFormat != GraphicsFormat.R8G8B8A8_UNorm)
                throw new InvalidOperationException("The actual host output must be a live RGBA8 UNorm RenderTexture.");
            var backbuffer = BorrowBackbuffer();
            var from = source == null ? new IntRect(0, 0, backbuffer.width, backbuffer.height) : *source;
            var to = destination == null ? new IntRect(0, 0, output.width, output.height) : *destination;
            if (from.Width <= 0 || from.Height <= 0 || to.Width <= 0 || to.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(source),
                    "Presentation rectangles must have positive extents.");
            commands.Clear();
            commands.SetRenderTarget(output);
            // The original native off-centre presentation witness requires the raw source destination Y.
            commands.SetViewport(new Rect(to.X, to.Y, to.Width, to.Height));
            commands.DisableScissorRect();
            commands.SetGlobalTexture(PresentTexture, backbuffer);
            commands.SetGlobalVector(PresentSource, new Vector4(from.X / (float)backbuffer.width,
                from.Y / (float)backbuffer.height, from.Width / (float)backbuffer.width,
                from.Height / (float)backbuffer.height));
            commands.DrawProcedural(Matrix4x4.identity, presentMaterial, 0, MeshTopology.Triangles, 3);
            try
            {
                UnityEngine.Graphics.ExecuteCommandBuffer(commands);
                if (!HasPresented) HasPresented = true;
            }
            finally
            {
                SubmissionStateVersion = unchecked(SubmissionStateVersion + 1);
            }
        }
    }
}
