using System;
using System.Collections.Generic;
using Celeste.Core.Geometry;
using Celeste.Core.Maps;
using Celeste.Graphics.Diagnostics.Presentation;
using Celeste.Graphics.Rendering.Batches;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Graphics.Scene.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using WorldBounds = Celeste.Core.Geometry.Bounds;

namespace Celeste.Unity.Editor
{
    // Reusable current-room values become immutable only when another room is actually consumed.
    internal sealed class SceneObservedRoomArchive : IDisposable
    {
        private static readonly ProfilerMarker FreezeBuilderMarker = new("Celeste.Scene.Archive.FreezeBuilder");
        private static readonly ProfilerMarker RebuildLayersMarker = new("Celeste.Scene.Archive.RebuildLayers");

        internal readonly struct DrawValue
        {
            internal readonly ScenePresentationMeshQuad Image;
            internal readonly double Depth;
            internal readonly int Ordinal;
            internal readonly int TileKind;
            internal readonly MapEntityId SourceId;
            internal readonly bool HasSourceId;

            internal DrawValue(SourceTexture2DResource texture, in SourceSpriteQuad quad, double depth,
                int ordinal, bool hasSourceId, MapEntityId sourceId, int tileKind = 0, byte primitiveTriangles = 0)
            {
                Image = new ScenePresentationMeshQuad(texture, in quad, primitiveTriangles);
                Depth = depth;
                Ordinal = ordinal;
                TileKind = tileKind;
                HasSourceId = hasSourceId;
                SourceId = sourceId;
            }
        }

        internal readonly struct TileArea
        {
            internal readonly double Depth;
            internal readonly int Kind;
            internal readonly WorldBounds Bounds;

            internal TileArea(double depth, int kind, in WorldBounds bounds)
            {
                Depth = depth;
                Kind = kind;
                Bounds = bounds;
            }
        }

        internal sealed class Layer : IDisposable
        {
            internal readonly double Depth;
            internal readonly string Room;
            internal readonly IntRect RoomBounds;
            internal readonly int FirstOrdinal;
            internal readonly int TileKind;
            internal readonly List<ScenePresentationMeshQuad> Images = new();
            private List<Selection> selectedOrdinals = new(), nextOrdinals = new();
            internal long SelectionRevision { get; private set; }
            internal int SelectedCount => selectedOrdinals.Count;

            internal ScenePresentationMeshQuad GetSelected(int index)
            {
                var selection = selectedOrdinals[index];
                var image = Images[selection.Index];
                return new ScenePresentationMeshQuad(image.Texture, in image.Quad, selection.PrimitiveTriangles);
            }

            private SceneTileScope previousScope;
            private WorldBounds previousView;
            private bool built;
            private WorldBounds aggregateBounds;
            private bool hasBounds;

            internal Layer(double depth, string room, IntRect bounds, int firstOrdinal, int tileKind)
            {
                Depth = depth;
                Room = room;
                RoomBounds = bounds;
                FirstOrdinal = firstOrdinal;
                TileKind = tileKind;
            }

            internal bool RefreshSelection(SceneTileScope scope, in WorldBounds view)
            {
                var area = new WorldBounds(RoomBounds.X, RoomBounds.Y, RoomBounds.Width, RoomBounds.Height);
                if (scope == SceneTileScope.VisibleRooms && !area.Intersects(view)) return false;
                if (scope == SceneTileScope.VisibleWorld && (!hasBounds || !aggregateBounds.Intersects(view)))
                    return false;
                bool changed = !built || previousScope != scope || scope == SceneTileScope.VisibleWorld &&
                    (!previousView.X.Equals(view.X) || !previousView.Y.Equals(view.Y) ||
                     !previousView.Width.Equals(view.Width) || !previousView.Height.Equals(view.Height));
                if (changed)
                {
                    nextOrdinals.Clear();
                    for (int i = 0; i < Images.Count; i++)
                    {
                        var image = Images[i];
                        byte triangles = image.PrimitiveTriangles;
                        if (scope == SceneTileScope.VisibleWorld)
                        {
                            if (triangles == 0)
                            {
                                if (!Bounds(in image.Quad).Intersects(view)) continue;
                            }
                            else
                            {
                                byte selected = 0;
                                if ((triangles & 1) != 0 &&
                                    ScenePresentationMeshQuad.TriangleBounds(in image.Quad, false).Intersects(view))
                                    selected |= 1;
                                if ((triangles & 2) != 0 &&
                                    ScenePresentationMeshQuad.TriangleBounds(in image.Quad, true).Intersects(view))
                                    selected |= 2;
                                if (selected == 0) continue;
                                triangles = selected;
                            }
                        }

                        nextOrdinals.Add(new Selection(i, triangles));
                    }

                    bool selectionChanged = !built || selectedOrdinals.Count != nextOrdinals.Count;
                    if (!selectionChanged)
                        for (int i = 0; i < nextOrdinals.Count; i++)
                            if (selectedOrdinals[i].Index != nextOrdinals[i].Index ||
                                selectedOrdinals[i].PrimitiveTriangles != nextOrdinals[i].PrimitiveTriangles)
                            {
                                selectionChanged = true;
                                break;
                            }

                    if (selectionChanged)
                    {
                        SelectionRevision++;
                    }

                    var previousOrdinals = selectedOrdinals;
                    selectedOrdinals = nextOrdinals;
                    nextOrdinals = previousOrdinals;
                    previousScope = scope;
                    previousView = view;
                    built = true;
                }

                return selectedOrdinals.Count != 0;
            }

            public void Dispose()
            {
                selectedOrdinals.Clear();
                nextOrdinals.Clear();
                built = false;
                SelectionRevision++;
            }

            internal void Add(ScenePresentationMeshQuad image)
            {
                if (image.PrimitiveTriangles == 0) AddBounds(Bounds(in image.Quad));
                else
                {
                    if ((image.PrimitiveTriangles & 1) != 0)
                        AddBounds(ScenePresentationMeshQuad.TriangleBounds(in image.Quad, false));
                    if ((image.PrimitiveTriangles & 2) != 0)
                        AddBounds(ScenePresentationMeshQuad.TriangleBounds(in image.Quad, true));
                }

                Images.Add(image);
            }

            private void AddBounds(in WorldBounds bounds)
            {
                if (!hasBounds) aggregateBounds = bounds;
                else
                {
                    float left = Math.Min(aggregateBounds.X, bounds.X);
                    float top = Math.Min(aggregateBounds.Y, bounds.Y);
                    float right = Math.Max(aggregateBounds.Right, bounds.Right);
                    float bottom = Math.Max(aggregateBounds.Bottom, bounds.Bottom);
                    aggregateBounds = new WorldBounds(left, top, right - left, bottom - top);
                }

                hasBounds = true;
            }

            private readonly struct Selection
            {
                internal readonly int Index;
                internal readonly byte PrimitiveTriangles;

                internal Selection(int index, byte primitiveTriangles)
                {
                    Index = index;
                    PrimitiveTriangles = primitiveTriangles;
                }
            }
        }

        private struct StoredDraw
        {
            internal SourceTexture2DResource Texture;
            internal SourceSpriteQuad Quad;
            internal byte PrimitiveTriangles;
            internal double Depth;
            internal int Ordinal;
            internal int TileKind;
            internal MapEntityId SourceId;
            internal bool HasSourceId;

            internal DrawValue Read()
                => new DrawValue(Texture, in Quad, Depth, Ordinal, HasSourceId, SourceId, TileKind,
                    PrimitiveTriangles);
        }

        private sealed class DrawBuffer
        {
            private StoredDraw[] items = Array.Empty<StoredDraw>();
            private int count;
            private int version;
            internal int Count => count;

            internal void Clear()
            {
                unchecked
                {
                    version++;
                }

                int previousCount = count;
                count = 0;
                Array.Clear(items, 0, previousCount);
            }

            internal void Add(SourceTexture2DResource texture, in SourceSpriteQuad quad, double depth,
                int ordinal, bool hasSourceId, MapEntityId sourceId, int tileKind, byte primitiveTriangles)
            {
                unchecked
                {
                    version++;
                }

                if (count == items.Length)
                {
                    int capacity = items.Length == 0 ? 4 : items.Length * 2;
                    if ((uint)capacity > 0X7FEFFFFF) capacity = 0X7FEFFFFF;
                    if (capacity < count + 1) capacity = count + 1;
                    Array.Resize(ref items, capacity);
                }

                ref var item = ref items[count];
                item.Texture = texture;
                item.Quad = quad;
                item.PrimitiveTriangles = primitiveTriangles;
                item.Depth = depth;
                item.Ordinal = ordinal;
                item.TileKind = tileKind;
                item.SourceId = sourceId;
                item.HasSourceId = hasSourceId;
                count++;
            }

            internal void Sort()
            {
                bool strictlySorted = true;
                for (int i = 1; i < count; i++)
                {
                    ref readonly var previous = ref items[i - 1];
                    ref readonly var current = ref items[i];
                    int order = current.Depth.CompareTo(previous.Depth);
                    if (order == 0) order = previous.Ordinal.CompareTo(current.Ordinal);
                    // Equal keys retain Array.Sort's original unstable tie ordering.
                    if (order >= 0)
                    {
                        strictlySorted = false;
                        break;
                    }
                }

                if (count > 1 && !strictlySorted)
                    Array.Sort(items, 0, count, Comparer<StoredDraw>.Create((a, b) =>
                    {
                        int depth = b.Depth.CompareTo(a.Depth);
                        return depth != 0 ? depth : a.Ordinal.CompareTo(b.Ordinal);
                    }));
                unchecked
                {
                    version++;
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(this);

            internal struct Enumerator
            {
                private readonly DrawBuffer owner;
                private readonly int version;
                private int index;
                private DrawValue current;

                internal Enumerator(DrawBuffer owner)
                {
                    this.owner = owner;
                    version = owner.version;
                    index = 0;
                    current = default;
                }

                public DrawValue Current => current;

                public bool MoveNext()
                {
                    if (version != owner.version)
                        throw new InvalidOperationException(
                            "Collection was modified; enumeration operation may not execute.");
                    if ((uint)index < (uint)owner.count)
                    {
                        current = owner.items[index].Read();
                        index++;
                        return true;
                    }

                    index = owner.count + 1;
                    current = default;
                    return false;
                }

                public void Dispose()
                {
                }
            }
        }

        private sealed class RoomFrame
        {
            internal bool IsValid;
            internal string Room;
            internal IntRect Bounds;
            internal readonly DrawBuffer Draws = new();
            internal readonly List<TileArea> Tiles = new();
            internal readonly List<Layer> Layers = new();
            internal readonly HashSet<SourceTexture2DResource> Pages = new();
        }

        private readonly Dictionary<string, RoomFrame> rooms = new(StringComparer.Ordinal);
        private RoomFrame builder = new();
        private readonly List<string> removedRooms = new();
        private readonly HashSet<SourceTexture2DResource> historicalPages = new(), builderPages = new();
        internal readonly List<Layer> Layers = new();
        internal long Revision { get; private set; }
        internal int RoomCount => rooms.Count;
        internal long ConsumedRevision { get; private set; } = -1;
        internal string CurrentRoom => builder.Room;
        internal bool HasValidConsumedFrame => builder.IsValid;

        internal bool BeginConsumed(string room, IntRect bounds, long revision)
            => SetConsumed(room, bounds, revision, true);

        internal void DiscardConsumed(string room, IntRect bounds, long revision)
            => SetConsumed(room, bounds, revision, false);

        private bool SetConsumed(string room, IntRect bounds, long revision, bool valid)
        {
            if (builder.Room == room && ConsumedRevision == revision && builder.IsValid == valid) return false;
            if (builder.Room != null && builder.Room != room)
            {
                if (builder.IsValid) FreezeBuilder();
                else Release(builder);
                builder = new RoomFrame();
            }

            // A re-entered room is live immediately; its earlier frozen frame is discarded.
            if (rooms.TryGetValue(room, out var old))
            {
                Release(old);
                rooms.Remove(room);
                RebuildLayers();
            }

            builder.Room = room;
            builder.Bounds = bounds;
            builder.IsValid = valid;
            builder.Draws.Clear();
            builder.Tiles.Clear();
            builderPages.Clear();
            ConsumedRevision = revision;
            return true;
        }

        internal void Add(in DrawValue value)
        {
            Add(value.Image.Texture, in value.Image.Quad, value.Depth, value.Ordinal, value.HasSourceId,
                value.SourceId, value.TileKind, value.Image.PrimitiveTriangles);
        }

        internal void Add(SourceTexture2DResource texture, in SourceSpriteQuad quad, double depth,
            int ordinal, bool hasSourceId, MapEntityId sourceId, int tileKind = 0, byte primitiveTriangles = 0)
        {
            if (!builder.IsValid) return;
            builder.Draws.Add(texture, in quad, depth, ordinal, hasSourceId, sourceId, tileKind,
                primitiveTriangles);
            builderPages.Add(texture);
        }

        internal void Add(in TileArea value)
        {
            if (builder.IsValid) builder.Tiles.Add(value);
        }

        private void FreezeBuilder()
        {
            using var freezeScope = FreezeBuilderMarker.Auto();
            if (rooms.TryGetValue(builder.Room, out var old)) Release(old);
            // Actual source depth comparison, then original completed-stream ordinal.
            builder.Draws.Sort();
            Layer layer = null;
            foreach (var value in builder.Draws)
            {
                if (layer == null || layer.Depth != value.Depth || layer.TileKind != value.TileKind)
                {
                    layer = new Layer(value.Depth, builder.Room, builder.Bounds, value.Ordinal, value.TileKind);
                    builder.Layers.Add(layer);
                }

                layer.Add(value.Image);
            }

            builder.Pages.UnionWith(builderPages);
            rooms[builder.Room] = builder;
            RebuildLayers();
        }

        private void RebuildLayers()
        {
            using var rebuildScope = RebuildLayersMarker.Auto();
            Layers.Clear();
            historicalPages.Clear();
            foreach (var frame in rooms.Values)
            {
                Layers.AddRange(frame.Layers);
                historicalPages.UnionWith(frame.Pages);
            }

            Layers.Sort((a, b) =>
            {
                int depth = b.Depth.CompareTo(a.Depth);
                if (depth != 0) return depth;
                int room = StringComparer.Ordinal.Compare(a.Room, b.Room);
                return room != 0 ? room : a.FirstOrdinal.CompareTo(b.FirstOrdinal);
            });
            Revision++;
        }

        internal void KeepPages(HashSet<SourceTexture2DResource> live)
        {
            foreach (var page in builderPages) live.Add(page);
            foreach (var page in historicalPages) live.Add(page);
        }

        internal void Invalidate(HashSet<SourceTexture2DResource> invalid)
        {
            if (invalid.Count == 0) return;
            removedRooms.Clear();
            foreach (var pair in rooms)
            foreach (var value in pair.Value.Draws)
                if (invalid.Contains(value.Image.Texture))
                {
                    removedRooms.Add(pair.Key);
                    break;
                }

            foreach (var room in removedRooms)
            {
                Release(rooms[room]);
                rooms.Remove(room);
            }

            foreach (var value in builder.Draws)
                if (invalid.Contains(value.Image.Texture))
                {
                    builder.Draws.Clear();
                    builder.Tiles.Clear();
                    builderPages.Clear();
                    builder.IsValid = false;
                    ConsumedRevision = -1;
                    break;
                }

            if (removedRooms.Count != 0) RebuildLayers();
        }

        internal void ExcludeTiles(Span<byte> mask, ReadOnlySpan<long> cells, in WorldBounds grid, int columns,
            int rows,
            int width, int height, double depth, int kind)
        {
            foreach (var frame in rooms.Values)
            foreach (var tile in frame.Tiles)
            {
                if (tile.Depth != depth || tile.Kind != kind || !tile.Bounds.Intersects(grid)) continue;
                int left = Math.Max(0, (int)Math.Floor((tile.Bounds.X - grid.X) / width));
                int top = Math.Max(0, (int)Math.Floor((tile.Bounds.Y - grid.Y) / height));
                int right = Math.Min(columns, (int)Math.Ceiling((tile.Bounds.Right - grid.X) / width));
                int bottom = Math.Min(rows, (int)Math.Ceiling((tile.Bounds.Bottom - grid.Y) / height));
                long first = (long)left * rows, end = (long)right * rows;
                int lo = 0, hi = cells.Length;
                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if (cells[mid] < first) lo = mid + 1;
                    else hi = mid;
                }

                for (int i = lo; i < cells.Length && cells[i] < end; i++)
                    if (cells[i] % rows >= top && cells[i] % rows < bottom)
                        mask[i] = 0;
            }
        }

        internal static WorldBounds Bounds(in SourceSpriteQuad q)
        {
            float left = Math.Min(Math.Min(q.Position0.X, q.Position1.X), Math.Min(q.Position2.X, q.Position3.X));
            float top = Math.Min(Math.Min(q.Position0.Y, q.Position1.Y), Math.Min(q.Position2.Y, q.Position3.Y));
            float right = Math.Max(Math.Max(q.Position0.X, q.Position1.X), Math.Max(q.Position2.X, q.Position3.X));
            float bottom = Math.Max(Math.Max(q.Position0.Y, q.Position1.Y), Math.Max(q.Position2.Y, q.Position3.Y));
            return new WorldBounds(left, top, right - left, bottom - top);
        }

        private static void Release(RoomFrame frame)
        {
            foreach (var layer in frame.Layers) layer.Dispose();
            frame.Layers.Clear();
            frame.Draws.Clear();
            frame.Tiles.Clear();
            frame.Pages.Clear();
            frame.IsValid = false;
        }

        public void Dispose()
        {
            foreach (var frame in rooms.Values) Release(frame);
            rooms.Clear();
            Release(builder);
            builder.Room = null;
            Layers.Clear();
            historicalPages.Clear();
            builderPages.Clear();
            ConsumedRevision = -1;
            Revision++;
        }
    }
}
