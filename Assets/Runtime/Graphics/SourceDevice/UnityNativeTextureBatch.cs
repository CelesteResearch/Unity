using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Graphics.Rendering.Devices.Native.Submission;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Graphics.Rendering.Devices.Native;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice
{
    // Resolve all pages, Commit once, then issue Unity atlas copies and Scene draws.
    public sealed class UnityNativeTextureBatch : INativeTextureExportBatch, IDisposable
    {
        private sealed class Mirror
        {
            internal SourceTexture2DResource Source;
            internal IntPtr Handle, Cache;
            internal Texture2D Texture;
            internal long Revision = long.MinValue, Capture = long.MinValue;
            internal long PendingRevision, PendingCapture;
            internal bool Pending;
        }

        private static readonly FieldInfo sourceHandle = typeof(SourceTextureResource)
            .GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IUnityNativeTextureExportHost host;
        private readonly NativeGraphicsDeviceIO device;
        private readonly ulong generation;
        private readonly Dictionary<SourceTexture2DResource, Mirror> mirrors = new();
        private readonly List<Mirror> pending = new();
        private readonly List<SourceTexture2DResource> retiredSources = new();
        private IntPtr[] handles = Array.Empty<IntPtr>();
        private IntPtr[] caches = Array.Empty<IntPtr>();
        private IntPtr[] loans = Array.Empty<IntPtr>();
        private bool disposed;

        internal UnityNativeTextureBatch(IUnityNativeTextureExportHost host, NativeGraphicsDeviceIO device,
            ulong generation)
        {
            this.host = host;
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.generation = generation;
        }

        public bool IsCurrent => !disposed && host.IsTextureGenerationCurrent(generation);

        public Texture2D Prepare(SourceTexture2DResource source, long capture)
        {
            RequireCurrent();
            if (source == null || source.IsDisposed || source.Format != SourceSurfaceFormat.Color ||
                source.GraphicsDevice is not ISourceTextureDevice owner || !ReferenceEquals(owner.TextureIO, device))
                throw new ArgumentException("The Scene texture must belong to this native graphics device.",
                    nameof(source));
            if (!mirrors.TryGetValue(source, out var mirror))
            {
                if (sourceHandle == null)
                    throw new MissingFieldException(typeof(SourceTextureResource).FullName, "texture");
                var texture = new Texture2D(source.Width, source.Height, TextureFormat.RGBA32, false, true)
                {
                    name = "Scene native texture", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
                try
                {
                    texture.Apply(false, true);
                    mirror = new Mirror
                    {
                        Source = source, Handle = (IntPtr)sourceHandle.GetValue(source), Texture = texture,
                        Cache = host.CreateTextureOutput(texture)
                    };
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw;
                }

                mirrors.Add(source, mirror);
            }

            bool changed = source.IsRenderTargetResource
                ? mirror.Capture != capture
                : mirror.Revision != source.ContentRevision;
            if (changed && !mirror.Pending)
            {
                mirror.Pending = true;
                mirror.PendingRevision = source.ContentRevision;
                mirror.PendingCapture = capture;
                pending.Add(mirror);
            }

            return mirror.Texture;
        }

        public Texture2D GetPrepared(SourceTexture2DResource source, long capture)
        {
            RequireCurrent();
            if (!mirrors.TryGetValue(source, out var mirror) || source.IsDisposed || mirror.Pending ||
                (source.IsRenderTargetResource ? mirror.Capture != capture : mirror.Revision != source.ContentRevision))
                throw new InvalidOperationException("Prepare and commit the Scene texture before drawing it.");
            return mirror.Texture;
        }

        public void Commit()
        {
            RequireCurrent();
            RetireDisposedSources();
            int count = pending.Count;
            if (count == 0) return;
            if (handles.Length < count)
            {
                int capacity = Math.Max(count, handles.Length * 2);
                Array.Resize(ref handles, capacity);
                Array.Resize(ref caches, capacity);
                Array.Resize(ref loans, capacity);
            }

            int acquired = 0;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var mirror = pending[i];
                    if (mirror.Source.IsDisposed || mirror.Source.ContentRevision != mirror.PendingRevision)
                        throw new InvalidOperationException(
                            "The Scene texture changed while preparing its GPU mirror.");
                    handles[i] = mirror.Handle;
                    caches[i] = mirror.Cache;
                    loans[i] = host.AcquireTextureOutputLoan(mirror.Cache);
                    acquired++;
                }

                device.ExportTextures(handles.AsSpan(0, count), loans.AsSpan(0, count), this);
                foreach (var mirror in pending)
                {
                    mirror.Revision = mirror.PendingRevision;
                    mirror.Capture = mirror.PendingCapture;
                }
            }
            finally
            {
                for (int i = 0; i < acquired; i++) device.ReleaseTextureExportOutput(loans[i]);
                foreach (var mirror in pending) mirror.Pending = false;
                pending.Clear();
            }
        }

        void INativeTextureExportBatch.Submit(IntPtr packet) => host.SubmitTextureBatch(packet, caches, pending.Count);

        private void RetireDisposedSources()
        {
            retiredSources.Clear();
            foreach (var pair in mirrors)
                if (pair.Key.IsDisposed)
                    retiredSources.Add(pair.Key);
            if (retiredSources.Count == 0) return;
            // One drain for the whole retirement group, never one wait per texture.
            // Leave all ownership untouched if the host cannot retire in-flight use.
            host.DrainTextureOutputs(generation);
            foreach (var source in retiredSources)
            {
                var mirror = mirrors[source];
                if (mirror.Pending) pending.Remove(mirror);
                host.ReleaseTextureOutputCache(mirror.Cache);
                UnityEngine.Object.DestroyImmediate(mirror.Texture);
                mirrors.Remove(source);
            }

            retiredSources.Clear();
        }

        private void RequireCurrent()
        {
            if (!IsCurrent)
                throw new InvalidOperationException("The Scene native texture batch belongs to a retired session.");
        }

        public void Dispose()
        {
            if (disposed) return;
            host.DrainTextureOutputs(generation);
            foreach (var mirror in mirrors.Values)
            {
                host.ReleaseTextureOutputCache(mirror.Cache);
                UnityEngine.Object.DestroyImmediate(mirror.Texture);
            }

            mirrors.Clear();
            retiredSources.Clear();
            pending.Clear();
            disposed = true;
        }
    }
}
