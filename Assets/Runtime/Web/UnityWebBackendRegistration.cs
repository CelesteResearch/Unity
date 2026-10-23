#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content.Manifests;
using Celeste.Unity.Audio.Fmod.Html5;
using Celeste.Unity.Graphics.SourceDevice;
using UnityEngine;

namespace Celeste.Unity.Web
{
    public static class UnityWebBackendRegistration
    {
        public const string StudioScriptRelativePath = "./Content/FMOD/2.03.15/fmodstudio.js";
        public const string StudioWasmRelativePath = "./Content/FMOD/2.03.15/fmodstudio.wasm";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => UnityWebGameHost.CreateGraphicsAudioDevicesAsync = CreateAsync;

        private static async Task<UnityWebGraphicsAudioDevices> CreateAsync(Camera camera,
            UnityWebSourceWindowIO window, Func<ContentFileInfo, Uri> resourceUri,
            CancellationToken cancellationToken)
        {
            var pageUri = new Uri(Application.absoluteURL, UriKind.Absolute);
            string scriptUri = new Uri(pageUri, StudioScriptRelativePath).AbsoluteUri;
            string wasmUri = new Uri(pageUri, StudioWasmRelativePath).AbsoluteUri;
            var audio = await FmodStudioHtml5DeviceIO.LoadAsync(scriptUri, wasmUri, cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The Host publishes the returned owner before its cancellation check; successful return transfers it.
                return UnityWebGraphicsAudioOwner.CreateDevices(camera, audio,
                    UnityCpuEffectNativeBinding.Instance, resourceUri);
            }
            catch
            {
                audio.Dispose();
                throw;
            }
        }
    }
}
#endif
