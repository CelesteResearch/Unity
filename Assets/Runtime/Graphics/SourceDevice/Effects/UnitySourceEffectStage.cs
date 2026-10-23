using System;
using System.Runtime.InteropServices;
using Celeste.Graphics.Effects;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnitySourceEffectStage
    {
        private readonly CpuShaderLease lease;
        public CpuShaderDescription NativeDescription { get; }
        public string Key { get; }
        public UnityEffectShaderStage Shader { get; }
        public Vector4[] Constants { get; }

        internal UnitySourceEffectStage(CpuShaderLease lease, in CpuShaderDescription description)
        {
            this.lease = lease;
            NativeDescription = description;
            Key = UnityEffectShaderCatalog.StageKey(in description);
            if (!UnityEffectShaderCatalog.TryGetStage(Key, out var shader))
                throw new NotSupportedException("The actual native shader stage is not in the cooked shader catalog: " +
                                                Key);
            Shader = shader;
            if (description.ConstantBufferBytes != shader.ConstantBufferBytes ||
                description.ConstantBufferBytes != shader.Float4Count * 16)
                throw new InvalidOperationException(
                    "Cooked shader constant layout differs from the actual native stage.");
            if (description.PackingCount != shader.Packing.Length)
                throw new InvalidOperationException("Cooked shader packing count differs from the native stage.");
            for (var index = 0; index < description.PackingCount; index++)
            {
                var packing =
                    Marshal.PtrToStructure<CpuUniformPacking>(description.Packing +
                                                              index * Marshal.SizeOf<CpuUniformPacking>());
                var cooked = shader.Packing[index];
                if (packing.Type != cooked.Type || packing.RegisterIndex != cooked.RegisterIndex ||
                    packing.RegisterCount != cooked.RegisterCount || packing.ByteOffset != cooked.ByteOffset)
                    throw new InvalidOperationException("Cooked shader packing differs from the native stage.");
                if (packing.Type != 0)
                    throw new NotSupportedException(
                        "The cooked float4 upload path requires float uniforms; GPU int/bool arrays need their own generated binding.");
            }

            Constants = new Vector4[shader.Float4Count];
        }

        internal unsafe void CopyConstants(CpuEffectFrontend frontend, IntPtr effect, bool vertex)
        {
            if (Constants.Length == 0) return;
            fixed (Vector4* pointer = Constants)
                frontend.CopyHlslConstants(effect, vertex, (IntPtr)pointer,
                    (uint)NativeDescription.ConstantBufferBytes);
        }

        internal void Dispose() => lease.Dispose();
    }
}
