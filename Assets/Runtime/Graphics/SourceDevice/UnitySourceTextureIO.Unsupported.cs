using System;
using Celeste.Graphics.Rendering.Textures;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceTextureIO
    {
        public IntPtr CreateTexture3D(SourceSurfaceFormat format, int width, int height, int depth, int levels)
            => throw new NotSupportedException("Texture3D is outside the first 2D resource consumer.");

        public void SetTextureData3D(IntPtr texture, int left, int top, int front, int width, int height,
            int depth, int level, IntPtr data, int dataLength)
            => throw new NotSupportedException("Texture3D upload is outside the first 2D resource consumer.");

        public void GetTextureData3D(IntPtr texture, int left, int top, int front, int width, int height,
            int depth, int level, IntPtr data, int dataLength)
            => throw new NotSupportedException("Texture3D readback is outside the first 2D resource consumer.");

        public IntPtr CreateTextureCube(SourceSurfaceFormat format, int size, int levels, byte renderTarget)
            => throw new NotSupportedException("Cubemaps are outside the first 2D resource consumer.");

        public void SetTextureDataCube(IntPtr texture, int x, int y, int width, int height, SourceCubeMapFace face,
            int level, IntPtr data, int dataLength)
            => throw new NotSupportedException("Cubemap upload is outside the first 2D resource consumer.");

        public void GetTextureDataCube(IntPtr texture, int x, int y, int width, int height, SourceCubeMapFace face,
            int level, IntPtr data, int dataLength)
            => throw new NotSupportedException("Cubemap readback is outside the first 2D resource consumer.");
    }
}
