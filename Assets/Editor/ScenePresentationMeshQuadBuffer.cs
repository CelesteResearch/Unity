using System;

namespace Celeste.Unity.Editor
{
    internal sealed class ScenePresentationMeshQuadBuffer
    {
        private ScenePresentationMeshQuad[] items = Array.Empty<ScenePresentationMeshQuad>();

        public int Count { get; private set; }

        internal ref readonly ScenePresentationMeshQuad this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                return ref items[index];
            }
        }

        internal ReadOnlySpan<ScenePresentationMeshQuad> AsSpan() => new(items, 0, Count);

        internal void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= items.Length) return;
            int capacity = items.Length;
            do
            {
                capacity = capacity > int.MaxValue / 2 ? int.MaxValue : Math.Max(4, capacity * 2);
            } while (capacity < requiredCapacity);

            // Reach the same capacity as repeated Add calls without allocating their intermediate arrays.
            Array.Resize(ref items, capacity);
        }

        internal void Add(in ScenePresentationMeshQuad value)
        {
            if (Count == items.Length)
            {
                int capacity = items.Length > int.MaxValue / 2 ? int.MaxValue : Math.Max(4, items.Length * 2);
                Array.Resize(ref items, capacity);
            }

            items[Count] = value;
            Count++;
        }

        internal void Add(global::Celeste.Graphics.Rendering.Textures.SourceTexture2DResource texture,
            in global::Celeste.Graphics.Rendering.Batches.SourceSpriteQuad quad, byte primitiveTriangles = 0)
        {
            if (Count == items.Length)
            {
                int capacity = items.Length > int.MaxValue / 2 ? int.MaxValue : Math.Max(4, items.Length * 2);
                Array.Resize(ref items, capacity);
            }

            // Fill the destination directly instead of copying a reference-containing temporary.
            ref var destination = ref items[Count];
            destination.Texture = texture;
            destination.Quad = quad;
            destination.PrimitiveTriangles = primitiveTriangles;
            destination.ForcePointClamp = false;
            Count++;
        }

        internal void Clear()
        {
            Array.Clear(items, 0, Count);
            Count = 0;
        }
    }
}
