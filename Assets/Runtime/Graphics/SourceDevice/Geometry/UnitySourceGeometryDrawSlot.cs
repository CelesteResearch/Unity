using Celeste.Graphics.Rendering.Vertices;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    internal sealed class UnitySourceGeometryDrawSlot
    {
        internal SubMeshDescriptor Descriptor;
        internal bool HasDescriptor;
        internal int SourceStart, PrimitiveCount, BaseVertex;
        internal SourcePrimitiveType Type;
        internal int OutputStart, OutputCapacity;
        internal int ProjectedStart, ProjectedCount;
        internal ulong ProjectedVersion;
    }
}
