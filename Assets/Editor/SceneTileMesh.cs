using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Celeste.Graphics.Scene.Diagnostics;
using Celeste.Graphics.Rendering.Batches;
using Celeste.Graphics.Rendering.Textures;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Editor
{
    internal sealed class SceneTileMesh : IDisposable
    {
        private static readonly int SourcePointAddress = Shader.PropertyToID("_SourcePointAddress");
        private static readonly int SceneQuads = Shader.PropertyToID("_SceneQuads");
        private static readonly int SceneWorld = Shader.PropertyToID("_SceneWorld");

        private static readonly ProfilerMarker DrawMarker = new("Celeste.Scene.SceneTileMesh.Draw");
        private static readonly ProfilerMarker FlushMarker = new("Celeste.Scene.SceneTileMesh.Flush");
        private static readonly ProfilerMarker RebuildTilesMarker = new("Celeste.Scene.SceneTileMesh.RebuildTiles");

        private static readonly ProfilerMarker RebuildAnimatedMarker =
            new("Celeste.Scene.SceneTileMesh.RebuildAnimated");

        private static readonly ProfilerMarker RebuildQuadsMarker = new("Celeste.Scene.SceneTileMesh.RebuildQuads");

        [StructLayout(LayoutKind.Sequential)]
        private struct SceneVertex
        {
            internal Vector3 Position;
            internal Color32 Color;
            internal Vector2 Uv;
            internal Vector2 Cell;
            internal Vector4 AtlasRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScenePointQuad
        {
            internal SourceSpriteQuad Quad;
            internal Vector2 Cell;
            internal Vector4 AtlasRect;
            internal ulong Padding;
        }

        private static readonly VertexAttributeDescriptor[] VertexLayout =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
            new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4)
        };

        private sealed class Batch
        {
            internal SourceTexture2DResource Texture;
            internal bool UsesPointAtlas;
            internal SceneGpuTextureAtlas.Page AtlasPage;
            internal readonly List<SourceTexture2DResource> AtlasSources = new();
            internal readonly List<SceneGpuTextureAtlas.Slot> AtlasSourceSlots = new();
            internal readonly List<SceneGpuTextureAtlas.Slot> AtlasSlots = new();
            internal ulong AtlasDeviceGeneration;
            internal Mesh Mesh;
            internal GraphicsBuffer PointQuads;
            internal int PointQuadCapacity;
            internal int VertexCount;
            internal int ImageStart, ImageCount;
            internal int InvalidQuadCount;
            internal bool ImageContentValid;
        }

        private sealed class TextureGroup
        {
            internal SourceTexture2DResource Texture;
            internal readonly List<int> Cells = new();
        }

        private readonly Dictionary<SourceTexture2DResource, int> textureGroupIndices = new();
        private readonly List<TextureGroup> textureGroups = new();
        private int activeTextureGroupCount;
        private readonly List<Batch> batches = new();
        private NativeArray<SceneVertex> vertices;
        private unsafe SceneVertex* vertexData;
        private NativeArray<ScenePointQuad> pointQuads;
        private unsafe ScenePointQuad* pointQuadData;
        private int vertexCount;
        private readonly List<SourceTexture2DResource> pendingAtlasSources = new();
        private readonly List<SceneGpuTextureAtlas.Slot> pendingAtlasSourceSlots = new();
        private readonly HashSet<SourceTexture2DResource> pendingAtlasSourceSet = new();
        private readonly List<SceneGpuTextureAtlas.Slot> atlasSlotScratch = new();
        private readonly List<bool> dirtyAtlasChunks = new();
        private const int DirtyAtlasChunkQuads = 64;
        private static readonly int ExcludeTiles = Shader.PropertyToID("_ExcludeTiles");
        private static readonly int ExcludedTileRange = Shader.PropertyToID("_ExcludedTileRange");

        private readonly List<int> indices = new();
        internal readonly TileGridGeometrySnapshot Snapshot = new();
        private readonly bool dynamicGeometry;
        private readonly bool proceduralImages;
        private int rebuildBatch, activeBatchCount;
        private int pendingInvalidQuadCount;
        internal int InvalidQuadCount { get; private set; }
        internal bool HasGeometry => activeBatchCount != 0;

        internal SceneTileMesh(bool dynamicGeometry = false, bool proceduralImages = false)
        {
            this.dynamicGeometry = dynamicGeometry;
            this.proceduralImages = proceduralImages;
        }

        internal void Rebuild(ReadOnlySpan<byte> mask)
        {
            using var rebuildScope = RebuildTilesMarker.Auto();
            rebuildBatch = 0;
            InvalidQuadCount = 0;
            pendingInvalidQuadCount = 0;
            if (PrepareTextureGroups(mask))
            {
                for (int groupIndex = 0; groupIndex < activeTextureGroupCount; groupIndex++)
                {
                    var group = textureGroups[groupIndex];
                    foreach (int cellIndex in group.Cells)
                    {
                        var cell = Snapshot[cellIndex];
                        Add(cell.Quad, new Vector2(cell.CellIndex / Snapshot.Rows, cell.CellIndex % Snapshot.Rows),
                            positionsValidated: true);
                    }

                    Flush(group.Texture);
                }

                TrimBatches();
                return;
            }

            SourceTexture2DResource current = null;
            for (int i = 0; i < Snapshot.Count; i++)
            {
                var cell = Snapshot[i];
                if (mask[i] == 0) continue;
                if (!ReferenceEquals(current, cell.Texture))
                {
                    Flush(current);
                    current = cell.Texture;
                }

                Add(cell.Quad, new Vector2(cell.CellIndex / Snapshot.Rows, cell.CellIndex % Snapshot.Rows));
            }

            Flush(current);
            TrimBatches();
        }

        private bool PrepareTextureGroups(ReadOnlySpan<byte> mask)
        {
            textureGroupIndices.Clear();
            for (int i = 0; i < activeTextureGroupCount; i++)
            {
                textureGroups[i].Texture = null;
                textureGroups[i].Cells.Clear();
            }

            activeTextureGroupCount = 0;
            for (int i = 0; i < Snapshot.Count; i++)
            {
                var cell = Snapshot[i];
                if (mask[i] != 0 && !FitsOwnCell(in cell.Quad, cell.CellIndex,
                        Snapshot.Rows, Snapshot.TileWidth, Snapshot.TileHeight)) return false;
            }

            for (int i = 0; i < Snapshot.Count; i++)
            {
                if (mask[i] == 0) continue;
                var texture = Snapshot[i].Texture;
                if (!textureGroupIndices.TryGetValue(texture, out int groupIndex))
                {
                    groupIndex = activeTextureGroupCount++;
                    if (groupIndex == textureGroups.Count) textureGroups.Add(new TextureGroup());
                    var group = textureGroups[groupIndex];
                    group.Texture = texture;
                    textureGroupIndices.Add(texture, groupIndex);
                }

                textureGroups[groupIndex].Cells.Add(i);
            }

            return true;
        }

        private static bool FitsOwnCell(in SourceSpriteQuad q, long cellIndex, int rows, int tileWidth,
            int tileHeight)
        {
            if (rows <= 0 || tileWidth <= 0 || tileHeight <= 0 || cellIndex < 0) return false;
            long leftValue = (long)(cellIndex / rows) * tileWidth;
            long topValue = (long)(cellIndex % rows) * tileHeight;
            double left = leftValue, top = topValue, right = leftValue + tileWidth, bottom = topValue + tileHeight;
            // Ordered coordinates inside finite cell bounds imply finite X/Y; Z must be zero.
            return q.Position0.Z == 0f && q.Position1.Z == 0f && q.Position2.Z == 0f && q.Position3.Z == 0f &&
                   q.Position0.X == q.Position2.X && q.Position1.X == q.Position3.X &&
                   q.Position0.Y == q.Position1.Y && q.Position2.Y == q.Position3.Y &&
                   q.Position0.X < q.Position1.X && q.Position0.Y < q.Position2.Y &&
                   q.Position0.X >= left && q.Position1.X <= right &&
                   q.Position0.Y >= top && q.Position2.Y <= bottom;
        }

        private static bool Finite(System.Numerics.Vector3 value) =>
            (BitConverter.SingleToInt32Bits(value.X) & 0x7f800000) != 0x7f800000 &&
            (BitConverter.SingleToInt32Bits(value.Y) & 0x7f800000) != 0x7f800000 &&
            (BitConverter.SingleToInt32Bits(value.Z) & 0x7f800000) != 0x7f800000;

        internal void Rebuild(AnimatedTileGeometrySnapshot snapshot, ReadOnlySpan<byte> mask)
        {
            using var rebuildScope = RebuildAnimatedMarker.Auto();
            rebuildBatch = 0;
            InvalidQuadCount = 0;
            pendingInvalidQuadCount = 0;
            SourceTexture2DResource current = null;
            for (int i = 0; i < snapshot.Count; i++)
            {
                var cell = snapshot[i];
                if (mask[i] == 0) continue;
                if (!ReferenceEquals(current, cell.Texture))
                {
                    Flush(current);
                    current = cell.Texture;
                }

                Add(cell.Quad, new Vector2(cell.CellIndex / snapshot.Rows, cell.CellIndex % snapshot.Rows));
            }

            Flush(current);
            TrimBatches();
        }

        internal void Rebuild(ScenePresentationMeshQuadBuffer images,
            ScenePresentationMeshQuadBuffer previous = null,
            SceneGpuTextureAtlas pointAtlas = null,
            Func<SourceTexture2DResource, Texture2D> resolveNative = null, Shader pointShader = null)
            => Rebuild(images.AsSpan(), previous == null ? default : previous.AsSpan(),
                pointAtlas, resolveNative, pointShader);

        internal void Rebuild(ReadOnlySpan<ScenePresentationMeshQuad> images,
            ReadOnlySpan<ScenePresentationMeshQuad> previous = default,
            SceneGpuTextureAtlas pointAtlas = null,
            Func<SourceTexture2DResource, Texture2D> resolveNative = null, Shader pointShader = null)
        {
            using var rebuildScope = RebuildQuadsMarker.Auto();
            rebuildBatch = 0;
            InvalidQuadCount = 0;
            pendingInvalidQuadCount = 0;
            for (int start = 0; start < images.Length;)
            {
                var texture = images[start].Texture;
                if (pointAtlas != null &&
                    pointAtlas.TryPrepare(texture, resolveNative, pointShader, out var firstSlot))
                {
                    int atlasEnd = start;
                    var lastSource = texture;
                    var lastSlot = firstSlot;
                    atlasSlotScratch.Clear();
                    while (atlasEnd < images.Length)
                    {
                        var source = images[atlasEnd].Texture;
                        SceneGpuTextureAtlas.Slot slot;
                        bool sourceChanged = !ReferenceEquals(source, lastSource);
                        if (!sourceChanged) slot = lastSlot;
                        else if (!pointAtlas.TryPrepare(source, resolveNative, pointShader, out slot, firstSlot.Page))
                            break;
                        if (!ReferenceEquals(slot.Page, firstSlot.Page)) break;
                        if ((atlasEnd == start || sourceChanged) && pendingAtlasSourceSet.Add(source))
                        {
                            pendingAtlasSources.Add(source);
                            pendingAtlasSourceSlots.Add(slot);
                        }

                        atlasSlotScratch.Add(slot);
                        lastSource = source;
                        lastSlot = slot;
                        atlasEnd++;
                    }

                    int atlasCount = atlasEnd - start;
                    int batchIndex = rebuildBatch;
                    if (UpdateAtlasBatch(images, previous, start, atlasCount, pointAtlas.DeviceGeneration) ||
                        ReuseAtlasBatch(images, previous, start, atlasCount, pointAtlas.DeviceGeneration))
                    {
                        rebuildBatch++;
                        pendingAtlasSources.Clear();
                        pendingAtlasSourceSlots.Clear();
                        pendingAtlasSourceSet.Clear();
                    }
                    else
                    {
                        for (int i = start; i < atlasEnd; i++)
                            AddImage(in images[i], atlasSlotScratch[i - start].Rect);
                        Flush(null, true, pointAtlas.DeviceGeneration, firstSlot.Page);
                    }

                    if (rebuildBatch > batchIndex)
                    {
                        var atlasBatch = batches[batchIndex];
                        atlasBatch.ImageStart = start;
                        atlasBatch.ImageCount = atlasCount;
                        atlasBatch.ImageContentValid = true;
                    }

                    atlasSlotScratch.Clear();
                    start = atlasEnd;
                    continue;
                }

                int end = start + 1;
                while (end < images.Length && ReferenceEquals(texture, images[end].Texture)) end++;
                int count = end - start;
                if (ReuseImageBatch(images, previous, start, count, texture)) rebuildBatch++;
                else
                {
                    int batchIndex = rebuildBatch;
                    for (int i = start; i < end; i++) AddImage(in images[i]);
                    Flush(texture);
                    if (rebuildBatch > batchIndex)
                    {
                        var batch = batches[batchIndex];
                        batch.ImageStart = start;
                        batch.ImageCount = count;
                        batch.ImageContentValid = true;
                    }
                }

                start = end;
            }

            TrimBatches();
        }

        // Called before drawing a cached mesh, since the source may have received SetData
        // without its geometry or the owning VirtualTexture changing.
        internal bool RefreshPointAtlas(SceneGpuTextureAtlas pointAtlas,
            Func<SourceTexture2DResource, Texture2D> resolveNative, Shader pointShader)
        {
            for (int i = 0; i < activeBatchCount; i++)
            {
                var batch = batches[i];
                if (proceduralImages && (batch.PointQuads == null || !batch.PointQuads.IsValid())) return false;
                if (!batch.UsesPointAtlas) continue;
                if (batch.AtlasDeviceGeneration != pointAtlas.DeviceGeneration ||
                    batch.AtlasSourceSlots.Count != batch.AtlasSources.Count) return false;
                for (int sourceIndex = 0; sourceIndex < batch.AtlasSources.Count; sourceIndex++)
                {
                    var previousSlot = batch.AtlasSourceSlots[sourceIndex];
                    if (!ReferenceEquals(previousSlot.Page, batch.AtlasPage) ||
                        !pointAtlas.TryRefreshExactSlot(batch.AtlasSources[sourceIndex], resolveNative, pointShader,
                            previousSlot))
                        return false;
                }

                if (batch.AtlasDeviceGeneration != pointAtlas.DeviceGeneration) return false;
            }

            return true;
        }

        private bool ReuseImageBatch(ReadOnlySpan<ScenePresentationMeshQuad> images,
            ReadOnlySpan<ScenePresentationMeshQuad> previous, int start, int count, SourceTexture2DResource texture)
        {
            if (rebuildBatch >= activeBatchCount) return false;
            var batch = batches[rebuildBatch];
            if (!batch.ImageContentValid || !ReferenceEquals(batch.Texture, texture) || batch.ImageCount != count ||
                proceduralImages && (batch.PointQuads == null || !batch.PointQuads.IsValid()) ||
                batch.ImageStart > previous.Length - count) return false;
            for (int i = 0; i < count; i++)
                if (!images[start + i].Matches(previous[batch.ImageStart + i]))
                    return false;
            batch.ImageStart = start;
            InvalidQuadCount += batch.InvalidQuadCount;
            return true;
        }

        private bool ReuseAtlasBatch(ReadOnlySpan<ScenePresentationMeshQuad> images,
            ReadOnlySpan<ScenePresentationMeshQuad> previous, int start, int count, ulong deviceGeneration)
        {
            if (rebuildBatch >= activeBatchCount) return false;
            var batch = batches[rebuildBatch];
            if (!batch.UsesPointAtlas || !batch.ImageContentValid || batch.ImageCount != count ||
                proceduralImages && (batch.PointQuads == null || !batch.PointQuads.IsValid()) ||
                !ReferenceEquals(batch.AtlasPage, atlasSlotScratch[0].Page) ||
                batch.AtlasDeviceGeneration != deviceGeneration || batch.AtlasSlots.Count != count ||
                batch.ImageStart > previous.Length - count) return false;
            for (int i = 0; i < count; i++)
                if (!images[start + i].Matches(previous[batch.ImageStart + i]) ||
                    !ReferenceEquals(batch.AtlasSlots[i].Page, atlasSlotScratch[i].Page) ||
                    batch.AtlasSlots[i].X != atlasSlotScratch[i].X ||
                    batch.AtlasSlots[i].Y != atlasSlotScratch[i].Y ||
                    batch.AtlasSlots[i].Width != atlasSlotScratch[i].Width ||
                    batch.AtlasSlots[i].Height != atlasSlotScratch[i].Height)
                    return false;
            batch.ImageStart = start;
            InvalidQuadCount += batch.InvalidQuadCount;
            return true;
        }

        private unsafe bool UpdateAtlasBatch(ReadOnlySpan<ScenePresentationMeshQuad> images,
            ReadOnlySpan<ScenePresentationMeshQuad> previous, int start, int count, ulong deviceGeneration)
        {
            if (rebuildBatch >= activeBatchCount || count > int.MaxValue / 4) return false;
            var batch = batches[rebuildBatch];
            if (!batch.UsesPointAtlas || !batch.ImageContentValid || batch.ImageCount != count ||
                proceduralImages && (batch.PointQuads == null || !batch.PointQuads.IsValid()) ||
                batch.InvalidQuadCount != 0 || batch.VertexCount != count * 4 ||
                !ReferenceEquals(batch.AtlasPage, atlasSlotScratch[0].Page) ||
                batch.AtlasDeviceGeneration != deviceGeneration || batch.AtlasSlots.Count != count ||
                batch.ImageStart > previous.Length - count) return false;

            // Fully validate the existing slot mapping before the first partial GPU write.
            dirtyAtlasChunks.Clear();
            for (int chunk = 0; chunk < count; chunk += DirtyAtlasChunkQuads)
            {
                int chunkEnd = Math.Min(count, chunk + DirtyAtlasChunkQuads);
                bool chunkDirty = false;
                for (int i = chunk; i < chunkEnd; i++)
                {
                    var slot = atlasSlotScratch[i];
                    var old = batch.AtlasSlots[i];
                    if (!ReferenceEquals(old.Page, slot.Page) || old.X != slot.X || old.Y != slot.Y ||
                        old.Width != slot.Width || old.Height != slot.Height) return false;
                    if (images[start + i].Matches(previous[batch.ImageStart + i])) continue;
                    ref readonly var image = ref images[start + i];
                    if (image.PrimitiveTriangles != 0)
                    {
                        if (!image.TryGpuQuad(out _, out int invalidTriangles) || invalidTriangles != 0) return false;
                    }
                    else
                    {
                        ref readonly var q = ref image.Quad;
                        if (!Finite(q.Position0) || !Finite(q.Position1) || !Finite(q.Position2) ||
                            !Finite(q.Position3)) return false;
                    }

                    chunkDirty = true;
                }

                dirtyAtlasChunks.Add(chunkDirty);
            }

            // Adjacent dirty chunks become one upload, with the original index buffer left intact.
            bool dirty = false;
            int rangeStart = -1;
            for (int chunk = 0; chunk < dirtyAtlasChunks.Count; chunk++)
            {
                if (dirtyAtlasChunks[chunk])
                {
                    dirty = true;
                    if (rangeStart < 0) rangeStart = chunk * DirtyAtlasChunkQuads;
                }
                else if (rangeStart >= 0)
                {
                    UploadAtlasRange(batch, images, start, rangeStart, chunk * DirtyAtlasChunkQuads);
                    rangeStart = -1;
                }
            }

            if (rangeStart >= 0) UploadAtlasRange(batch, images, start, rangeStart, count);
            if (dirty && !proceduralImages) batch.Mesh.RecalculateBounds();

            batch.AtlasSources.Clear();
            batch.AtlasSources.AddRange(pendingAtlasSources);
            batch.AtlasSourceSlots.Clear();
            batch.AtlasSourceSlots.AddRange(pendingAtlasSourceSlots);
            batch.ImageStart = start;
            batch.ImageContentValid = true;
            InvalidQuadCount += batch.InvalidQuadCount;
            return true;
        }

        private unsafe void UploadAtlasRange(Batch batch, ReadOnlySpan<ScenePresentationMeshQuad> images,
            int start, int first, int end)
        {
            if (proceduralImages)
            {
                int length = end - first;
                if (pointQuads.Length < length) GrowPointQuads(length);
                for (int i = first; i < end; i++)
                {
                    ref readonly var image = ref images[start + i];
                    int triangles = image.PrimitiveTriangles & 3;
                    if (triangles == 1 || triangles == 2)
                    {
                        var quad = ValidatedGpuQuad(in image);
                        WritePointQuad(ref pointQuadData[i - first], in quad, default, atlasSlotScratch[i].Rect,
                            image.ForcePointClamp);
                    }
                    else
                    {
                        WritePointQuad(ref pointQuadData[i - first], in image.Quad, default,
                            atlasSlotScratch[i].Rect, image.ForcePointClamp);
                    }
                }

                batch.ImageContentValid = false;
                batch.PointQuads.SetData(pointQuads, 0, first, length);
                return;
            }

            int vertexLength = (end - first) * 4;
            if (vertices.Length < vertexLength) GrowVertices(vertexLength);
            for (int i = first; i < end; i++)
            {
                int n = (i - first) * 4;
                ref readonly var image = ref images[start + i];
                var q = ValidatedGpuQuad(in image);
                var rect = atlasSlotScratch[i].Rect;
                WriteVertices(vertexData + n, in q, default, rect);
            }

            // A failed partial write must not leave the batch eligible for another reuse.
            batch.ImageContentValid = false;
            batch.Mesh.SetVertexBufferData(vertices, 0, first * 4, vertexLength);
        }

        // UpdateAtlasBatch validates every changed row before uploading. Unchanged rows
        // match a batch with no invalid quads, including those in a dirty chunk.
        private static SourceSpriteQuad ValidatedGpuQuad(in ScenePresentationMeshQuad image)
        {
            int triangles = image.PrimitiveTriangles & 3;
            return triangles == 1 ? ScenePresentationMeshQuad.Triangle(in image.Quad, false) :
                triangles == 2 ? ScenePresentationMeshQuad.Triangle(in image.Quad, true) : image.Quad;
        }

        private void AddImage(in ScenePresentationMeshQuad image, Vector4 atlasRect = default)
        {
            if (image.ForcePointClamp && !proceduralImages)
                throw new InvalidOperationException("Per-image addressing requires the point-quad path.");
            if (image.PrimitiveTriangles == 0)
            {
                Add(in image.Quad, atlasRect: atlasRect, forcePointClamp: image.ForcePointClamp);
                return;
            }

            bool valid = image.TryGpuQuad(out var quad, out int invalidTriangles);
            InvalidQuadCount += invalidTriangles;
            pendingInvalidQuadCount += invalidTriangles;
            if (valid)
                Add(in quad, atlasRect: atlasRect, positionsValidated: true,
                    forcePointClamp: image.ForcePointClamp);
        }

        private unsafe void Add(in SourceSpriteQuad q, Vector2 cell = default, Vector4 atlasRect = default,
            bool positionsValidated = false, bool forcePointClamp = false)
        {
            if (!positionsValidated &&
                (!Finite(q.Position0) || !Finite(q.Position1) || !Finite(q.Position2) || !Finite(q.Position3)))
            {
                InvalidQuadCount++;
                pendingInvalidQuadCount++;
                return;
            }

            int n = vertexCount;
            if (proceduralImages)
            {
                int quad = n / 4;
                if (quad == pointQuads.Length)
                    GrowPointQuads(Math.Max(quad + 1, Math.Max(4, pointQuads.Length * 2)));
                WritePointQuad(ref pointQuadData[quad], in q, cell, atlasRect, forcePointClamp);
                vertexCount += 4;
                return;
            }

            if (vertices.Length - n < 4)
                GrowVertices(Math.Max(n + 4, Math.Max(4, vertices.Length * 2)));
            WriteVertices(vertexData + n, in q, cell, atlasRect);
            vertexCount += 4;
            indices.Add(n);
            indices.Add(n + 1);
            indices.Add(n + 2);
            indices.Add(n + 2);
            indices.Add(n + 1);
            indices.Add(n + 3);
        }

        private unsafe void GrowVertices(int capacity)
        {
            var next = new NativeArray<SceneVertex>(capacity, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            SceneVertex* nextData;
            try
            {
                nextData = (SceneVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(next);
                if (vertexCount != 0) NativeArray<SceneVertex>.Copy(vertices, next, vertexCount);
            }
            catch
            {
                next.Dispose();
                throw;
            }

            if (vertices.IsCreated) vertices.Dispose();
            vertices = next;
            vertexData = nextData;
        }

        private unsafe void GrowPointQuads(int capacity)
        {
            var next = new NativeArray<ScenePointQuad>(capacity, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            ScenePointQuad* nextData;
            try
            {
                nextData = (ScenePointQuad*)NativeArrayUnsafeUtility.GetUnsafePtr(next);
                if (vertexCount != 0) NativeArray<ScenePointQuad>.Copy(pointQuads, next, vertexCount / 4);
            }
            catch
            {
                next.Dispose();
                throw;
            }

            if (pointQuads.IsCreated) pointQuads.Dispose();
            pointQuads = next;
            pointQuadData = nextData;
        }

        private static void WritePointQuad(ref ScenePointQuad target, in SourceSpriteQuad source,
            Vector2 cell, Vector4 atlasRect, bool forcePointClamp = false)
        {
            target.Quad = source;
            target.Cell = cell;
            target.AtlasRect = atlasRect;
            target.Padding = forcePointClamp ? 1UL : 0UL;
            target.Quad.Position0.Y = -source.Position0.Y;
            target.Quad.Position0.Z = 0;
            target.Quad.TextureCoordinate0.Y = 1f - source.TextureCoordinate0.Y;
            target.Quad.Position1.Y = -source.Position1.Y;
            target.Quad.Position1.Z = 0;
            target.Quad.TextureCoordinate1.Y = 1f - source.TextureCoordinate1.Y;
            target.Quad.Position2.Y = -source.Position2.Y;
            target.Quad.Position2.Z = 0;
            target.Quad.TextureCoordinate2.Y = 1f - source.TextureCoordinate2.Y;
            target.Quad.Position3.Y = -source.Position3.Y;
            target.Quad.Position3.Z = 0;
            target.Quad.TextureCoordinate3.Y = 1f - source.TextureCoordinate3.Y;
        }

        private static unsafe void WriteVertices(SceneVertex* target, in SourceSpriteQuad q,
            Vector2 cell, Vector4 atlasRect)
        {
            target[0].Position.x = q.Position0.X;
            target[0].Position.y = -q.Position0.Y;
            target[0].Position.z = 0;
            target[0].Color.r = (byte)q.Color0;
            target[0].Color.g = (byte)(q.Color0 >> 8);
            target[0].Color.b = (byte)(q.Color0 >> 16);
            target[0].Color.a = (byte)(q.Color0 >> 24);
            target[0].Uv.x = q.TextureCoordinate0.X;
            target[0].Uv.y = 1f - q.TextureCoordinate0.Y;
            target[0].Cell = cell;
            target[0].AtlasRect = atlasRect;

            target[1].Position.x = q.Position1.X;
            target[1].Position.y = -q.Position1.Y;
            target[1].Position.z = 0;
            target[1].Color.r = (byte)q.Color1;
            target[1].Color.g = (byte)(q.Color1 >> 8);
            target[1].Color.b = (byte)(q.Color1 >> 16);
            target[1].Color.a = (byte)(q.Color1 >> 24);
            target[1].Uv.x = q.TextureCoordinate1.X;
            target[1].Uv.y = 1f - q.TextureCoordinate1.Y;
            target[1].Cell = cell;
            target[1].AtlasRect = atlasRect;

            target[2].Position.x = q.Position2.X;
            target[2].Position.y = -q.Position2.Y;
            target[2].Position.z = 0;
            target[2].Color.r = (byte)q.Color2;
            target[2].Color.g = (byte)(q.Color2 >> 8);
            target[2].Color.b = (byte)(q.Color2 >> 16);
            target[2].Color.a = (byte)(q.Color2 >> 24);
            target[2].Uv.x = q.TextureCoordinate2.X;
            target[2].Uv.y = 1f - q.TextureCoordinate2.Y;
            target[2].Cell = cell;
            target[2].AtlasRect = atlasRect;

            target[3].Position.x = q.Position3.X;
            target[3].Position.y = -q.Position3.Y;
            target[3].Position.z = 0;
            target[3].Color.r = (byte)q.Color3;
            target[3].Color.g = (byte)(q.Color3 >> 8);
            target[3].Color.b = (byte)(q.Color3 >> 16);
            target[3].Color.a = (byte)(q.Color3 >> 24);
            target[3].Uv.x = q.TextureCoordinate3.X;
            target[3].Uv.y = 1f - q.TextureCoordinate3.Y;
            target[3].Cell = cell;
            target[3].AtlasRect = atlasRect;
        }

        private void Flush(SourceTexture2DResource texture, bool usesPointAtlas = false, ulong deviceGeneration = 0,
            SceneGpuTextureAtlas.Page atlasPage = null)
        {
            using var flushScope = FlushMarker.Auto();
            if (vertexCount == 0)
            {
                pendingAtlasSources.Clear();
                pendingAtlasSourceSlots.Clear();
                pendingAtlasSourceSet.Clear();
                pendingInvalidQuadCount = 0;
                return;
            }

            Batch batch;
            bool newBatch = rebuildBatch == batches.Count;
            if (newBatch)
            {
                batch = new Batch
                {
                    Mesh = proceduralImages
                        ? null
                        : new Mesh { name = "Scene tiles", hideFlags = HideFlags.HideAndDontSave }
                };
                if (dynamicGeometry && batch.Mesh != null) batch.Mesh.MarkDynamic();
                batches.Add(batch);
            }
            else batch = batches[rebuildBatch];

            rebuildBatch++;
            batch.ImageContentValid = false;
            if (proceduralImages)
            {
                int count = vertexCount / 4;
                int stride = UnsafeUtility.SizeOf<ScenePointQuad>();
                if (stride != 128) throw new InvalidOperationException("The source point quad GPU stride changed.");
                bool replace = batch.PointQuads == null || !batch.PointQuads.IsValid() ||
                               batch.PointQuadCapacity < count || batch.AtlasDeviceGeneration != deviceGeneration;
                int capacity = batch.PointQuadCapacity;
                if (capacity < count)
                    capacity = Math.Max(count, capacity <= int.MaxValue / 2 ? Math.Max(32, capacity * 2) : capacity);
                var target = replace
                    ? new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride)
                    : batch.PointQuads;
                try
                {
                    target.SetData(pointQuads, 0, 0, count);
                }
                catch
                {
                    if (replace) target.Dispose();
                    throw;
                }

                if (replace)
                {
                    batch.PointQuads?.Dispose();
                    batch.PointQuads = target;
                    batch.PointQuadCapacity = capacity;
                }
            }
            else
            {
                var mesh = batch.Mesh;
                var format = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                bool topologyChanged = newBatch || batch.VertexCount != vertexCount;
                if (topologyChanged)
                {
                    mesh.Clear();
                    mesh.indexFormat = format;
                    mesh.SetVertexBufferParams(vertexCount, VertexLayout);
                }

                mesh.SetVertexBufferData(vertices, 0, 0, vertexCount);
                if (topologyChanged) mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
            }

            batch.Texture = texture;
            batch.UsesPointAtlas = usesPointAtlas;
            batch.AtlasPage = atlasPage;
            batch.AtlasDeviceGeneration = deviceGeneration;
            batch.AtlasSources.Clear();
            batch.AtlasSourceSlots.Clear();
            batch.AtlasSlots.Clear();
            if (usesPointAtlas) batch.AtlasSources.AddRange(pendingAtlasSources);
            if (usesPointAtlas) batch.AtlasSourceSlots.AddRange(pendingAtlasSourceSlots);
            if (usesPointAtlas) batch.AtlasSlots.AddRange(atlasSlotScratch);
            batch.VertexCount = vertexCount;
            batch.InvalidQuadCount = pendingInvalidQuadCount;
            pendingInvalidQuadCount = 0;
            vertexCount = 0;
            pendingAtlasSources.Clear();
            pendingAtlasSourceSlots.Clear();
            pendingAtlasSourceSet.Clear();
            indices.Clear();
        }

        private void TrimBatches()
        {
            for (int i = rebuildBatch; i < activeBatchCount; i++)
            {
                // Keep the buffer capacity for a later run; clear every live image binding.
                batches[i].VertexCount = 0;
                batches[i].ImageStart = 0;
                batches[i].ImageCount = 0;
                batches[i].InvalidQuadCount = 0;
                batches[i].Texture = null;
                batches[i].UsesPointAtlas = false;
                batches[i].AtlasPage = null;
                batches[i].AtlasSources.Clear();
                batches[i].AtlasSourceSlots.Clear();
                batches[i].AtlasSlots.Clear();
                batches[i].ImageContentValid = false;
            }

            activeBatchCount = rebuildBatch;
        }

        internal void KeepPages(HashSet<SourceTexture2DResource> live)
        {
            for (int i = 0; i < activeBatchCount; i++)
            {
                var batch = batches[i];
                if (batch.UsesPointAtlas)
                {
                    foreach (var source in batch.AtlasSources) live.Add(source);
                }
                else live.Add(batch.Texture);
            }
        }

        internal void Draw(Matrix4x4 matrix, Func<SourceTexture2DResource, Material> resolve,
            Celeste.Core.Geometry.IntRect? excluded = null, int pointAddress = 0, int materialPass = 0)
        {
            using var drawScope = DrawMarker.Auto();
            for (int i = 0; i < activeBatchCount; i++)
            {
                var batch = batches[i];
                var material = batch.UsesPointAtlas ? batch.AtlasPage.Material : resolve(batch.Texture);
                if (!material) continue;
                material.SetFloat(SourcePointAddress, pointAddress);
                material.SetFloat(ExcludeTiles, excluded.HasValue ? 1f : 0f);
                if (excluded.HasValue)
                {
                    var clip = excluded.Value;
                    material.SetVector(ExcludedTileRange,
                        new Vector4(clip.X, clip.Y, (float)((long)clip.X + clip.Width),
                            (float)((long)clip.Y + clip.Height)));
                }

                if (proceduralImages)
                {
                    if (materialPass != 0)
                        throw new InvalidOperationException("Point image batch requires point pass.");
                    material.SetBuffer(SceneQuads, batch.PointQuads);
                    material.SetMatrix(SceneWorld, matrix);
                    if (material.SetPass(3))
                        UnityEngine.Graphics.DrawProceduralNow(MeshTopology.Triangles, batch.VertexCount / 4 * 6, 1);
                }
                else if (material.SetPass(materialPass)) UnityEngine.Graphics.DrawMeshNow(batch.Mesh, matrix);
            }
        }

        internal void Suspend()
        {
            InvalidQuadCount = 0;
            pendingInvalidQuadCount = 0;
            vertexCount = 0;
            pendingAtlasSources.Clear();
            pendingAtlasSourceSlots.Clear();
            pendingAtlasSourceSet.Clear();
            atlasSlotScratch.Clear();
            indices.Clear();
            for (int i = 0; i < Math.Max(activeBatchCount, rebuildBatch); i++)
            {
                // Keep the buffer capacity for a later run; clear every live image binding.
                batches[i].VertexCount = 0;
                batches[i].ImageStart = 0;
                batches[i].ImageCount = 0;
                batches[i].InvalidQuadCount = 0;
                batches[i].Texture = null;
                batches[i].UsesPointAtlas = false;
                batches[i].AtlasPage = null;
                batches[i].AtlasSources.Clear();
                batches[i].AtlasSourceSlots.Clear();
                batches[i].AtlasSlots.Clear();
                batches[i].ImageContentValid = false;
            }

            activeBatchCount = 0;
            rebuildBatch = 0;
        }

        public unsafe void Dispose()
        {
            InvalidQuadCount = 0;
            pendingInvalidQuadCount = 0;
            if (vertices.IsCreated) vertices.Dispose();
            if (pointQuads.IsCreated) pointQuads.Dispose();
            vertices = default;
            vertexData = null;
            pointQuads = default;
            pointQuadData = null;
            vertexCount = 0;
            foreach (var batch in batches)
            {
                batch.PointQuads?.Dispose();
                if (batch.Mesh != null) UnityEngine.Object.DestroyImmediate(batch.Mesh);
            }

            batches.Clear();
            textureGroupIndices.Clear();
            textureGroups.Clear();
            activeTextureGroupCount = 0;
            activeBatchCount = 0;
            rebuildBatch = 0;
        }
    }
}
