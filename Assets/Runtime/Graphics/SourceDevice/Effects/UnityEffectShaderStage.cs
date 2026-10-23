namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnityEffectShaderStage
    {
        public readonly string Key, UniformProperty;
        public readonly int Stage, ConstantBufferBytes, Float4Count;
        public readonly uint ColorOutputMask;
        public readonly UnityEffectUniformPacking[] Packing;
        public readonly UnityEffectShaderInput[] Inputs;
        public readonly UnityEffectShaderTextureBinding[] Textures;

        public UnityEffectShaderStage(string key, int stage, int bytes, UnityEffectUniformPacking[] packing,
            UnityEffectShaderInput[] inputs, UnityEffectShaderTextureBinding[] textures, uint colorOutputMask)
        {
            Key = key;
            Stage = stage;
            ConstantBufferBytes = bytes;
            Float4Count = bytes / 16;
            UniformProperty = stage == 2 ? "_CelesteVS" : "_CelestePS";
            Packing = packing;
            Inputs = inputs;
            Textures = textures;
            ColorOutputMask = colorOutputMask;
        }
    }
}
