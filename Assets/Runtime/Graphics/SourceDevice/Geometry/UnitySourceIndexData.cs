using System;
using Celeste.Graphics.Rendering.Buffers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    internal sealed class UnitySourceIndexData
    {
        private ushort[] indices16;
        private uint[] indices32;
        internal IndexFormat Format => indices16 != null ? IndexFormat.UInt16 : IndexFormat.UInt32;
        internal int Capacity => indices16 != null ? indices16.Length : indices32.Length;

        internal UnitySourceIndexData(SourceIndexElementSize size, int capacity)
        {
            if (size == SourceIndexElementSize.SixteenBits) indices16 = new ushort[capacity];
            else if (size == SourceIndexElementSize.ThirtyTwoBits) indices32 = new uint[capacity];
            else throw new ArgumentOutOfRangeException(nameof(size));
        }

        internal bool Grow(int needed)
        {
            if (Capacity >= needed) return false;
            int capacity = Math.Max(needed, checked(Capacity * 2));
            if (indices16 != null) Array.Resize(ref indices16, capacity);
            else Array.Resize(ref indices32, capacity);
            return true;
        }

        internal uint Get(int index) => indices16 != null ? indices16[index] : indices32[index];

        internal void CopyRange(int start, int count, int outputStart)
        {
            if (indices16 != null) Array.Copy(indices16, start, indices16, outputStart, count);
            else Array.Copy(indices32, start, indices32, outputStart, count);
        }

        private void Set(int index, uint value)
        {
            if (indices16 != null) indices16[index] = (ushort)value;
            else indices32[index] = value;
        }

        internal void CopySource(UnitySourceGeometryBuffer source, int first, int count)
        {
            for (int index = first; index < first + count; index++)
            {
                uint value;
                if (source == null) value = (uint)index;
                else if (indices16 != null)
                {
                    int offset = index * 2;
                    value = (uint)(source.Bytes[offset] | source.Bytes[offset + 1] << 8);
                }
                else
                {
                    int offset = index * 4;
                    value = (uint)(source.Bytes[offset] | source.Bytes[offset + 1] << 8 |
                                   source.Bytes[offset + 2] << 16 | source.Bytes[offset + 3] << 24);
                }

                Set(index, value);
            }
        }

        internal void ExpandStrip(int start, int count, int outputStart)
        {
            for (int primitive = 0; primitive < count; primitive++)
            {
                int a = start + primitive;
                int b = a + 1;
                if ((primitive & 1) != 0)
                {
                    int swap = a;
                    a = b;
                    b = swap;
                }

                int target = outputStart + primitive * 3;
                Set(target, Get(a));
                Set(target + 1, Get(b));
                Set(target + 2, Get(start + primitive + 2));
            }
        }

        internal void Upload(Mesh mesh, int start, int count, MeshUpdateFlags flags)
        {
            if (indices16 != null) mesh.SetIndexBufferData(indices16, start, start, count, flags);
            else mesh.SetIndexBufferData(indices32, start, start, count, flags);
        }
    }
}
