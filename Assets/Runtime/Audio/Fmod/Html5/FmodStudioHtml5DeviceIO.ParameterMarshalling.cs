using System;
using System.Runtime.InteropServices;
using System.Text;
using Celeste.Audio.Fmod.Interop;
using Celeste.Audio.Fmod.Parameters;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    public sealed partial class FmodStudioHtml5DeviceIO
    {
        private FmodResult ConvertParameter(FmodResult result, FmodHtml5ParameterDescription native, bool genericInfo,
            out FmodParameterDescription parameter)
        {
            parameter = default;
            if (result != FmodResult.OK) return result;
            try
            {
                parameter.Name = ReadUtf8(native.Name, genericInfo);
                parameter.Index = native.Index;
                parameter.Minimum = native.Minimum;
                parameter.Maximum = native.Maximum;
                parameter.DefaultValue = native.DefaultValue;
                parameter.Type = native.Type;
                return result;
            }
            finally
            {
                if (native.Name != IntPtr.Zero) FmodHtml5NativeMethods.Free(ModuleHandle, native.Name);
            }
        }

        internal static string ReadUtf8(IntPtr pointer, bool genericInfo)
        {
            if (genericInfo && pointer == IntPtr.Zero) return null;
            var size = 0;
            while (Marshal.ReadByte(pointer, size) != 0)
            {
                size++;
                if (genericInfo && size > 1024 * 1024)
                    throw new InvalidOperationException("FMOD returned an invalid UTF-8 string.");
            }

            if (size == 0) return string.Empty;
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, size);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
