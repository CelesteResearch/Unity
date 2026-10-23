using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content;

namespace Celeste.Unity.Web
{
    internal static class UnityWebBrowserInterop
    {
        private delegate void Completion(int request, int error);

        private static readonly Completion Completed = OnCompleted;
        private static TaskCompletionSource<bool> pending;
        private static int sequence;
        private static readonly SemaphoreSlim SyncLock = new SemaphoreSlim(1, 1);

        private static TaskCompletionSource<int> fileSelection;
        private static readonly Completion FileSelected = OnFileSelected;

        public static async Task<bool> SelectSaveFileAsync(string path)
        {
            if (fileSelection != null) throw new InvalidOperationException("A file picker is already open.");
            fileSelection = new TaskCompletionSource<int>();
            try
            {
                CelesteWebSelectSaveFile(path, FileSelected);
                int result = await fileSelection.Task;
                if (result < 0) throw new System.IO.IOException("Unable to read the selected file.");
                return result == 1;
            }
            finally
            {
                fileSelection = null;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Completion))]
        private static void OnFileSelected(int unused, int result) => fileSelection?.TrySetResult(result);

        public static void DownloadSaveFile(string name, byte[] bytes) =>
            CelesteWebDownloadSaveFile(name, bytes, bytes.Length);

        public static bool ConfirmSaveImport(string message) => CelesteWebConfirmSaveImport(message) != 0;
        public static void ReloadPage() => CelesteWebReloadPage();
        public static void ShowSaveError(string message) => CelesteWebShowSaveError(message);
        public static void SetFullscreen(bool enabled) => CelesteWebSetFullscreen(enabled ? 1 : 0);

        [DllImport("__Internal")]
        private static extern void CelesteWebSetFullscreen(int enabled);

        [DllImport("__Internal")]
        private static extern void CelesteWebSelectSaveFile(string path, Completion callback);

        [DllImport("__Internal")]
        private static extern void CelesteWebDownloadSaveFile(string name, byte[] bytes, int count);

        [DllImport("__Internal")]
        private static extern int CelesteWebConfirmSaveImport(string message);

        [DllImport("__Internal")]
        private static extern void CelesteWebReloadPage();

        [DllImport("__Internal")]
        private static extern void CelesteWebShowSaveError(string message);

        public static async Task SyncPersistentFilesAsync(bool populate)
        {
            await SyncLock.WaitAsync();
            try
            {
                if (pending != null)
                    throw new InvalidOperationException("A persistent filesystem request is already pending.");
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending = completion;
                int request = checked(++sequence);
                try
                {
                    CelesteWebSyncPersistentFiles(populate ? 1 : 0, request, Completed);
                    await completion.Task;
                }
                finally
                {
                    pending = null;
                }
            }
            finally
            {
                SyncLock.Release();
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Completion))]
        private static void OnCompleted(int request, int error)
        {
            if (request != sequence || pending == null)
                throw new InvalidOperationException("Unexpected persistent filesystem completion.");
            if (error == 0) pending.TrySetResult(true);
            else
                pending.TrySetException(
                    new System.IO.IOException(
                        "Browser IndexedDB filesystem synchronization failed; see browser console."));
        }

        public static void InstallSteamLink(string url)
        {
            CelesteWebInstallSteamLink(url);
        }

        public static void SetSteamLinkRegion(float left, float top, float width, float height)
        {
            CelesteWebSetSteamLinkRegion(left, top, width, height);
        }

        public static void RemoveSteamLink()
        {
            CelesteWebRemoveSteamLink();
        }

        [DllImport("__Internal")]
        private static extern void CelesteWebInstallSteamLink(string url);

        [DllImport("__Internal")]
        private static extern void CelesteWebSetSteamLinkRegion(float left, float top, float width, float height);

        [DllImport("__Internal")]
        private static extern void CelesteWebRemoveSteamLink();

        public static void ReportContentProgress(ContentLoadProgress progress) =>
            CelesteWebReportContentProgress(progress.TotalBytes, progress.ReceivedBytes, progress.ReadyBytes,
                progress.TotalFiles, progress.ReadyFiles, progress.CachedBytes);

        public static void NotifyGameReady() => CelesteWebNotifyGameReady();

        public static void ReportStartupStage(string stage) => CelesteWebReportStartupStage(stage);

        public static void NotifyGameFailed() => CelesteWebNotifyGameFailed();

        public static void CloseGamePage()
        {
            CelesteWebCloseGamePage();
        }

        public static void SetTitle(string title) =>
            CelesteWebSetTitle(title ?? throw new ArgumentNullException(nameof(title)));

        public static void FillEntropy(Span<byte> destination)
        {
            byte[] bytes = new byte[destination.Length];
            if (CelesteWebFillEntropy(bytes, bytes.Length) != 0)
                throw new PlatformNotSupportedException("Browser crypto.getRandomValues is unavailable.");
            bytes.AsSpan().CopyTo(destination);
        }

        [DllImport("__Internal")]
        private static extern void CelesteWebReportContentProgress(double totalBytes, double receivedBytes,
            double readyBytes, int totalFiles, int readyFiles, double cachedBytes);

        [DllImport("__Internal")]
        private static extern void CelesteWebNotifyGameReady();

        [DllImport("__Internal")]
        private static extern void CelesteWebReportStartupStage(string stage);

        [DllImport("__Internal")]
        private static extern void CelesteWebNotifyGameFailed();

        [DllImport("__Internal")]
        private static extern void CelesteWebCloseGamePage();

        [DllImport("__Internal")]
        private static extern void CelesteWebSyncPersistentFiles(int populate, int request, Completion callback);

        [DllImport("__Internal")]
        private static extern void CelesteWebSetTitle(string title);

        [DllImport("__Internal")]
        private static extern int CelesteWebFillEntropy([Out] byte[] bytes, int count);
    }
}
