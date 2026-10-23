using System;
using Celeste.Graphics.Content.Images;
using Celeste.Graphics.Rendering.Textures;

namespace Celeste.Unity.Graphics.SourceDevice
{
    // Source uploads and readbacks are synchronous. Commit earlier deferred draws before accessing their resources.
    public sealed class UnitySourceTextureSubmissionIO : ISourceTextureIO
    {
        private readonly UnitySourceTextureIO resources;
        private readonly Action flush;

        public SourceImageStreamProcess ImageStreams => resources.ImageStreams;

        public UnitySourceTextureSubmissionIO(UnitySourceTextureIO resources, Action flush)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.flush = flush ?? throw new ArgumentNullException(nameof(flush));
        }

        // Called once at an external rendering boundary, never once per borrowed texture.
        public void FlushPendingDraws() => flush();

        // Borrowed lifetime and thread rules are the same as the physical resource owner.
        public UnityEngine.Texture BorrowTexture(IntPtr handle) => resources.BorrowTexture(handle);

        public void CopyTexture2D(IntPtr source, IntPtr destination)
        {
            flush();
            resources.CopyTexture2D(source, destination);
        }

        public void SetTextureData2D(IntPtr texture, int x, int y, int width, int height, int level,
            IntPtr data, int dataLength)
        {
            flush();
            resources.SetTextureData2D(texture, x, y, width, height, level, data, dataLength);
        }

        public void GetTextureData2D(IntPtr texture, int x, int y, int width, int height, int level,
            IntPtr data, int dataLength)
        {
            flush();
            resources.GetTextureData2D(texture, x, y, width, height, level, data, dataLength);
        }

        public void SetTextureName(IntPtr texture, string name) => resources.SetTextureName(texture, name);
        public void AddDisposeTexture(IntPtr texture) => resources.AddDisposeTexture(texture);
        public void AddDisposeRenderbuffer(IntPtr renderbuffer) => resources.AddDisposeRenderbuffer(renderbuffer);
        public byte SupportsSRGBRenderTargets() => resources.SupportsSRGBRenderTargets();
        public byte SupportsDXT1() => resources.SupportsDXT1();
        public byte SupportsS3TC() => resources.SupportsS3TC();
        public IntPtr Allocate(int bytes) => resources.Allocate(bytes);
        public void Free(IntPtr pointer) => resources.Free(pointer);
        public void Image_Free(IntPtr pointer) => resources.Image_Free(pointer);

        public int GetMaxMultiSampleCount(SourceSurfaceFormat format, int preferredCount)
            => resources.GetMaxMultiSampleCount(format, preferredCount);

        public IntPtr CreateTexture2D(SourceSurfaceFormat format, int width, int height, int levels, byte renderTarget)
            => resources.CreateTexture2D(format, width, height, levels, renderTarget);

        public IntPtr CreateTexture3D(SourceSurfaceFormat format, int width, int height, int depth, int levels)
            => resources.CreateTexture3D(format, width, height, depth, levels);

        public void SetTextureData3D(IntPtr texture, int left, int top, int front, int width, int height, int depth,
            int level, IntPtr data, int dataLength)
            => resources.SetTextureData3D(texture, left, top, front, width, height, depth, level, data, dataLength);

        public void GetTextureData3D(IntPtr texture, int left, int top, int front, int width, int height, int depth,
            int level, IntPtr data, int dataLength)
            => resources.GetTextureData3D(texture, left, top, front, width, height, depth, level, data, dataLength);

        public IntPtr CreateTextureCube(SourceSurfaceFormat format, int size, int levels, byte renderTarget)
            => resources.CreateTextureCube(format, size, levels, renderTarget);

        public void SetTextureDataCube(IntPtr texture, int x, int y, int width, int height, SourceCubeMapFace face,
            int level, IntPtr data, int dataLength)
            => resources.SetTextureDataCube(texture, x, y, width, height, face, level, data, dataLength);

        public void GetTextureDataCube(IntPtr texture, int x, int y, int width, int height, SourceCubeMapFace face,
            int level, IntPtr data, int dataLength)
            => resources.GetTextureDataCube(texture, x, y, width, height, face, level, data, dataLength);

        public IntPtr GenColorRenderbuffer(int width, int height, SourceSurfaceFormat format, int sampleCount,
            IntPtr texture)
            => resources.GenColorRenderbuffer(width, height, format, sampleCount, texture);

        public IntPtr GenDepthStencilRenderbuffer(int width, int height, SourceDepthFormat format, int sampleCount)
            => resources.GenDepthStencilRenderbuffer(width, height, format, sampleCount);
    }
}
