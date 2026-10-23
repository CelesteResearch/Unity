using System;
using System.Threading;
using Celeste.Audio.Fmod.Capture;
using Celeste.Audio.Fmod.IO;
using Celeste.Audio.Fmod.Interop;

// ReSharper disable once ConvertToFileScopedNamespace
namespace Celeste.Unity.Audio.Fmod.Html5
{
    public sealed partial class FmodStudioHtml5DeviceIO : IFmodStudioDeviceIO, IDisposable
    {
        private int moduleHandle;
        private readonly int ownerThreadId;

        private FmodStudioHtml5DeviceIO(int moduleHandle)
        {
            if (moduleHandle <= 0) throw new ArgumentOutOfRangeException(nameof(moduleHandle));
            this.moduleHandle = moduleHandle;
            ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private int ModuleHandle => moduleHandle != 0
            ? moduleHandle
            : throw new ObjectDisposedException(nameof(FmodStudioHtml5DeviceIO));

        public uint HeaderVersion => 0x00020315;

        public void ValidateRuntimeArchitecture()
        {
            _ = ModuleHandle;
            if (IntPtr.Size != 4)
                throw new PlatformNotSupportedException("FMOD HTML5 requires the Unity Wasm32 heap ABI.");
        }

        public FmodAudioCapture CreateAudioCapture(IntPtr core, Action<float[], int, int> receive) =>
            throw new NotSupportedException("The desktop FMOD capture device is not available in HTML5.");

        public void ConfigureSourcePlatform(IntPtr core)
        {
            _ = ModuleHandle;
            // HTML5 Studio owns its WebAudio device; it has no SDL registration step.
        }

        public FmodResult
            StudioSystemLoadBankCustom(IntPtr studio, ref FmodBankInfo info, uint flags, out IntPtr bank) =>
            throw new NotSupportedException(
                "Managed file callbacks cannot be passed into the independent FMOD Wasm heap. Use LoadBankMemory.");

        public unsafe FmodResult StudioSystemLoadBankMemory(IntPtr studiosystem, ReadOnlyMemory<byte> buffer,
            out IntPtr bank)
        {
            var module = ModuleHandle;
            using var pinned = buffer.Pin();
            return FmodHtml5NativeMethods.StudioSystemLoadBankMemory(module, studiosystem, (IntPtr)pinned.Pointer,
                buffer.Length, out bank);
        }

        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
                throw new InvalidOperationException("FMOD HTML5 module disposal requires its Unity owner thread.");
            var module = Interlocked.Exchange(ref moduleHandle, 0);
            if (module == 0) return;
            try
            {
                CancelFilePreparations(module);
            }
            finally
            {
                FmodHtml5NativeMethods.DisposeModule(module);
            }
        }
    }
}
