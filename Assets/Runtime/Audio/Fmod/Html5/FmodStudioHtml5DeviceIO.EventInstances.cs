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
        public FmodResult StudioEventInstanceGet3DAttributes(IntPtr _event, out FmodAttributes attributes) =>
            FmodHtml5NativeMethods.StudioEventInstanceGet3DAttributes(ModuleHandle, _event, out attributes);

        public FmodResult StudioEventInstanceGetDescription(IntPtr _event, out IntPtr description) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetDescription(ModuleHandle, _event, out description);

        public FmodResult StudioEventInstanceGetParameterValue(IntPtr _event, byte[] name, out float value,
            out float finalvalue) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetParameterValue(ModuleHandle, _event, name, out value,
                out finalvalue);

        public FmodResult StudioEventInstanceGetPaused(IntPtr _event, out int paused) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetPaused(ModuleHandle, _event, out paused);

        public FmodResult StudioEventInstanceGetPitch(IntPtr _event, out float pitch, out float finalpitch) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetPitch(ModuleHandle, _event, out pitch, out finalpitch);

        public FmodResult StudioEventInstanceGetPlaybackState(IntPtr _event, out FmodPlaybackState state) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetPlaybackState(ModuleHandle, _event, out state);

        public FmodResult StudioEventInstanceGetTimelinePosition(IntPtr _event, out int position) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetTimelinePosition(ModuleHandle, _event, out position);

        public FmodResult StudioEventInstanceGetVolume(IntPtr _event, out float volume, out float finalvolume) =>
            FmodHtml5NativeMethods.StudioEventInstanceGetVolume(ModuleHandle, _event, out volume, out finalvolume);

        public FmodResult StudioEventInstanceRelease(IntPtr _event) =>
            FmodHtml5NativeMethods.StudioEventInstanceRelease(ModuleHandle, _event);

        public FmodResult StudioEventInstanceSet3DAttributes(IntPtr _event, ref FmodAttributes attributes) =>
            FmodHtml5NativeMethods.StudioEventInstanceSet3DAttributes(ModuleHandle, _event, ref attributes);

        public FmodResult StudioEventInstanceSetParameterValue(IntPtr _event, byte[] name, float value) =>
            FmodHtml5NativeMethods.StudioEventInstanceSetParameterValue(ModuleHandle, _event, name, value);

        public FmodResult StudioEventInstanceSetPaused(IntPtr _event, int paused) =>
            FmodHtml5NativeMethods.StudioEventInstanceSetPaused(ModuleHandle, _event, paused);

        public FmodResult StudioEventInstanceSetPitch(IntPtr _event, float pitch) =>
            FmodHtml5NativeMethods.StudioEventInstanceSetPitch(ModuleHandle, _event, pitch);

        public FmodResult StudioEventInstanceSetTimelinePosition(IntPtr _event, int position) =>
            FmodHtml5NativeMethods.StudioEventInstanceSetTimelinePosition(ModuleHandle, _event, position);

        public FmodResult StudioEventInstanceSetVolume(IntPtr _event, float volume) =>
            FmodHtml5NativeMethods.StudioEventInstanceSetVolume(ModuleHandle, _event, volume);

        public FmodResult StudioEventInstanceStart(IntPtr _event) =>
            FmodHtml5NativeMethods.StudioEventInstanceStart(ModuleHandle, _event);

        public FmodResult StudioEventInstanceStop(IntPtr _event, int mode) =>
            FmodHtml5NativeMethods.StudioEventInstanceStop(ModuleHandle, _event, mode);

        public FmodResult StudioEventInstanceTriggerCue(IntPtr _event) =>
            FmodHtml5NativeMethods.StudioEventInstanceTriggerCue(ModuleHandle, _event);
    }
}
