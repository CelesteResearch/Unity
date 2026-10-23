namespace Celeste.Unity.Web.Content
{
    public readonly struct WebContentDownloadProgress
    {
        public WebContentDownloadState State { get; }
        public long ReadyBytes { get; }
        public long TotalBytes { get; }
        public long CachedBytes { get; }
        public long ReceivedBytes { get; }
        public long DownloadedBytes { get; }
        public int ReadyFiles { get; }
        public int TotalFiles { get; }
        public string CurrentPath { get; }
        public string Error { get; }

        public WebContentDownloadProgress(WebContentDownloadState state, long readyBytes, long totalBytes,
            long cachedBytes, int readyFiles, int totalFiles, string currentPath, string error,
            long receivedBytes = 0, long downloadedBytes = 0)
        {
            State = state;
            ReadyBytes = readyBytes;
            TotalBytes = totalBytes;
            CachedBytes = cachedBytes;
            ReceivedBytes = receivedBytes;
            DownloadedBytes = downloadedBytes;
            ReadyFiles = readyFiles;
            TotalFiles = totalFiles;
            CurrentPath = currentPath;
            Error = error;
        }
    }
}
