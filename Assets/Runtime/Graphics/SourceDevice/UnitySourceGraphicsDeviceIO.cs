using System;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsDeviceIO : ISourceGraphicsDeviceIO, IDisposable
    {
        private readonly UnitySourceTextureIO resources;
        private readonly UnitySourceGraphicsDeviceInitializer initialize;
        private readonly int pixelTextureSlots;
        private readonly int vertexTextureSlots;
        private UnitySourceGraphicsDeviceComponents components;
        private bool destroyed;

        public ISourceTextureIO TextureIO { get; }

        public UnitySourceGraphicsDeviceIO(UnitySourceTextureIO resources,
            UnitySourceGraphicsDeviceInitializer initialize, int pixelTextureSlots, int vertexTextureSlots)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.initialize = initialize ?? throw new ArgumentNullException(nameof(initialize));
            this.pixelTextureSlots = pixelTextureSlots;
            this.vertexTextureSlots = vertexTextureSlots;
            TextureIO = new UnitySourceTextureSubmissionIO(resources, Flush);
        }

        public IntPtr CreateDevice(ref SourceNativePresentationParameters parameters, byte debugMode)
        {
            if (destroyed || components != null)
                throw new InvalidOperationException("The graphics device can only be created once.");
            components = initialize(ref parameters) ??
                         throw new InvalidOperationException("The host did not construct the graphics device.");
            // SourceGraphicsDevice treats this as an opaque lifetime identity, never an operating system pointer.
            return new IntPtr(1);
        }

        public void Flush()
        {
            // The camera may release its last output after SourceGraphicsDevice has already closed this owner.
            if (!destroyed) components?.Flush();
        }

        public void BeginAcceptedSourceDraw()
        {
            Flush();
            components.Geometry.DrainDisposals();
            components.Effects.DrainDisposals();
            resources.DrainDisposals();
        }

        public void GetMaxTextureSlots(out int fragmentSlots, out int vertexSlots)
        {
            fragmentSlots = pixelTextureSlots;
            vertexSlots = vertexTextureSlots;
        }

        public void AbortUnsubmittedDraws() => components?.AbortUnsubmittedDraws();

        public IntPtr Allocate(int bytes) => resources.Allocate(bytes);
        public void Free(IntPtr pointer) => resources.Free(pointer);
        public void DestroyDevice() => Dispose();

        public void Dispose()
        {
            if (destroyed) return;
            destroyed = true;
            try
            {
                components?.Dispose();
            }
            finally
            {
                resources.Dispose();
            }
        }
    }
}
