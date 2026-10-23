using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SourceVector4 = System.Numerics.Vector4;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsTargets : ISourceGraphicsDeviceTargetIO, IDisposable
    {
        private sealed class ColorTargets
        {
            public readonly RenderTargetIdentifier[] Targets;
            public readonly RenderBufferLoadAction[] Loads;
            public readonly RenderBufferStoreAction[] Stores;

            public ColorTargets(int count)
            {
                Targets = new RenderTargetIdentifier[count];
                Loads = new RenderBufferLoadAction[count];
                Stores = new RenderBufferStoreAction[count];
                for (var index = 0; index < count; index++) Stores[index] = RenderBufferStoreAction.Store;
            }
        }

        private readonly UnitySourceTextureIO textures;
        private readonly Func<RenderTexture> getHostOutput;
        private readonly CommandBuffer commands;
        private readonly Material presentMaterial;
        private readonly ColorTargets[] colorTargets = new ColorTargets[5];
        private readonly IntPtr[] targetHandles = new IntPtr[4];
        private readonly bool nearestPresentation;
        private readonly bool matchHostOutputSize;
        private readonly Action<SourcePresentInterval> setPresentationSyncInterval;
        private static readonly int PresentTexture = Shader.PropertyToID("_SourceBackbufferPresentTexture");
        private static readonly int PresentSource = Shader.PropertyToID("_SourceBackbufferPresentSource");
        private IntPtr backbufferColor;
        private IntPtr backbufferDepth;
        private SourceDepthFormat backbufferDepthFormat;
        private IntPtr hostWindow;
        private IntPtr currentDepth;
        private int currentCount;
        private int currentWidth;
        private int currentHeight;
        private CameraViewport viewport;
        private IntRect scissor;
        private bool scissorEnabled;
        private bool currentStencilStorage;

        public int SubmissionStateVersion { get; private set; }
        public int ResourceStateVersion => textures.SubmissionStateVersion;
        public SourceDepthFormat CurrentDepthFormat { get; private set; }

        public UnitySourceGraphicsTargets(UnitySourceTextureIO textures, Shader presentShader,
            Func<RenderTexture> getHostOutput, ref SourceNativePresentationParameters parameters,
            bool nearestPresentation = false, bool matchHostOutputSize = false,
            Action<SourcePresentInterval> setPresentationSyncInterval = null)
        {
            this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
            this.getHostOutput = getHostOutput ?? throw new ArgumentNullException(nameof(getHostOutput));
            if (presentShader == null || !presentShader.isSupported)
                throw new NotSupportedException("The actual backbuffer presentation shader is required.");
            this.nearestPresentation = nearestPresentation;
            this.matchHostOutputSize = matchHostOutputSize;
            this.setPresentationSyncInterval = setPresentationSyncInterval;
            commands = new CommandBuffer { name = "Source targets and backbuffer" };
            presentMaterial = new Material(presentShader);
            for (var count = 1; count < colorTargets.Length; count++) colorTargets[count] = new ColorTargets(count);
            try
            {
                ResetBackbuffer(ref parameters);
            }
            catch
            {
                ReleaseBackbuffer();
                commands.Dispose();
                Object.Destroy(presentMaterial);
                throw;
            }
        }

        public void SetViewport(ref CameraViewport value)
        {
            if (value.MinDepth != 0 || value.MaxDepth != 1)
                throw new NotSupportedException(
                    "Non-default viewport depth ranges need the full effect shader depth consumer.");
            if (value.Width <= 0 || value.Height <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            viewport = value;
            RecordAndExecuteBindings(true);
        }

        public void SetScissorRect(ref IntRect rectangle)
        {
            if (rectangle.Width < 0 || rectangle.Height < 0) throw new ArgumentOutOfRangeException(nameof(rectangle));
            scissor = rectangle;
            RecordAndExecuteBindings(true);
        }

        // Rasterizer state owns the enable flag; a source scissor rectangle alone does not enable clipping.
        public void SetScissorEnabled(bool enabled)
        {
            scissorEnabled = enabled;
            RecordAndExecuteBindings(true);
        }

        private Rect SourcePixelRect(int x, int y, int width, int height)
            // Viewport/scissor pixel rows already match source resource rows; shader UV conventions are separate.
            => new Rect(x, y, width, height);

        // The later draw consumer records these bindings into its real command stream before drawing.
        // It must also rebuild its shader/pass and other state after either exposed version changes.
        public void RecordSubmissionBindings(CommandBuffer destination)
        {
            RecordBindings(destination, true);
            RecordViewportAndScissor(destination);
        }

        private void RecordViewportAndScissor(CommandBuffer destination)
        {
            destination.SetViewport(SourcePixelRect(viewport.X, viewport.Y, viewport.Width, viewport.Height));
            if (scissorEnabled)
                destination.EnableScissorRect(SourcePixelRect(scissor.X, scissor.Y, scissor.Width, scissor.Height));
            else
                destination.DisableScissorRect();
        }

        private void RecordAndExecuteBindings(bool preserve)
        {
            commands.Clear();
            RecordBindings(commands, preserve);
            RecordViewportAndScissor(commands);
            UnityEngine.Graphics.ExecuteCommandBuffer(commands);
        }

        // Called only after successful execution, before switching away from this attachment set.
        // The draw owner supplies the actual write mask; recording a draw does not change content versions.
        public void MarkCurrentAttachmentsWritten(SourceClearOptions written)
            => MarkCurrentAttachmentsWritten(written, 1);

        public void MarkCurrentAttachmentsWritten(SourceClearOptions written, uint colorAttachmentMask)
        {
            if ((written & SourceClearOptions.Target) != 0)
                for (var index = 0; index < currentCount; index++)
                    if ((colorAttachmentMask & (1u << index)) != 0)
                        textures.MarkResourceWritten(targetHandles[index]);
            if (currentDepth != IntPtr.Zero &&
                ((written & SourceClearOptions.DepthBuffer) != 0 ||
                 (currentStencilStorage &&
                  (written & SourceClearOptions.Stencil) != 0)))
                textures.MarkResourceWritten(currentDepth);
        }

        public void Clear(SourceClearOptions options, ref SourceVector4 color, float depth, int stencil)
        {
            var flags = RTClearFlags.None;
            if ((options & SourceClearOptions.Target) != 0) flags |= RTClearFlags.Color;
            if ((options & SourceClearOptions.DepthBuffer) != 0) flags |= RTClearFlags.Depth;
            if ((options & SourceClearOptions.Stencil) != 0) flags |= RTClearFlags.Stencil;
            commands.Clear();
            RecordBindings(commands, true);
            // Source Clear is attachment-wide, independent of the source viewport and rasterizer scissor.
            commands.SetViewport(new Rect(0, 0, currentWidth, currentHeight));
            commands.DisableScissorRect();
            commands.ClearRenderTarget(flags, new Color(color.X, color.Y, color.Z, color.W), depth,
                unchecked((byte)stencil));
            RecordViewportAndScissor(commands);
            UnityEngine.Graphics.ExecuteCommandBuffer(commands);
            MarkCurrentAttachmentsWritten(options, (1u << currentCount) - 1);
        }

        public void Dispose()
        {
            ReleaseBackbuffer();
            commands.Dispose();
            Object.Destroy(presentMaterial);
        }
    }
}
