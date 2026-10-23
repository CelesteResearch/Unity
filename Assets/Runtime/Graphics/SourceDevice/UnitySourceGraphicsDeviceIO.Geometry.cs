using System;
using Celeste.Graphics.Rendering.Buffers;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Vertices;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsDeviceIO
    {
        public IntPtr GenVertexBuffer(byte dynamic, SourceBufferUsage usage, int byteCount)
            => components.Geometry.GenVertexBuffer(dynamic, usage, byteCount);

        public IntPtr GenIndexBuffer(byte dynamic, SourceBufferUsage usage, int byteCount)
            => components.Geometry.GenIndexBuffer(dynamic, usage, byteCount);

        public void SetVertexBufferData(IntPtr buffer, int offset, IntPtr data, int elementCount,
            int elementSize, int stride, SourceSetDataOptions options)
            => components.Geometry.SetVertexBufferData(buffer, offset, data, elementCount, elementSize, stride,
                options);

        public void SetIndexBufferData(IntPtr buffer, int offset, IntPtr data, int byteCount,
            SourceSetDataOptions options)
            => components.Geometry.SetIndexBufferData(buffer, offset, data, byteCount, options);

        public void GetVertexBufferData(IntPtr buffer, int offset, IntPtr data, int elementCount, int elementSize,
            int stride) =>
            components.Geometry.GetVertexBufferData(buffer, offset, data, elementCount, elementSize, stride);

        public void GetIndexBufferData(IntPtr buffer, int offset, IntPtr data, int byteCount)
            => components.Geometry.GetIndexBufferData(buffer, offset, data, byteCount);

        public void ApplyVertexBufferBindings(SourceNativeVertexBufferBinding* bindings, int count, byte updated,
            int baseVertex) => components.Geometry.ApplyVertexBufferBindings(bindings, count, updated, baseVertex);

        public void DrawPrimitives(SourcePrimitiveType primitiveType, int startVertex, int primitiveCount)
            => components.Geometry.DrawPrimitives(primitiveType, startVertex, primitiveCount);

        public void DrawIndexedPrimitives(SourcePrimitiveType type, int baseVertex, int minVertexIndex, int numVertices,
            int startIndex, int primitiveCount, IntPtr indices, SourceIndexElementSize indexSize)
            => components.Geometry.DrawIndexedPrimitives(type, baseVertex, minVertexIndex, numVertices, startIndex,
                primitiveCount, indices, indexSize);

        public void DrawInstancedPrimitives(SourcePrimitiveType type, int baseVertex, int minVertexIndex,
            int numVertices,
            int startIndex, int primitiveCount, int instanceCount, IntPtr indices, SourceIndexElementSize indexSize)
            => components.Geometry.DrawInstancedPrimitives(type, baseVertex, minVertexIndex, numVertices, startIndex,
                primitiveCount, instanceCount, indices, indexSize);

        public byte SupportsNoOverwrite() => components.Geometry.SupportsNoOverwrite();
        public byte SupportsHardwareInstancing() => components.Geometry.SupportsHardwareInstancing();
        public void AddDisposeVertexBuffer(IntPtr buffer) => components.Geometry.AddDisposeVertexBuffer(buffer);
        public void AddDisposeIndexBuffer(IntPtr buffer) => components.Geometry.AddDisposeIndexBuffer(buffer);
    }
}
