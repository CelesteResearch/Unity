using System;
using System.Collections.Generic;
using Celeste.Graphics.Rendering.Vertices;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    internal sealed unsafe class UnitySourceGeometryBuffer
    {
        internal readonly byte[] Bytes;
        internal readonly bool IsIndex;
        internal readonly bool Dynamic;
        internal readonly List<UnitySourceGeometryMesh> Consumers = new List<UnitySourceGeometryMesh>();

        internal UnitySourceGeometryBuffer(int byteCount, bool index, bool dynamic)
        {
            Bytes = new byte[byteCount];
            IsIndex = index;
            Dynamic = dynamic;
        }

        internal void Write(int offset, IntPtr source, int count, int size, int stride, SourceSetDataOptions options)
        {
            if (stride == 0) stride = size;
            int end = count == 0 ? offset : checked(offset + (count - 1) * stride + size);
            if (offset < 0 || count < 0 || size <= 0 || stride < size || end > Bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            // Discard makes unwritten bytes undefined; retaining their old mirror is legal.
            // NoOverwrite preserves all other bytes. Neither option zeroes/reallocates the buffer.
            fixed (byte* target = Bytes)
            {
                byte* input = (byte*)source;
                if (stride == size)
                    Buffer.MemoryCopy(input, target + offset, Bytes.Length - offset, (long)count * size);
                else
                    for (int i = 0; i < count; i++)
                        Buffer.MemoryCopy(input + i * size, target + offset + i * stride, size, size);
            }

            for (int i = 0; i < Consumers.Count; i++) Consumers[i].MarkDirty(this, offset, end);
        }

        internal void Read(int offset, IntPtr target, int count, int size, int stride)
        {
            if (stride == 0) stride = size;
            int end = count == 0 ? offset : checked(offset + (count - 1) * stride + size);
            if (offset < 0 || count < 0 || size <= 0 || stride < size || end > Bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            fixed (byte* source = Bytes)
            {
                byte* output = (byte*)target;
                if (stride == size)
                    Buffer.MemoryCopy(source + offset, output, (long)count * size, (long)count * size);
                else
                    for (int i = 0; i < count; i++)
                        Buffer.MemoryCopy(source + offset + i * stride, output + i * size, size, size);
            }
        }
    }
}
