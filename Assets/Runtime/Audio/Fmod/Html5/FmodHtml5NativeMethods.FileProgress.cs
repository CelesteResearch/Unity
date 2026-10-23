using System.Runtime.InteropServices;

namespace Celeste.Unity.Audio.Fmod.Html5
{
    internal static partial class FmodHtml5NativeMethods
    {
        [DllImport("__Internal", EntryPoint = "CelesteFmod_ReadBankFileProgress",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ReadBankFileProgress(int moduleHandle, out double totalBytes,
            out double receivedBytes, out double readyBytes, out int totalFiles, out int readyFiles,
            out double cachedBytes);
    }
}
