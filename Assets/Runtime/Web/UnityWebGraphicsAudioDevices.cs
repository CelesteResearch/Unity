using System;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.Framework.Startup;
using Celeste.Core.IO.Content;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Audio.Fmod.IO;
using UnityEngine;

namespace Celeste.Unity.Web
{
    // Only the actual graphics and FMOD backends are supplied by the integration owner.
    public sealed class UnityWebGraphicsAudioDevices : IDisposable
    {
        public ISourceGraphicsDeviceIOFactory GraphicsFactory { get; }
        public ISourceHostDrawTransportIO DrawTransport { get; }
        public IFmodStudioDeviceIO StudioAudio { get; }
        public Func<string, ResidentContentSource, CancellationToken, ValueTask> PrepareAudioBanks { get; }
        public Func<RenderTexture> ReadOutput { get; }
        public Func<ContentLoadProgress> ReadAudioProgress { get; }
        public Action AbortUnsubmittedDraws { get; }
        private readonly IDisposable graphicsAudioOwner;
        private readonly Func<bool> readFirstSourcePresentation;

        public bool FirstSourcePresentationSubmitted => readFirstSourcePresentation?.Invoke() == true;

        public UnityWebGraphicsAudioDevices(ISourceGraphicsDeviceIOFactory graphicsFactory,
            ISourceHostDrawTransportIO drawTransport, IFmodStudioDeviceIO studioAudio,
            Func<string, ResidentContentSource, CancellationToken, ValueTask> prepareAudioBanks,
            Func<RenderTexture> readOutput, IDisposable graphicsAudioOwner, Action abortUnsubmittedDraws,
            Func<ContentLoadProgress> readAudioProgress, Func<bool> readFirstSourcePresentation = null)
        {
            GraphicsFactory = graphicsFactory ?? throw new ArgumentNullException(nameof(graphicsFactory));
            DrawTransport = drawTransport ?? throw new ArgumentNullException(nameof(drawTransport));
            StudioAudio = studioAudio ?? throw new ArgumentNullException(nameof(studioAudio));
            PrepareAudioBanks = prepareAudioBanks ?? throw new ArgumentNullException(nameof(prepareAudioBanks));
            ReadOutput = readOutput ?? throw new ArgumentNullException(nameof(readOutput));
            this.graphicsAudioOwner = graphicsAudioOwner ?? throw new ArgumentNullException(nameof(graphicsAudioOwner));
            AbortUnsubmittedDraws =
                abortUnsubmittedDraws ?? throw new ArgumentNullException(nameof(abortUnsubmittedDraws));
            this.readFirstSourcePresentation = readFirstSourcePresentation;
            ReadAudioProgress = readAudioProgress ?? throw new ArgumentNullException(nameof(readAudioProgress));
        }

        public void Dispose() => graphicsAudioOwner.Dispose();
    }
}
