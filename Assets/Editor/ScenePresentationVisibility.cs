using System;
using Celeste.Core.Geometry;
using Celeste.Graphics.Scene.Diagnostics;
using WorldBounds = Celeste.Core.Geometry.Bounds;

namespace Celeste.Unity.Editor
{
    internal sealed class ScenePresentationVisibility
    {
        private WorldBounds[] selectedRooms = Array.Empty<WorldBounds>();
        private int selectedRoomCount;
        private WorldBounds previousView;
        private long previousRevision;
        private bool hasSelection;
        internal int SelectedRoomCount => selectedRoomCount;

        internal void RefreshVisibleRooms(SceneTileScope scope, in WorldBounds view,
            ReadOnlySpan<IntRect> rooms, long revision)
        {
            if (scope != SceneTileScope.VisibleRooms) return;
            if (hasSelection && previousRevision == revision && Same(in previousView, in view)) return;
            selectedRoomCount = 0;
            foreach (var room in rooms)
                if (room.Width > 0 && room.Height > 0 &&
                    new WorldBounds(room.X, room.Y, room.Width, room.Height).Intersects(view))
                {
                    if (selectedRoomCount == selectedRooms.Length)
                    {
                        int capacity = selectedRooms.Length > int.MaxValue / 2
                            ? int.MaxValue
                            : Math.Max(4, selectedRooms.Length * 2);
                        Array.Resize(ref selectedRooms, capacity);
                    }

                    selectedRooms[selectedRoomCount] = new WorldBounds(room.X, room.Y, room.Width, room.Height);
                    selectedRoomCount++;
                }

            previousRevision = revision;
            previousView = view;
            hasSelection = true;
        }

        internal bool IncludesVisibleRoom(in WorldBounds image)
        {
            for (int i = 0; i < selectedRoomCount; i++)
            {
                ref readonly var room = ref selectedRooms[i];
                if (room.Right > image.X && room.Bottom > image.Y &&
                    room.X < image.Right && room.Y < image.Bottom)
                    return true;
            }

            return false;
        }

        internal void Clear()
        {
            selectedRoomCount = 0;
            hasSelection = false;
        }

        internal static bool IsFullyCovered(SceneTileScope scope, bool hasMainPass, bool supportedScope,
            bool sameCamera, int dropped, in WorldBounds view, in WorldBounds grid, int columns, int rows,
            int tileWidth, int tileHeight, IntRect coverage)
        {
            if (scope != SceneTileScope.VisibleWorld || !hasMainPass || !supportedScope || !sameCamera ||
                dropped != 0 ||
                tileWidth <= 0 || tileHeight <= 0 || columns < 0 || rows < 0 ||
                grid.Width != (double)columns * tileWidth || grid.Height != (double)rows * tileHeight ||
                !Finite(view.X) || !Finite(view.Y) || !Finite(view.Width) || !Finite(view.Height) ||
                !Finite(grid.X) || !Finite(grid.Y) || !Finite(grid.Width) || !Finite(grid.Height) ||
                !Finite(view.Right) || !Finite(view.Bottom) || !Finite(grid.Right) || !Finite(grid.Bottom) ||
                coverage.X < 0 || coverage.Y < 0 || coverage.Width < 0 || coverage.Height < 0 ||
                (long)coverage.X + coverage.Width > columns || (long)coverage.Y + coverage.Height > rows) return false;
            // Same cell bounds as SceneTileScopeMask.Include; no map mask allocation or clear.
            if (view.Width <= 0 || view.Height <= 0) return true;
            double left = Math.Max((double)view.X, grid.X);
            double top = Math.Max((double)view.Y, grid.Y);
            double right = Math.Min((double)view.Right, (double)grid.Right);
            double bottom = Math.Min((double)view.Bottom, (double)grid.Bottom);
            if (left >= right || top >= bottom) return true;
            int x0 = (int)((left - grid.X) / tileWidth);
            int y0 = (int)((top - grid.Y) / tileHeight);
            int x1 = Math.Min((int)Math.Ceiling((right - grid.X) / tileWidth), columns);
            int y1 = Math.Min((int)Math.Ceiling((bottom - grid.Y) / tileHeight), rows);
            return x0 >= coverage.X && y0 >= coverage.Y &&
                   x1 <= (long)coverage.X + coverage.Width && y1 <= (long)coverage.Y + coverage.Height;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool Same(in WorldBounds a, in WorldBounds b)
            => a.X.Equals(b.X) && a.Y.Equals(b.Y) && a.Width.Equals(b.Width) && a.Height.Equals(b.Height);
    }
}
