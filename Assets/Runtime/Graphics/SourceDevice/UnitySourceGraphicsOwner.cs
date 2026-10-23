using System;
using Celeste.Graphics.Effects;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed class UnitySourceGraphicsOwner : IDisposable
    {
        public const string CopyShaderResource = "Celeste/Effects/UnitySourceTextureCopy";
        public const string PresentShaderResource = "Celeste/Effects/UnitySourceBackbufferPresent";
        private readonly UnitySourceCameraOutput cameraOutput;
        private readonly int previousVSyncCount = QualitySettings.vSyncCount;
        private bool disposed;

        public UnitySourceGraphicsDeviceIOFactory Factory { get; }
        public RenderTexture GameOutput => cameraOutput.ExistingOutput;

        public UnitySourceGraphicsOwner(Camera camera, ICpuEffectNativeBinding effectBinding,
            bool nearestPresentation = false, bool matchHostOutputSize = false)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (effectBinding == null) throw new ArgumentNullException(nameof(effectBinding));
            Shader copy = RequireShader(CopyShaderResource);
            Shader present = RequireShader(PresentShaderResource);
            UnitySourceGraphicsDeviceIOFactory createdGraphics = null;
            cameraOutput = new UnitySourceCameraOutput(camera, () => createdGraphics.Flush());
            try
            {
                createdGraphics = new UnitySourceGraphicsDeviceIOFactory(copy, present,
                    cameraOutput.GetOrCreate, effectBinding, nearestPresentation, matchHostOutputSize,
                    SetPresentationSyncInterval);
                Factory = createdGraphics;
            }
            catch
            {
                cameraOutput.Dispose();
                throw;
            }
        }

        private static void SetPresentationSyncInterval(SourcePresentInterval interval)
        {
            QualitySettings.vSyncCount = interval == SourcePresentInterval.Immediate ? 0 :
                interval == SourcePresentInterval.Two ? 2 : 1;
        }

        private static Shader RequireShader(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            if (shader == null || !shader.isSupported)
                throw new NotSupportedException("A supported source graphics shader is required: " + resource);
            return shader;
        }

        public void StopCameraCopy()
        {
            cameraOutput.StopCameraCopy();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                // Flush before the camera command and its borrowed output are detached.
                cameraOutput.Dispose();
            }
            finally
            {
                try
                {
                    Factory.Dispose();
                }
                finally
                {
                    QualitySettings.vSyncCount = previousVSyncCount;
                }
            }
        }
    }
}
