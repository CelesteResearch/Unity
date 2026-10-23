using System;
using System.Collections.Generic;
using Celeste.Graphics.Rendering.Buffers;
using Celeste.Graphics.Rendering.Vertices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    internal sealed class UnitySourceGeometryMesh : IDisposable
    {
        private const MeshUpdateFlags UploadFlags =
            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        private readonly IUnitySourceGeometrySubmission submission;
        internal readonly UnitySourceVertexLayout Layout;
        private readonly UnitySourceGeometryBuffer[] buffers;
        internal readonly UnitySourceGeometryBuffer IndexBuffer;
        private readonly SourceIndexElementSize indexSize;
        internal SourceIndexElementSize IndexBufferSize => indexSize;
        private readonly byte[] packedVertices;
        private readonly int vertexCount, sourceIndexCount;
        private int dirtyVertexFirst, dirtyVertexLast;
        private int dirtyIndexFirst, dirtyIndexLast;
        private readonly UnitySourceIndexData indices;
        private int usedIndices;
        private readonly List<UnitySourceGeometryDrawSlot> slots = new List<UnitySourceGeometryDrawSlot>();
        private readonly UnitySourceSubmissionSlots submissionSlots = new UnitySourceSubmissionSlots();
        private ulong sourceIndexVersion;
        private readonly Mesh mesh;

        internal UnitySourceGeometryMesh(UnitySourceVertexLayout layout, UnitySourceGeometryBuffer[] buffers,
            UnitySourceGeometryBuffer indexBuffer, SourceIndexElementSize size,
            IUnitySourceGeometrySubmission submission)
        {
            this.submission = submission;
            Layout = layout;
            this.buffers = buffers;
            IndexBuffer = indexBuffer;
            indexSize = size;
            int count = int.MaxValue;
            bool dynamic = indexBuffer != null && indexBuffer.Dynamic;
            for (int i = 0; i < buffers.Length; i++)
            {
                count = Math.Min(count,
                    buffers[i].Bytes.Length / layout.Bindings[i].vertexDeclaration.vertexStride -
                    layout.Bindings[i].vertexOffset);
                dynamic |= buffers[i].Dynamic;
            }

            if (count < 0 || count == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(buffers));
            vertexCount = count;
            // Canonical zero-offset streams can upload the owning raw mirror directly.
            // Reordered/padded/nonzero-offset layouts keep a separate persistent packed view.
            packedVertices = layout.DirectCopy && layout.Bindings[0].vertexOffset == 0
                ? buffers[0].Bytes
                : new byte[checked(vertexCount * layout.Stride)];
            dirtyVertexFirst = 0;
            dirtyVertexLast = vertexCount;
            int elementSize = size == SourceIndexElementSize.SixteenBits ? 2 : 4;
            sourceIndexCount = indexBuffer == null ? vertexCount : indexBuffer.Bytes.Length / elementSize;
            usedIndices = sourceIndexCount;
            int capacity = Math.Max(1, sourceIndexCount);
            indices = new UnitySourceIndexData(size, capacity);
            dirtyIndexFirst = 0;
            dirtyIndexLast = sourceIndexCount;
            submission.BeforeGeometryMutation();
            mesh = new Mesh { name = "Celeste source geometry" };
            try
            {
                if (dynamic) mesh.MarkDynamic();
                for (int i = 0; i < layout.Descriptors.Length; i++)
                {
                    var descriptor = layout.Descriptors[i];
                    if (!SystemInfo.SupportsVertexAttributeFormat(descriptor.format, descriptor.dimension))
                        throw new NotSupportedException(
                            "This graphics backend does not support the source vertex format.");
                }

                mesh.SetVertexBufferParams(vertexCount, layout.Descriptors);
                mesh.SetIndexBufferParams(capacity,
                    size == SourceIndexElementSize.SixteenBits ? IndexFormat.UInt16 : IndexFormat.UInt32);
                for (int i = 0; i < buffers.Length; i++)
                    if (!buffers[i].Consumers.Contains(this))
                        buffers[i].Consumers.Add(this);
                indexBuffer?.Consumers.Add(this);
            }
            catch
            {
                UnityEngine.Object.Destroy(mesh);
                throw;
            }
        }

        internal void MarkDirty(UnitySourceGeometryBuffer buffer, int first, int end)
        {
            if (end <= first) return;
            if (ReferenceEquals(buffer, IndexBuffer))
            {
                int size = indexSize == SourceIndexElementSize.SixteenBits ? 2 : 4;
                dirtyIndexFirst = Math.Min(dirtyIndexFirst, first / size);
                dirtyIndexLast = Math.Max(dirtyIndexLast, Math.Min(sourceIndexCount, (end + size - 1) / size));
            }
            else
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    if (!ReferenceEquals(buffers[i], buffer)) continue;
                    int stride = Layout.Bindings[i].vertexDeclaration.vertexStride;
                    int origin = Layout.Bindings[i].vertexOffset;
                    int a = Math.Max(0, first / stride - origin);
                    int b = Math.Min(vertexCount, (end + stride - 1) / stride - origin);
                    if (b > a)
                    {
                        dirtyVertexFirst = Math.Min(dirtyVertexFirst, a);
                        dirtyVertexLast = Math.Max(dirtyVertexLast, b);
                    }
                }
            }
        }

        internal void Draw(SourcePrimitiveType type, int start, int count, int baseVertex)
        {
            if (count == 0) return;
            int required = RequiredIndices(type, count);
            if (start < 0 || count < 0 || required > sourceIndexCount - start)
                throw new ArgumentOutOfRangeException(nameof(start));
            UploadDirtyVertices();
            UploadDirtyIndices();
            int subMesh = submissionSlots.Reserve(submission.SubmissionVersion);
            if (subMesh == slots.Count)
            {
                slots.Add(new UnitySourceGeometryDrawSlot());
                mesh.subMeshCount = slots.Count;
            }

            var slot = slots[subMesh];
            int outputCount = type == SourcePrimitiveType.TriangleStrip ? checked(count * 3) : required;
            if (slot.OutputCapacity < outputCount)
            {
                // Unity requires disjoint submesh ranges, even when source draws reuse the same indices.
                // Keep one persistent projection range per simultaneous slot, not every historical source range.
                slot.OutputStart = usedIndices;
                slot.OutputCapacity = Math.Max(outputCount, checked(slot.OutputCapacity * 2));
                usedIndices = checked(usedIndices + slot.OutputCapacity);
                submission.BeforeGeometryMutation();
                EnsureIndexCapacity(usedIndices);
                slot.ProjectedCount = 0;
            }

            int outputStart = slot.OutputStart;
            if (slot.Type != type || slot.ProjectedVersion != sourceIndexVersion || slot.ProjectedStart != start ||
                slot.ProjectedCount != count)
            {
                submission.BeforeGeometryMutation();
                if (type == SourcePrimitiveType.TriangleStrip) indices.ExpandStrip(start, count, outputStart);
                else indices.CopyRange(start, outputCount, outputStart);
                UploadIndexRange(outputStart, outputCount);
                slot.ProjectedStart = start;
                slot.ProjectedCount = count;
                slot.ProjectedVersion = sourceIndexVersion;
            }

            if (!slot.HasDescriptor || slot.Type != type || slot.SourceStart != start ||
                slot.PrimitiveCount != count || slot.BaseVertex != baseVertex ||
                slot.Descriptor.indexStart != outputStart)
            {
                slot.Descriptor = new SubMeshDescriptor(outputStart, outputCount, Topology(type))
                {
                    baseVertex = baseVertex, firstVertex = 0, vertexCount = vertexCount
                };
                mesh.SetSubMesh(subMesh, slot.Descriptor, UploadFlags);
                slot.HasDescriptor = true;
                slot.Type = type;
                slot.SourceStart = start;
                slot.PrimitiveCount = count;
                slot.BaseVertex = baseVertex;
            }

            if (submission.Submit(mesh, subMesh))
                submissionSlots.ObserveSubmitted(submission.SubmissionVersion);
            else
                submissionSlots.ReleaseUnsubmitted(submission.SubmissionVersion);
        }

        private void UploadDirtyVertices()
        {
            int count = dirtyVertexLast - dirtyVertexFirst;
            if (count <= 0) return;
            submission.BeforeGeometryMutation();
            Layout.Pack(buffers, packedVertices, dirtyVertexFirst, count);
            int byteStart = dirtyVertexFirst * Layout.Stride;
            mesh.SetVertexBufferData(packedVertices, byteStart, byteStart, count * Layout.Stride, 0, UploadFlags);
            dirtyVertexFirst = vertexCount;
            dirtyVertexLast = 0;
        }

        private void UploadDirtyIndices()
        {
            int count = dirtyIndexLast - dirtyIndexFirst;
            if (count <= 0) return;
            submission.BeforeGeometryMutation();
            indices.CopySource(IndexBuffer, dirtyIndexFirst, count);
            UploadIndexRange(dirtyIndexFirst, count);
            sourceIndexVersion = unchecked(sourceIndexVersion + 1);
            dirtyIndexFirst = sourceIndexCount;
            dirtyIndexLast = 0;
        }

        private void EnsureIndexCapacity(int needed)
        {
            if (indices.Capacity >= needed) return;
            submission.BeforeGeometryMutation();
            indices.Grow(needed);
            mesh.SetIndexBufferParams(indices.Capacity, indices.Format);
            UploadIndexRange(0, usedIndices);
            mesh.subMeshCount = slots.Count;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].HasDescriptor)
                    mesh.SetSubMesh(i, slots[i].Descriptor, UploadFlags);
        }

        private void UploadIndexRange(int start, int count)
            => indices.Upload(mesh, start, count, UploadFlags);

        internal static int RequiredIndices(SourcePrimitiveType type, int count)
        {
            switch (type)
            {
                case SourcePrimitiveType.TriangleList: return checked(count * 3);
                case SourcePrimitiveType.TriangleStrip: return checked(count + 2);
                case SourcePrimitiveType.LineList: return checked(count * 2);
                case SourcePrimitiveType.LineStrip: return checked(count + 1);
                case SourcePrimitiveType.PointListEXT: return count;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        internal static MeshTopology Topology(SourcePrimitiveType type)
        {
            switch (type)
            {
                case SourcePrimitiveType.TriangleList:
                case SourcePrimitiveType.TriangleStrip: return MeshTopology.Triangles;
                case SourcePrimitiveType.LineList: return MeshTopology.Lines;
                case SourcePrimitiveType.LineStrip: return MeshTopology.LineStrip;
                case SourcePrimitiveType.PointListEXT: return MeshTopology.Points;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Dispose()
        {
            submission.BeforeGeometryMutation();
            for (int i = 0; i < buffers.Length; i++) buffers[i].Consumers.Remove(this);
            IndexBuffer?.Consumers.Remove(this);
            UnityEngine.Object.Destroy(mesh);
        }
    }
}
