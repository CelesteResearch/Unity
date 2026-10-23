using System;
using Celeste.Core.Framework.Startup;
using Celeste.Graphics.Content.Images;
using Celeste.Graphics.Effects;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Unity.Graphics.SourceDevice.Effects;
using Celeste.Unity.Graphics.SourceDevice.Geometry;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed class UnitySourceGraphicsDeviceIOFactory : ISourceGraphicsDeviceIOFactory,
        ISourceHostDrawTransportIO, IDisposable
    {
        private const int PixelTextureSlots = 16;
        private const int VertexTextureSlots = 4;
        private readonly Shader copyShader;
        private readonly Shader presentShader;
        private readonly Func<RenderTexture> getHostOutput;
        private readonly ICpuEffectNativeBinding effectBinding;
        private readonly bool nearestPresentation;
        private readonly bool matchHostOutputSize;
        private readonly Action<SourcePresentInterval> setPresentationSyncInterval;
        private UnitySourceTextureIO resources;
        private UnitySourceGraphicsDeviceIO device;
        private UnitySourceGraphicsTargets presentationTargets;

        public bool HasPresented => presentationTargets?.HasPresented == true;

        public UnitySourceGraphicsDeviceIOFactory(Shader copyShader, Shader presentShader,
            Func<RenderTexture> getHostOutput, ICpuEffectNativeBinding effectBinding,
            bool nearestPresentation = false, bool matchHostOutputSize = false,
            Action<SourcePresentInterval> setPresentationSyncInterval = null)
        {
            this.copyShader = copyShader ?? throw new ArgumentNullException(nameof(copyShader));
            this.presentShader = presentShader ?? throw new ArgumentNullException(nameof(presentShader));
            this.getHostOutput = getHostOutput ?? throw new ArgumentNullException(nameof(getHostOutput));
            this.effectBinding = effectBinding ?? throw new ArgumentNullException(nameof(effectBinding));
            this.nearestPresentation = nearestPresentation;
            this.matchHostOutputSize = matchHostOutputSize;
            this.setPresentationSyncInterval = setPresentationSyncInterval;
        }

        public ISourceGraphicsDeviceIO CreateGraphicsDevice(SourceImageStreamProcess actualImageStreams)
        {
            if (device != null) throw new InvalidOperationException("The source graphics device already exists.");
            resources = new UnitySourceTextureIO(actualImageStreams, actualImageStreams.FreeImage, copyShader);
            device = new UnitySourceGraphicsDeviceIO(resources, Initialize, PixelTextureSlots, VertexTextureSlots);
            return device;
        }

        private UnitySourceGraphicsDeviceComponents Initialize(ref SourceNativePresentationParameters parameters)
        {
            UnitySourceGraphicsTargets targets = null;
            UnitySourceEffectSubmission submission = null;
            UnitySourceGeometryIO geometry = null;
            try
            {
                targets = new UnitySourceGraphicsTargets(resources, presentShader, getHostOutput, ref parameters,
                    nearestPresentation, matchHostOutputSize, setPresentationSyncInterval);
                var state = new UnitySourceEffectState(PixelTextureSlots, VertexTextureSlots);
                var actualTargets = targets;
                submission = new UnitySourceEffectSubmission(effectBinding, state, resources, targets,
                    actualTargets.MarkCurrentAttachmentsWritten, () => actualTargets.CurrentDepthFormat);
                geometry = new UnitySourceGeometryIO(submission);
                var components = new UnitySourceGraphicsDeviceComponents(targets, submission, geometry);
                presentationTargets = targets;
                return components;
            }
            catch
            {
                try
                {
                    geometry?.Dispose();
                }
                finally
                {
                    try
                    {
                        submission?.Dispose();
                    }
                    finally
                    {
                        targets?.Dispose();
                    }
                }

                throw;
            }
        }

        public void BeginAcceptedSourceDraw() => device.BeginAcceptedSourceDraw();
        public void AbortUnsubmittedDraws() => device?.AbortUnsubmittedDraws();
        public void Flush() => device?.Flush();
        public void Dispose() => device?.Dispose();
    }
}
