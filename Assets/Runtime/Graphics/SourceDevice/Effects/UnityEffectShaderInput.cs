namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public readonly struct UnityEffectShaderInput
    {
        public readonly int SourceUsage, SourceIndex, UnityUsage, UnityIndex;

        public UnityEffectShaderInput(int sourceUsage, int sourceIndex, int unityUsage, int unityIndex)
        {
            SourceUsage = sourceUsage;
            SourceIndex = sourceIndex;
            UnityUsage = unityUsage;
            UnityIndex = unityIndex;
        }
    }
}
