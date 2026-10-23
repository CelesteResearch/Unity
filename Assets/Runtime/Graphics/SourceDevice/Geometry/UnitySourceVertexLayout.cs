using System;
using System.Collections.Generic;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Vertices;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice.Geometry
{
    internal sealed unsafe class UnitySourceVertexLayout
    {
        internal readonly SourceNativeVertexBufferBinding[] Bindings;
        internal readonly SourceVertexElement[][] Elements;
        internal readonly VertexAttributeDescriptor[] Descriptors;
        internal readonly int[] SourceSlots, SourceOffsets, TargetOffsets, Sizes;
        private readonly SourceVertexElementFormat[] sourceFormats;
        internal readonly int Stride;
        internal readonly bool DirectCopy;

        internal UnitySourceVertexLayout(SourceNativeVertexBufferBinding* bindings, int count)
        {
            Bindings = new SourceNativeVertexBufferBinding[count];
            Elements = new SourceVertexElement[count][];
            var fields =
                new List<(VertexAttributeDescriptor descriptor, int slot, int offset, int size,
                    SourceVertexElementFormat format)>();
            int attributes = 0;
            for (int slot = 0; slot < count; slot++)
            {
                var binding = bindings[slot];
                if (binding.instanceFrequency != 0)
                    throw new NotSupportedException("Instanced vertex bindings require a separate instancing backend.");
                if (binding.vertexOffset < 0 || binding.vertexDeclaration.vertexStride <= 0)
                    throw new ArgumentOutOfRangeException(nameof(bindings));
                Bindings[slot] = binding;
                Elements[slot] = new SourceVertexElement[binding.vertexDeclaration.elementCount];
                var elements = (SourceVertexElement*)binding.vertexDeclaration.elements;
                for (int i = 0; i < Elements[slot].Length; i++)
                {
                    var element = elements[i];
                    Elements[slot][i] = element;
                    var descriptor = Describe(element);
                    int bit = 1 << (int)descriptor.attribute;
                    if ((attributes & bit) != 0)
                        throw new NotSupportedException(
                            "Duplicate source vertex semantics cannot share a Unity Mesh attribute.");
                    attributes |= bit;
                    int size = FormatSize(element.VertexElementFormat);
                    if (element.Offset < 0 || element.Offset + size > binding.vertexDeclaration.vertexStride)
                        throw new ArgumentOutOfRangeException(nameof(bindings));
                    fields.Add((descriptor, slot, element.Offset, size, element.VertexElementFormat));
                }
            }

            fields.Sort((left, right) => left.descriptor.attribute.CompareTo(right.descriptor.attribute));
            int fieldCount = fields.Count;
            Descriptors = new VertexAttributeDescriptor[fieldCount];
            SourceSlots = new int[fieldCount];
            SourceOffsets = new int[fieldCount];
            TargetOffsets = new int[fieldCount];
            Sizes = new int[fieldCount];
            sourceFormats = new SourceVertexElementFormat[fieldCount];
            bool direct = count == 1;
            int stride = 0;
            for (int i = 0; i < fieldCount; i++)
            {
                var field = fields[i];
                Descriptors[i] = field.descriptor;
                SourceSlots[i] = field.slot;
                SourceOffsets[i] = field.offset;
                TargetOffsets[i] = stride;
                sourceFormats[i] = field.format;
                bool expand = field.format == SourceVertexElementFormat.Byte4 ||
                              field.format == SourceVertexElementFormat.Short2 ||
                              field.format == SourceVertexElementFormat.Short4;
                Sizes[i] = expand ? field.descriptor.dimension * sizeof(float) : field.size;
                direct &= !expand && field.slot == 0 && field.offset == stride;
                stride += Sizes[i];
            }

            Stride = stride;
            DirectCopy = direct && stride == Bindings[0].vertexDeclaration.vertexStride;
        }

        internal bool Matches(SourceNativeVertexBufferBinding* bindings, int count)
        {
            if (count != Bindings.Length) return false;
            for (int slot = 0; slot < count; slot++)
            {
                var a = Bindings[slot];
                var b = bindings[slot];
                if (a.vertexBuffer != b.vertexBuffer || a.vertexOffset != b.vertexOffset ||
                    a.instanceFrequency != b.instanceFrequency ||
                    a.vertexDeclaration.vertexStride != b.vertexDeclaration.vertexStride ||
                    Elements[slot].Length != b.vertexDeclaration.elementCount) return false;
                var elements = (SourceVertexElement*)b.vertexDeclaration.elements;
                for (int i = 0; i < Elements[slot].Length; i++)
                    if (Elements[slot][i] != elements[i])
                        return false;
            }

            return true;
        }

        internal void Pack(UnitySourceGeometryBuffer[] buffers, byte[] output, int first, int count)
        {
            if (DirectCopy)
            {
                if (ReferenceEquals(buffers[0].Bytes, output)) return;
                int offset = (Bindings[0].vertexOffset + first) * Stride;
                Buffer.BlockCopy(buffers[0].Bytes, offset, output, first * Stride, count * Stride);
                return;
            }

            for (int field = 0; field < Descriptors.Length; field++)
            {
                int slot = SourceSlots[field];
                int sourceStride = Bindings[slot].vertexDeclaration.vertexStride;
                int offset = (Bindings[slot].vertexOffset + first) * sourceStride + SourceOffsets[field];
                fixed (byte* source = buffers[slot].Bytes)
                fixed (byte* target = output)
                {
                    // Source SM3 vertex inputs are floating point. Expand unnormalized integer
                    // attributes only when their existing persistent packed view is dirty.
                    if (sourceFormats[field] == SourceVertexElementFormat.Byte4)
                    {
                        for (int vertex = first; vertex < first + count; vertex++, offset += sourceStride)
                        {
                            float* values = (float*)(target + vertex * Stride + TargetOffsets[field]);
                            for (int component = 0; component < 4; component++)
                                values[component] = source[offset + component];
                        }
                    }
                    else if (sourceFormats[field] == SourceVertexElementFormat.Short2 ||
                             sourceFormats[field] == SourceVertexElementFormat.Short4)
                    {
                        int components = Descriptors[field].dimension;
                        for (int vertex = first; vertex < first + count; vertex++, offset += sourceStride)
                        {
                            short* values = (short*)(source + offset);
                            float* packed = (float*)(target + vertex * Stride + TargetOffsets[field]);
                            for (int component = 0; component < components; component++)
                                packed[component] = values[component];
                        }
                    }
                    else
                        for (int vertex = first; vertex < first + count; vertex++, offset += sourceStride)
                            Buffer.MemoryCopy(source + offset, target + vertex * Stride + TargetOffsets[field],
                                Sizes[field], Sizes[field]);
                }
            }
        }

        internal static int FormatSize(SourceVertexElementFormat format)
        {
            switch (format)
            {
                case SourceVertexElementFormat.Single:
                case SourceVertexElementFormat.Color:
                case SourceVertexElementFormat.Byte4:
                case SourceVertexElementFormat.Short2:
                case SourceVertexElementFormat.NormalizedShort2:
                case SourceVertexElementFormat.HalfVector2: return 4;
                case SourceVertexElementFormat.Vector2:
                case SourceVertexElementFormat.Short4:
                case SourceVertexElementFormat.NormalizedShort4:
                case SourceVertexElementFormat.HalfVector4: return 8;
                case SourceVertexElementFormat.Vector3: return 12;
                case SourceVertexElementFormat.Vector4: return 16;
                default: throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        internal static VertexAttributeDescriptor Describe(SourceVertexElement element)
        {
            VertexAttribute attribute;
            if (element.VertexElementUsage == SourceVertexElementUsage.TextureCoordinate &&
                (uint)element.UsageIndex < 8)
                attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + element.UsageIndex);
            else if (element.VertexElementUsage == SourceVertexElementUsage.Color && element.UsageIndex == 1)
                attribute = VertexAttribute.TexCoord7; // Original Lighting COLOR1 input.
            else
            {
                if (element.UsageIndex != 0)
                    throw new NotSupportedException("This source semantic requires an explicit shader input mapping.");
                switch (element.VertexElementUsage)
                {
                    case SourceVertexElementUsage.Position: attribute = VertexAttribute.Position; break;
                    case SourceVertexElementUsage.Normal: attribute = VertexAttribute.Normal; break;
                    case SourceVertexElementUsage.Tangent: attribute = VertexAttribute.Tangent; break;
                    case SourceVertexElementUsage.Color: attribute = VertexAttribute.Color; break;
                    case SourceVertexElementUsage.BlendWeight: attribute = VertexAttribute.BlendWeight; break;
                    case SourceVertexElementUsage.BlendIndices: attribute = VertexAttribute.BlendIndices; break;
                    default:
                        throw new NotSupportedException(
                            "This source semantic requires an explicit shader input mapping.");
                }
            }

            VertexAttributeFormat format;
            int dimension;
            switch (element.VertexElementFormat)
            {
                case SourceVertexElementFormat.Single:
                    format = VertexAttributeFormat.Float32;
                    dimension = 1;
                    break;
                case SourceVertexElementFormat.Vector2:
                    format = VertexAttributeFormat.Float32;
                    dimension = 2;
                    break;
                case SourceVertexElementFormat.Vector3:
                    format = VertexAttributeFormat.Float32;
                    dimension = 3;
                    break;
                case SourceVertexElementFormat.Vector4:
                    format = VertexAttributeFormat.Float32;
                    dimension = 4;
                    break;
                case SourceVertexElementFormat.Color:
                    format = VertexAttributeFormat.UNorm8;
                    dimension = 4;
                    break;
                case SourceVertexElementFormat.Byte4:
                    format = VertexAttributeFormat.Float32;
                    dimension = 4;
                    break;
                case SourceVertexElementFormat.Short2:
                    format = VertexAttributeFormat.Float32;
                    dimension = 2;
                    break;
                case SourceVertexElementFormat.Short4:
                    format = VertexAttributeFormat.Float32;
                    dimension = 4;
                    break;
                case SourceVertexElementFormat.NormalizedShort2:
                    format = VertexAttributeFormat.SNorm16;
                    dimension = 2;
                    break;
                case SourceVertexElementFormat.NormalizedShort4:
                    format = VertexAttributeFormat.SNorm16;
                    dimension = 4;
                    break;
                case SourceVertexElementFormat.HalfVector2:
                    format = VertexAttributeFormat.Float16;
                    dimension = 2;
                    break;
                case SourceVertexElementFormat.HalfVector4:
                    format = VertexAttributeFormat.Float16;
                    dimension = 4;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }

            return new VertexAttributeDescriptor(attribute, format, dimension, 0);
        }
    }
}
