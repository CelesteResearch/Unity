using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;

namespace Celeste.Unity.Web
{
    public sealed class UnityWebSourceWindowIO : ISourceHostWindowIO, ISourceGraphicsAdapterDisplayIO
    {
        private readonly Camera camera;
        private int observedWidth, observedHeight;
        private bool resizable;
        private bool? requestedFullscreen;

        public UnityWebSourceWindowIO(Camera camera)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            GetDrawableSize(out observedWidth, out observedHeight);
        }

        public IntPtr Handle => IntPtr.Zero;
        public IntRect Bounds => new IntRect(0, 0, camera.pixelWidth, camera.pixelHeight);

        public string DisplayName => @"\\.\DISPLAY1";

        // The browser owns outer-window decoration. Source resize policy remains a request.
        public bool Resizable
        {
            get => resizable;
            set => resizable = value;
        }

        public bool Borderless
        {
            get => true;
            set
            {
                if (!value) LogWarning("Browser canvas has no OS window border.");
            }
        }

        public void SetTitle(string title) => UnityWebBrowserInterop.SetTitle(title);

        public void ApplyResolution(int width, int height, bool fullscreen, string displayName)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (displayName != DisplayName)
                throw new NotSupportedException("The browser canvas is the only source display.");
            // The page owns canvas size; source window scale must not shrink its CSS viewport.
            // Fullscreen is optional; the browser bridge defers entry and handles refusal locally.
            if (requestedFullscreen == fullscreen) return;
            UnityWebBrowserInterop.SetFullscreen(fullscreen);
            requestedFullscreen = fullscreen;
        }

        public void GetWindowSize(out int width, out int height)
        {
            width = camera.pixelWidth;
            height = camera.pixelHeight;
        }

        public void GetDrawableSize(out int width, out int height) => GetWindowSize(out width, out height);
        public void LogWarning(string message) => Debug.LogWarning(message);

        public bool ObserveDrawableSizeChange()
        {
            GetDrawableSize(out int width, out int height);
            if (width == observedWidth && height == observedHeight) return false;
            observedWidth = width;
            observedHeight = height;
            return true;
        }

        public int GetNumVideoDisplays() => 1;

        public int GetNumDisplayModes(int displayIndex)
        {
            CheckDisplay(displayIndex);
            return 1;
        }

        public string GetDisplayName(int displayIndex)
        {
            CheckDisplay(displayIndex);
            return "Unity browser canvas";
        }

        public void GetDisplayMode(int displayIndex, int modeIndex, out SourceNativeDisplayMode mode)
        {
            if (modeIndex != 0) throw new ArgumentOutOfRangeException(nameof(modeIndex));
            GetCurrentDisplayMode(displayIndex, out mode);
        }

        public void GetCurrentDisplayMode(int displayIndex, out SourceNativeDisplayMode mode)
        {
            CheckDisplay(displayIndex);
            mode = new SourceNativeDisplayMode { w = camera.pixelWidth, h = camera.pixelHeight };
        }

        private static void CheckDisplay(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
