#if UNITY_EDITOR
using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;

namespace Celeste.Unity
{
    // A Unity GameView surface has no independently owned OS window.
    public sealed class UnityEditorSourceWindowIO : ISourceHostWindowIO
    {
        private readonly Camera camera;
        private int observedWidth;
        private int observedHeight;
        public IntPtr Handle => IntPtr.Zero;
        public IntRect Bounds => new IntRect(0, 0, camera.pixelWidth, camera.pixelHeight);
        public string DisplayName => "Unity GameView";
        public bool Resizable { get; set; }
        public bool Borderless { get; set; }
        public int RequestedWidth { get; private set; }
        public int RequestedHeight { get; private set; }
        public bool RequestedFullscreen { get; private set; }
        public string Title { get; private set; } = string.Empty;

        public UnityEditorSourceWindowIO(Camera camera)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            RequestedWidth = camera.pixelWidth;
            RequestedHeight = camera.pixelHeight;
            observedWidth = RequestedWidth;
            observedHeight = RequestedHeight;
        }

        public bool ObserveDrawableSizeChange()
        {
            int width = camera.pixelWidth;
            int height = camera.pixelHeight;
            if (width == observedWidth && height == observedHeight) return false;
            observedWidth = width;
            observedHeight = height;
            return true;
        }

        public void SetTitle(string title) => Title = title;

        public void ApplyResolution(int width, int height, bool fullscreen, string displayName)
        {
            RequestedWidth = width;
            RequestedHeight = height;
            RequestedFullscreen = fullscreen;
            // Source backbuffer settings remain effective. The enclosing GameView's
            // drawable dimensions are owned by the Editor and the actual camera.
        }

        public void GetWindowSize(out int width, out int height)
        {
            width = camera.pixelWidth;
            height = camera.pixelHeight;
        }

        public void GetDrawableSize(out int width, out int height) => GetWindowSize(out width, out height);
        public void LogWarning(string message) => Debug.LogWarning(message);
    }
}
#endif
