using System;
using System.Runtime.InteropServices;
using Celeste.Audio.Fmod.Interop;
using Celeste.Audio.Fmod.Events;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    internal static partial class FmodHtml5NativeMethods
    {
        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetBusCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetBusCount(int moduleHandle, IntPtr bank, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetBusList",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetBusList(int moduleHandle, IntPtr bank,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            IntPtr[] array, int capacity, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetEventCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetEventCount(int moduleHandle, IntPtr bank, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetEventList",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetEventList(int moduleHandle, IntPtr bank,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            IntPtr[] array, int capacity, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetID",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetID(int moduleHandle, IntPtr bank, out Guid id);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetPath",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetPath(int moduleHandle, IntPtr bank,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] path, int size, out int retrieved);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetVCACount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetVCACount(int moduleHandle, IntPtr bank, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankGetVCAList",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankGetVCAList(int moduleHandle, IntPtr bank,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            IntPtr[] array, int capacity, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankLoadSampleData",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankLoadSampleData(int moduleHandle, IntPtr bank);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBankUnload",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBankUnload(int moduleHandle, IntPtr bank);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusGetMute",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusGetMute(int moduleHandle, IntPtr bus, out int mute);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusGetPath",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusGetPath(int moduleHandle, IntPtr bus,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] path, int size, out int retrieved);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusGetPaused",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusGetPaused(int moduleHandle, IntPtr bus, out int paused);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusGetVolume",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusGetVolume(int moduleHandle, IntPtr bus, out float volume,
            out float finalvolume);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusSetMute",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusSetMute(int moduleHandle, IntPtr bus, int mute);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusSetPaused",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusSetPaused(int moduleHandle, IntPtr bus, int paused);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusSetVolume",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusSetVolume(int moduleHandle, IntPtr bus, float volume);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioBusStopAllEvents",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioBusStopAllEvents(int moduleHandle, IntPtr bus, int mode);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionCreateInstance",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionCreateInstance(int moduleHandle,
            IntPtr eventdescription,
            out IntPtr instance);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetID",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetID(int moduleHandle, IntPtr eventdescription,
            out Guid id);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetInstanceCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetInstanceCount(int moduleHandle,
            IntPtr eventdescription,
            out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetLength",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetLength(int moduleHandle, IntPtr eventdescription,
            out int length);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetParameter",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetParameter(int moduleHandle, IntPtr description,
            [In] byte[] name, out FmodHtml5ParameterDescription parameter);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetParameterByIndex",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetParameterByIndex(int moduleHandle,
            IntPtr eventdescription, int index, out FmodHtml5ParameterDescription parameter);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetParameterCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetParameterCount(int moduleHandle,
            IntPtr eventdescription,
            out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetPath",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetPath(int moduleHandle, IntPtr eventdescription,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] path, int size, out int retrieved);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionIs3D",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionIs3D(int moduleHandle, IntPtr eventdescription,
            out int is3D);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionIsOneshot",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionIsOneshot(int moduleHandle, IntPtr eventdescription,
            out int oneshot);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionLoadSampleData",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionLoadSampleData(int moduleHandle,
            IntPtr eventdescription);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionUnloadSampleData",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionUnloadSampleData(int moduleHandle, IntPtr description);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGet3DAttributes",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGet3DAttributes(int moduleHandle, IntPtr _event,
            out FmodAttributes attributes);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetDescription",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetDescription(int moduleHandle, IntPtr _event,
            out IntPtr description);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetParameterValue",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetParameterValue(int moduleHandle, IntPtr _event,
            [In] byte[] name, out float value, out float finalvalue);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetPaused",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetPaused(int moduleHandle, IntPtr _event, out int paused);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetPitch",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetPitch(int moduleHandle, IntPtr _event, out float pitch,
            out float finalpitch);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetPlaybackState",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetPlaybackState(int moduleHandle, IntPtr _event,
            out FmodPlaybackState state);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetTimelinePosition",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetTimelinePosition(int moduleHandle, IntPtr _event,
            out int position);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceGetVolume",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceGetVolume(int moduleHandle, IntPtr _event,
            out float volume,
            out float finalvolume);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceRelease",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceRelease(int moduleHandle, IntPtr _event);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceSet3DAttributes",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceSet3DAttributes(int moduleHandle, IntPtr _event,
            ref FmodAttributes attributes);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceSetParameterValue",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceSetParameterValue(int moduleHandle, IntPtr _event,
            [In] byte[] name, float value);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceSetPaused",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceSetPaused(int moduleHandle, IntPtr _event, int paused);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceSetPitch",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceSetPitch(int moduleHandle, IntPtr _event, float pitch);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceSetTimelinePosition",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceSetTimelinePosition(int moduleHandle, IntPtr _event,
            int position);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceSetVolume",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceSetVolume(int moduleHandle, IntPtr _event, float volume);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceStart",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceStart(int moduleHandle, IntPtr _event);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceStop",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceStop(int moduleHandle, IntPtr _event, int mode);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventInstanceTriggerCue",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventInstanceTriggerCue(int moduleHandle, IntPtr _event);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemCreate",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemCreate(int moduleHandle, out IntPtr studiosystem,
            uint headerversion);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemFlushCommands",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemFlushCommands(int moduleHandle, IntPtr studiosystem);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemFlushSampleLoading",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemFlushSampleLoading(int moduleHandle, IntPtr studiosystem);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetBankCount",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult
            StudioSystemGetBankCount(int moduleHandle, IntPtr studiosystem, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetBankList",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemGetBankList(int moduleHandle, IntPtr studiosystem,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            IntPtr[] array, int capacity, out int count);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetBus",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemGetBus(int moduleHandle, IntPtr studiosystem, [In] byte[] path,
            out IntPtr bus);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetEvent",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemGetEvent(int moduleHandle, IntPtr studiosystem, [In] byte[] path,
            out IntPtr description);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetEventByID",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemGetEventByID(int moduleHandle, IntPtr studio, ref Guid id,
            out IntPtr description);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetLowLevelSystem",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemGetLowLevelSystem(int moduleHandle, IntPtr studiosystem,
            out IntPtr system);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemGetVCA",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemGetVCA(int moduleHandle, IntPtr studiosystem, [In] byte[] path,
            out IntPtr vca);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemInitialize",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemInitialize(int moduleHandle, IntPtr studiosystem, int maxchannels,
            uint studioFlags, uint flags, IntPtr extradriverdata);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemLoadBankFile",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemLoadBankFile(int moduleHandle, IntPtr studiosystem,
            [In] byte[] filename, uint flags, out IntPtr bank);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemRelease",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemRelease(int moduleHandle, IntPtr studiosystem);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemSetListenerAttributes",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemSetListenerAttributes(int moduleHandle, IntPtr studiosystem,
            int listener, ref FmodAttributes attributes);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemUnloadAll",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemUnloadAll(int moduleHandle, IntPtr studiosystem);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioSystemUpdate",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioSystemUpdate(int moduleHandle, IntPtr studiosystem);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioVCAGetPath",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioVCAGetPath(int moduleHandle, IntPtr vca,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] path, int size, out int retrieved);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioVCAGetVolume",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioVCAGetVolume(int moduleHandle, IntPtr vca, out float volume,
            out float finalvolume);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioVCASetVolume",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioVCASetVolume(int moduleHandle, IntPtr vca, float value);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_SystemGetOutput",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult SystemGetOutput(int moduleHandle, IntPtr system, out int output);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_SystemGetVersion",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult SystemGetVersion(int moduleHandle, IntPtr system, out uint version);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_SystemSetOutput",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult SystemSetOutput(int moduleHandle, IntPtr system, int output);

        [DllImport("__Internal", EntryPoint = "CelesteFmod_StudioEventDescriptionGetParameterInfoByIndex",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern FmodResult StudioEventDescriptionGetParameterInfoByIndex(int moduleHandle,
            IntPtr description, int index, out FmodHtml5ParameterDescription parameter);
    }
}
