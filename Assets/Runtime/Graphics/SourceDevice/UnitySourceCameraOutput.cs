using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed class UnitySourceCameraOutput : IDisposable
    {
        private readonly Camera camera;
        private readonly Action flushDevicePrefix;
        private readonly int mainThread;
        private RenderTexture output;
        private CommandBuffer cameraCopy;
        private bool copyStopped;
        private bool disposed;

        public RenderTexture ExistingOutput => disposed || copyStopped ? null : output;

        public UnitySourceCameraOutput(Camera camera, Action flushDevicePrefix)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            this.flushDevicePrefix = flushDevicePrefix ?? throw new ArgumentNullException(nameof(flushDevicePrefix));
            mainThread = Thread.CurrentThread.ManagedThreadId;
        }

        public RenderTexture GetOrCreate()
        {
            EnsureOutput();
            return output;
        }

        public void EnsureOutput()
        {
            RequireMainThread();
            if (disposed)
                throw new ObjectDisposedException(nameof(UnitySourceCameraOutput));
            if (GraphicsSettings.currentRenderPipeline != null)
                throw new NotSupportedException("This camera output requires the Unity Builtin pipeline.");
            if (!camera.isActiveAndEnabled || camera.targetTexture != null || camera.targetDisplay != 0
                || camera.allowHDR || camera.allowMSAA || camera.rect != new Rect(0, 0, 1, 1))
                throw new InvalidOperationException("Supply the active full-window LDR, non-MSAA game camera.");
            int width = camera.pixelWidth;
            int height = camera.pixelHeight;
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("The Unity camera output has no drawable size.");
            if (output != null && output.IsCreated() && output.width == width && output.height == height)
                return;
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.Render)
                || !SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.Sample))
                throw new NotSupportedException("RGBA8 UNorm rendering and sampling are required.");

            ReleaseOutput();
            RenderTexture createdOutput = null;
            CommandBuffer createdCopy = null;
            try
            {
                createdOutput = new RenderTexture(
                    new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_UNorm, 0)
                    {
                        msaaSamples = 1, useMipMap = false, autoGenerateMips = false
                    });
                createdOutput.name = "Celeste Camera Output";
                createdOutput.filterMode = FilterMode.Point;
                createdOutput.wrapMode = TextureWrapMode.Clamp;
                if (!createdOutput.Create())
                    throw new InvalidOperationException("Unity camera output allocation failed.");
                using (var initializeOutput = new CommandBuffer { name = "Celeste Camera Output Initialization" })
                {
                    initializeOutput.SetRenderTarget(createdOutput);
                    initializeOutput.SetViewport(new Rect(0, 0, width, height));
                    initializeOutput.DisableScissorRect();
                    initializeOutput.ClearRenderTarget(RTClearFlags.Color, Color.black, 1f, 0);
                    UnityEngine.Graphics.ExecuteCommandBuffer(initializeOutput);
                }

                if (!copyStopped)
                {
                    createdCopy = new CommandBuffer();
                    createdCopy.name = "Celeste Camera Copy";
                    // Source targets retain top-row coordinates; camera presentation needs the opposite
                    // texture orientation on top-origin graphics backends.
                    if (SystemInfo.graphicsUVStartsAtTop)
                        createdCopy.Blit(createdOutput, BuiltinRenderTextureType.CameraTarget,
                            new Vector2(1, -1), new Vector2(0, 1));
                    else
                        createdCopy.Blit(createdOutput, BuiltinRenderTextureType.CameraTarget);
                    camera.AddCommandBuffer(CameraEvent.AfterEverything, createdCopy);
                }

                output = createdOutput;
                cameraCopy = createdCopy;
            }
            catch
            {
                try
                {
                    if (createdCopy != null)
                    {
                        try
                        {
                            camera.RemoveCommandBuffer(CameraEvent.AfterEverything, createdCopy);
                        }
                        finally
                        {
                            createdCopy.Dispose();
                        }
                    }
                }
                finally
                {
                    if (createdOutput != null)
                    {
                        try
                        {
                            createdOutput.Release();
                        }
                        finally
                        {
                            DestroyOutput(createdOutput);
                        }
                    }
                }

                throw;
            }
        }

        public void StopCameraCopy()
        {
            RequireMainThread();
            if (disposed || copyStopped)
                return;
            if (output != null)
                flushDevicePrefix();
            copyStopped = true;
            ReleaseCameraCopy();
        }

        private void ReleaseCameraCopy()
        {
            if (cameraCopy == null)
                return;
            try
            {
                camera.RemoveCommandBuffer(CameraEvent.AfterEverything, cameraCopy);
            }
            finally
            {
                try
                {
                    cameraCopy.Dispose();
                }
                finally
                {
                    cameraCopy = null;
                }
            }
        }

        private void ReleaseOutput()
        {
            if (output != null)
                flushDevicePrefix();
            try
            {
                ReleaseCameraCopy();
            }
            finally
            {
                if (output != null)
                {
                    try
                    {
                        output.Release();
                    }
                    finally
                    {
                        try
                        {
                            DestroyOutput(output);
                        }
                        finally
                        {
                            output = null;
                        }
                    }
                }
            }
        }

        private static void DestroyOutput(RenderTexture texture)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }

        private void RequireMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                throw new InvalidOperationException("Camera output operations require its Unity main owner thread.");
        }

        public void Dispose()
        {
            RequireMainThread();
            if (disposed)
                return;
            ReleaseOutput();
            disposed = true;
        }
    }
}
