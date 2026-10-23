using System;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content;
using Celeste.Core.IO.Content.Manifests;
using Celeste.Game.Audio.Banks;
using Celeste.Graphics.Effects;
using Celeste.Unity.Audio.Fmod.Html5;
using Celeste.Unity.Graphics.SourceDevice;
using UnityEngine;

namespace Celeste.Unity.Web
{
    public sealed class UnityWebGraphicsAudioOwner : IDisposable
    {
        public const string CopyShaderResource = "Celeste/Effects/UnitySourceTextureCopy";
        public const string PresentShaderResource = "Celeste/Effects/UnitySourceBackbufferPresent";
        private readonly UnitySourceCameraOutput cameraOutput;
        private readonly UnitySourceGraphicsDeviceIOFactory graphics;
        private readonly FmodStudioHtml5DeviceIO studioAudio;
        private readonly Func<ContentFileInfo, Uri> resourceUri;
        private OriginalAudioBankPreparation audioPreparation;
        private FmodBankFilePreparation filePreparation;
        private ResidentContentSource audioSource;
        private bool disposed;

        private UnityWebGraphicsAudioOwner(Camera camera, Shader copyShader, Shader presentShader,
            ICpuEffectNativeBinding effectBinding, FmodStudioHtml5DeviceIO studioAudio,
            Func<ContentFileInfo, Uri> resourceUri)
        {
            this.studioAudio = studioAudio;
            this.resourceUri = resourceUri;
            UnitySourceGraphicsDeviceIOFactory createdGraphics = null;
            cameraOutput = new UnitySourceCameraOutput(camera, () => createdGraphics.Flush());
            try
            {
                createdGraphics = new UnitySourceGraphicsDeviceIOFactory(copyShader, presentShader,
                    cameraOutput.GetOrCreate, effectBinding, matchHostOutputSize: true);
                graphics = createdGraphics;
            }
            catch
            {
                cameraOutput.Dispose();
                throw;
            }
        }

        // The caller must await the real audio module's initialization before calling this on Unity's main thread.
        // Audio ownership transfers only when this method returns successfully; the game session closes first.
        public static UnityWebGraphicsAudioDevices CreateDevices(Camera camera, FmodStudioHtml5DeviceIO studioAudio,
            ICpuEffectNativeBinding effectBinding, Func<ContentFileInfo, Uri> resourceUri)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (studioAudio == null) throw new ArgumentNullException(nameof(studioAudio));
            if (effectBinding == null) throw new ArgumentNullException(nameof(effectBinding));
            if (resourceUri == null) throw new ArgumentNullException(nameof(resourceUri));
            Shader copy = RequireShader(CopyShaderResource);
            Shader present = RequireShader(PresentShaderResource);
            var owner = new UnityWebGraphicsAudioOwner(camera, copy, present, effectBinding, studioAudio, resourceUri);
            return new UnityWebGraphicsAudioDevices(owner.graphics, owner.graphics, studioAudio,
                owner.PrepareAudioBanksAsync, () => owner.cameraOutput.ExistingOutput, owner,
                owner.graphics.AbortUnsubmittedDraws, studioAudio.ReadBankFileProgress,
                () => owner.graphics.HasPresented);
        }

        private ValueTask PrepareAudioBanksAsync(string workName, ResidentContentSource source,
            CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityWebGraphicsAudioOwner));
            cancellationToken.ThrowIfCancellationRequested();
            if (audioPreparation == null)
            {
                filePreparation = new FmodBankFilePreparation(studioAudio, source.TitleLocation, resourceUri);
                audioPreparation = new OriginalAudioBankPreparation(source, filePreparation.PrepareFileAsync,
                    filePreparation.IsReady);
                audioSource = source;
            }
            else if (!ReferenceEquals(audioSource, source))
                throw new InvalidOperationException("The Web audio preparation belongs to a different content source.");

            return audioPreparation.PrepareAdditionalResourcesAsync(workName, cancellationToken);
        }

        private static Shader RequireShader(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            if (shader == null || !shader.isSupported)
                throw new NotSupportedException("A supported source graphics shader is required: " + resource);
            return shader;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                // Finish the unique device prefix before detaching the camera command and releasing its output.
                cameraOutput.Dispose();
            }
            finally
            {
                try
                {
                    graphics.Dispose();
                }
                finally
                {
                    try
                    {
                        try
                        {
                            audioPreparation?.Dispose();
                        }
                        finally
                        {
                            filePreparation?.Dispose();
                        }
                    }
                    finally
                    {
                        studioAudio.Dispose();
                    }
                }
            }
        }
    }
}
