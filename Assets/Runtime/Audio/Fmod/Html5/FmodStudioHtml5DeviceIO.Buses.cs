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
        public FmodResult StudioBusGetMute(IntPtr bus, out int mute) =>
            FmodHtml5NativeMethods.StudioBusGetMute(ModuleHandle, bus, out mute);

        public FmodResult StudioBusGetPath(IntPtr bus, byte[] path, int size, out int retrieved) =>
            FmodHtml5NativeMethods.StudioBusGetPath(ModuleHandle, bus, path, size, out retrieved);

        public FmodResult StudioBusGetPaused(IntPtr bus, out int paused) =>
            FmodHtml5NativeMethods.StudioBusGetPaused(ModuleHandle, bus, out paused);

        public FmodResult StudioBusGetVolume(IntPtr bus, out float volume, out float finalvolume) =>
            FmodHtml5NativeMethods.StudioBusGetVolume(ModuleHandle, bus, out volume, out finalvolume);

        public FmodResult StudioBusSetMute(IntPtr bus, int mute) =>
            FmodHtml5NativeMethods.StudioBusSetMute(ModuleHandle, bus, mute);

        public FmodResult StudioBusSetPaused(IntPtr bus, int paused) =>
            FmodHtml5NativeMethods.StudioBusSetPaused(ModuleHandle, bus, paused);

        public FmodResult StudioBusSetVolume(IntPtr bus, float volume) =>
            FmodHtml5NativeMethods.StudioBusSetVolume(ModuleHandle, bus, volume);

        public FmodResult StudioBusStopAllEvents(IntPtr bus, int mode) =>
            FmodHtml5NativeMethods.StudioBusStopAllEvents(ModuleHandle, bus, mode);
    }
}
