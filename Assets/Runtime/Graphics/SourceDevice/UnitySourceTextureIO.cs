using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Celeste.Graphics.Content.Images;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceTextureIO : ISourceTextureIO, IDisposable
    {
        private sealed class Resource
        {
            public Texture Gpu;
            public Texture2D Upload;
            public Texture2D Readback;
            public RenderTexture ReadTarget;
            public bool UploadCurrent;
            public bool DepthOnly;
            public ulong ContentVersion;
            public SourceDepthFormat DepthFormat;
        }

        private readonly Dictionary<IntPtr, Resource> resources = new();
        private readonly ConcurrentQueue<IntPtr> disposals = new();
        private readonly Action<IntPtr> freeImage;
        private readonly Material copyMaterial;
        private readonly CommandBuffer copyCommands;
        private static readonly int CopySource = Shader.PropertyToID("_SourceResourceCopyTexture");
        private static readonly int CopyDimensions = Shader.PropertyToID("_SourceResourceCopyDimensions");
        private int nextHandle;

        public SourceImageStreamProcess ImageStreams { get; }
        public int SubmissionStateVersion { get; private set; }

        public UnitySourceTextureIO(SourceImageStreamProcess imageStreams, Action<IntPtr> freeImage, Shader copyShader)
        {
            ImageStreams = imageStreams ?? throw new ArgumentNullException(nameof(imageStreams));
            this.freeImage = freeImage ?? throw new ArgumentNullException(nameof(freeImage));
            if (copyShader == null || !copyShader.isSupported)
                throw new NotSupportedException("The exact resource copy shader must be supplied and supported.");
            copyMaterial = new Material(copyShader);
            copyCommands = new CommandBuffer { name = "Source RGBA8 resource copy" };
        }

        // Handles are table tokens, never Unity native pointers or addresses into managed memory.
        private IntPtr Own(Resource resource)
        {
            var handle = new IntPtr(checked(++nextHandle));
            resources.Add(handle, resource);
            return handle;
        }

        // Borrowed objects remain owned here. Only the submitting Unity thread may use them.
        public Texture BorrowTexture(IntPtr handle) => resources[handle].Gpu;

        public ulong GetContentVersion(IntPtr handle) => resources[handle].ContentVersion;

        public SourceDepthFormat GetDepthFormat(IntPtr handle)
        {
            var resource = resources[handle];
            if (!resource.DepthOnly)
                throw new ArgumentException("The handle does not own a depth buffer.", nameof(handle));
            return resource.DepthFormat;
        }

        // The command owner calls this after executing a successful external write, never while recording.
        // Borrowed Unity objects do not automatically report writes to this resource owner.
        public void MarkResourceWritten(IntPtr handle)
        {
            var resource = resources[handle];
            resource.UploadCurrent = false;
            resource.ContentVersion = unchecked(resource.ContentVersion + 1);
        }

        public RenderTexture BorrowRenderTarget(IntPtr handle)
        {
            var resource = resources[handle];
            if (resource.DepthOnly || resource.Gpu is not RenderTexture target)
                throw new ArgumentException("The handle does not own a color render target.", nameof(handle));
            return target;
        }

        public RenderBuffer BorrowDepthBuffer(IntPtr handle)
        {
            var resource = resources[handle];
            if (!resource.DepthOnly)
                throw new ArgumentException("The handle does not own a depth buffer.", nameof(handle));
            return ((RenderTexture)resource.Gpu).depthBuffer;
        }

        public IntPtr CreateTexture2D(SourceSurfaceFormat format, int width, int height, int levels,
            byte renderTarget)
        {
            RequireColor(format);
            if (levels != 1)
                throw new NotSupportedException("This resource candidate supports one mip level.");
            if (renderTarget != 0)
                return Own(new Resource { Gpu = CreateColorTarget(width, height) });

            var texture = CreateColorTexture(width, height);
            return Own(new Resource { Gpu = texture, Upload = texture, UploadCurrent = true });
        }

        private static Texture2D CreateColorTexture(int width, int height)
        {
            RequireDimensions(width, height);
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.Sample))
                throw new NotSupportedException("RGBA8 sampling is unavailable on this GPU.");
            return new Texture2D(width, height, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
        }

        private static RenderTexture CreateColorTarget(int width, int height)
        {
            RequireDimensions(width, height);
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.Render))
                throw new NotSupportedException("RGBA8 rendering is unavailable on this GPU.");
            return CreateTarget(new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1, mipCount = 1, useMipMap = false, autoGenerateMips = false,
            });
        }

        private static RenderTexture CreateTarget(RenderTextureDescriptor descriptor)
        {
            var target = new RenderTexture(descriptor)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            if (target.Create()) return target;
            Object.Destroy(target);
            throw new InvalidOperationException("Unity failed to allocate the requested render target.");
        }

        public void SetTextureName(IntPtr texture, string name) => resources[texture].Gpu.name = name;
        public void AddDisposeTexture(IntPtr texture) => disposals.Enqueue(texture);
        public void AddDisposeRenderbuffer(IntPtr renderbuffer) => disposals.Enqueue(renderbuffer);

        // SourceGraphicsResource finalizers can enqueue from another thread; only this drain calls Unity.
        public void DrainDisposals()
        {
            while (disposals.TryDequeue(out var handle))
            {
                if (!resources.TryGetValue(handle, out var resource)) continue;
                resources.Remove(handle);
                Release(resource);
            }
        }

        public void Dispose()
        {
            DrainDisposals();
            foreach (var resource in resources.Values) Release(resource);
            resources.Clear();
            copyCommands.Dispose();
            Object.Destroy(copyMaterial);
        }

        private static void Release(Resource resource)
        {
            if (resource.Upload != null && resource.Upload != resource.Gpu) Object.Destroy(resource.Upload);
            if (resource.Readback != null) Object.Destroy(resource.Readback);
            if (resource.ReadTarget != null)
            {
                resource.ReadTarget.Release();
                Object.Destroy(resource.ReadTarget);
            }

            if (resource.Gpu is RenderTexture target) target.Release();
            Object.Destroy(resource.Gpu);
        }

        public IntPtr Allocate(int bytes) => Marshal.AllocHGlobal(bytes);
        public void Free(IntPtr pointer) => Marshal.FreeHGlobal(pointer);
        public void Image_Free(IntPtr pointer) => freeImage(pointer);
        public byte SupportsSRGBRenderTargets() => 0;
        public byte SupportsDXT1() => 0;
        public byte SupportsS3TC() => 0;

        private static void RequireColor(SourceSurfaceFormat format)
        {
            if (format != SourceSurfaceFormat.Color)
                throw new NotSupportedException($"The resource candidate does not support {format}.");
        }

        private static void RequireDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions exceed the GPU limit.");
        }
    }
}
