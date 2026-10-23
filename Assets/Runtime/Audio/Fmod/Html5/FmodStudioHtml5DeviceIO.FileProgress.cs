using Celeste.Core.IO.Content;

namespace Celeste.Unity.Audio.Fmod.Html5
{
    public sealed partial class FmodStudioHtml5DeviceIO
    {
        public ContentLoadProgress ReadBankFileProgress()
        {
            FmodHtml5NativeMethods.ReadBankFileProgress(ModuleHandle, out var totalBytes, out var receivedBytes,
                out var readyBytes, out var totalFiles, out var readyFiles, out var cachedBytes);
            return new ContentLoadProgress((long)totalBytes, (long)receivedBytes, (long)readyBytes,
                totalFiles, readyFiles, (long)cachedBytes);
        }
    }
}
