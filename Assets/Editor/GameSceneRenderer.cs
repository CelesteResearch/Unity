using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Rendering;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Content.Tiles;
using Celeste.Graphics.Display;
using Celeste.Graphics.Diagnostics.Presentation;
using Celeste.Graphics.Rendering.Batches;
using Celeste.Core.Rendering.States;
using Celeste.Graphics.Scene.Diagnostics;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Scene.Entities;
using Celeste.Scene.Scenes;
using Celeste.Unity.Graphics.SourceDevice;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using WorldBounds = Celeste.Core.Geometry.Bounds;

namespace Celeste.Unity.Editor
{
    internal sealed partial class GameSceneRenderer : IDisposable
    {
        private static readonly ProfilerMarker DrawMarker = new("Celeste.Scene.GameSceneRenderer.Draw");
        private static readonly ProfilerMarker PrepareMarker = new("Celeste.Scene.Renderer.Prepare");
        private static readonly ProfilerMarker CaptureQueueMarker = new("Celeste.Scene.Renderer.CaptureQueue");

        private static readonly ProfilerMarker CaptureQueueGeometryMarker =
            new("Celeste.Scene.Renderer.CaptureQueue.PrepareGeometry");

        private static readonly ProfilerMarker CaptureQueueRecordsMarker =
            new("Celeste.Scene.Renderer.CaptureQueue.Records");

        private static readonly ProfilerMarker RemainingTilesMarker = new("Celeste.Scene.Renderer.RemainingTiles");
        private static readonly ProfilerMarker CleanupMarker = new("Celeste.Scene.Renderer.Cleanup");
        private static readonly ProfilerMarker CanStitchArchiveMarker = new("Celeste.Scene.Renderer.CanStitchArchive");
        private static readonly ProfilerMarker ConsumeArchiveMarker = new("Celeste.Scene.Renderer.ConsumeArchive");

        private static readonly ProfilerMarker ConsumeArchivePreflightMarker =
            new("Celeste.Scene.Renderer.ConsumeArchive.Preflight");

        private static readonly ProfilerMarker ConsumeArchiveCommitMarker =
            new("Celeste.Scene.Renderer.ConsumeArchive.Commit");

        private static readonly ProfilerMarker FlushImagesMarker = new("Celeste.Scene.Renderer.FlushImages");
        private static readonly ProfilerMarker ResolveMarker = new("Celeste.Scene.Renderer.Resolve");

        private static readonly ProfilerMarker PrepareTileCoverageOrderMarker =
            new("Celeste.Scene.Renderer.PrepareTileCoverageOrder");

        private static readonly ProfilerMarker PrepareStaticCoverageMarker =
            new("Celeste.Scene.Renderer.PrepareStaticCoverage");

        private static readonly ProfilerMarker DrawBackgroundMarker = new("Celeste.Scene.Renderer.DrawBackground");

        private sealed class ViewState
        {
            internal SceneTileScope Scope = SceneTileScope.VisibleRooms;
        }

        private class GridCache
        {
            internal readonly SceneTileMesh Mesh;

            internal readonly HashSet<SourceTexture2DResource> Sources = new();
            internal byte[] Mask = Array.Empty<byte>(), NextMask = Array.Empty<byte>();
            internal long[] CellIndices = Array.Empty<long>();
            internal int CellCount;
            internal IntRect[] SnapshotRanges = Array.Empty<IntRect>();
            internal WorldBounds LastView, LastGrid;
            internal SceneTileScope LastScope;
            internal bool HasMask;
            internal int LastColumns, LastRows, LastTileWidth, LastTileHeight;
            internal long RoomRevision;
            internal long ArchiveRevision;
            internal double LayerDepth;
            internal int LayerKind;
            internal byte[] ArchiveMask = Array.Empty<byte>();
            internal WorldBounds ArchiveGrid;
            internal int ArchiveColumns, ArchiveRows, ArchiveTileWidth, ArchiveTileHeight, ArchiveKind;
            internal double ArchiveDepth;
            internal bool HasArchiveMask;
            internal long Seen;
            internal long TexturePreparedGeneration = -1;
            internal bool TexturePreparedChanged;

            internal GridCache(bool dynamicGeometry = false)
            {
                Mesh = new SceneTileMesh(dynamicGeometry);
            }
        }

        private sealed class AnimatedGridCache : GridCache
        {
            internal readonly AnimatedTileGeometrySnapshot Animation = new();

            internal AnimatedGridCache() : base(true)
            {
            }
        }

        private sealed class ImageRunCache
        {
            internal readonly SceneTileMesh Mesh;
            internal int PointAddress;
            internal bool RebuildRequired;
            internal bool PointAtlasRetrySpent;
            internal ScenePresentationMeshQuadBuffer Last = new(), Next = new();

            internal ImageRunCache(bool livePointImages = false)
            {
                Mesh = new SceneTileMesh(true, proceduralImages: livePointImages);
            }
        }

        private sealed class Page
        {
            internal Texture Texture;
            internal SceneDirect3D11TexturePage Native;
            internal SceneManagedTexturePage Managed;
            internal bool Mirrored;
            internal Material Material;
        }

        private static readonly ConditionalWeakTable<SceneView, ViewState> views = new();
        private readonly Dictionary<TileGrid, GridCache> grids = new();
        private readonly Dictionary<AnimatedTiles, AnimatedGridCache> animatedGrids = new();
        private readonly List<AnimatedTiles> removedAnimations = new();
        private readonly Dictionary<SourceTexture2DResource, Page> pages = new();
        private readonly SceneGpuTextureAtlas pointAtlas = new();
        private readonly HashSet<SourceTexture2DResource> livePages = new();
        private readonly SceneTileMesh backgroundMesh = new();
        private readonly ScenePresentationMeshQuad[] backgroundQuad = new ScenePresentationMeshQuad[1];
        private SourceTexture2DResource backgroundTexture;
        private readonly SceneTileMesh differenceMesh = new();
        private readonly ScenePresentationMeshQuad[] differenceQuad = new ScenePresentationMeshQuad[1];
        private SourceTexture2DResource differenceTexture;
        private Material differenceMaterial;
        private readonly Func<SourceTexture2DResource, Material> resolveDifference;
        private static readonly int SceneBeforeTexture = Shader.PropertyToID("_SceneBeforeTex");
        private readonly SceneTileMesh foregroundMesh = new();
        private readonly ScenePresentationMeshQuad[] foregroundQuad = new ScenePresentationMeshQuad[1];
        private SourceTexture2DResource foregroundTexture;
        private Material foregroundMaterial;
        private readonly Func<SourceTexture2DResource, Material> resolveForeground;
        private readonly List<TileGrid> removedGrids = new();
        private readonly List<SourceTexture2DResource> removedPages = new();

        private sealed class CaptureLease
        {
            internal readonly SourceSpritePresentationCapture Capture = new(parallelRecords: true);
            internal SourceSpritePresentationCapture Previous;
            internal int Users;
        }

        private static readonly ConditionalWeakTable<SourceSpriteBatch, CaptureLease> captures = new();
        private SourceSpriteBatch batch;
        private SourceSpriteBatch retainedBatch;
        private CaptureLease lease;
        private SourceSpritePresentationCapture presentation;
        private SourceSpritePresentationCapture tileCoverageOwner;
        private long tileCoverageRevision;
        private int tileCoverageCount, tileCoverageEntityCount;
        private bool[] tileCoverageEntities = Array.Empty<bool>();
        private SourceSpritePresentationCapture archiveCapture;
        private readonly List<ImageRunCache> imageRuns = new();
        private bool[] captureStateSupported = Array.Empty<bool>();
        private byte[] archiveUnitUv = Array.Empty<byte>();
        private SceneCaptureGeometryWorkers geometryWorkers;
        private SourceSpritePresentationCapture geometryCapture;
        private readonly ImageRunCache cameraLayerImages = new();
        private readonly List<SceneArchiveRangeBatch> archiveRanges = new();
        private int archiveRangeCount, activeArchiveRangeCount;
        private ImageRunCache pendingImages;
        private int imageRunCount, activeImageRunCount;
        private int[] tileComponentCursors = Array.Empty<int>();
        private readonly Func<SourceTexture2DResource, Material> resolve;
        private readonly Func<SourceTexture2DResource, Texture2D> resolveNativeTexture;
        private readonly Action flushImages;
        private SourceSceneState scene;
        private long generation, roomRevision;
        private IntRect[] knownRooms = Array.Empty<IntRect>();
        private Shader shader;
        private readonly SceneObservedRoomArchive archive = new();
        private readonly HashSet<SourceTexture2DResource> atlasPages = new(), invalidArchivePages = new();
        private readonly HashSet<SourceTexture2DResource> archiveReadyPages = new();
        private bool[] archiveTileEligibility = Array.Empty<bool>();
        private readonly Dictionary<(int Entity, int Component, int Kind), int> archiveCoverageIndices = new();
        private (int Entity, int Component, int Kind)[] archiveCoverageKeys = Array.Empty<(int, int, int)>();
        private int archiveCoverageKeyCount;
        private readonly List<int> tileCoverageOrder = new();
        private double[] tileCoverageDepths = Array.Empty<double>();
        private int[] tileCoverageKinds = Array.Empty<int>();
        private readonly HashSet<(double Depth, int Kind)> recordedTileLayers = new();
        private readonly Comparison<int> compareTileCoverage;
        private int tileCoverageCursor;
        private List<VirtualTexture> atlasSources;
        private List<VirtualTexture>.Enumerator atlasSourcesCheckpoint;
        private long atlasResourceRevision;
        private bool atlasRefreshRequired;
        private AtlasData atlas;
        private object mapIdentity;
        private int archiveLayerCursor;
        private bool canStitchArchive;
        private readonly ScenePresentationVisibility visibility = new();
        private bool staticCoveragePrepared, captureCameraMatches;
        internal long CompletedFrameSequence => lease?.Capture.FrameSequence ?? 0;
        internal bool HasCapture => lease != null;
        internal long CompletedSceneDrawSequence { get; private set; }
        internal long LastCompletedSceneCaptureSequence { get; private set; }
        internal SourceSceneState LastCompletedSceneDrawScene { get; private set; }
        internal int ArchivedRoomCount => archive.RoomCount;
        internal int UnmappedArchiveQuadCount { get; private set; }
        internal int UnsupportedArchiveQuadCount { get; private set; }
        internal int UnsupportedArchiveLayerCount { get; private set; }

        internal GameSceneRenderer(
            Func<Celeste.Graphics.Rendering.Devices.Native.NativeGraphicsDeviceIO, UnityNativeTextureBatch>
                textureBatchFactory =
                null)
        {
            this.textureBatchFactory = textureBatchFactory;
            resolve = Resolve;
            resolveDifference = ResolveDifferenceMaterial;
            resolveForeground = ResolveForegroundMaterial;
            resolveNativeTexture = source => ResolveGpuTexture(source) as Texture2D;
            flushImages = FlushImages;
            compareTileCoverage = CompareTileCoverage;
        }

        internal bool HasPresentation { get; private set; }
        internal int UnsupportedRenderStateCount { get; private set; }
        internal int UnsupportedForegroundPairCount { get; private set; }
        internal int InvalidScopeCount { get; private set; }
        internal int DroppedQuadCount { get; private set; }

        internal void Bind(SourceSpriteBatch next)
        {
            if (ReferenceEquals(batch, next)) return;
            ReleaseCapture();
            if (!ReferenceEquals(retainedBatch, next)) ClearMeshes();
            retainedBatch = next;
            batch = next;
            if (next == null) return;
            if (!captures.TryGetValue(next, out lease))
            {
                lease = new CaptureLease { Previous = next.PresentationCapture };
                captures.Add(next, lease);
                next.PresentationCapture = lease.Capture;
            }

            lease.Users++;
        }

        internal void SuspendCapture()
        {
            ReleaseCapture();
            presentation = null;
            archiveCapture = null;
            HasPresentation = false;
        }

        private void ReleaseCapture()
        {
            DetachGeometryCapture();
            if (lease != null && --lease.Users == 0)
            {
                if (ReferenceEquals(batch.PresentationCapture, lease.Capture))
                    batch.PresentationCapture = lease.Previous;
                lease.Capture.Clear();
                lease.Previous = null;
                captures.Remove(batch);
            }

            lease = null;
            batch = null;
        }

        internal int UnsupportedImageTextureCount { get; private set; }

        internal int SnapshotTexturePageCount
        {
            get
            {
                int count = 0;
                foreach (var page in pages.Values)
                    if (page.Native == null && page.Managed == null && !page.Mirrored)
                        count++;
                return count;
            }
        }

        internal static SceneTileScope GetScope(SceneView view) => views.GetOrCreateValue(view).Scope;

        internal static void SetScope(SceneView view, SceneTileScope scope)
        {
            views.GetOrCreateValue(view).Scope = scope;
            view.Repaint();
        }

        internal void Draw(SceneView view, SourceSceneState current, ReadOnlySpan<IntRect> rooms,
            in WorldBounds worldView)
        {
            using var drawScope = DrawMarker.Auto();
            if (batch?.GraphicsDevice is ISourceTextureDevice sourceDevice &&
                sourceDevice.TextureIO is UnitySourceTextureSubmissionIO managedIO)
                managedIO.FlushPendingDraws();
            var levelForArchive = current as Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
            var nextMap = levelForArchive?.Session?.MapData;
            if (!ReferenceEquals(scene, current) || !ReferenceEquals(mapIdentity, nextMap))
            {
                ClearMeshes(nextMap != null && ReferenceEquals(mapIdentity, nextMap));
                lease?.Capture.Clear();
                scene = current;
                mapIdentity = nextMap;
            }

            using (PrepareMarker.Auto())
            {
                RefreshAtlas(levelForArchive);
                InvalidateArchivePages();
            }

            archiveLayerCursor = 0;
            archiveRangeCount = 0;

            var scope = GetScope(view);
            generation++;
            livePages.Clear();
            bool roomChanged = knownRooms.Length != rooms.Length;
            if (!roomChanged)
                for (int i = 0; i < rooms.Length; i++)
                    if (knownRooms[i].X != rooms[i].X || knownRooms[i].Y != rooms[i].Y ||
                        knownRooms[i].Width != rooms[i].Width || knownRooms[i].Height != rooms[i].Height)
                    {
                        roomChanged = true;
                        break;
                    }

            if (roomChanged)
            {
                knownRooms = rooms.ToArray();
                roomRevision++;
            }

            visibility.RefreshVisibleRooms(scope, in worldView, rooms, roomRevision);
            var entities = current.SourceScene.Entities.Entities;
            UnsupportedImageTextureCount = 0;
            UnsupportedRenderStateCount = 0;
            UnsupportedForegroundPairCount = 0;
            InvalidScopeCount = 0;
            UnsupportedArchiveLayerCount = 0;
            imageRunCount = 0;
            pendingImages = null;
            var capture = lease?.Capture;
            var records = capture == null ? ReadOnlySpan<SourceSpritePresentationQuad>.Empty : capture.CompletedQuads;
            presentation = capture;
            HasPresentation = capture != null && ReferenceEquals(batch.PresentationCapture, capture) &&
                              capture.FrameSequence > 0 &&
                              current is Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level &&
                              capture.Room == level.Session.Level;
            PrepareTileCoverageEntities(capture, entities.Count);
            var camera = HasPresentation ? capture.CameraTransform : default;
            DroppedQuadCount = HasPresentation ? capture.DroppedCount : 0;
            canStitchArchive = HasPresentation && CanStitchArchive(capture);
            PrepareNativeTextures(current, capture, scope, rooms, in worldView);
            if (HasPresentation) DrawBackground(capture);
            if (canStitchArchive) PrepareTileCoverageOrder(capture);
            captureCameraMatches = HasPresentation && levelForArchive != null &&
                                   camera.Equals(levelForArchive.Camera.Matrix);
            staticCoveragePrepared = scope == SceneTileScope.VisibleWorld && canStitchArchive && captureCameraMatches;
            if (staticCoveragePrepared) PrepareStaticCoverage(capture);
            if (tileComponentCursors.Length < entities.Count)
                tileComponentCursors = new int[entities.Count];
            Array.Clear(tileComponentCursors, 0, entities.Count);
            var inverse = default(System.Numerics.Matrix4x4);
            bool hasInverse = HasPresentation &&
                              System.Numerics.Matrix4x4.Invert(camera, out inverse);
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry = default;
            if (hasInverse)
            {
                using var captureQueueScope = CaptureQueueMarker.Auto();
                if (records.Length >= SceneCaptureGeometryWorkers.MinimumQuadCount)
                {
                    geometryWorkers ??= new SceneCaptureGeometryWorkers();
                    AttachGeometryCapture(capture);
                    using var prepareGeometryScope = CaptureQueueGeometryMarker.Auto();
                    geometry = geometryWorkers.Process(capture, camera, inverse);
                }

                using var captureRecordsScope = CaptureQueueRecordsMarker.Auto();
                if (captureStateSupported.Length < capture.Count)
                    Array.Resize(ref captureStateSupported, capture.Count);
                var batchGroups = capture.CompletedBatchGroups;
                int previousBatchGroup = 0;
                bool previousBatchStateSupported = false;
                for (int i = 0; i < capture.Count; i++)
                {
                    ref readonly var record = ref records[i];
                    if (record.Scope.ComponentKind == SourceSpritePresentationCapture.Debug) continue;
                    if (canStitchArchive && tileCoverageCursor < tileCoverageOrder.Count)
                        DrawTilesBeforeDepth(record.Scope.RenderPhase.ActualDepth, entities, scope, rooms,
                            in worldView);
                    if (record.Scope.RenderPhase.HasDepth)
                    {
                        if (archiveLayerCursor < archive.Layers.Count)
                            DrawArchiveBefore(record.Scope.RenderPhase.ActualDepth, false, scope, in worldView);
                    }
                    else if (archive.Layers.Count != 0)
                        UnsupportedArchiveLayerCount += record.IsPrimitiveTrianglePair ? 2 : 1;

                    int entityIndex = record.Scope.EntityIndex;
                    if (entityIndex >= 0 && entityIndex < entities.Count)
                    {
                        int componentIndex = record.Scope.ComponentIndex;
                        if (componentIndex >= 0)
                        {
                            var entity = entities[entityIndex] as SceneEntityState;
                            if (entity != null && componentIndex < entity.Components.Count)
                            {
                                int end = componentIndex + 1;
                                int start = tileComponentCursors[entityIndex];
                                if (start < end)
                                    DrawRemainingTiles(entityIndex, entity, start, end, scope, rooms, in worldView);
                                tileComponentCursors[entityIndex] = Math.Max(start, end);
                            }
                            else InvalidScopeCount += record.IsPrimitiveTrianglePair ? 2 : 1;
                        }
                    }
                    else if (entityIndex != -1) InvalidScopeCount += record.IsPrimitiveTrianglePair ? 2 : 1;

                    // Sorted batches can return to an earlier owner. Scope only anchors static tiles;
                    // the completed source quad stream is always consumed in its recorded order.
                    int batchGroup = batchGroups[i];
                    bool batchStateSupported;
                    if (batchGroup != 0 && batchGroup == previousBatchGroup)
                        batchStateSupported = previousBatchStateSupported;
                    else
                    {
                        batchStateSupported = SupportsStateSnapshot(in record, capture.Target);
                        previousBatchGroup = batchGroup;
                        previousBatchStateSupported = batchStateSupported;
                    }

                    bool stateSupported = record.UnsupportedPrimitiveCount == 0 && batchStateSupported;
                    captureStateSupported[i] = stateSupported;
                    QueueCaptured(in record, stateSupported, in camera, in inverse, scope, rooms,
                        in worldView, geometry, i);
                }
            }

            using (RemainingTilesMarker.Auto())
            {
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    DrawRemainingTiles(entityIndex, entities[entityIndex] as SceneEntityState,
                        tileComponentCursors[entityIndex], int.MaxValue, scope, rooms, in worldView);
            }

            FlushImages();
            if (canStitchArchive)
                DrawArchiveBefore(double.NegativeInfinity, true, scope, in worldView);
            if (HasPresentation)
            {
                DrawGameplayDifference(capture);
                DrawCameraLayer(capture);
                DrawForegroundPair(capture, levelForArchive);
            }

            // Unknown live ordering has no synthetic overlay fallback.
            UnsupportedArchiveLayerCount += archive.Layers.Count - archiveLayerCursor;
            if (HasPresentation)
                ConsumeArchive(capture, levelForArchive, canStitchArchive, hasInverse, in inverse,
                    captureStateSupported, geometry);
            using (CleanupMarker.Auto())
            {
                archive.KeepPages(livePages);
                for (int i = archiveRangeCount; i < activeArchiveRangeCount; i++) archiveRanges[i].Suspend();
                activeArchiveRangeCount = archiveRangeCount;
                for (int i = imageRunCount; i < activeImageRunCount; i++)
                {
                    var unused = imageRuns[i];
                    unused.Last.Clear();
                    unused.Next.Clear();
                    unused.PointAtlasRetrySpent = false;
                    unused.Mesh.Suspend();
                }

                activeImageRunCount = imageRunCount;

                removedGrids.Clear();
                foreach (var pair in grids)
                    if (pair.Value.Seen != generation)
                        removedGrids.Add(pair.Key);
                foreach (var grid in removedGrids)
                {
                    grids[grid].Mesh.Dispose();
                    grids.Remove(grid);
                }

                removedAnimations.Clear();
                foreach (var pair in animatedGrids)
                    if (pair.Value.Seen != generation)
                        removedAnimations.Add(pair.Key);
                foreach (var animated in removedAnimations)
                {
                    animatedGrids[animated].Mesh.Dispose();
                    animatedGrids.Remove(animated);
                }

                removedPages.Clear();
                foreach (var pair in pages)
                    if (!livePages.Contains(pair.Key))
                        removedPages.Add(pair.Key);
                foreach (var source in removedPages)
                {
                    Release(pages[source]);
                    pages.Remove(source);
                }
            }

            LastCompletedSceneDrawScene = current;
            LastCompletedSceneCaptureSequence = HasPresentation ? capture.FrameSequence : 0;
            CompletedSceneDrawSequence++;
        }

        private Material ResolveDifferenceMaterial(SourceTexture2DResource source) => differenceMaterial;

        private void DrawGameplayDifference(SourceSpritePresentationCapture capture)
        {
            var difference = capture.GameplayDifference;
            var before = difference.Before.Texture;
            var after = difference.After.Texture;
            if (difference.FrameSequence != capture.FrameSequence || difference.Room != capture.Room) return;
            UnsupportedRenderStateCount += difference.UnsupportedCount;
            if (!ReferenceEquals(difference.Target, capture.Target))
            {
                UnsupportedRenderStateCount++;
                return;
            }

            if (before == null || after == null ||
                !System.Numerics.Matrix4x4.Invert(difference.CameraTransform, out var inverse)) return;
            if (before.Width != 320 || before.Height != 180 || after.Width != 320 || after.Height != 180)
            {
                UnsupportedRenderStateCount++;
                return;
            }

            var beforeTexture = ResolveGpuTexture(before);
            var afterTexture = ResolveGpuTexture(after);
            if (!beforeTexture || !afterTexture) return;
            livePages.Add(before);
            livePages.Add(after);
            if (!differenceMaterial)
            {
                differenceMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                differenceMaterial.SetFloat("_SourceTextureOriginTop", 1f);
            }

            differenceMaterial.mainTexture = afterTexture;
            differenceMaterial.SetTexture(SceneBeforeTexture, beforeTexture);
            if (!ReferenceEquals(differenceTexture, after))
            {
                var quad = new SourceSpriteQuad
                {
                    Position0 = new System.Numerics.Vector3(0f, 0f, 0f),
                    Position1 = new System.Numerics.Vector3(320f, 0f, 0f),
                    Position2 = new System.Numerics.Vector3(0f, 180f, 0f),
                    Position3 = new System.Numerics.Vector3(320f, 180f, 0f),
                    TextureCoordinate0 = new System.Numerics.Vector2(0f, 0f),
                    TextureCoordinate1 = new System.Numerics.Vector2(1f, 0f),
                    TextureCoordinate2 = new System.Numerics.Vector2(0f, 1f),
                    TextureCoordinate3 = new System.Numerics.Vector2(1f, 1f),
                    Color0 = uint.MaxValue,
                    Color1 = uint.MaxValue,
                    Color2 = uint.MaxValue,
                    Color3 = uint.MaxValue
                };
                differenceQuad[0] = new ScenePresentationMeshQuad(after, in quad);
                differenceMesh.Rebuild(differenceQuad);
                differenceTexture = after;
            }

            var matrix = new Matrix4x4(
                new Vector4(inverse.M11, -inverse.M12, inverse.M13, inverse.M14),
                new Vector4(-inverse.M21, inverse.M22, -inverse.M23, -inverse.M24),
                new Vector4(inverse.M31, -inverse.M32, inverse.M33, inverse.M34),
                new Vector4(inverse.M41, -inverse.M42, inverse.M43, inverse.M44));
            differenceMesh.Draw(matrix, resolveDifference, materialPass: 2);
        }

        private Material ResolveForegroundMaterial(SourceTexture2DResource source) => foregroundMaterial;

        private void RejectForegroundPair()
        {
            UnsupportedForegroundPairCount++;
            UnsupportedRenderStateCount++;
        }

        private void DrawForegroundPair(SourceSpritePresentationCapture capture,
            Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level)
        {
            var pair = capture.ForegroundPair;
            // Unpublished snapshots can retain their old individual metadata. Never consume them.
            if (pair.FrameSequence <= 0) return;
            var before = pair.Before.Texture;
            var after = pair.After.Texture;
            if (pair.IsRecording || pair.FrameSequence != capture.FrameSequence || pair.Room != capture.Room ||
                !pair.CameraTransform.Equals(capture.CameraTransform) || level == null ||
                pair.Target is not SourceRenderTarget2DResource target ||
                !ReferenceEquals(target, level.Buffers.Level.Target))
            {
                RejectForegroundPair();
                return;
            }

            if (before == null || after == null || before.IsDisposed || after.IsDisposed || target.IsDisposed ||
                pair.Before.FrameSequence != pair.FrameSequence || pair.After.FrameSequence != pair.FrameSequence ||
                pair.Before.Room != pair.Room || pair.After.Room != pair.Room ||
                !pair.Before.CameraTransform.Equals(pair.CameraTransform) ||
                !pair.After.CameraTransform.Equals(pair.CameraTransform) ||
                !ReferenceEquals(before.GraphicsDevice, target.GraphicsDevice) ||
                !ReferenceEquals(after.GraphicsDevice, target.GraphicsDevice) ||
                before.Format != SourceSurfaceFormat.Color || after.Format != SourceSurfaceFormat.Color ||
                target.Format != SourceSurfaceFormat.Color ||
                before.Width != 320 || before.Height != 180 || after.Width != 320 || after.Height != 180 ||
                target.Width != 320 || target.Height != 180 ||
                !System.Numerics.Matrix4x4.Invert(pair.CameraTransform, out var inverse))
            {
                RejectForegroundPair();
                return;
            }

            var beforeTexture = ResolveGpuTexture(before);
            var afterTexture = ResolveGpuTexture(after);
            if (!beforeTexture || !afterTexture)
            {
                RejectForegroundPair();
                return;
            }

            livePages.Add(before);
            livePages.Add(after);
            if (!foregroundMaterial)
            {
                foregroundMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                foregroundMaterial.SetFloat("_SourceTextureOriginTop", 1f);
            }

            foregroundMaterial.mainTexture = afterTexture;
            foregroundMaterial.SetTexture(SceneBeforeTexture, beforeTexture);
            if (!ReferenceEquals(foregroundTexture, after))
            {
                var quad = new SourceSpriteQuad
                {
                    Position0 = new System.Numerics.Vector3(0f, 0f, 0f),
                    Position1 = new System.Numerics.Vector3(320f, 0f, 0f),
                    Position2 = new System.Numerics.Vector3(0f, 180f, 0f),
                    Position3 = new System.Numerics.Vector3(320f, 180f, 0f),
                    TextureCoordinate0 = new System.Numerics.Vector2(0f, 0f),
                    TextureCoordinate1 = new System.Numerics.Vector2(1f, 0f),
                    TextureCoordinate2 = new System.Numerics.Vector2(0f, 1f),
                    TextureCoordinate3 = new System.Numerics.Vector2(1f, 1f),
                    Color0 = uint.MaxValue,
                    Color1 = uint.MaxValue,
                    Color2 = uint.MaxValue,
                    Color3 = uint.MaxValue
                };
                foregroundQuad[0] = new ScenePresentationMeshQuad(after, in quad);
                foregroundMesh.Rebuild(foregroundQuad);
                foregroundTexture = after;
            }

            var matrix = new Matrix4x4(
                new Vector4(inverse.M11, -inverse.M12, inverse.M13, inverse.M14),
                new Vector4(-inverse.M21, inverse.M22, -inverse.M23, -inverse.M24),
                new Vector4(inverse.M31, -inverse.M32, inverse.M33, inverse.M34),
                new Vector4(inverse.M41, -inverse.M42, inverse.M43, inverse.M44));
            // Each pass emits a nonnegative RGBA delta. Blend subtraction precedes addition.
            foregroundMesh.Draw(matrix, resolveForeground, materialPass: 4);
            foregroundMesh.Draw(matrix, resolveForeground, materialPass: 5);
        }

        private void DrawCameraLayer(SourceSpritePresentationCapture capture)
        {
            var layer = capture.CameraLayer;
            var cache = cameraLayerImages;
            cache.Next.Clear();
            if (layer.FrameSequence != capture.FrameSequence || layer.Room != capture.Room ||
                !System.Numerics.Matrix4x4.Invert(layer.CameraTransform, out var inverse))
            {
                cache.Last.Clear();
                cache.Mesh.Suspend();
                return;
            }

            UnsupportedRenderStateCount += layer.UnsupportedCount;
            var records = layer.CompletedQuads;
            for (int i = 0; i < records.Length; i++)
            {
                ref readonly var record = ref records[i];
                if (!SupportsCameraLayerState(in record, layer.Target))
                {
                    UnsupportedRenderStateCount++;
                    continue;
                }

                if (record.Texture == null || !ReferenceEquals(record.Texture, layer.Snapshot.Texture) ||
                    record.Texture.IsDisposed || record.Texture.Format != SourceSurfaceFormat.Color)
                {
                    UnsupportedImageTextureCount++;
                    continue;
                }

                var quad = TransformWorldQuad(in record.Quad, in record.State.Transform, in inverse);
                cache.Next.Add(new ScenePresentationMeshQuad(record.Texture, in quad));
            }

            bool changed = cache.RebuildRequired || cache.Last.Count != cache.Next.Count;
            if (!changed)
                for (int i = 0; i < cache.Next.Count; i++)
                    if (!cache.Last[i].Matches(cache.Next[i]))
                    {
                        changed = true;
                        break;
                    }

            if (changed)
            {
                try
                {
                    cache.Mesh.Rebuild(cache.Next, cache.Last);
                    cache.RebuildRequired = false;
                }
                catch
                {
                    cache.RebuildRequired = true;
                    cache.Mesh.Suspend();
                    throw;
                }
            }

            var previous = cache.Last;
            cache.Last = cache.Next;
            cache.Next = previous;
            cache.Mesh.KeepPages(livePages);
            cache.Mesh.Draw(Matrix4x4.identity, resolve, materialPass: 1);
        }

        private static bool SupportsCameraLayerState(in SourceSpritePresentationQuad record, object target)
        {
            ref readonly var blend = ref record.BlendState;
            ref readonly var sampler = ref record.SamplerState;
            ref readonly var depth = ref record.DepthStencilState;
            ref readonly var rasterizer = ref record.RasterizerState;
            return record.UnsupportedPrimitiveCount == 0 && record.TargetCount == 1 &&
                   ReferenceEquals(record.Target, target) &&
                   record.State.Transform.Equals(System.Numerics.Matrix4x4.Identity) &&
                   CameraLayerQuadSupported(in record.Quad, rasterizer.cullMode) &&
                   record.State.Effect == null && record.State.Blend != null &&
                   record.State.Sampler != null && record.State.DepthStencil != null &&
                   record.State.Rasterizer != null &&
                   blend.colorBlendFunction == SourceBlendFunction.Add &&
                   blend.alphaBlendFunction == SourceBlendFunction.Add &&
                   blend.colorSourceBlend == SourceBlendFactor.SourceAlpha &&
                   blend.colorDestinationBlend == SourceBlendFactor.One &&
                   blend.alphaSourceBlend == SourceBlendFactor.Zero &&
                   blend.alphaDestinationBlend == SourceBlendFactor.One &&
                   blend.colorWriteEnable == SourceColorWriteChannels.All && blend.multiSampleMask == -1 &&
                   sampler.filter == SourceTextureFilter.Linear && sampler.maxMipLevel == 0 &&
                   sampler.mipMapLevelOfDetailBias == 0f &&
                   sampler.addressU == SourceTextureAddressMode.Clamp &&
                   sampler.addressV == SourceTextureAddressMode.Clamp &&
                   depth.depthBufferEnable == 0 && depth.depthBufferWriteEnable == 0 &&
                   depth.stencilEnable == 0 &&
                   rasterizer.fillMode == SourceFillMode.Solid && rasterizer.scissorTestEnable == 0 &&
                   rasterizer.depthBias == 0f && rasterizer.slopeScaleDepthBias == 0f &&
                   (record.State.SortMode == SourceSpriteSortMode.Deferred ||
                    record.State.SortMode == SourceSpriteSortMode.Immediate ||
                    record.State.SortMode == SourceSpriteSortMode.Texture ||
                    record.State.SortMode == SourceSpriteSortMode.FrontToBack ||
                    record.State.SortMode == SourceSpriteSortMode.BackToFront);
        }

        private static bool CameraLayerQuadSupported(in SourceSpriteQuad quad, SourceCullMode cull)
        {
            if (!CameraLayerPosition(quad.Position0) || !CameraLayerPosition(quad.Position1) ||
                !CameraLayerPosition(quad.Position2) || !CameraLayerPosition(quad.Position3) ||
                !FiniteUv(quad.TextureCoordinate0) || !FiniteUv(quad.TextureCoordinate1) ||
                !FiniteUv(quad.TextureCoordinate2) || !FiniteUv(quad.TextureCoordinate3)) return false;
            if (cull == SourceCullMode.None) return true;
            return cull == SourceCullMode.CullCounterClockwiseFace &&
                   CameraLayerCross(quad.Position0, quad.Position1, quad.Position2) > 0d &&
                   CameraLayerCross(quad.Position2, quad.Position1, quad.Position3) > 0d;
        }

        private static bool CameraLayerPosition(System.Numerics.Vector3 value) => value.Z == 0f &&
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);

        private static double CameraLayerCross(System.Numerics.Vector3 a, System.Numerics.Vector3 b,
            System.Numerics.Vector3 c) => ((double)b.X - a.X) * ((double)c.Y - a.Y) -
                                          ((double)b.Y - a.Y) * ((double)c.X - a.X);

        private void DrawBackground(SourceSpritePresentationCapture capture)
        {
            using var drawBackgroundScope = DrawBackgroundMarker.Auto();
            var snapshot = capture.BackgroundSnapshot;
            var texture = snapshot.Texture;
            if (texture == null || snapshot.FrameSequence != capture.FrameSequence ||
                snapshot.Room != capture.Room ||
                !System.Numerics.Matrix4x4.Invert(snapshot.CameraTransform, out var inverse)) return;
            livePages.Add(texture);
            if (!ReferenceEquals(backgroundTexture, texture))
            {
                var quad = new SourceSpriteQuad
                {
                    Position0 = new System.Numerics.Vector3(0f, 0f, 0f),
                    Position1 = new System.Numerics.Vector3(320f, 0f, 0f),
                    Position2 = new System.Numerics.Vector3(0f, 180f, 0f),
                    Position3 = new System.Numerics.Vector3(320f, 180f, 0f),
                    TextureCoordinate0 = new System.Numerics.Vector2(0f, 0f),
                    TextureCoordinate1 = new System.Numerics.Vector2(1f, 0f),
                    TextureCoordinate2 = new System.Numerics.Vector2(0f, 1f),
                    TextureCoordinate3 = new System.Numerics.Vector2(1f, 1f),
                    Color0 = uint.MaxValue,
                    Color1 = uint.MaxValue,
                    Color2 = uint.MaxValue,
                    Color3 = uint.MaxValue
                };
                backgroundQuad[0] = new ScenePresentationMeshQuad(texture, in quad);
                backgroundMesh.Rebuild(backgroundQuad);
                backgroundTexture = texture;
            }

            // Scene mesh coordinates negate source Y; conjugate the inverse camera accordingly.
            var matrix = new Matrix4x4(
                new Vector4(inverse.M11, -inverse.M12, inverse.M13, inverse.M14),
                new Vector4(-inverse.M21, inverse.M22, -inverse.M23, -inverse.M24),
                new Vector4(inverse.M31, -inverse.M32, inverse.M33, inverse.M34),
                new Vector4(inverse.M41, -inverse.M42, inverse.M43, inverse.M44));
            backgroundMesh.Draw(matrix, resolve);
        }

        private void PrepareTileCoverageEntities(SourceSpritePresentationCapture capture, int entityCount)
        {
            tileCoverageOwner = null;
            tileCoverageEntityCount = 0;
            if (!HasPresentation) return;
            if (tileCoverageEntities.Length < entityCount)
                Array.Resize(ref tileCoverageEntities, entityCount);
            Array.Clear(tileCoverageEntities, 0, entityCount);
            int count = capture.TileCoverageCount;
            for (int i = 0; i < count; i++)
            {
                int entityIndex = capture.GetTileCoverage(i).Scope.EntityIndex;
                if ((uint)entityIndex < (uint)entityCount) tileCoverageEntities[entityIndex] = true;
            }

            tileCoverageOwner = capture;
            tileCoverageRevision = capture.Revision;
            tileCoverageCount = count;
            tileCoverageEntityCount = entityCount;
        }

        private void DrawRemainingTiles(int entityIndex, SceneEntityState entity, int start, int end,
            SceneTileScope scope, ReadOnlySpan<IntRect> rooms, in WorldBounds worldView)
        {
            if (entity == null || !entity.Visible || start < 0) return;
            // With a valid capture, the original scan draws only components with captured coverage.
            // A changed or unavailable capture keeps the original component scan.
            if (HasPresentation && ReferenceEquals(presentation, tileCoverageOwner) &&
                presentation.Revision == tileCoverageRevision && presentation.TileCoverageCount == tileCoverageCount &&
                (uint)entityIndex < (uint)tileCoverageEntityCount && !tileCoverageEntities[entityIndex]) return;
            for (int i = start; i < Math.Min(end, entity.Components.Count); i++)
            {
                var component = entity.Components[i];
                if (!(component is TileGrid) && !(component is AnimatedTiles)) continue;
                IntRect? coverage = null;
                var position = entity.Position;
                double? recordedDepth = null;
                bool supportedCoverage = false;
                if (HasPresentation)
                {
                    bool found = false;
                    for (int c = 0; c < presentation.TileCoverageCount; c++)
                    {
                        var item = presentation.GetTileCoverage(c);
                        if (item.Scope.EntityIndex != entityIndex || item.Scope.ComponentIndex != i) continue;
                        bool matches = component is TileGrid tile &&
                                       item.Scope.ComponentKind == SourceSpritePresentationCapture.TileGridComponent &&
                                       item.TileWidth == tile.TileWidth && item.TileHeight == tile.TileHeight ||
                                       component is AnimatedTiles &&
                                       item.Scope.ComponentKind ==
                                       SourceSpritePresentationCapture.AnimatedTilesComponent &&
                                       item.TileWidth == 8 && item.TileHeight == 8;
                        if (!matches)
                        {
                            InvalidScopeCount++;
                            break;
                        }

                        supportedCoverage = staticCoveragePrepared && archiveTileEligibility[c];
                        coverage = item.Tiles;
                        position = item.WorldPosition;
                        if (item.Scope.RenderPhase.HasDepth) recordedDepth = item.Scope.RenderPhase.ActualDepth;
                        found = true;
                        break;
                    }

                    // Custom component renders and zero-draw suppression have no synthetic tile fallback.
                    if (!found) continue;
                }

                if (component is AnimatedTiles animated && animated.Visible && animated.Alpha > 0f)
                {
                    if (animatedGrids.TryGetValue(animated, out var existing) && existing.Seen == generation)
                        continue;
                    if (recordedDepth.HasValue)
                        DrawArchiveBefore(recordedDepth.Value, false, scope, in worldView);
                    DrawAnimated(animated, HasPresentation ? position : entity.Position + animated.Position,
                        scope, rooms, in worldView, coverage, recordedDepth, supportedCoverage);
                    if (recordedDepth.HasValue)
                        DrawArchiveBefore(recordedDepth.Value, true, scope, in worldView);
                }
                else if (component is TileGrid grid && grid.Visible && grid.Alpha > 0f)
                {
                    if (grids.TryGetValue(grid, out var existing) && existing.Seen == generation) continue;
                    if (recordedDepth.HasValue)
                        DrawArchiveBefore(recordedDepth.Value, false, scope, in worldView);
                    DrawGrid(grid, HasPresentation ? position : entity.Position + grid.Position,
                        scope, rooms, in worldView, coverage, recordedDepth, supportedCoverage);
                    if (recordedDepth.HasValue)
                        DrawArchiveBefore(recordedDepth.Value, true, scope, in worldView);
                }
            }
        }

        private void DrawGrid(TileGrid grid, System.Numerics.Vector2 position, SceneTileScope scope,
            ReadOnlySpan<IntRect> rooms, in WorldBounds worldView, IntRect? coverage, double? recordedDepth,
            bool supportedCoverage)
        {
            var map = grid.Tiles;
            var extent = new WorldBounds(position.X, position.Y, map.Columns * grid.TileWidth,
                map.Rows * grid.TileHeight);
            if (coverage.HasValue && ScenePresentationVisibility.IsFullyCovered(scope, HasPresentation,
                    supportedCoverage, captureCameraMatches, DroppedQuadCount, in worldView, in extent, map.Columns,
                    map.Rows, grid.TileWidth, grid.TileHeight, coverage.Value))
            {
                if (grids.TryGetValue(grid, out var retained)) retained.Seen = generation;
                return;
            }

            FlushImages();
            if (!grids.TryGetValue(grid, out var cache)) grids.Add(grid, cache = new GridCache());
            cache.Seen = generation;
            var ranges = SelectSnapshotRanges(cache, scope, in worldView, rooms, in extent,
                map.Columns, map.Rows, grid.TileWidth, grid.TileHeight);
            bool changed = cache.TexturePreparedGeneration == generation
                ? cache.TexturePreparedChanged
                : cache.Mesh.Snapshot.Refresh(grid, ranges);
            var snapshot = cache.Mesh.Snapshot;
            if (changed)
            {
                RefreshCellIndices(cache, snapshot, null);
                cache.Sources.Clear();
                for (int i = 0; i < snapshot.Count; i++)
                    cache.Sources.Add(snapshot[i].Texture);
            }

            foreach (var source in cache.Sources) livePages.Add(source);
            var gridWorld = new WorldBounds(position.X, position.Y, snapshot.Columns * grid.TileWidth,
                snapshot.Rows * grid.TileHeight);
            SetArchiveLayer(cache, recordedDepth, SourceSpritePresentationCapture.TileGridComponent);
            changed = RefreshMask(cache, scope, in worldView, in gridWorld, rooms, snapshot.Columns,
                snapshot.Rows, grid.TileWidth, grid.TileHeight, changed);

            if (changed) cache.Mesh.Rebuild(cache.Mask.AsSpan(0, cache.CellCount));
            cache.TexturePreparedChanged = false;
            cache.Mesh.Draw(Matrix4x4.Translate(new Vector3(position.X, -position.Y, 0f)), resolve, coverage);
        }

        private static bool SupportsState(in SourceSpritePresentationQuad record, object target) =>
            record.UnsupportedPrimitiveCount == 0 && SupportsStateSnapshot(in record, target);

        private static bool SupportsStateSnapshot(in SourceSpritePresentationQuad record, object target)
        {
            ref readonly var blend = ref record.BlendState;
            ref readonly var sampler = ref record.SamplerState;
            ref readonly var depth = ref record.DepthStencilState;
            ref readonly var rasterizer = ref record.RasterizerState;
            return record.TargetCount == 1 &&
                   ReferenceEquals(record.Target, target) &&
                   record.State.Effect == null && record.State.Blend != null &&
                   record.State.Sampler != null && record.State.DepthStencil != null &&
                   record.State.Rasterizer != null &&
                   blend.colorBlendFunction == SourceBlendFunction.Add &&
                   blend.alphaBlendFunction == SourceBlendFunction.Add &&
                   blend.colorSourceBlend == SourceBlendFactor.One &&
                   blend.alphaSourceBlend == SourceBlendFactor.One &&
                   blend.colorDestinationBlend == SourceBlendFactor.InverseSourceAlpha &&
                   blend.alphaDestinationBlend == SourceBlendFactor.InverseSourceAlpha &&
                   blend.colorWriteEnable == SourceColorWriteChannels.All && blend.multiSampleMask == -1 &&
                   sampler.filter == SourceTextureFilter.Point && sampler.maxMipLevel == 0 &&
                   sampler.mipMapLevelOfDetailBias == 0f &&
                   (sampler.addressU == SourceTextureAddressMode.Clamp ||
                    sampler.addressU == SourceTextureAddressMode.Wrap) &&
                   (sampler.addressV == SourceTextureAddressMode.Clamp ||
                    sampler.addressV == SourceTextureAddressMode.Wrap) &&
                   depth.depthBufferEnable == 0 && depth.depthBufferWriteEnable == 0 &&
                   depth.stencilEnable == 0 && rasterizer.cullMode == SourceCullMode.None &&
                   rasterizer.fillMode == SourceFillMode.Solid && rasterizer.scissorTestEnable == 0 &&
                   rasterizer.depthBias == 0f && rasterizer.slopeScaleDepthBias == 0f &&
                   (record.State.SortMode == SourceSpriteSortMode.Deferred ||
                    record.State.SortMode == SourceSpriteSortMode.Immediate ||
                    record.State.SortMode == SourceSpriteSortMode.Texture ||
                    record.State.SortMode == SourceSpriteSortMode.FrontToBack ||
                    record.State.SortMode == SourceSpriteSortMode.BackToFront);
        }

        private static SourceSpriteQuad WorldQuad(in SourceSpriteQuad source,
            in System.Numerics.Matrix4x4 transform, in System.Numerics.Matrix4x4 camera,
            in System.Numerics.Matrix4x4 inverse)
        {
            // Captured vertices precede the batch transform. The game-camera batch already uses world coordinates.
            if (transform.Equals(camera)) return source;
            return TransformWorldQuad(in source, in transform, in inverse);
        }

        private static SourceSpriteQuad TransformWorldQuad(in SourceSpriteQuad source,
            in System.Numerics.Matrix4x4 transform, in System.Numerics.Matrix4x4 inverse)
        {
            var quad = source;
            var relative = transform * inverse;
            quad.Position0 = System.Numerics.Vector3.Transform(quad.Position0, relative);
            quad.Position1 = System.Numerics.Vector3.Transform(quad.Position1, relative);
            quad.Position2 = System.Numerics.Vector3.Transform(quad.Position2, relative);
            quad.Position3 = System.Numerics.Vector3.Transform(quad.Position3, relative);
            return quad;
        }

        private void QueueCaptured(in SourceSpritePresentationQuad record, bool stateSupported,
            in System.Numerics.Matrix4x4 camera, in System.Numerics.Matrix4x4 inverse, SceneTileScope scope,
            ReadOnlySpan<IntRect> rooms, in WorldBounds worldView,
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry, int recordIndex)
        {
            if (!stateSupported)
            {
                FlushImages();
                UnsupportedRenderStateCount += record.IsPrimitiveTrianglePair ? 2 : 1;
                return;
            }

            if (record.Texture == null ||
                record.Texture.IsDisposed || record.Texture.Format != SourceSurfaceFormat.Color)
            {
                FlushImages();
                UnsupportedImageTextureCount += record.IsPrimitiveTrianglePair ? 2 : 1;
                return;
            }

            if (!geometry.IsEmpty)
            {
                ref readonly var prepared = ref geometry[recordIndex];
                if (record.IsPrimitiveTrianglePair)
                {
                    QueuePrimitivePair(in record, in prepared.Quad, scope, rooms, in worldView, geometry, recordIndex);
                    return;
                }

                if (!prepared.FiniteUv)
                {
                    FlushImages();
                    UnsupportedRenderStateCount++;
                    return;
                }

                QueueVisibleWorldQuad(in record, in prepared.Quad, in prepared.Bounds, scope, rooms, in worldView);
                return;
            }

            if (record.State.Transform.Equals(camera))
                QueueWorldQuad(in record, in record.Quad, scope, rooms, in worldView);
            else
            {
                var quad = TransformWorldQuad(in record.Quad, in record.State.Transform, in inverse);
                QueueWorldQuad(in record, in quad, scope, rooms, in worldView);
            }
        }

        private void QueueWorldQuad(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            SceneTileScope scope, ReadOnlySpan<IntRect> rooms, in WorldBounds worldView)
        {
            if (record.IsPrimitiveTrianglePair)
            {
                QueuePrimitivePair(in record, in quad, scope, rooms, in worldView, default, 0);
                return;
            }

            // The captured point sampler determines addressing; only non-finite UVs are unsupported.
            if (!FiniteUv(quad.TextureCoordinate0) || !FiniteUv(quad.TextureCoordinate1) ||
                !FiniteUv(quad.TextureCoordinate2) || !FiniteUv(quad.TextureCoordinate3))
            {
                FlushImages();
                UnsupportedRenderStateCount++;
                return;
            }

            float left = Math.Min(Math.Min(quad.Position0.X, quad.Position1.X),
                Math.Min(quad.Position2.X, quad.Position3.X));
            float top = Math.Min(Math.Min(quad.Position0.Y, quad.Position1.Y),
                Math.Min(quad.Position2.Y, quad.Position3.Y));
            float right = Math.Max(Math.Max(quad.Position0.X, quad.Position1.X),
                Math.Max(quad.Position2.X, quad.Position3.X));
            float bottom = Math.Max(Math.Max(quad.Position0.Y, quad.Position1.Y),
                Math.Max(quad.Position2.Y, quad.Position3.Y));
            var bounds = new WorldBounds(left, top, right - left, bottom - top);
            QueueVisibleWorldQuad(in record, in quad, in bounds, scope, rooms, in worldView);
        }

        private void QueueVisibleWorldQuad(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            in WorldBounds bounds, SceneTileScope scope, ReadOnlySpan<IntRect> rooms, in WorldBounds worldView)
        {
            if (!IncludesImage(scope, in bounds, in worldView, rooms)) return;
            QueueImage(in record, in quad, 0);
        }

        private void QueuePrimitivePair(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            SceneTileScope scope, ReadOnlySpan<IntRect> rooms, in WorldBounds worldView,
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry, int recordIndex)
        {
            bool commonUv = geometry.IsEmpty && FiniteUv(quad.TextureCoordinate1) && FiniteUv(quad.TextureCoordinate2);
            bool firstUv = geometry.IsEmpty
                ? commonUv && FiniteUv(quad.TextureCoordinate0)
                : (geometry[recordIndex].PrimitiveFiniteUv & 1) != 0;
            bool secondUv = geometry.IsEmpty
                ? commonUv && FiniteUv(quad.TextureCoordinate3)
                : (geometry[recordIndex].PrimitiveFiniteUv & 2) != 0;
            var firstBounds = geometry.IsEmpty
                ? ScenePresentationMeshQuad.TriangleBounds(in quad, false)
                : geometry[recordIndex].Bounds;
            var secondBounds = geometry.IsEmpty
                ? ScenePresentationMeshQuad.TriangleBounds(in quad, true)
                : geometry[recordIndex].SecondTriangleBounds;
            bool firstVisible = firstUv && IncludesImage(scope, in firstBounds, in worldView, rooms);
            bool secondVisible = secondUv && IncludesImage(scope, in secondBounds, in worldView, rooms);
            if (!firstUv)
            {
                FlushImages();
                UnsupportedRenderStateCount++;
                if (secondVisible) QueueImage(in record, in quad, 2);
                if (!secondUv)
                {
                    FlushImages();
                    UnsupportedRenderStateCount++;
                }

                return;
            }

            if (!secondUv)
            {
                if (firstVisible) QueueImage(in record, in quad, 1);
                FlushImages();
                UnsupportedRenderStateCount++;
                return;
            }

            byte selected = (byte)((firstVisible ? 1 : 0) | (secondVisible ? 2 : 0));
            if (selected != 0) QueueImage(in record, in quad, selected);
        }

        private void QueueImage(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            byte primitiveTriangles)
        {
            int pointAddress = (record.SamplerState.addressU == SourceTextureAddressMode.Wrap ? 1 : 0) |
                               (record.SamplerState.addressV == SourceTextureAddressMode.Wrap ? 2 : 0);
            if (pendingImages != null && pendingImages.PointAddress != pointAddress) FlushImages();
            if (pendingImages == null)
            {
                if (imageRunCount == imageRuns.Count) imageRuns.Add(new ImageRunCache(livePointImages: true));
                pendingImages = imageRuns[imageRunCount++];
                pendingImages.Next.Clear();
                pendingImages.PointAddress = pointAddress;
            }

            pendingImages.Next.Add(record.Texture, in quad, primitiveTriangles);
        }

        private static bool FiniteUv(System.Numerics.Vector2 uv) =>
            (BitConverter.SingleToInt32Bits(uv.X) & 0x7f800000) != 0x7f800000 &&
            (BitConverter.SingleToInt32Bits(uv.Y) & 0x7f800000) != 0x7f800000;

        private static bool AtlasUv(System.Numerics.Vector2 uv) =>
            uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f;

        private bool IncludesImage(SceneTileScope scope, in WorldBounds bounds,
            in WorldBounds worldView, ReadOnlySpan<IntRect> rooms)
        {
            switch (scope)
            {
                case SceneTileScope.EntireMap: return true;
                case SceneTileScope.VisibleWorld: return bounds.Intersects(worldView);
                case SceneTileScope.VisibleRooms: return visibility.IncludesVisibleRoom(in bounds);
                default: throw new ArgumentOutOfRangeException(nameof(scope));
            }
        }

        private void FlushImages()
        {
            var cache = pendingImages;
            if (cache == null) return;
            using var flushImagesScope = FlushImagesMarker.Auto();
            bool changed = cache.RebuildRequired || cache.Last.Count != cache.Next.Count;
            if (!changed)
                for (int i = 0; i < cache.Next.Count; i++)
                    if (!cache.Last[i].Matches(cache.Next[i]))
                    {
                        changed = true;
                        break;
                    }

            if (changed)
            {
                try
                {
                    cache.Mesh.Rebuild(cache.Next, cache.Last, pointAtlas, resolveNativeTexture, PointShader());
                    cache.RebuildRequired = false;
                }
                catch
                {
                    cache.RebuildRequired = true;
                    cache.Mesh.Suspend();
                    throw;
                }
            }

            try
            {
                if (!cache.Mesh.RefreshPointAtlas(pointAtlas, resolveNativeTexture, PointShader()))
                {
                    cache.Mesh.Rebuild(cache.Next);
                    // One retry on the next paint covers a transient device/revision race.
                    // A permanently unavailable source must not rebuild this run forever.
                    if (!cache.PointAtlasRetrySpent)
                    {
                        cache.RebuildRequired = true;
                        cache.PointAtlasRetrySpent = true;
                    }
                }
                else cache.PointAtlasRetrySpent = false;
            }
            catch
            {
                cache.RebuildRequired = true;
                cache.Mesh.Suspend();
                throw;
            }

            var previous = cache.Last;
            cache.Last = cache.Next;
            cache.Next = previous;
            cache.Mesh.KeepPages(livePages);
            cache.Mesh.Draw(Matrix4x4.identity, resolve, null, cache.PointAddress);
            pendingImages = null;
        }

        private static ReadOnlySpan<IntRect> SelectSnapshotRanges(GridCache cache, SceneTileScope scope,
            in WorldBounds view, ReadOnlySpan<IntRect> rooms, in WorldBounds grid,
            int columns, int rows, int width, int height)
        {
            int capacity = scope == SceneTileScope.VisibleRooms ? rooms.Length : 1;
            if (cache.SnapshotRanges.Length < capacity) cache.SnapshotRanges = new IntRect[capacity];
            int count = 0;
            switch (scope)
            {
                case SceneTileScope.EntireMap:
                    cache.SnapshotRanges[count++] = new IntRect(0, 0, columns, rows);
                    break;
                case SceneTileScope.VisibleWorld:
                    AddSnapshotRange(cache.SnapshotRanges, ref count, in view, in grid, columns, rows, width, height);
                    break;
                case SceneTileScope.VisibleRooms:
                    foreach (var room in rooms)
                        if (room.Width > 0 && room.Height > 0 && view.Width > 0 && view.Height > 0 &&
                            new WorldBounds(room.X, room.Y, room.Width, room.Height).Intersects(view))
                            AddSnapshotRange(cache.SnapshotRanges, ref count,
                                new WorldBounds(room.X, room.Y, room.Width, room.Height),
                                in grid, columns, rows, width, height);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(scope));
            }

            return cache.SnapshotRanges.AsSpan(0, count);
        }

        private static void AddSnapshotRange(IntRect[] ranges, ref int count, in WorldBounds area,
            in WorldBounds grid, int columns, int rows, int width, int height)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            double left = Math.Max((double)area.X, grid.X), top = Math.Max((double)area.Y, grid.Y);
            double right = Math.Min((double)area.Right, grid.Right),
                bottom = Math.Min((double)area.Bottom, grid.Bottom);
            if (left >= right || top >= bottom) return;
            int x0 = (int)((left - grid.X) / width), y0 = (int)((top - grid.Y) / height);
            int x1 = Math.Min((int)Math.Ceiling((right - grid.X) / width), columns);
            int y1 = Math.Min((int)Math.Ceiling((bottom - grid.Y) / height), rows);
            ranges[count++] = new IntRect(x0, y0, x1 - x0, y1 - y0);
        }

        private static void RefreshCellIndices(GridCache cache, TileGridGeometrySnapshot tiles,
            AnimatedTileGeometrySnapshot animation)
        {
            int count = tiles != null ? tiles.Count : animation.Count;
            bool changed = cache.CellCount != count;
            if (cache.CellIndices.Length < count) cache.CellIndices = new long[count];
            cache.CellCount = count;
            for (int i = 0; i < count; i++)
            {
                long cell = tiles != null ? tiles[i].CellIndex : animation[i].CellIndex;
                changed |= cache.CellIndices[i] != cell;
                cache.CellIndices[i] = cell;
            }

            if (!changed) return;
            cache.HasMask = false;
            cache.HasArchiveMask = false;
        }

        private static void FillCompactMask(ReadOnlySpan<long> cells, SceneTileScope scope,
            in WorldBounds view, ReadOnlySpan<IntRect> rooms, in WorldBounds grid,
            int columns, int rows, int width, int height, Span<byte> mask)
        {
            if (width <= 0 || height <= 0 || columns < 0 || rows < 0 ||
                grid.Width != (double)columns * width || grid.Height != (double)rows * height)
                throw new ArgumentOutOfRangeException(nameof(grid));
            if (mask.Length != cells.Length)
                throw new ArgumentException("One mask byte per snapshot entry is required.", nameof(mask));
            mask.Clear();
            switch (scope)
            {
                case SceneTileScope.EntireMap:
                    mask.Fill(1);
                    return;
                case SceneTileScope.VisibleWorld:
                    IncludeCompact(cells, in view, in grid, columns, rows, width, height, mask);
                    return;
                case SceneTileScope.VisibleRooms:
                    foreach (var room in rooms)
                        if (room.Width > 0 && room.Height > 0 && view.Width > 0 && view.Height > 0 &&
                            new WorldBounds(room.X, room.Y, room.Width, room.Height).Intersects(view))
                            IncludeCompact(cells, new WorldBounds(room.X, room.Y, room.Width, room.Height),
                                in grid, columns, rows, width, height, mask);
                    return;
                default: throw new ArgumentOutOfRangeException(nameof(scope));
            }
        }

        private static void IncludeCompact(ReadOnlySpan<long> cells, in WorldBounds area,
            in WorldBounds grid, int columns, int rows, int width, int height, Span<byte> mask)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            double left = Math.Max((double)area.X, grid.X), top = Math.Max((double)area.Y, grid.Y);
            double right = Math.Min((double)area.Right, grid.Right),
                bottom = Math.Min((double)area.Bottom, grid.Bottom);
            if (left >= right || top >= bottom) return;
            int x0 = (int)((left - grid.X) / width), y0 = (int)((top - grid.Y) / height);
            int x1 = Math.Min((int)Math.Ceiling((right - grid.X) / width), columns);
            int y1 = Math.Min((int)Math.Ceiling((bottom - grid.Y) / height), rows);
            long first = (long)x0 * rows, end = (long)x1 * rows;
            int lo = 0, hi = cells.Length;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (cells[mid] < first) lo = mid + 1;
                else hi = mid;
            }

            for (int i = lo; i < cells.Length && cells[i] < end; i++)
                if (cells[i] % rows >= y0 && cells[i] % rows < y1)
                    mask[i] = 1;
        }

        private bool RefreshMask(GridCache cache, SceneTileScope scope, in WorldBounds worldView,
            in WorldBounds gridWorld, ReadOnlySpan<IntRect> rooms, int columns, int rows,
            int tileWidth, int tileHeight, bool changed)
        {
            if (cache.Mask.Length < cache.CellCount)
            {
                cache.Mask = new byte[cache.CellIndices.Length];
                cache.NextMask = new byte[cache.CellIndices.Length];
                cache.HasMask = false;
            }

            bool viewChanged = !Same(cache.LastView, worldView);
            bool roomSelectionChanged = false;
            if (scope == SceneTileScope.VisibleRooms && viewChanged && cache.HasMask &&
                cache.LastScope == scope && cache.RoomRevision == roomRevision)
            {
                for (int i = 0; i < rooms.Length; i++)
                {
                    var room = rooms[i];
                    if (room.Width <= 0 || room.Height <= 0) continue;
                    var bounds = new WorldBounds(room.X, room.Y, room.Width, room.Height);
                    bool wasVisible = cache.LastView.Width > 0 && cache.LastView.Height > 0 &&
                                      bounds.Intersects(cache.LastView);
                    bool isVisible = worldView.Width > 0 && worldView.Height > 0 &&
                                     bounds.Intersects(worldView);
                    if (wasVisible != isVisible)
                    {
                        roomSelectionChanged = true;
                        break;
                    }
                }
            }

            bool maskInputsChanged = scope != SceneTileScope.EntireMap &&
                                     (cache.LastColumns != columns || cache.LastRows != rows ||
                                      cache.LastTileWidth != tileWidth || cache.LastTileHeight != tileHeight ||
                                      !Same(cache.LastGrid, gridWorld) ||
                                      (scope == SceneTileScope.VisibleWorld && viewChanged) ||
                                      (scope == SceneTileScope.VisibleRooms &&
                                       (cache.RoomRevision != roomRevision || roomSelectionChanged)));
            bool archiveMaskChanged = RefreshArchiveMask(cache, in gridWorld, columns, rows, tileWidth, tileHeight);
            if (!cache.HasMask || cache.LastScope != scope || maskInputsChanged || archiveMaskChanged)
            {
                FillCompactMask(cache.CellIndices.AsSpan(0, cache.CellCount), scope, in worldView, rooms, in gridWorld,
                    columns, rows,
                    tileWidth, tileHeight, cache.NextMask.AsSpan(0, cache.CellCount));
                bool maskChanged = !cache.HasMask;
                for (int i = 0; i < cache.CellCount; i++)
                {
                    cache.NextMask[i] &= cache.ArchiveMask[i];
                    if (cache.Mask[i] != cache.NextMask[i]) maskChanged = true;
                }

                if (maskChanged)
                {
                    var old = cache.Mask;
                    cache.Mask = cache.NextMask;
                    cache.NextMask = old;
                }

                changed |= maskChanged;
                cache.HasMask = true;
                cache.LastScope = scope;
                cache.LastColumns = columns;
                cache.LastRows = rows;
                cache.LastTileWidth = tileWidth;
                cache.LastTileHeight = tileHeight;
                cache.LastView = worldView;
                cache.LastGrid = gridWorld;
                cache.RoomRevision = roomRevision;
            }

            cache.LastView = worldView;
            return changed;
        }

        private void DrawAnimated(AnimatedTiles animated, System.Numerics.Vector2 position, SceneTileScope scope,
            ReadOnlySpan<IntRect> rooms, in WorldBounds worldView, IntRect? coverage = null,
            double? recordedDepth = null, bool supportedCoverage = false)
        {
            var selectedMap = animated.Sprites;
            var extent = new WorldBounds(position.X, position.Y, selectedMap.Columns * 8f, selectedMap.Rows * 8f);
            if (coverage.HasValue && ScenePresentationVisibility.IsFullyCovered(scope, HasPresentation,
                    supportedCoverage, captureCameraMatches, DroppedQuadCount, in worldView, in extent,
                    selectedMap.Columns, selectedMap.Rows, 8, 8, coverage.Value))
            {
                if (animatedGrids.TryGetValue(animated, out var retained)) retained.Seen = generation;
                return;
            }

            FlushImages();
            if (!animatedGrids.TryGetValue(animated, out var cache))
                animatedGrids.Add(animated, cache = new AnimatedGridCache());
            cache.Seen = generation;
            var map = animated.Sprites;
            var gridWorld = new WorldBounds(position.X, position.Y, map.Columns * 8f, map.Rows * 8f);
            var ranges = SelectSnapshotRanges(cache, scope, in worldView, rooms, in gridWorld,
                map.Columns, map.Rows, 8, 8);
            bool changed = cache.TexturePreparedGeneration == generation
                ? cache.TexturePreparedChanged
                : cache.Animation.Refresh(animated, ranges);
            if (changed) RefreshCellIndices(cache, null, cache.Animation);
            SetArchiveLayer(cache, recordedDepth,
                SourceSpritePresentationCapture.AnimatedTilesComponent);
            changed = RefreshMask(cache, scope, in worldView, in gridWorld, rooms,
                map.Columns, map.Rows, 8, 8, changed);
            if (changed)
            {
                cache.Sources.Clear();
                for (int i = 0; i < cache.Animation.Count; i++) cache.Sources.Add(cache.Animation[i].Texture);
                cache.Mesh.Rebuild(cache.Animation, cache.Mask.AsSpan(0, cache.CellCount));
                cache.TexturePreparedChanged = false;
            }

            foreach (var source in cache.Sources) livePages.Add(source);
            cache.Mesh.Draw(Matrix4x4.Translate(new Vector3(position.X, -position.Y, 0f)), resolve, coverage);
        }

        private void SetArchiveLayer(GridCache cache, double? depth, int kind)
        {
            int nextKind = canStitchArchive && depth.HasValue ? kind : 0;
            double nextDepth = depth.GetValueOrDefault();
            cache.LayerKind = nextKind;
            cache.LayerDepth = nextDepth;
        }

        private void DrawArchiveBefore(double depth, bool includeEqual, SceneTileScope scope,
            in WorldBounds view)
        {
            if (!canStitchArchive) return;
            SceneArchiveRangeBatch range = null;
            while (archiveLayerCursor < archive.Layers.Count)
            {
                var layer = archive.Layers[archiveLayerCursor];
                if (layer.Depth < depth || !includeEqual && layer.Depth == depth) break;
                if (layer.RefreshSelection(scope, in view))
                {
                    if (range == null)
                    {
                        if (archiveRangeCount == archiveRanges.Count) archiveRanges.Add(new SceneArchiveRangeBatch());
                        range = archiveRanges[archiveRangeCount++];
                        range.Begin();
                    }

                    range.Add(layer);
                }

                archiveLayerCursor++;
            }

            if (range != null)
            {
                if (TryQueueSmallArchiveRange(range)) return;
                range.Complete(pointAtlas, resolveNativeTexture, PointShader());
                FlushImages();
                range.Draw(resolve, pointAtlas, resolveNativeTexture, PointShader());
            }
        }

        private bool TryQueueSmallArchiveRange(SceneArchiveRangeBatch range)
        {
            if (!range.HasSmallPendingSelection(32)) return false;
            // Each appended image retains clamp addressing in the point-quad payload.
            // Live sampler changes and tile draws retain their original flush boundaries.
            if (pendingImages == null)
            {
                if (imageRunCount == imageRuns.Count) imageRuns.Add(new ImageRunCache(livePointImages: true));
                pendingImages = imageRuns[imageRunCount++];
                pendingImages.Next.Clear();
                pendingImages.PointAddress = 0;
            }

            range.AppendPendingSelection(pendingImages.Next);
            return true;
        }

        private bool HasRecordedTileLayer(double depth, int kind)
            => recordedTileLayers.Contains((depth, kind));

        private static bool CanStitchArchive(SourceSpritePresentationCapture capture)
        {
            var records = capture.CompletedQuads;
            using var canStitchArchiveScope = CanStitchArchiveMarker.Auto();
            if (capture.DroppedCount != 0) return false;
            double previousDepth = double.PositiveInfinity;
            for (int i = 0; i < capture.Count; i++)
            {
                ref readonly var record = ref records[i];
                if (record.Scope.ComponentKind == SourceSpritePresentationCapture.Debug) continue;
                if (!record.Scope.RenderPhase.HasDepth || record.State.SortMode != SourceSpriteSortMode.Deferred ||
                    double.IsNaN(record.Scope.RenderPhase.ActualDepth) ||
                    double.IsInfinity(record.Scope.RenderPhase.ActualDepth)) return false;
                if (record.Scope.RenderPhase.ActualDepth > previousDepth) return false;
                previousDepth = record.Scope.RenderPhase.ActualDepth;
            }

            for (int c = 0; c < capture.TileCoverageCount; c++)
            {
                var item = capture.GetTileCoverage(c);
                if (!item.Scope.RenderPhase.HasDepth || double.IsNaN(item.Scope.RenderPhase.ActualDepth) ||
                    double.IsInfinity(item.Scope.RenderPhase.ActualDepth)) return false;
            }

            return true;
        }

        private void DrawTilesBeforeDepth(double depth, IReadOnlyList<object> entities, SceneTileScope scope,
            ReadOnlySpan<IntRect> rooms, in WorldBounds worldView)
        {
            while (tileCoverageCursor < tileCoverageOrder.Count)
            {
                int c = tileCoverageOrder[tileCoverageCursor];
                if (tileCoverageDepths[c] <= depth) break;
                var item = presentation.GetTileCoverage(c);
                tileCoverageCursor++;
                int e = item.Scope.EntityIndex;
                int component = item.Scope.ComponentIndex;
                if (e < 0 || e >= entities.Count || component < 0) continue;
                DrawRemainingTiles(e, entities[e] as SceneEntityState, component, component + 1,
                    scope, rooms, in worldView);
            }
        }

        private bool RefreshArchiveMask(GridCache cache, in WorldBounds grid, int columns, int rows,
            int tileWidth, int tileHeight)
        {
            if (cache.HasArchiveMask && cache.ArchiveRevision == archive.Revision &&
                cache.ArchiveColumns == columns && cache.ArchiveRows == rows &&
                cache.ArchiveTileWidth == tileWidth && cache.ArchiveTileHeight == tileHeight &&
                cache.ArchiveDepth == cache.LayerDepth && cache.ArchiveKind == cache.LayerKind &&
                Same(in cache.ArchiveGrid, in grid)) return false;
            int length = cache.CellCount;
            if (cache.ArchiveMask.Length < length) cache.ArchiveMask = new byte[cache.CellIndices.Length];
            cache.ArchiveMask.AsSpan(0, length).Fill(1);
            if (cache.LayerKind != 0)
                archive.ExcludeTiles(cache.ArchiveMask.AsSpan(0, length), cache.CellIndices.AsSpan(0, length), in grid,
                    columns, rows, tileWidth,
                    tileHeight,
                    cache.LayerDepth, cache.LayerKind);
            cache.ArchiveGrid = grid;
            cache.ArchiveColumns = columns;
            cache.ArchiveRows = rows;
            cache.ArchiveTileWidth = tileWidth;
            cache.ArchiveTileHeight = tileHeight;
            cache.ArchiveDepth = cache.LayerDepth;
            cache.ArchiveKind = cache.LayerKind;
            cache.ArchiveRevision = archive.Revision;
            cache.HasArchiveMask = true;
            return true;
        }

        private int CompareTileCoverage(int a, int b)
        {
            int depth = tileCoverageDepths[b].CompareTo(tileCoverageDepths[a]);
            return depth != 0 ? depth : a.CompareTo(b);
        }

        private void PrepareTileCoverageOrder(SourceSpritePresentationCapture capture)
        {
            using var prepareTileCoverageOrderScope = PrepareTileCoverageOrderMarker.Auto();
            int count = capture.TileCoverageCount;
            if (tileCoverageDepths.Length < count) tileCoverageDepths = new double[count];
            if (tileCoverageKinds.Length < count) tileCoverageKinds = new int[count];
            bool changed = tileCoverageOrder.Count != count;
            for (int c = 0; c < count; c++)
            {
                var scope = capture.GetTileCoverage(c).Scope;
                double depth = scope.RenderPhase.ActualDepth;
                if (tileCoverageDepths[c] != depth || tileCoverageKinds[c] != scope.ComponentKind) changed = true;
                tileCoverageDepths[c] = depth;
                tileCoverageKinds[c] = scope.ComponentKind;
            }

            if (changed)
            {
                tileCoverageOrder.Clear();
                recordedTileLayers.Clear();
                for (int c = 0; c < count; c++)
                {
                    tileCoverageOrder.Add(c);
                    recordedTileLayers.Add((tileCoverageDepths[c], tileCoverageKinds[c]));
                }

                tileCoverageOrder.Sort(compareTileCoverage);
            }

            tileCoverageCursor = 0;
        }

        private void RefreshAtlas(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level)
        {
            var next = (level?.SourceRenderContext as ISourceGameAtlasRenderContext)?.GameAtlas;
            var sources = next?.Sources;
            long resourceRevision = VirtualTexture.ResourceRevision;
            bool changed = atlasRefreshRequired || !ReferenceEquals(atlas, next) ||
                           !ReferenceEquals(atlasSources, sources) || atlasResourceRevision != resourceRevision;
            if (!changed && sources != null)
            {
                var checkpoint = atlasSourcesCheckpoint;
                try
                {
                    checkpoint.MoveNext();
                }
                catch (InvalidOperationException)
                {
                    changed = true;
                }
            }

            if (!changed) return;
            var nextCheckpoint = sources?.GetEnumerator() ?? default;
            if (!atlasRefreshRequired) invalidArchivePages.Clear();
            atlasRefreshRequired = true;
            if (!ReferenceEquals(atlas, next)) archive.Dispose();
            else
                foreach (var resource in atlasPages)
                    invalidArchivePages.Add(resource);
            atlas = next;
            atlasPages.Clear();
            archiveReadyPages.Clear();
            if (sources != null)
                foreach (var owner in sources)
                {
                    var resource = owner?.Texture;
                    if (resource != null && !(resource is SourceRenderTarget2DResource) &&
                        !resource.IsDisposed && resource.Format == SourceSurfaceFormat.Color)
                    {
                        atlasPages.Add(resource);
                        invalidArchivePages.Remove(resource);
                    }
                }

            archive.Invalidate(invalidArchivePages);
            invalidArchivePages.Clear();
            atlasSources = sources;
            atlasSourcesCheckpoint = nextCheckpoint;
            atlasResourceRevision = resourceRevision;
            atlasRefreshRequired = false;
        }

        private void InvalidateArchivePages()
        {
            invalidArchivePages.Clear();
            foreach (var pair in pages)
                if (pair.Key.IsDisposed || pair.Value.Native != null && !pair.Value.Native.IsCurrent ||
                    pair.Value.Managed != null && !pair.Value.Managed.IsCurrent)
                    invalidArchivePages.Add(pair.Key);
            archive.Invalidate(invalidArchivePages);
            foreach (var source in invalidArchivePages)
            {
                archiveReadyPages.Remove(source);
                Release(pages[source]);
                pages.Remove(source);
            }

            invalidArchivePages.Clear();
        }

        private bool SupportsArchiveQuad(in SourceSpritePresentationQuad record, bool stateSupported)
        {
            // ConsumeArchive already requires CanStitchArchive for this immutable completed capture.
            // Keep every live membership/disposal read in its original short-circuit order.
            return stateSupported && atlasPages.Contains(record.Texture) &&
                   !record.Texture.IsDisposed;
        }

        private bool PreflightArchiveQuad(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            bool supported, in System.Numerics.Matrix4x4 camera, in WorldBounds roomWorld,
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry, int recordIndex)
        {
            if (record.IsPrimitiveTrianglePair)
                return PreflightArchivePair(in record, in quad, supported, in camera, in roomWorld, geometry,
                    recordIndex);
            bool unitUv = geometry.IsEmpty
                ? AtlasUv(quad.TextureCoordinate0) && AtlasUv(quad.TextureCoordinate1) &&
                  AtlasUv(quad.TextureCoordinate2) && AtlasUv(quad.TextureCoordinate3)
                : (geometry[recordIndex].AtlasUnitUv & 1) != 0;
            archiveUnitUv[recordIndex] = unitUv ? (byte)1 : (byte)0;
            supported = supported && unitUv;
            ref readonly var owner = ref record.Scope;
            if ((owner.ComponentKind == SourceSpritePresentationCapture.TileGridComponent ||
                 owner.ComponentKind == SourceSpritePresentationCapture.AnimatedTilesComponent) &&
                archiveCoverageIndices.TryGetValue((owner.EntityIndex, owner.ComponentIndex, owner.ComponentKind),
                    out int coverageIndex) && coverageIndex >= 0 && archiveTileEligibility[coverageIndex])
            {
                var bounds = geometry.IsEmpty ? SceneObservedRoomArchive.Bounds(in quad) : geometry[recordIndex].Bounds;
                if (!supported || !record.State.Transform.Equals(camera) ||
                    !ArchiveContained(in bounds, in roomWorld))
                    archiveTileEligibility[coverageIndex] = false;
            }

            return !supported || !archiveReadyPages.Add(record.Texture) || Resolve(record.Texture) != null;
        }

        private bool PreflightArchivePair(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            bool supported, in System.Numerics.Matrix4x4 camera, in WorldBounds roomWorld,
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry, int recordIndex)
        {
            archiveUnitUv[recordIndex] = 0;
            ref readonly var owner = ref record.Scope;
            bool tile = owner.ComponentKind == SourceSpritePresentationCapture.TileGridComponent ||
                        owner.ComponentKind == SourceSpritePresentationCapture.AnimatedTilesComponent;
            for (int triangle = 0; triangle < 2; triangle++)
            {
                if (triangle != 0 && supported) supported = SupportsArchiveQuad(in record, true);
                var uv0 = triangle == 0 ? quad.TextureCoordinate0 : quad.TextureCoordinate2;
                var uv2 = triangle == 0 ? quad.TextureCoordinate2 : quad.TextureCoordinate3;
                bool unitUv = geometry.IsEmpty
                    ? AtlasUv(uv0) && AtlasUv(quad.TextureCoordinate1) && AtlasUv(uv2) && AtlasUv(uv2)
                    : (geometry[recordIndex].AtlasUnitUv & (1 << triangle)) != 0;
                if (unitUv) archiveUnitUv[recordIndex] |= (byte)(1 << triangle);
                bool triangleSupported = supported && unitUv;
                if (tile && archiveCoverageIndices.TryGetValue(
                        (owner.EntityIndex, owner.ComponentIndex, owner.ComponentKind), out int c) &&
                    c >= 0 && archiveTileEligibility[c])
                {
                    var bounds = ScenePresentationMeshQuad.TriangleBounds(in quad, triangle != 0);
                    if (!triangleSupported || !record.State.Transform.Equals(camera) ||
                        !ArchiveContained(in bounds, in roomWorld)) archiveTileEligibility[c] = false;
                }

                if (triangleSupported && archiveReadyPages.Add(record.Texture) && Resolve(record.Texture) == null)
                    return false;
            }

            return true;
        }

        private static bool ArchiveContained(in WorldBounds bounds, in WorldBounds roomWorld) =>
            !bounds.Intersects(roomWorld) || !(bounds.X < roomWorld.X || bounds.Y < roomWorld.Y ||
                                               bounds.Right > roomWorld.Right || bounds.Bottom > roomWorld.Bottom);

        private void CommitArchiveQuad(in SourceSpritePresentationQuad record, in SourceSpriteQuad quad,
            bool tile, int recordIndex, int ordinal, in WorldBounds roomWorld,
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry)
        {
            byte triangles = archiveUnitUv[recordIndex];
            if (record.IsPrimitiveTrianglePair)
            {
                bool secondSupported = SupportsArchiveQuad(in record, true);
                UnsupportedArchiveQuadCount += (triangles & 1) == 0 ? 1 : 0;
                if (!secondSupported)
                {
                    UnsupportedArchiveQuadCount++;
                    triangles &= 1;
                }
                else if ((triangles & 2) == 0) UnsupportedArchiveQuadCount++;
            }
            else if (triangles == 0) UnsupportedArchiveQuadCount++;

            if (triangles == 0)
            {
                return;
            }

            ref readonly var owner = ref record.Scope;
            if (tile)
            {
                bool covered = archiveCoverageIndices.TryGetValue(
                                   (owner.EntityIndex, owner.ComponentIndex, owner.ComponentKind), out int c) &&
                               c >= 0 && archiveTileEligibility[c];
                if (!covered) return;
                if (record.IsPrimitiveTrianglePair)
                {
                    var first = ScenePresentationMeshQuad.TriangleBounds(in quad, false);
                    var second = ScenePresentationMeshQuad.TriangleBounds(in quad, true);
                    if (first.X < roomWorld.X || first.Y < roomWorld.Y || first.Right > roomWorld.Right ||
                        first.Bottom > roomWorld.Bottom) triangles &= 2;
                    if (second.X < roomWorld.X || second.Y < roomWorld.Y || second.Right > roomWorld.Right ||
                        second.Bottom > roomWorld.Bottom) triangles &= 1;
                    if (triangles == 0) return;
                }
                else
                {
                    var bounds = geometry.IsEmpty
                        ? SceneObservedRoomArchive.Bounds(in quad)
                        : geometry[recordIndex].Bounds;
                    if (bounds.X < roomWorld.X || bounds.Y < roomWorld.Y ||
                        bounds.Right > roomWorld.Right || bounds.Bottom > roomWorld.Bottom) return;
                }
            }

            archive.Add(record.Texture, in quad,
                owner.RenderPhase.ActualDepth, ordinal + (record.IsPrimitiveTrianglePair && triangles == 2 ? 1 : 0),
                owner.HasSourceId, owner.SourceId, tile ? owner.ComponentKind : 0,
                record.IsPrimitiveTrianglePair ? triangles : (byte)0);
        }

        private void PrepareStaticCoverage(SourceSpritePresentationCapture capture)
        {
            var records = capture.CompletedQuads;
            var camera = capture.CameraTransform;
            using var prepareStaticCoverageScope = PrepareStaticCoverageMarker.Auto();
            PrepareArchiveCoverage(capture);
            for (int i = 0; i < capture.Count; i++)
            {
                ref readonly var record = ref records[i];
                ref readonly var owner = ref record.Scope;
                if (owner.ComponentKind != SourceSpritePresentationCapture.TileGridComponent &&
                    owner.ComponentKind != SourceSpritePresentationCapture.AnimatedTilesComponent) continue;
                if (!archiveCoverageIndices.TryGetValue((owner.EntityIndex, owner.ComponentIndex, owner.ComponentKind),
                        out int c) || c < 0 || !archiveTileEligibility[c]) continue;
                ref readonly var quad = ref record.Quad;
                if (!SupportsState(in record, capture.Target) || record.Texture == null || record.Texture.IsDisposed ||
                    record.Texture.Format != SourceSurfaceFormat.Color ||
                    !record.State.Transform.Equals(camera) ||
                    !AtlasUv(quad.TextureCoordinate0) || !AtlasUv(quad.TextureCoordinate1) ||
                    !AtlasUv(quad.TextureCoordinate2) || !AtlasUv(quad.TextureCoordinate3))
                    archiveTileEligibility[c] = false;
            }
        }

        private void PrepareArchiveCoverage(SourceSpritePresentationCapture capture)
        {
            int count = capture.TileCoverageCount;
            if (archiveTileEligibility.Length < count) archiveTileEligibility = new bool[count];
            if (archiveCoverageKeys.Length < count) archiveCoverageKeys = new (int, int, int)[count];
            bool changed = archiveCoverageKeyCount != count;
            for (int c = 0; c < count; c++)
            {
                var owner = capture.GetTileCoverage(c).Scope;
                var key = (owner.EntityIndex, owner.ComponentIndex, owner.ComponentKind);
                if (archiveCoverageKeys[c] != key) changed = true;
                archiveCoverageKeys[c] = key;
            }

            if (changed)
            {
                archiveCoverageIndices.Clear();
                for (int c = 0; c < count; c++)
                    if (!archiveCoverageIndices.TryAdd(archiveCoverageKeys[c], c))
                        archiveCoverageIndices[archiveCoverageKeys[c]] = -1;
                archiveCoverageKeyCount = count;
            }

            for (int c = 0; c < count; c++)
            {
                var item = capture.GetTileCoverage(c);
                archiveTileEligibility[c] = archiveCoverageIndices[archiveCoverageKeys[c]] == c &&
                                            item.Scope.RenderPhase.HasDepth &&
                                            !double.IsNaN(item.Scope.RenderPhase.ActualDepth) &&
                                            !double.IsInfinity(item.Scope.RenderPhase.ActualDepth) &&
                                            item.TileWidth > 0 &&
                                            item.TileHeight > 0;
            }
        }

        private void ConsumeArchive(SourceSpritePresentationCapture capture,
            Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level, bool canStitch, bool hasInverse,
            in System.Numerics.Matrix4x4 inverse, ReadOnlySpan<bool> supportedStates,
            ReadOnlySpan<SceneCaptureGeometryWorkers.Result> geometry)
        {
            var records = capture.CompletedQuads;
            using var consumeArchiveScope = ConsumeArchiveMarker.Auto();
            if (!(level?.Session?.LevelData is Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData roomData) ||
                string.IsNullOrEmpty(capture.Room)) return;
            var room = roomData.Bounds;
            if (!canStitch || !hasInverse)
            {
                archive.DiscardConsumed(capture.Room, room, capture.Revision);
                return;
            }

            if (ReferenceEquals(archiveCapture, capture) && archive.CurrentRoom == capture.Room &&
                archive.ConsumedRevision == capture.Revision &&
                archive.HasValidConsumedFrame) return;
            var camera = capture.CameraTransform;
            var roomWorld = new WorldBounds(room.X, room.Y, room.Width, room.Height);
            // Resolve before changing the reusable builder; no pending GPU resources enter the archive.
            using (ConsumeArchivePreflightMarker.Auto())
            {
                archiveReadyPages.Clear();
                PrepareArchiveCoverage(capture);
                if (archiveUnitUv.Length < capture.Count)
                    Array.Resize(ref archiveUnitUv, capture.Count);
                for (int i = 0; i < capture.Count; i++)
                {
                    ref readonly var record = ref records[i];
                    if (record.Scope.ComponentKind == SourceSpritePresentationCapture.Debug) continue;
                    bool supported = SupportsArchiveQuad(in record, supportedStates[i]);
                    bool ready;
                    if (supported && !geometry.IsEmpty)
                        ready = PreflightArchiveQuad(in record, in geometry[i].Quad, true, in camera, in roomWorld,
                            geometry, i);
                    else if (!supported || record.State.Transform.Equals(camera))
                        ready = PreflightArchiveQuad(in record, in record.Quad, supported, in camera, in roomWorld,
                            geometry, i);
                    else
                    {
                        var quad = TransformWorldQuad(in record.Quad, in record.State.Transform, in inverse);
                        ready = PreflightArchiveQuad(in record, in quad, true, in camera, in roomWorld, geometry, i);
                    }

                    if (!ready)
                    {
                        archive.DiscardConsumed(capture.Room, room, capture.Revision);
                        return;
                    }
                }
            }

            using var commitScope = ConsumeArchiveCommitMarker.Auto();
            if (!ReferenceEquals(archiveCapture, capture))
                archive.DiscardConsumed(capture.Room, room, capture.Revision);
            if (!archive.BeginConsumed(capture.Room, room, capture.Revision)) return;
            archiveCapture = capture;
            UnmappedArchiveQuadCount = 0;
            UnsupportedArchiveQuadCount = 0;
            int ordinal = 0;
            for (int i = 0; i < capture.Count; ordinal += records[i].IsPrimitiveTrianglePair ? 2 : 1, i++)
            {
                ref readonly var record = ref records[i];
                ref readonly var owner = ref record.Scope;
                if (owner.ComponentKind == SourceSpritePresentationCapture.Debug) continue;
                bool tile = owner.ComponentKind == SourceSpritePresentationCapture.TileGridComponent ||
                            owner.ComponentKind == SourceSpritePresentationCapture.AnimatedTilesComponent;
                if (!tile && (owner.Room == null || owner.Room != capture.Room))
                {
                    UnmappedArchiveQuadCount += record.IsPrimitiveTrianglePair ? 2 : 1;
                    continue;
                }

                if (!SupportsArchiveQuad(in record, supportedStates[i]))
                {
                    UnsupportedArchiveQuadCount += record.IsPrimitiveTrianglePair ? 2 : 1;
                    continue;
                }

                if (!geometry.IsEmpty)
                    CommitArchiveQuad(in record, in geometry[i].Quad, tile, i, ordinal, in roomWorld, geometry);
                else if (record.State.Transform.Equals(camera))
                    CommitArchiveQuad(in record, in record.Quad, tile, i, ordinal, in roomWorld, geometry);
                else
                {
                    var quad = TransformWorldQuad(in record.Quad, in record.State.Transform, in inverse);
                    CommitArchiveQuad(in record, in quad, tile, i, ordinal, in roomWorld, geometry);
                }
            }

            for (int c = 0; c < capture.TileCoverageCount; c++)
            {
                var item = capture.GetTileCoverage(c);
                if (!archiveTileEligibility[c]) continue;
                var tiles = item.Tiles;
                float left = Math.Max(roomWorld.X, item.WorldPosition.X + (float)tiles.X * item.TileWidth);
                float top = Math.Max(roomWorld.Y, item.WorldPosition.Y + (float)tiles.Y * item.TileHeight);
                float right = Math.Min(roomWorld.Right,
                    item.WorldPosition.X + ((float)tiles.X + tiles.Width) * item.TileWidth);
                float bottom = Math.Min(roomWorld.Bottom,
                    item.WorldPosition.Y + ((float)tiles.Y + tiles.Height) * item.TileHeight);
                if (right <= left || bottom <= top) continue;
                var area = new WorldBounds(left, top, right - left, bottom - top);
                archive.Add(new SceneObservedRoomArchive.TileArea(item.Scope.RenderPhase.ActualDepth,
                    item.Scope.ComponentKind, in area));
            }
        }


        private static bool Same(in WorldBounds a, in WorldBounds b) => a.X.Equals(b.X) && a.Y.Equals(b.Y) &&
                                                                        a.Width.Equals(b.Width) &&
                                                                        a.Height.Equals(b.Height) &&
                                                                        a.Right.Equals(b.Right) &&
                                                                        a.Bottom.Equals(b.Bottom);

        private Shader PointShader()
        {
            if (!shader) shader = Shader.Find("Hidden/Celeste/SceneTileGrid");
            if (!shader) throw new InvalidOperationException("SceneTileGrid Editor shader is not imported.");
            return shader;
        }

        private Texture ResolveGpuTexture(SourceTexture2DResource source)
        {
            if ((!SceneDirect3D11TexturePage.UsesNativeTextureIO(source) &&
                 !SceneManagedTexturePage.UsesManagedTextureIO(source) && !UsesNativeTextureExport(source)) ||
                !Resolve(source)) return null;
            return pages.TryGetValue(source, out var page) ? page.Texture : null;
        }

        private Material Resolve(SourceTexture2DResource source)
        {
            using var resolveScope = ResolveMarker.Auto();
            if (source.IsDisposed ||
                source.Format != SourceSurfaceFormat.Color)
            {
                UnsupportedImageTextureCount++;
                return null;
            }

            if (pages.TryGetValue(source, out var page))
            {
                if ((page.Native == null || page.Native.IsCurrent) &&
                    (page.Managed == null || page.Managed.IsCurrent))
                {
                    if (page.Mirrored) nativeTextures.GetPrepared(source, nativeCapture);
                    return page.Material;
                }

                Release(page);
                pages.Remove(source);
            }

            PointShader();
            if (UsesNativeTextureExport(source))
            {
                var mirrorTexture = nativeTextures.GetPrepared(source, nativeCapture);
                var mirrorMaterial = new Material(shader)
                    { hideFlags = HideFlags.HideAndDontSave, mainTexture = mirrorTexture };
                mirrorMaterial.SetFloat("_SourceTextureOriginTop", 1f);
                pages.Add(source, new Page { Texture = mirrorTexture, Material = mirrorMaterial, Mirrored = true });
                return mirrorMaterial;
            }

            if (SceneDirect3D11TexturePage.UsesNativeTextureIO(source))
            {
                if (!SceneDirect3D11TexturePage.TryCreate(source, out var native))
                {
                    UnsupportedImageTextureCount++;
                    return null;
                }

                Material nativeMaterial = null;
                try
                {
                    nativeMaterial = new Material(shader)
                        { hideFlags = HideFlags.HideAndDontSave, mainTexture = native.Texture };
                    nativeMaterial.SetFloat("_SourceTextureOriginTop", 1f);
                    pages.Add(source, new Page
                        { Texture = native.Texture, Material = nativeMaterial, Native = native });
                    return nativeMaterial;
                }
                catch
                {
                    if (nativeMaterial) UnityEngine.Object.DestroyImmediate(nativeMaterial);
                    native.Dispose();
                    throw;
                }
            }

            if (SceneManagedTexturePage.TryCreate(source, out var managed))
            {
                var managedMaterial = new Material(shader)
                    { hideFlags = HideFlags.HideAndDontSave, mainTexture = managed.Texture };
                managedMaterial.SetFloat("_SourceTextureOriginTop", 1f);
                pages.Add(source, new Page
                    { Texture = managed.Texture, Material = managedMaterial, Managed = managed });
                return managedMaterial;
            }

            // Other ordinary textures have only a static snapshot contract here.
            if (source is SourceRenderTarget2DResource)
            {
                UnsupportedImageTextureCount++;
                return null;
            }

            var pixels = new PackedColor[checked(source.Width * source.Height)];
            source.GetData(pixels);
            var output = new Color32[pixels.Length];
            for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var c = pixels[y * source.Width + x];
                output[(source.Height - 1 - y) * source.Width + x] = new Color32(c.R, c.G, c.B, c.A);
            }

            var texture = new Texture2D(source.Width, source.Height, TextureFormat.RGBA32, false, true)
            {
                name = "Scene tile page", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Material material = null;
            try
            {
                texture.SetPixels32(output);
                texture.Apply(false, true);
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
                page = new Page { Texture = texture, Material = material };
                pages.Add(source, page);
                return material;
            }
            catch
            {
                if (material) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private void AttachGeometryCapture(SourceSpritePresentationCapture capture)
        {
            if (ReferenceEquals(geometryCapture, capture)) return;
            DetachGeometryCapture();
            geometryCapture = capture;
            capture.GeometryFrameChanging += geometryWorkers.Wait;
            capture.GeometryFrameCompleted += StartCompletedGeometry;
        }

        private void DetachGeometryCapture()
        {
            if (geometryCapture == null) return;
            geometryWorkers.Wait();
            geometryCapture.GeometryFrameCompleted -= StartCompletedGeometry;
            geometryCapture.GeometryFrameChanging -= geometryWorkers.Wait;
            geometryCapture = null;
        }

        // Called on the producer/Unity owner thread; workers receive only completed numeric capture values.
        private void StartCompletedGeometry()
        {
            var capture = geometryCapture;
            if (capture == null || capture.Count < SceneCaptureGeometryWorkers.MinimumQuadCount) return;
            var camera = capture.CameraTransform;
            if (System.Numerics.Matrix4x4.Invert(camera, out var inverse))
                geometryWorkers.Start(capture, camera, inverse);
        }

        private static void Release(Page page)
        {
            UnityEngine.Object.DestroyImmediate(page.Material);
            if (page.Native != null) page.Native.Dispose();
            else if (page.Managed == null && !page.Mirrored) UnityEngine.Object.DestroyImmediate(page.Texture);
        }

        public void Dispose()
        {
            DetachGeometryCapture();
            geometryWorkers?.Dispose();
            geometryWorkers = null;
            LastCompletedSceneDrawScene = null;
            ReleaseCapture();
            retainedBatch = null;
            ClearMeshes();
        }

        private void ClearMeshes(bool preserveArchive = false)
        {
            tileCoverageOwner = null;
            tileCoverageEntityCount = 0;
            tileCoverageCount = 0;
            pointAtlas.Dispose();
            if (!preserveArchive)
            {
                nativeTextures?.Dispose();
                nativeTextures = null;
                nativeTextureDevice = null;
            }

            archiveCapture = null;
            visibility.Clear();
            staticCoveragePrepared = false;
            captureCameraMatches = false;
            if (!preserveArchive)
            {
                archive.Dispose();
                atlas = null;
                mapIdentity = null;
                atlasSources = null;
                atlasSourcesCheckpoint = default;
                atlasResourceRevision = 0;
                atlasRefreshRequired = false;
                invalidArchivePages.Clear();
                atlasPages.Clear();
                archiveReadyPages.Clear();
                archiveCoverageIndices.Clear();
                archiveCoverageKeyCount = 0;
                tileCoverageOrder.Clear();
                recordedTileLayers.Clear();
            }

            foreach (var range in archiveRanges) range.Dispose();
            archiveRanges.Clear();
            archiveRangeCount = activeArchiveRangeCount = 0;
            cameraLayerImages.Mesh.Dispose();
            cameraLayerImages.Last.Clear();
            cameraLayerImages.Next.Clear();
            cameraLayerImages.RebuildRequired = false;
            differenceMesh.Dispose();
            differenceTexture = null;
            differenceQuad[0] = default;
            if (differenceMaterial) UnityEngine.Object.DestroyImmediate(differenceMaterial);
            differenceMaterial = null;
            foregroundMesh.Dispose();
            foregroundTexture = null;
            foregroundQuad[0] = default;
            if (foregroundMaterial) UnityEngine.Object.DestroyImmediate(foregroundMaterial);
            foregroundMaterial = null;
            backgroundMesh.Dispose();
            backgroundTexture = null;
            backgroundQuad[0] = default;
            foreach (var cache in imageRuns) cache.Mesh.Dispose();
            imageRuns.Clear();
            pendingImages = null;
            imageRunCount = activeImageRunCount = 0;
            foreach (var cache in grids.Values) cache.Mesh.Dispose();
            grids.Clear();
            foreach (var cache in animatedGrids.Values) cache.Mesh.Dispose();
            animatedGrids.Clear();
            removedAnimations.Clear();
            if (!preserveArchive)
            {
                foreach (var page in pages.Values) Release(page);
                pages.Clear();
            }

            livePages.Clear();
            removedGrids.Clear();
            removedPages.Clear();
            knownRooms = Array.Empty<IntRect>();
            scene = null;
            presentation = null;
        }
    }
}
