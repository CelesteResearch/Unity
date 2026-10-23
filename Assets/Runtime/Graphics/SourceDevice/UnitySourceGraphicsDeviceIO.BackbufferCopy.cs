using System;
using Celeste.Graphics.Rendering.Devices;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsDeviceIO : ISourceGraphicsBackbufferCopyIO
    {
        public bool HasPresentedBackbuffer => components.Targets.HasPresented;

        public void CopyBackbufferTo(IntPtr destination)
        {
            Flush();
            components.Targets.CopyBackbufferTo(destination);
        }

        public void RestoreBackbufferFrom(IntPtr source)
        {
            Flush();
            components.Targets.RestoreBackbufferFrom(source);
        }
    }
}
