using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content.Manifests;

namespace Celeste.Unity.Audio.Fmod.Html5
{
    // Borrows the live module. The module owner retains prepared files through game disposal.
    public sealed class FmodBankFilePreparation : IDisposable
    {
        private readonly FmodStudioHtml5DeviceIO device;
        private readonly string virtualTitleRoot;
        private readonly Func<ContentFileInfo, Uri> resourceUri;
        private readonly HashSet<string> preparedFiles = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource lifetime = new();
        private bool disposed;

        public FmodBankFilePreparation(FmodStudioHtml5DeviceIO device, string virtualTitleRoot,
            Func<ContentFileInfo, Uri> resourceUri)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            if (string.IsNullOrEmpty(virtualTitleRoot) || !Path.IsPathRooted(virtualTitleRoot))
                throw new ArgumentException("An absolute virtual title root is required.", nameof(virtualTitleRoot));
            this.virtualTitleRoot = virtualTitleRoot;
            this.resourceUri = resourceUri ?? throw new ArgumentNullException(nameof(resourceUri));
        }

        public bool IsReady(string logicalPath) => !disposed && preparedFiles.Contains(logicalPath);

        public async Task PrepareFileAsync(ContentFileInfo file, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FmodBankFilePreparation));
            if (file == null) throw new ArgumentNullException(nameof(file));
            cancellationToken.ThrowIfCancellationRequested();
            if (preparedFiles.Contains(file.Path)) return;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            await device.PrepareBankFileAsync(Path.Combine(virtualTitleRoot, file.Path).Replace('\\', '/'),
                resourceUri(file).AbsoluteUri, file.Size, file.Sha256, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            preparedFiles.Add(file.Path);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            preparedFiles.Clear();
            try
            {
                lifetime.Cancel();
            }
            finally
            {
                lifetime.Dispose();
            }
        }
    }
}
