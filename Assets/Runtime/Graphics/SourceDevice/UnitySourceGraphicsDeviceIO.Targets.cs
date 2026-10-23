using System;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Graphics.Rendering.Textures;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsDeviceIO
    {
        public void GetBackbufferSize(out int width, out int height)
            => components.Targets.GetBackbufferSize(out width, out height);

        public SourceSurfaceFormat GetBackbufferSurfaceFormat() => components.Targets.GetBackbufferSurfaceFormat();
        public SourceDepthFormat GetBackbufferDepthFormat() => components.Targets.GetBackbufferDepthFormat();

        public void SetViewport(ref CameraViewport viewport)
        {
            Flush();
            components.Targets.SetViewport(ref viewport);
        }

        public void SetScissorRect(ref IntRect rectangle)
        {
            Flush();
            components.Targets.SetScissorRect(ref rectangle);
        }

        public void Clear(SourceClearOptions options, ref Vector4 color, float depth, int stencil)
        {
            Flush();
            components.Targets.Clear(options, ref color, depth, stencil);
        }

        public void SetRenderTargets(SourceNativeRenderTargetBinding* targets, int count, IntPtr depthStencilBuffer,
            SourceDepthFormat depthFormat, byte preserve)
        {
            Flush();
            components.Targets.SetRenderTargets(targets, count, depthStencilBuffer, depthFormat, preserve);
        }

        public void SetRenderTargets(IntPtr targets, int count, IntPtr depthStencilBuffer,
            SourceDepthFormat depthFormat, byte preserve)
        {
            Flush();
            components.Targets.SetRenderTargets(targets, count, depthStencilBuffer, depthFormat, preserve);
        }

        public void ResolveTarget(ref SourceNativeRenderTargetBinding binding)
        {
            Flush();
            components.Targets.ResolveTarget(ref binding);
        }

        public void ResetBackbuffer(ref SourceNativePresentationParameters parameters)
        {
            Flush();
            components.Targets.ResetBackbuffer(ref parameters);
        }

        public void ReadBackbuffer(int x, int y, int width, int height, IntPtr data, int dataLength)
        {
            Flush();
            components.Targets.ReadBackbuffer(x, y, width, height, data, dataLength);
        }

        public void SwapBuffers(IntPtr source, IntPtr destination, IntPtr window)
        {
            Flush();
            components.Targets.SwapBuffers(source, destination, window);
        }

        public void SwapBuffers(ref IntRect source, IntPtr destination, IntPtr window)
        {
            Flush();
            components.Targets.SwapBuffers(ref source, destination, window);
        }

        public void SwapBuffers(IntPtr source, ref IntRect destination, IntPtr window)
        {
            Flush();
            components.Targets.SwapBuffers(source, ref destination, window);
        }

        public void SwapBuffers(ref IntRect source, ref IntRect destination, IntPtr window)
        {
            Flush();
            components.Targets.SwapBuffers(ref source, ref destination, window);
        }
    }
}
