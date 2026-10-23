using System;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsTargets
    {
        public void CopyBackbufferTo(IntPtr destination) => textures.CopyTexture2D(backbufferColor, destination);
        public void RestoreBackbufferFrom(IntPtr source) => textures.CopyTexture2D(source, backbufferColor);
    }
}
