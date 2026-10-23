using System;
using System.Runtime.InteropServices;
using Celeste.Audio.Fmod.Interop;
using Celeste.Audio.Fmod.Parameters;
using Celeste.Audio.Fmod.Events;
using Celeste.Audio.Fmod.Capture;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    public sealed partial class FmodStudioHtml5DeviceIO
    {
        public FmodResult StudioVCAGetPath(IntPtr vca, byte[] path, int size, out int retrieved) =>
            FmodHtml5NativeMethods.StudioVCAGetPath(ModuleHandle, vca, path, size, out retrieved);

        public FmodResult StudioVCAGetVolume(IntPtr vca, out float volume, out float finalvolume) =>
            FmodHtml5NativeMethods.StudioVCAGetVolume(ModuleHandle, vca, out volume, out finalvolume);

        public FmodResult StudioVCASetVolume(IntPtr vca, float value) =>
            FmodHtml5NativeMethods.StudioVCASetVolume(ModuleHandle, vca, value);
    }
}
