#if UNITY_EDITOR
using System;
using Celeste.Graphics.Rendering.Devices;
using UnityEngine;

namespace Celeste.Unity
{
    // The Editor's display is its GameView, not an independently switchable OS monitor.
    public sealed class UnityEditorSourceDisplayIO : ISourceGraphicsAdapterDisplayIO
    {
        private readonly Camera camera;

        public UnityEditorSourceDisplayIO(Camera camera)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public int GetNumVideoDisplays() => 1;

        public int GetNumDisplayModes(int displayIndex)
        {
            if (displayIndex != 0) throw new ArgumentOutOfRangeException(nameof(displayIndex));
            return 1;
        }

        public void GetDisplayMode(int displayIndex, int modeIndex, out SourceNativeDisplayMode mode)
        {
            if (modeIndex != 0) throw new ArgumentOutOfRangeException(nameof(modeIndex));
            GetCurrentDisplayMode(displayIndex, out mode);
        }

        public void GetCurrentDisplayMode(int displayIndex, out SourceNativeDisplayMode mode)
        {
            if (displayIndex != 0) throw new ArgumentOutOfRangeException(nameof(displayIndex));
            mode = new SourceNativeDisplayMode { w = camera.pixelWidth, h = camera.pixelHeight };
        }

        public string GetDisplayName(int displayIndex)
        {
            if (displayIndex != 0) throw new ArgumentOutOfRangeException(nameof(displayIndex));
            return "Unity GameView";
        }
    }
}
#endif
