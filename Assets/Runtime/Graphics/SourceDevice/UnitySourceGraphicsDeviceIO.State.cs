using System;
using Celeste.Core.Display;
using Celeste.Core.Rendering.States;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed unsafe partial class UnitySourceGraphicsDeviceIO
    {
        private bool scissorEnabled;
        public void GetBlendFactor(out PackedColor color) => components.State.GetBlendFactor(out color);
        public void SetBlendFactor(ref PackedColor color) => components.State.SetBlendFactor(ref color);
        public int GetMultiSampleMask() => components.State.GetMultiSampleMask();
        public void SetMultiSampleMask(int mask) => components.State.SetMultiSampleMask(mask);
        public int GetReferenceStencil() => components.State.GetReferenceStencil();
        public void SetReferenceStencil(int stencil) => components.State.SetReferenceStencil(stencil);
        public void SetBlendState(ref SourceNativeBlendState state) => components.State.SetBlendState(ref state);

        public void SetDepthStencilState(ref SourceNativeDepthStencilState state)
            => components.State.SetDepthStencilState(ref state);

        public void ApplyRasterizerState(ref SourceNativeRasterizerState state)
        {
            bool enable = state.scissorTestEnable != 0;
            if (enable != scissorEnabled)
            {
                Flush();
                components.Targets.SetScissorEnabled(enable);
                scissorEnabled = enable;
            }

            components.State.ApplyRasterizerState(ref state);
        }

        public void VerifySampler(int index, IntPtr texture, ref SourceNativeSamplerState state)
            => components.State.VerifySampler(index, texture, ref state);

        public void VerifyVertexSampler(int index, IntPtr texture, ref SourceNativeSamplerState state)
            => components.State.VerifyVertexSampler(index, texture, ref state);
    }
}
