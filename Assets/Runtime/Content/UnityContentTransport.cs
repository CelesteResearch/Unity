using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content.Manifests;
using Celeste.Core.IO.Content;
using Celeste.Formats.Content.Manifests;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_WEBGL && !UNITY_EDITOR
using Celeste.Unity.Content.Web;
#endif

namespace Celeste.Unity.Content
{
    public sealed class UnityContentTransport : IDisposable
    {
        private readonly int maximumConcurrentRequests;
        private UnityContentRequestSlots requestSlots;
        private readonly CancellationTokenSource lifetime = new();
        private int activeCalls;
        private int disposalState;
        private int resourcesDisposed;
        private readonly List<(UnityWebRequest Request, ContentFileInfo File)> contentDownloads = new();
        private readonly Dictionary<string, long> receivedContent = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> cachedContent = new(StringComparer.Ordinal);
        private long receivedContentBytes, cachedContentBytes;

        // Payload received from persistent cache; this is not an HTTP transfer counter.
        public long CachedContentBytes => cachedContentBytes;

        public Uri BaseContentUri { get; }

        // Sample on Unity's owner loop; never copies a download buffer or adds a request lock.
        public long ReceivedContentBytes
        {
            get
            {
                foreach (var download in contentDownloads)
                    RecordRequestReceivedBytes(download.Request, download.File);
                return receivedContentBytes;
            }
        }

        private void RecordRequestReceivedBytes(UnityWebRequest request, ContentFileInfo file)
        {
            RecordReceivedBytes(file, (long)Math.Min((ulong)file.Size, request.downloadedBytes));
        }

        private void RecordReceivedBytes(ContentFileInfo file, long receivedBytes)
        {
            long bytes = Math.Max(0, Math.Min(file.Size, receivedBytes));
            if (!receivedContent.TryGetValue(file.Path, out long previous) || bytes > previous)
            {
                receivedContent[file.Path] = bytes;
                receivedContentBytes += bytes - previous;
            }
        }

        private void RecordCachedFile(ContentFileInfo file)
        {
            if (!cachedContent.TryGetValue(file.Path, out long previous) || file.Size > previous)
            {
                cachedContent[file.Path] = file.Size;
                cachedContentBytes += file.Size - previous;
            }
        }

        // Limit concurrent native download buffers; this is a loading policy, not a simulation limit.
        public UnityContentTransport(Uri baseContentUri, int maximumConcurrentRequests = 4)
        {
            ValidateHttpUri(baseContentUri);
            if (!baseContentUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
                || baseContentUri.Query.Length != 0 || baseContentUri.Fragment.Length != 0)
                throw new ArgumentException(
                    "The content base must be an HTTP(S) directory URI without a query or fragment.",
                    nameof(baseContentUri));
            if (maximumConcurrentRequests <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumConcurrentRequests));
            BaseContentUri = baseContentUri;
            this.maximumConcurrentRequests = maximumConcurrentRequests;
        }

        public Task<byte[]> LoadOwnedBytesAsync(ContentFileInfo file, CancellationToken cancellationToken)
            => DownloadAsync(GetContentUri(file), cancellationToken, file);

        public async Task<VerifiedContentBytes> LoadVerifiedOwnedBytesAsync(ContentFileInfo file,
            CancellationToken cancellationToken)
        {
            byte[] bytes = await LoadOwnedBytesAsync(file, cancellationToken);
#if UNITY_WEBGL && !UNITY_EDITOR
            // PrepareAsync verified the complete bytes before publishing their immutable Blob URL.
            return new VerifiedContentBytes(bytes, file.Sha256);
#else
            using var hash = System.Security.Cryptography.SHA256.Create();
            string digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
            return new VerifiedContentBytes(bytes, digest);
#endif
        }

        public Uri GetContentUri(ContentFileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            var relative = new StringBuilder(file.ResourcePath.Length);
            foreach (string segment in file.ResourcePath.Split('/'))
            {
                if (relative.Length != 0)
                    relative.Append('/');
                relative.Append(Uri.EscapeDataString(segment));
            }

            return new Uri(BaseContentUri, relative.ToString());
        }

        public async Task<ContentManifest> ReadManifestAsync(Uri manifestUri,
            CancellationToken cancellationToken = default)
        {
            byte[] bytes = await DownloadAsync(manifestUri, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            using var stream = new MemoryStream(bytes, false);
            ContentManifest manifest = ContentManifestReader.Read(stream);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            return manifest;
        }

        private async Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken,
            ContentFileInfo contentFile = null)
        {
            ValidateHttpUri(uri);
            Interlocked.Increment(ref activeCalls);
            try
            {
                ThrowIfDisposed();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
                CancellationToken token = linked.Token;
                bool entered = false;
                try
                {
                    token.ThrowIfCancellationRequested();
                    await Awaitable.MainThreadAsync();
                    token.ThrowIfCancellationRequested();
                    requestSlots ??= new UnityContentRequestSlots(maximumConcurrentRequests,
                        SynchronizationContext.Current ??
                        throw new InvalidOperationException("Unity content loading requires its owner context."));
                    await requestSlots.WaitAsync(token);
                    entered = true;
                    token.ThrowIfCancellationRequested();
                    UnityWebRequest request = null;
#if UNITY_WEBGL && !UNITY_EDITOR
                    UnityWebContentBlobLease blobLease = null;
#endif
                    try
                    {
                        string requestUrl = uri.AbsoluteUri;
#if UNITY_WEBGL && !UNITY_EDITOR
                        if (contentFile != null)
                        {
                            blobLease = await UnityWebContentCacheInterop.PrepareAsync(uri, contentFile,
                                (bytes, cached) => RecordReceivedBytes(contentFile, bytes), token);
                            token.ThrowIfCancellationRequested();
                            requestUrl = blobLease.Url;
                        }
#endif
                        request = UnityWebRequest.Get(requestUrl);
                        if (contentFile != null) contentDownloads.Add((request, contentFile));
                        await Awaitable.FromAsyncOperation(request.SendWebRequest(), token);
                        await Awaitable.MainThreadAsync();
                        token.ThrowIfCancellationRequested();
                        if (request.result != UnityWebRequest.Result.Success)
                            throw new IOException(
                                $"Content GET '{uri.GetLeftPart(UriPartial.Path)}' failed: HTTP {request.responseCode}, {request.result}, {request.error}");
                        // DownloadHandlerBuffer copies native data once; ownership of this array passes to the resident source.
                        byte[] ownedBytes = request.downloadHandler.data ?? Array.Empty<byte>();
#if UNITY_WEBGL && !UNITY_EDITOR
                        if (blobLease != null && blobLease.Cached && ownedBytes.LongLength == contentFile.Size)
                            RecordCachedFile(contentFile);
#endif
                        return ownedBytes;
                    }
                    finally
                    {
                        await Awaitable.MainThreadAsync();
                        try
                        {
                            if (request != null)
                            {
                                try
                                {
                                    if (contentFile != null)
                                    {
                                        RecordRequestReceivedBytes(request, contentFile);
                                        for (int index = contentDownloads.Count - 1; index >= 0; index--)
                                            if (ReferenceEquals(contentDownloads[index].Request, request))
                                            {
                                                contentDownloads.RemoveAt(index);
                                                break;
                                            }
                                    }

                                    if (token.IsCancellationRequested) request.Abort();
                                }
                                finally
                                {
                                    request.Dispose();
                                }
                            }
                        }
                        finally
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                            blobLease?.Dispose();
#endif
                        }
                    }
                }
                finally
                {
                    if (entered)
                        requestSlots.Release();
                }
            }
            finally
            {
                if (Interlocked.Decrement(ref activeCalls) == 0)
                    DisposeResourcesIfIdle();
            }
        }

        private static void ValidateHttpUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("An absolute HTTP(S) URI is required.", nameof(uri));
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposalState) != 0)
                throw new ObjectDisposedException(nameof(UnityContentTransport));
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposalState, 1, 0) != 0)
                return;
            try
            {
                lifetime.Cancel();
            }
            finally
            {
                Volatile.Write(ref disposalState, 2);
                DisposeResourcesIfIdle();
            }
        }

        private void DisposeResourcesIfIdle()
        {
            if (Volatile.Read(ref disposalState) != 2 || Volatile.Read(ref activeCalls) != 0
                                                      || Interlocked.Exchange(ref resourcesDisposed, 1) != 0)
                return;
            requestSlots?.Dispose();
            lifetime.Dispose();
        }
    }
}
