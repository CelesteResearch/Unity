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
        public FmodResult StudioEventDescriptionCreateInstance(IntPtr eventdescription, out IntPtr instance) =>
            FmodHtml5NativeMethods.StudioEventDescriptionCreateInstance(ModuleHandle, eventdescription, out instance);

        public FmodResult StudioEventDescriptionGetID(IntPtr eventdescription, out Guid id) =>
            FmodHtml5NativeMethods.StudioEventDescriptionGetID(ModuleHandle, eventdescription, out id);

        public FmodResult StudioEventDescriptionGetInstanceCount(IntPtr eventdescription, out int count) =>
            FmodHtml5NativeMethods.StudioEventDescriptionGetInstanceCount(ModuleHandle, eventdescription, out count);

        public FmodResult StudioEventDescriptionGetLength(IntPtr eventdescription, out int length) =>
            FmodHtml5NativeMethods.StudioEventDescriptionGetLength(ModuleHandle, eventdescription, out length);

        public FmodResult StudioEventDescriptionGetParameterCount(IntPtr eventdescription, out int count) =>
            FmodHtml5NativeMethods.StudioEventDescriptionGetParameterCount(ModuleHandle, eventdescription, out count);

        public FmodResult
            StudioEventDescriptionGetPath(IntPtr eventdescription, byte[] path, int size, out int retrieved) =>
            FmodHtml5NativeMethods.StudioEventDescriptionGetPath(ModuleHandle, eventdescription, path, size,
                out retrieved);

        public FmodResult StudioEventDescriptionIs3D(IntPtr eventdescription, out int is3D) =>
            FmodHtml5NativeMethods.StudioEventDescriptionIs3D(ModuleHandle, eventdescription, out is3D);

        public FmodResult StudioEventDescriptionIsOneshot(IntPtr eventdescription, out int oneshot) =>
            FmodHtml5NativeMethods.StudioEventDescriptionIsOneshot(ModuleHandle, eventdescription, out oneshot);

        public FmodResult StudioEventDescriptionLoadSampleData(IntPtr eventdescription) =>
            FmodHtml5NativeMethods.StudioEventDescriptionLoadSampleData(ModuleHandle, eventdescription);

        public FmodResult StudioEventDescriptionUnloadSampleData(IntPtr description) =>
            FmodHtml5NativeMethods.StudioEventDescriptionUnloadSampleData(ModuleHandle, description);
    }
}
