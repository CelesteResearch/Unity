using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Celeste.Unity.Web.Content
{
    public sealed class UnityWebContentDownloadController : IDisposable
    {
        private delegate void ProgressCallback(int id, int state, double readyBytes, double totalBytes,
            double cachedBytes, double receivedBytes, double downloadedBytes, int readyFiles, int totalFiles,
            IntPtr currentPath, IntPtr error);

        private static readonly Dictionary<int, UnityWebContentDownloadController> Instances = new();
        private static readonly ProgressCallback ProgressChanged = OnProgress;
        private static int sequence;
        private readonly int id;
        private WebContentDownloadProgress progress;
        private double nextPoll;
        private bool disposed;

        public UnityWebContentDownloadController(Uri manifestUri, Uri contentUri)
        {
            id = checked(++sequence);
            Instances.Add(id, this);
            try
            {
                CelesteWebDownloadCreate(id, manifestUri.AbsoluteUri, contentUri.AbsoluteUri);
            }
            catch
            {
                Instances.Remove(id);
                throw;
            }
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityWebContentDownloadController));
            CelesteWebDownloadStart(id);
            nextPoll = 0;
        }

        public void Cancel()
        {
            if (disposed) return;
            CelesteWebDownloadCancel(id);
            nextPoll = 0;
        }

        public WebContentDownloadProgress ReadProgress()
        {
            if (!disposed && Time.realtimeSinceStartupAsDouble >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartupAsDouble + .25;
                CelesteWebDownloadRead(id, ProgressChanged);
            }

            return progress;
        }

        [AOT.MonoPInvokeCallback(typeof(ProgressCallback))]
        private static void OnProgress(int id, int state, double readyBytes, double totalBytes,
            double cachedBytes, double receivedBytes, double downloadedBytes, int readyFiles, int totalFiles,
            IntPtr currentPath, IntPtr error)
        {
            if (!Instances.TryGetValue(id, out var owner)) return;
            string message = Marshal.PtrToStringUTF8(error);
            if (state == (int)WebContentDownloadState.Failed &&
                (owner.progress.State != WebContentDownloadState.Failed || owner.progress.Error != message))
                Debug.LogWarning("Celeste resource download failed: " + message);
            owner.progress = new WebContentDownloadProgress((WebContentDownloadState)state,
                (long)readyBytes, (long)totalBytes, (long)cachedBytes, readyFiles, totalFiles,
                Marshal.PtrToStringUTF8(currentPath), message, (long)receivedBytes, (long)downloadedBytes);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Instances.Remove(id);
            CelesteWebDownloadDispose(id);
        }

        [DllImport("__Internal")]
        private static extern void CelesteWebDownloadCreate(int id, string manifestUrl, string contentUrl);

        [DllImport("__Internal")]
        private static extern void CelesteWebDownloadStart(int id);

        [DllImport("__Internal")]
        private static extern void CelesteWebDownloadCancel(int id);

        [DllImport("__Internal")]
        private static extern void CelesteWebDownloadRead(int id, ProgressCallback callback);

        [DllImport("__Internal")]
        private static extern void CelesteWebDownloadDispose(int id);
    }
}
