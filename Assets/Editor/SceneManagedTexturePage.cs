using System;
using System.Reflection;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Unity.Graphics.SourceDevice;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    internal sealed class SceneManagedTexturePage
    {
        private static readonly FieldInfo sourceHandle = typeof(SourceTextureResource)
            .GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly SourceTexture2DResource source;
        internal readonly Texture Texture;
        internal bool IsCurrent => !source.IsDisposed && Texture;

        private SceneManagedTexturePage(SourceTexture2DResource source, Texture texture)
        {
            this.source = source;
            Texture = texture;
        }

        internal static bool UsesManagedTextureIO(SourceTexture2DResource source) =>
            source.GraphicsDevice is ISourceTextureDevice device &&
            device.TextureIO is UnitySourceTextureSubmissionIO;

        internal static bool TryCreate(SourceTexture2DResource source, out SceneManagedTexturePage page)
        {
            page = null;
            if (source == null || source.IsDisposed || source.Format != SourceSurfaceFormat.Color ||
                source is SourceRenderTarget2DResource target && target.MultiSampleCount != 0 ||
                sourceHandle == null || source.GraphicsDevice is not ISourceTextureDevice device ||
                device.TextureIO is not UnitySourceTextureSubmissionIO io) return false;
            var texture = io.BorrowTexture((IntPtr)sourceHandle.GetValue(source));
            if (!texture) return false;
            page = new SceneManagedTexturePage(source, texture);
            return true;
        }
    }
}
