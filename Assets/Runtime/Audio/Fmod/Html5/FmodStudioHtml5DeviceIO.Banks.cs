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
        public FmodResult StudioBankGetBusCount(IntPtr bank, out int count) =>
            FmodHtml5NativeMethods.StudioBankGetBusCount(ModuleHandle, bank, out count);

        public FmodResult StudioBankGetBusList(IntPtr bank, IntPtr[] array, int capacity, out int count) =>
            FmodHtml5NativeMethods.StudioBankGetBusList(ModuleHandle, bank, array, capacity, out count);

        public FmodResult StudioBankGetEventCount(IntPtr bank, out int count) =>
            FmodHtml5NativeMethods.StudioBankGetEventCount(ModuleHandle, bank, out count);

        public FmodResult StudioBankGetEventList(IntPtr bank, IntPtr[] array, int capacity, out int count) =>
            FmodHtml5NativeMethods.StudioBankGetEventList(ModuleHandle, bank, array, capacity, out count);

        public FmodResult StudioBankGetID(IntPtr bank, out Guid id) =>
            FmodHtml5NativeMethods.StudioBankGetID(ModuleHandle, bank, out id);

        public FmodResult StudioBankGetPath(IntPtr bank, byte[] path, int size, out int retrieved) =>
            FmodHtml5NativeMethods.StudioBankGetPath(ModuleHandle, bank, path, size, out retrieved);

        public FmodResult StudioBankGetVCACount(IntPtr bank, out int count) =>
            FmodHtml5NativeMethods.StudioBankGetVCACount(ModuleHandle, bank, out count);

        public FmodResult StudioBankGetVCAList(IntPtr bank, IntPtr[] array, int capacity, out int count) =>
            FmodHtml5NativeMethods.StudioBankGetVCAList(ModuleHandle, bank, array, capacity, out count);

        public FmodResult StudioBankLoadSampleData(IntPtr bank) =>
            FmodHtml5NativeMethods.StudioBankLoadSampleData(ModuleHandle, bank);

        public FmodResult StudioBankUnload(IntPtr bank) =>
            FmodHtml5NativeMethods.StudioBankUnload(ModuleHandle, bank);
    }
}
