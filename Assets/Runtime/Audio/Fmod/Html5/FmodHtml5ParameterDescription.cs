using System;
using System.Runtime.InteropServices;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct FmodHtml5ParameterDescription
    {
        public IntPtr Name;
        public int Index;
        public float Minimum;
        public float Maximum;
        public float DefaultValue;
        public int Type;
    }
}
