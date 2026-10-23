using Celeste.Unity.Graphics.SourceDevice;
using System;
using System.Collections.Generic;
using Celeste.Graphics.Direct3D11.Submission;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Editor
{
    // GPU-only pages for ordinary point-sampled Scene images. The source pages
    // and their native view loans remain owned by GameSceneRenderer.
    internal sealed class SceneGpuTextureAtlas : IDisposable
    {
        internal readonly struct Slot
        {
            internal readonly Page Page;
            internal readonly int X, Y, Width, Height;

            internal Slot(Page page, int x, int y, int width, int height)
            {
                Page = page;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            internal Vector4 Rect => new(X, Y, Width, Height);
        }

        internal sealed class Page
        {
            internal Texture2D Texture;
            internal Material Material;
            internal int NextX, NextY, RowHeight;
            internal bool IsResident = true;
            internal readonly Dictionary<SourceTexture2DResource, Entry> Copies = new();
        }

        internal sealed class Entry
        {
            internal Slot Slot;
            internal long CopiedRevision = long.MinValue;
        }

        private const int PageSize = 1024;
        private const int MaxSourceSize = 128;
        private readonly Dictionary<SourceTexture2DResource, Entry> entries = new();
        private readonly List<Page> pages = new();
        private ulong generation;
        private ISourceTextureIO textureOwner;

        internal ulong DeviceGeneration => generation;

        internal bool TryPrepare(SourceTexture2DResource source,
            Func<SourceTexture2DResource, Texture2D> resolveNative, Shader shader, out Slot slot,
            Page preferredPage = null)
            => TryPrepareCore(source, resolveNative, shader, out slot, preferredPage, false);

        internal bool TryRefreshExactSlot(SourceTexture2DResource source,
            Func<SourceTexture2DResource, Texture2D> resolveNative, Shader shader, in Slot expected)
            => TryPrepareCore(source, resolveNative, shader, out var current, expected.Page, true) &&
               ReferenceEquals(current.Page, expected.Page) && current.X == expected.X && current.Y == expected.Y &&
               current.Width == expected.Width && current.Height == expected.Height;

        private bool TryPrepareCore(SourceTexture2DResource source,
            Func<SourceTexture2DResource, Texture2D> resolveNative, Shader shader, out Slot slot,
            Page preferredPage, bool exactPage)
        {
            slot = default;
            if (source == null || source.IsDisposed || source.IsRenderTargetResource ||
                source.Format != SourceSurfaceFormat.Color || source.LevelCount != 1 ||
                source.Width <= 0 || source.Height <= 0 ||
                source.Width > MaxSourceSize || source.Height > MaxSourceSize ||
                (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0 ||
                (!SceneDirect3D11TexturePage.UsesNativeTextureIO(source) &&
                 !SceneManagedTexturePage.UsesManagedTextureIO(source) &&
                 !GameSceneRenderer.UsesNativeTextureExport(source)) || !shader)
                return false;

            var owner = ((ISourceTextureDevice)source.GraphicsDevice).TextureIO;
            ulong currentGeneration = SceneDirect3D11TexturePage.UsesNativeTextureIO(source)
                ? UnityDirect3D11Bindings.SourceGpuUnityDeviceGeneration()
                : 0;
            if (!ReferenceEquals(textureOwner, owner) || generation != currentGeneration)
            {
                ReleasePages();
                entries.Clear();
                generation = currentGeneration;
                textureOwner = owner;
            }

            // Resolve the current view before a copy. Its loan is held by the renderer
            // through this draw, including when the source has just changed device.
            var sourceTexture = resolveNative(source);
            if (!sourceTexture) return false;
            if (!entries.TryGetValue(source, out var entry))
            {
                if (exactPage) return false;
                Page page = null;
                int x = 0, y = 0, height = 0;
                foreach (var candidate in pages)
                {
                    x = candidate.NextX;
                    y = candidate.NextY;
                    height = candidate.RowHeight;
                    if (x + source.Width > PageSize)
                    {
                        x = 0;
                        y += height;
                        height = 0;
                    }

                    if (y + source.Height > PageSize) continue;
                    page = candidate;
                    break;
                }

                if (page == null)
                {
                    if (!CreatePage(shader, out page)) return false;
                    pages.Add(page);
                    x = y = height = 0;
                }

                entry = new Entry { Slot = new Slot(page, x, y, source.Width, source.Height) };
                entries.Add(source, entry);
                page.NextX = x + source.Width;
                page.NextY = y;
                page.RowHeight = Math.Max(height, source.Height);
            }

            if (preferredPage != null && !ReferenceEquals(entry.Slot.Page, preferredPage))
            {
                if (preferredPage.IsResident)
                {
                    if (!preferredPage.Copies.TryGetValue(source, out var copy) && !exactPage)
                    {
                        int x = preferredPage.NextX, y = preferredPage.NextY, height = preferredPage.RowHeight;
                        if (x + source.Width > PageSize)
                        {
                            x = 0;
                            y += height;
                            height = 0;
                        }

                        if (y + source.Height <= PageSize)
                        {
                            copy = new Entry { Slot = new Slot(preferredPage, x, y, source.Width, source.Height) };
                            preferredPage.Copies.Add(source, copy);
                            preferredPage.NextX = x + source.Width;
                            preferredPage.NextY = y;
                            preferredPage.RowHeight = Math.Max(height, source.Height);
                        }
                    }

                    if (copy != null && TryCopy(source, sourceTexture, shader, copy, currentGeneration))
                    {
                        slot = copy.Slot;
                        return true;
                    }
                }

                if (exactPage) return false;
            }

            if (!TryCopy(source, sourceTexture, shader, entry, currentGeneration)) return false;
            slot = entry.Slot;
            return true;
        }

        private bool TryCopy(SourceTexture2DResource source, Texture2D sourceTexture, Shader shader,
            Entry entry, ulong currentGeneration)
        {
            var entryPage = entry.Slot.Page;
            if (!entryPage.IsResident) return false;
            if (!entryPage.Texture)
            {
                if (!CreatePage(shader, out var replacement)) return false;
                if (entryPage.Material) UnityEngine.Object.DestroyImmediate(entryPage.Material);
                entryPage.Texture = replacement.Texture;
                entryPage.Material = replacement.Material;
                foreach (var value in entries.Values)
                    if (ReferenceEquals(value.Slot.Page, entryPage))
                        value.CopiedRevision = long.MinValue;
                foreach (var value in entryPage.Copies.Values) value.CopiedRevision = long.MinValue;
            }

            long before = source.ContentRevision;
            if (entry.CopiedRevision != before)
            {
                try
                {
                    UnityEngine.Graphics.CopyTexture(sourceTexture, 0, 0, 0, 0,
                        source.Width, source.Height, entry.Slot.Page.Texture, 0, 0, entry.Slot.X, entry.Slot.Y);
                }
                catch (Exception exception) when (exception is UnityException || exception is ArgumentException)
                {
                    return false;
                }

                if (source.IsDisposed ||
                    !ReferenceEquals(textureOwner, ((ISourceTextureDevice)source.GraphicsDevice).TextureIO) ||
                    SceneDirect3D11TexturePage.UsesNativeTextureIO(source) &&
                    UnityDirect3D11Bindings.SourceGpuUnityDeviceGeneration() != currentGeneration ||
                    source.ContentRevision != before)
                    return false;
                entry.CopiedRevision = before;
            }

            return true;
        }

        private bool CreatePage(Shader shader, out Page page)
        {
            page = null;
            Texture2D nextTexture = null;
            Material nextMaterial = null;
            try
            {
                nextTexture = new Texture2D(PageSize, PageSize, TextureFormat.RGBA32, false, true)
                {
                    name = "Scene point texture atlas",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                // Discard the initial CPU backing before any GPU copies. Never Apply after copying.
                nextTexture.Apply(false, true);
                nextMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = nextTexture
                };
                nextMaterial.SetFloat("_SourceTextureOriginTop", 1f);
                nextMaterial.SetFloat("_ScenePointAtlas", 1f);
                page = new Page { Texture = nextTexture, Material = nextMaterial };
                return true;
            }
            catch (Exception exception) when (exception is UnityException || exception is ArgumentException)
            {
                if (nextMaterial) UnityEngine.Object.DestroyImmediate(nextMaterial);
                if (nextTexture) UnityEngine.Object.DestroyImmediate(nextTexture);
                page = null;
                return false;
            }
            catch
            {
                if (nextMaterial) UnityEngine.Object.DestroyImmediate(nextMaterial);
                if (nextTexture) UnityEngine.Object.DestroyImmediate(nextTexture);
                page = null;
                throw;
            }
        }

        private void ReleasePages()
        {
            foreach (var page in pages)
            {
                if (page.Material) UnityEngine.Object.DestroyImmediate(page.Material);
                if (page.Texture) UnityEngine.Object.DestroyImmediate(page.Texture);
                page.Material = null;
                page.Texture = null;
                page.IsResident = false;
                page.Copies.Clear();
            }

            pages.Clear();
        }

        public void Dispose()
        {
            ReleasePages();
            entries.Clear();
            generation = 0;
        }
    }
}
