using System;
using System.Collections.Generic;

namespace Celeste.Unity.Web.Content
{
    internal sealed class WebDownloadEstimate
    {
        private readonly Queue<(double Time, long Received, long Downloaded)> samples = new();
        private double lastReceivedAt, lastDownloadedAt;
        private double lastSampleAt = double.NegativeInfinity;
        private long previousReceived, previousDownloaded;
        private WebContentDownloadState previousState;

        internal double BytesPerSecond { get; private set; }
        internal double RemainingSeconds { get; private set; } = -1;

        internal void Update(double now, WebContentDownloadProgress value)
        {
            if (value.State != WebContentDownloadState.Running || previousState != value.State ||
                value.ReceivedBytes < previousReceived || value.DownloadedBytes < previousDownloaded)
            {
                samples.Clear();
                lastSampleAt = double.NegativeInfinity;
                lastReceivedAt = lastDownloadedAt = now;
            }

            if (value.ReceivedBytes > previousReceived) lastReceivedAt = now;
            if (value.DownloadedBytes > previousDownloaded) lastDownloadedAt = now;
            previousReceived = value.ReceivedBytes;
            previousDownloaded = value.DownloadedBytes;
            previousState = value.State;
            BytesPerSecond = 0;
            RemainingSeconds = -1;
            if (value.State != WebContentDownloadState.Running) return;
            if (now - lastSampleAt >= .25)
            {
                samples.Enqueue((now, value.ReceivedBytes, value.DownloadedBytes));
                lastSampleAt = now;
            }

            while (samples.Count > 1 && now - samples.Peek().Time > 8) samples.Dequeue();
            if (samples.Count == 0) return;
            var first = samples.Peek();
            double elapsed = now - first.Time;
            if (elapsed < 1) return;
            if (now - lastDownloadedAt < 4)
                BytesPerSecond = Math.Max(0, value.DownloadedBytes - first.Downloaded) / elapsed;
            double receivedRate = Math.Max(0, value.ReceivedBytes - first.Received) / elapsed;
            if (elapsed >= 2 && now - lastReceivedAt < 4 && receivedRate > 0 && value.TotalBytes > value.ReceivedBytes)
                RemainingSeconds = (value.TotalBytes - value.ReceivedBytes) / receivedRate;
        }
    }
}
