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
        public FmodResult StudioSystemCreate(out IntPtr studiosystem, uint headerversion) =>
            FmodHtml5NativeMethods.StudioSystemCreate(ModuleHandle, out studiosystem, headerversion);

        public FmodResult StudioSystemFlushCommands(IntPtr studiosystem) =>
            FmodHtml5NativeMethods.StudioSystemFlushCommands(ModuleHandle, studiosystem);

        public FmodResult StudioSystemFlushSampleLoading(IntPtr studiosystem) =>
            FmodHtml5NativeMethods.StudioSystemFlushSampleLoading(ModuleHandle, studiosystem);

        public FmodResult StudioSystemGetBankCount(IntPtr studiosystem, out int count) =>
            FmodHtml5NativeMethods.StudioSystemGetBankCount(ModuleHandle, studiosystem, out count);

        public FmodResult StudioSystemGetBankList(IntPtr studiosystem, IntPtr[] array, int capacity, out int count) =>
            FmodHtml5NativeMethods.StudioSystemGetBankList(ModuleHandle, studiosystem, array, capacity, out count);

        public FmodResult StudioSystemGetBus(IntPtr studiosystem, byte[] path, out IntPtr bus) =>
            FmodHtml5NativeMethods.StudioSystemGetBus(ModuleHandle, studiosystem, path, out bus);

        public FmodResult StudioSystemGetEvent(IntPtr studiosystem, byte[] path, out IntPtr description) =>
            FmodHtml5NativeMethods.StudioSystemGetEvent(ModuleHandle, studiosystem, path, out description);

        public FmodResult StudioSystemGetEventByID(IntPtr studio, ref Guid id, out IntPtr description) =>
            FmodHtml5NativeMethods.StudioSystemGetEventByID(ModuleHandle, studio, ref id, out description);

        public FmodResult StudioSystemGetLowLevelSystem(IntPtr studiosystem, out IntPtr system) =>
            FmodHtml5NativeMethods.StudioSystemGetLowLevelSystem(ModuleHandle, studiosystem, out system);

        public FmodResult StudioSystemGetVCA(IntPtr studiosystem, byte[] path, out IntPtr vca) =>
            FmodHtml5NativeMethods.StudioSystemGetVCA(ModuleHandle, studiosystem, path, out vca);

        public FmodResult StudioSystemInitialize(IntPtr studiosystem, int maxchannels, uint studioFlags, uint flags,
            IntPtr extradriverdata) =>
            FmodHtml5NativeMethods.StudioSystemInitialize(ModuleHandle, studiosystem, maxchannels, studioFlags, flags,
                extradriverdata);

        public FmodResult StudioSystemLoadBankFile(IntPtr studiosystem, byte[] filename, uint flags, out IntPtr bank) =>
            FmodHtml5NativeMethods.StudioSystemLoadBankFile(ModuleHandle, studiosystem, filename, flags, out bank);

        public FmodResult StudioSystemRelease(IntPtr studiosystem) =>
            FmodHtml5NativeMethods.StudioSystemRelease(ModuleHandle, studiosystem);

        public FmodResult StudioSystemSetListenerAttributes(IntPtr studiosystem, int listener,
            ref FmodAttributes attributes) =>
            FmodHtml5NativeMethods.StudioSystemSetListenerAttributes(ModuleHandle, studiosystem, listener,
                ref attributes);

        public FmodResult StudioSystemUnloadAll(IntPtr studiosystem) =>
            FmodHtml5NativeMethods.StudioSystemUnloadAll(ModuleHandle, studiosystem);

        public FmodResult StudioSystemUpdate(IntPtr studiosystem) =>
            FmodHtml5NativeMethods.StudioSystemUpdate(ModuleHandle, studiosystem);

        public FmodResult SystemGetOutput(IntPtr system, out int output) =>
            FmodHtml5NativeMethods.SystemGetOutput(ModuleHandle, system, out output);

        public FmodResult SystemGetVersion(IntPtr system, out uint version) =>
            FmodHtml5NativeMethods.SystemGetVersion(ModuleHandle, system, out version);

        public FmodResult SystemSetOutput(IntPtr system, int output) =>
            FmodHtml5NativeMethods.SystemSetOutput(ModuleHandle, system, output);
    }
}
