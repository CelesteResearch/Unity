using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Diagnostics.Presentation;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Graphics.Rendering.Devices.Native;
using Celeste.Graphics.Scene.Diagnostics;
using Celeste.Scene.Entities;
using Celeste.Scene.Scenes;
using Celeste.Unity.Graphics.SourceDevice;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBounds = Celeste.Core.Geometry.Bounds;

namespace Celeste.Unity.Editor
{
    internal sealed partial class GameSceneRenderer
    {
        private readonly Func<NativeGraphicsDeviceIO, UnityNativeTextureBatch> textureBatchFactory;
        private UnityNativeTextureBatch nativeTextures;
        private NativeGraphicsDeviceIO nativeTextureDevice;
        private long nativeCapture;

        internal static bool UsesNativeTextureExport(SourceTexture2DResource source)
            => source?.GraphicsDevice is ISourceTextureDevice owner && owner.TextureIO is NativeGraphicsDeviceIO;

        private void PrepareNativeTexture(SourceTexture2DResource source)
        {
            if (source == null || source.IsDisposed || source.Format != SourceSurfaceFormat.Color ||
                !UsesNativeTextureExport(source)) return;
            var owner = (NativeGraphicsDeviceIO)((ISourceTextureDevice)source.GraphicsDevice).TextureIO;
            if (nativeTextures == null)
            {
                nativeTextures = textureBatchFactory(owner) ??
                                 throw new InvalidOperationException("The native Scene texture host is unavailable.");
                nativeTextureDevice = owner;
            }

            if (!ReferenceEquals(nativeTextureDevice, owner))
                throw new InvalidOperationException("Scene textures belong to different native graphics devices.");
            nativeTextures.Prepare(source, nativeCapture);
        }

        private void PrepareNativeTextures(SourceSceneState current, SourceSpritePresentationCapture capture,
            SceneTileScope scope, ReadOnlySpan<IntRect> rooms, in WorldBounds worldView)
        {
            if (textureBatchFactory == null || batch?.GraphicsDevice is not ISourceTextureDevice sceneDevice ||
                sceneDevice.TextureIO is not NativeGraphicsDeviceIO) return;
            if (nativeTextures != null && !nativeTextures.IsCurrent)
            {
                nativeTextures.Dispose();
                nativeTextures = null;
                nativeTextureDevice = null;
                removedPages.Clear();
                foreach (var pair in pages)
                    if (pair.Value.Mirrored)
                        removedPages.Add(pair.Key);
                foreach (var source in removedPages)
                {
                    Release(pages[source]);
                    pages.Remove(source);
                }
            }

            nativeCapture = HasPresentation ? capture.FrameSequence : generation;
            if (HasPresentation)
            {
                foreach (ref readonly var record in capture.CompletedQuads) PrepareNativeTexture(record.Texture);
                PrepareNativeTexture(capture.BackgroundSnapshot.Texture);
                PrepareNativeTexture(capture.GameplayDifference.Before.Texture);
                PrepareNativeTexture(capture.GameplayDifference.After.Texture);
                PrepareNativeTexture(capture.ForegroundPair.Before.Texture);
                PrepareNativeTexture(capture.ForegroundPair.After.Texture);
                foreach (ref readonly var record in capture.CameraLayer.CompletedQuads)
                    PrepareNativeTexture(record.Texture);
            }

            if (canStitchArchive)
                foreach (var layer in archive.Layers)
                    if (layer.RefreshSelection(scope, in worldView))
                        for (int i = 0; i < layer.SelectedCount; i++)
                            PrepareNativeTexture(layer.GetSelected(i).Texture);

            var entities = current.SourceScene.Entities.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
            {
                if (entities[entityIndex] is not SceneEntityState entity || !entity.Visible) continue;
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++)
                {
                    var component = entity.Components[componentIndex];
                    if (component is not TileGrid && component is not AnimatedTiles) continue;
                    var position = entity.Position;
                    if (HasPresentation)
                    {
                        bool found = false;
                        for (int i = 0; i < capture.TileCoverageCount; i++)
                        {
                            var item = capture.GetTileCoverage(i);
                            if (item.Scope.EntityIndex != entityIndex || item.Scope.ComponentIndex != componentIndex)
                                continue;
                            found = component is TileGrid grid && item.Scope.ComponentKind ==
                                    SourceSpritePresentationCapture.TileGridComponent &&
                                    item.TileWidth == grid.TileWidth && item.TileHeight == grid.TileHeight ||
                                    component is AnimatedTiles && item.Scope.ComponentKind ==
                                    SourceSpritePresentationCapture.AnimatedTilesComponent &&
                                    item.TileWidth == 8 && item.TileHeight == 8;
                            position = item.WorldPosition;
                            break;
                        }

                        if (!found) continue;
                    }

                    if (component is TileGrid tile && tile.Visible && tile.Alpha > 0f)
                    {
                        if (!HasPresentation) position += tile.Position;
                        if (!grids.TryGetValue(tile, out var cache)) grids.Add(tile, cache = new GridCache());
                        var map = tile.Tiles;
                        var extent = new WorldBounds(position.X, position.Y, map.Columns * tile.TileWidth,
                            map.Rows * tile.TileHeight);
                        var ranges = SelectSnapshotRanges(cache, scope, in worldView, rooms, in extent, map.Columns,
                            map.Rows, tile.TileWidth, tile.TileHeight);
                        cache.TexturePreparedChanged |= cache.Mesh.Snapshot.Refresh(tile, ranges);
                        cache.TexturePreparedGeneration = generation;
                        for (int i = 0; i < cache.Mesh.Snapshot.Count; i++)
                            PrepareNativeTexture(cache.Mesh.Snapshot[i].Texture);
                    }
                    else if (component is AnimatedTiles animated && animated.Visible && animated.Alpha > 0f)
                    {
                        if (!HasPresentation) position += animated.Position;
                        if (!animatedGrids.TryGetValue(animated, out var cache))
                            animatedGrids.Add(animated, cache = new AnimatedGridCache());
                        var map = animated.Sprites;
                        var extent = new WorldBounds(position.X, position.Y, map.Columns * 8f, map.Rows * 8f);
                        var ranges = SelectSnapshotRanges(cache, scope, in worldView, rooms, in extent, map.Columns,
                            map.Rows, 8, 8);
                        cache.TexturePreparedChanged |= cache.Animation.Refresh(animated, ranges);
                        cache.TexturePreparedGeneration = generation;
                        for (int i = 0; i < cache.Animation.Count; i++)
                            PrepareNativeTexture(cache.Animation[i].Texture);
                    }
                }
            }

            nativeTextures?.Commit();
        }
    }
}
