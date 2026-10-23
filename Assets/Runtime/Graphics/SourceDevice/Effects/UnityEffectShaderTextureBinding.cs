namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public readonly struct UnityEffectShaderTextureBinding
    {
        public readonly int Stage, Slot, Type;
        public readonly string Property, Sampler;

        public UnityEffectShaderTextureBinding(int stage, int slot, int type, string property, string sampler)
        {
            Stage = stage;
            Slot = slot;
            Type = type;
            Property = property;
            Sampler = sampler;
        }
    }
}
