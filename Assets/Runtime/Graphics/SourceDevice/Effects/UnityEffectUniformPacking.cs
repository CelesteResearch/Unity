namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public readonly struct UnityEffectUniformPacking
    {
        public readonly int Type, RegisterIndex, RegisterCount, ByteOffset;

        public UnityEffectUniformPacking(int type, int registerIndex, int registerCount, int byteOffset)
        {
            Type = type;
            RegisterIndex = registerIndex;
            RegisterCount = registerCount;
            ByteOffset = byteOffset;
        }
    }
}
