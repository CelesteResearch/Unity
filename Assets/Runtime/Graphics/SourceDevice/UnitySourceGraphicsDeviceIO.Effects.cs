using System;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsDeviceIO
    {
        public void CreateEffect(byte[] code, int length, out IntPtr effect, out IntPtr effectData)
            => components.Effects.CreateEffect(code, length, out effect, out effectData);

        public void CloneEffect(IntPtr originalEffect, out IntPtr effect, out IntPtr effectData)
            => components.Effects.CloneEffect(originalEffect, out effect, out effectData);

        public void SetEffectTechnique(IntPtr effect, IntPtr technique)
            => components.Effects.SetEffectTechnique(effect, technique);

        public void ApplyEffect(IntPtr effect, uint pass, IntPtr changes)
            => components.Effects.ApplyEffect(effect, pass, changes);

        public void AddDisposeEffect(IntPtr effect) => components.Effects.AddDisposeEffect(effect);
    }
}
