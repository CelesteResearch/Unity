using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Celeste.Graphics.Rendering.Buffers;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Vertices;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    public sealed unsafe class UnitySourceGeometryIO : IDisposable
    {
        private readonly IUnitySourceGeometrySubmission submission;

        private readonly Dictionary<IntPtr, UnitySourceGeometryBuffer> buffers =
            new Dictionary<IntPtr, UnitySourceGeometryBuffer>();

        private readonly List<UnitySourceVertexLayout> layouts = new List<UnitySourceVertexLayout>();

        private readonly Dictionary<(UnitySourceVertexLayout layout, UnitySourceGeometryBuffer indices,
            SourceIndexElementSize size), UnitySourceGeometryMesh> meshes =
            new Dictionary<(UnitySourceVertexLayout, UnitySourceGeometryBuffer, SourceIndexElementSize),
                UnitySourceGeometryMesh>();

        private readonly ConcurrentQueue<(IntPtr token, bool index)> disposals = new ConcurrentQueue<(IntPtr, bool)>();
        private volatile bool disposed;
        private UnitySourceVertexLayout current;
        private int nextToken = 1;

        public UnitySourceGeometryIO(IUnitySourceGeometrySubmission submission)
        {
            this.submission = submission ?? throw new ArgumentNullException(nameof(submission));
        }

        public IntPtr GenVertexBuffer(byte dynamic, SourceBufferUsage usage, int byteCount) =>
            Generate(dynamic, byteCount, false);

        public IntPtr GenIndexBuffer(byte dynamic, SourceBufferUsage usage, int byteCount) =>
            Generate(dynamic, byteCount, true);

        private IntPtr Generate(byte dynamic, int byteCount, bool index)
        {
            var token = new IntPtr(nextToken++);
            buffers.Add(token, new UnitySourceGeometryBuffer(byteCount, index, dynamic != 0));
            return token;
        }

        public byte SupportsNoOverwrite() => 1;
        public byte SupportsHardwareInstancing() => 0;

        public void SetVertexBufferData(IntPtr buffer, int offset, IntPtr data, int elementCount, int elementSize,
            int stride, SourceSetDataOptions options)
        {
            var resource = Borrow(buffer, false);
            if (resource.Consumers.Count != 0) submission.BeforeGeometryMutation();
            resource.Write(offset, data, elementCount, elementSize, stride, options);
        }

        public void SetIndexBufferData(IntPtr buffer, int offset, IntPtr data, int byteCount,
            SourceSetDataOptions options)
        {
            var resource = Borrow(buffer, true);
            if (resource.Consumers.Count != 0) submission.BeforeGeometryMutation();
            resource.Write(offset, data, byteCount, 1, 1, options);
        }

        public void GetVertexBufferData(IntPtr buffer, int offset, IntPtr data, int elementCount, int elementSize,
            int stride)
            => Borrow(buffer, false).Read(offset, data, elementCount, elementSize, stride);

        public void GetIndexBufferData(IntPtr buffer, int offset, IntPtr data, int byteCount)
            => Borrow(buffer, true).Read(offset, data, byteCount, 1, 1);

        public void ApplyVertexBufferBindings(SourceNativeVertexBufferBinding* bindings, int count, byte updated,
            int baseVertex)
        {
            if (current != null && (updated == 0 || current.Matches(bindings, count))) return;
            for (int i = 0; i < layouts.Count; i++)
            {
                if (!layouts[i].Matches(bindings, count)) continue;
                current = layouts[i];
                return;
            }

            current = new UnitySourceVertexLayout(bindings, count);
            layouts.Add(current);
        }

        public void DrawIndexedPrimitives(SourcePrimitiveType type, int baseVertex, int minVertexIndex, int numVertices,
            int startIndex, int primitiveCount, IntPtr indices, SourceIndexElementSize indexSize)
            => BorrowMesh(Borrow(indices, true), indexSize).Draw(type, startIndex, primitiveCount, baseVertex);

        public void DrawPrimitives(SourcePrimitiveType primitiveType, int startVertex, int primitiveCount)
            => BorrowMesh(null, SourceIndexElementSize.ThirtyTwoBits)
                .Draw(primitiveType, startVertex, primitiveCount, 0);

        public void DrawInstancedPrimitives(SourcePrimitiveType type, int baseVertex, int minVertexIndex,
            int numVertices,
            int startIndex, int primitiveCount, int instanceCount, IntPtr indices, SourceIndexElementSize indexSize)
            => throw new NotSupportedException("This Mesh backend does not advertise hardware instancing.");

        private UnitySourceGeometryMesh BorrowMesh(UnitySourceGeometryBuffer indices, SourceIndexElementSize size)
        {
            if (current == null) throw new InvalidOperationException("Apply source vertex bindings before drawing.");
            var key = (current, indices, size);
            if (meshes.TryGetValue(key, out var mesh)) return mesh;
            var vertices = new UnitySourceGeometryBuffer[current.Bindings.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Borrow(current.Bindings[i].vertexBuffer, false);
            mesh = new UnitySourceGeometryMesh(current, vertices, indices, size, submission);
            meshes.Add(key, mesh);
            return mesh;
        }

        private UnitySourceGeometryBuffer Borrow(IntPtr token, bool index)
        {
            var buffer = buffers[token];
            if (buffer.IsIndex != index)
                throw new InvalidOperationException("The opaque buffer handle has a different resource kind.");
            return buffer;
        }

        // Source resource finalizers may call these from a different managed thread.
        public void AddDisposeVertexBuffer(IntPtr buffer)
        {
            if (!disposed) disposals.Enqueue((buffer, false));
        }

        public void AddDisposeIndexBuffer(IntPtr buffer)
        {
            if (!disposed) disposals.Enqueue((buffer, true));
        }

        // Call from the Unity owner thread, alongside the other physical resource drains.
        public void DrainDisposals()
        {
            bool flushed = false;
            while (disposals.TryDequeue(out var disposal))
            {
                if (!flushed)
                {
                    submission.BeforeGeometryMutation();
                    flushed = true;
                }

                Remove(disposal.token, disposal.index);
            }
        }

        private void Remove(IntPtr token, bool index)
        {
            var buffer = Borrow(token, index);
            while (buffer.Consumers.Count != 0)
            {
                var mesh = buffer.Consumers[buffer.Consumers.Count - 1];
                meshes.Remove((mesh.Layout, mesh.IndexBuffer, mesh.IndexBufferSize));
                mesh.Dispose();
            }

            for (int i = layouts.Count - 1; i >= 0; i--)
            {
                bool uses = false;
                for (int slot = 0; slot < layouts[i].Bindings.Length; slot++)
                    uses |= layouts[i].Bindings[slot].vertexBuffer == token;
                if (!uses || index) continue;
                if (ReferenceEquals(current, layouts[i])) current = null;
                layouts.RemoveAt(i);
            }

            buffers.Remove(token);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DrainDisposals();
            submission.BeforeGeometryMutation();
            foreach (var mesh in meshes.Values) mesh.Dispose();
            meshes.Clear();
            buffers.Clear();
            layouts.Clear();
            current = null;
        }
    }
}
