using System;
using Celeste.Graphics.Rendering.Batches;
using Celeste.Graphics.Rendering.Textures;

namespace Celeste.Unity.Editor
{
    // Buffers fill elements in place; readers use readonly refs, spans and methods.
    internal struct ScenePresentationMeshQuad
    {
        internal SourceTexture2DResource Texture;

        internal SourceSpriteQuad Quad;

        // Explicit source primitive provenance: 0 ordinary, 1 original 012, 2 original 213, 3 both.
        internal byte PrimitiveTriangles;
        internal bool ForcePointClamp;

        internal ScenePresentationMeshQuad(SourceTexture2DResource texture, in SourceSpriteQuad quad,
            byte primitiveTriangles = 0)
        {
            Texture = texture;
            Quad = quad;
            PrimitiveTriangles = primitiveTriangles;
            ForcePointClamp = false;
        }

        internal readonly unsafe bool Matches(in ScenePresentationMeshQuad other)
        {
            if (!ReferenceEquals(Texture, other.Texture) || PrimitiveTriangles != other.PrimitiveTriangles ||
                ForcePointClamp != other.ForcePointClamp)
                return false;
            fixed (SourceSpriteQuad* quadA = &Quad)
            fixed (SourceSpriteQuad* quadB = &other.Quad)
            {
                var wordsA = (ulong*)quadA;
                var wordsB = (ulong*)quadB;
                for (int i = 0; i < sizeof(SourceSpriteQuad) / sizeof(ulong); i++)
                    if (wordsA[i] != wordsB[i])
                        return false;
                return true;
            }
        }

        internal static SourceSpriteQuad Triangle(in SourceSpriteQuad quad, bool second)
        {
            var triangle = quad;
            if (second)
            {
                triangle.Position0 = quad.Position2;
                triangle.TextureCoordinate0 = quad.TextureCoordinate2;
                triangle.Color0 = quad.Color2;
                triangle.Position2 = quad.Position3;
                triangle.TextureCoordinate2 = quad.TextureCoordinate3;
                triangle.Color2 = quad.Color3;
            }

            triangle.Position3 = triangle.Position2;
            triangle.TextureCoordinate3 = triangle.TextureCoordinate2;
            triangle.Color3 = triangle.Color2;
            return triangle;
        }

        internal static Celeste.Core.Geometry.Bounds TriangleBounds(in SourceSpriteQuad quad, bool second)
        {
            var a = second ? quad.Position2 : quad.Position0;
            var b = quad.Position1;
            var c = second ? quad.Position3 : quad.Position2;
            // Same operation grouping as Bounds on the original (a,b,c,c) record.
            float left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, c.X));
            float top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, c.Y));
            float right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, c.X));
            float bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, c.Y));
            return new Celeste.Core.Geometry.Bounds(left, top, right - left, bottom - top);
        }

        internal readonly bool TryGpuQuad(out SourceSpriteQuad quad, out int invalidTriangles)
        {
            bool first = (PrimitiveTriangles & 1) != 0;
            bool second = (PrimitiveTriangles & 2) != 0;
            bool commonFinite = Finite(Quad.Position1) && Finite(Quad.Position2);
            bool firstFinite = first && commonFinite && Finite(Quad.Position0);
            bool secondFinite = second && commonFinite && Finite(Quad.Position3);
            invalidTriangles = (first && !firstFinite ? 1 : 0) + (second && !secondFinite ? 1 : 0);
            if (firstFinite && secondFinite) quad = Quad;
            else if (firstFinite) quad = Triangle(in Quad, false);
            else if (secondFinite) quad = Triangle(in Quad, true);
            else
            {
                quad = default;
                return false;
            }

            return true;
        }

        private static bool Finite(System.Numerics.Vector3 value) =>
            (BitConverter.SingleToInt32Bits(value.X) & 0x7f800000) != 0x7f800000 &&
            (BitConverter.SingleToInt32Bits(value.Y) & 0x7f800000) != 0x7f800000 &&
            (BitConverter.SingleToInt32Bits(value.Z) & 0x7f800000) != 0x7f800000;
    }
}
