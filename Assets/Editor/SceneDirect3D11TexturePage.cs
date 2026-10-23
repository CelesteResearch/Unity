using Celeste.Unity.Graphics.SourceDevice;
using System;
using System.Reflection;
using Celeste.Graphics.Direct3D11.Devices;
using Celeste.Graphics.Direct3D11.Submission;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Editor
{
    internal sealed class SceneDirect3D11TexturePage : IDisposable
    {
        private static readonly FieldInfo sourceHandle = typeof(SourceTextureResource)
            .GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly SourceTexture2DResource source;
        private readonly ulong generation;
        private IntPtr loan;
        internal readonly Texture2D Texture;

        internal bool IsCurrent =>
            !source.IsDisposed && UnityDirect3D11Bindings.SourceGpuUnityDeviceGeneration() == generation;

        private SceneDirect3D11TexturePage(SourceTexture2DResource source, ulong generation,
            IntPtr loan, Texture2D texture)
        {
            this.source = source;
            this.generation = generation;
            this.loan = loan;
            Texture = texture;
        }

        internal static bool UsesNativeTextureIO(SourceTexture2DResource source) =>
            source.GraphicsDevice is ISourceTextureDevice device && device.TextureIO is Direct3D11GraphicsDeviceIO;

        internal static bool TryCreate(SourceTexture2DResource source, out SceneDirect3D11TexturePage page)
        {
            page = null;
            if (source == null || source.IsDisposed || source.Format != SourceSurfaceFormat.Color ||
                source is SourceRenderTarget2DResource target && target.MultiSampleCount != 0 ||
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11 ||
                GraphicsSettings.currentRenderPipeline != null || sourceHandle == null ||
                !UsesNativeTextureIO(source)) return false;
            // The opaque source handle is inspected only once when creating this page.
            var handle = (IntPtr)sourceHandle.GetValue(source);
            ulong generation = UnityDirect3D11Bindings.SourceGpuUnityDeviceGeneration();
            var loan = UnityDirect3D11Bindings.SourceGpuCreateUnityTextureViewLoan(handle, generation,
                (uint)source.Width, (uint)source.Height, out var view);
            if (loan == IntPtr.Zero) return false;
            Texture2D texture = null;
            try
            {
                texture = Texture2D.CreateExternalTexture(source.Width, source.Height, TextureFormat.RGBA32,
                    source.LevelCount > 1, true, view);
                if (!texture) throw new InvalidOperationException("Unity did not wrap the actual source texture view.");
                texture.name = "Scene source texture";
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                page = new SceneDirect3D11TexturePage(source, generation, loan, texture);
                return true;
            }
            catch
            {
                if (texture) UnityEngine.Object.DestroyImmediate(texture);
                UnityDirect3D11Bindings.SourceGpuReleaseUnityTextureViewLoan(loan);
                throw;
            }
        }

        public void Dispose()
        {
            if (loan == IntPtr.Zero) return;
            UnityEngine.Object.DestroyImmediate(Texture);
            using var commands = new CommandBuffer { name = "Scene source texture lease release" };
            commands.IssuePluginEventAndData(UnityDirect3D11Bindings.SourceGpuGetUnityTextureViewReleaseEvent(), 0,
                loan);
            // Execute may enqueue before throwing; release ownership cannot return to this page.
            loan = IntPtr.Zero;
            UnityEngine.Graphics.ExecuteCommandBuffer(commands);
        }
    }
}
