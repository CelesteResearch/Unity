using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Celeste.Graphics.Effects;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public static partial class UnityEffectShaderCatalog
    {
        private static readonly Dictionary<string, UnityEffectShaderStage> stages = new(StringComparer.Ordinal);
        private static readonly Dictionary<(string Vertex, string Pixel), UnityEffectShaderProgram> programs = new();

        static UnityEffectShaderCatalog()
        {
            foreach (var stage in Stages) stages.Add(stage.Key, stage);
            foreach (var program in Programs) programs.Add((program.Vertex.Key, program.Pixel.Key), program);
        }

        public static bool TryGetStage(string key, out UnityEffectShaderStage stage) =>
            stages.TryGetValue(key, out stage);

        public static bool TryGetProgram(string vertex, string pixel, out UnityEffectShaderProgram program)
            => programs.TryGetValue((vertex, pixel), out program);

        // Cold shader creation identity. No native pointers or parameter values form this key.
        public static string StageKey(in CpuShaderDescription description)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                var profile = Encoding.UTF8.GetBytes(Marshal.PtrToStringUTF8(description.Profile));
                writer.Write((uint)profile.Length);
                writer.Write(profile);
                writer.Write(description.Stage);
                writer.Write(description.TokenLength);
                Write(writer, description.Tokens, checked((int)description.TokenLength));
                writer.Write(description.SwizzleCount);
                Write(writer, description.Swizzles, checked((int)description.SwizzleCount * 12));
                writer.Write(description.SamplerMapCount);
                Write(writer, description.SamplerMap, checked((int)description.SamplerMapCount * 8));
            }

            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        private static void Write(BinaryWriter writer, IntPtr pointer, int length)
        {
            if (length == 0) return;
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            writer.Write(bytes);
        }
    }
}
