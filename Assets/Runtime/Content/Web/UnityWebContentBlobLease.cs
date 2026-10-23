#if UNITY_WEBGL && !UNITY_EDITOR
using System;

namespace Celeste.Unity.Content.Web
{
    internal sealed class UnityWebContentBlobLease : IDisposable
    {
        private int request;
        internal string Url { get; }
        internal bool Cached { get; }

        internal UnityWebContentBlobLease(int request, string url, bool cached)
        {
            this.request = request;
            Url = url;
            Cached = cached;
        }

        public void Dispose()
        {
            if (request == 0) return;
            int ownedRequest = request;
            request = 0;
            UnityWebContentCacheInterop.Release(ownedRequest);
        }
    }
}
#endif
