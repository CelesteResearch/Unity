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
        public FmodResult StudioEventDescriptionGetParameter(IntPtr description, byte[] name,
            out FmodParameterDescription parameter)
        {
            var result =
                FmodHtml5NativeMethods.StudioEventDescriptionGetParameter(ModuleHandle, description, name,
                    out var native);
            return ConvertParameter(result, native, false, out parameter);
        }

        public FmodResult StudioEventDescriptionGetParameterByIndex(IntPtr eventdescription, int index,
            out FmodParameterDescription parameter)
        {
            var result =
                FmodHtml5NativeMethods.StudioEventDescriptionGetParameterByIndex(ModuleHandle, eventdescription, index,
                    out var native);
            return ConvertParameter(result, native, false, out parameter);
        }

        public FmodResult StudioEventDescriptionGetParameterInfoByIndex(IntPtr description, int index,
            out FmodParameterDescription parameter)
        {
            var result =
                FmodHtml5NativeMethods.StudioEventDescriptionGetParameterInfoByIndex(ModuleHandle, description, index,
                    out var native);
            return ConvertParameter(result, native, true, out parameter);
        }
    }
}
