using System;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Graphics.Rendering.Devices;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnitySourceEffectState : ISourceGraphicsDeviceStateIO
    {
        private SourceNativeBlendState blend;
        private SourceNativeDepthStencilState depthStencil;
        private SourceNativeRasterizerState rasterizer;
        private readonly UnitySourceEffectSampler[] pixelSamplers;
        private readonly UnitySourceEffectSampler[] vertexSamplers;

        public ulong PipelineVersion { get; private set; }
        public UnitySourceEffectPipeline Pipeline => new(in blend, in depthStencil, in rasterizer);
        public ReadOnlySpan<UnitySourceEffectSampler> PixelSamplers => pixelSamplers;
        public ReadOnlySpan<UnitySourceEffectSampler> VertexSamplers => vertexSamplers;

        public UnitySourceEffectState(int pixelSlots, int vertexSlots)
        {
            pixelSamplers = new UnitySourceEffectSampler[pixelSlots];
            vertexSamplers = new UnitySourceEffectSampler[vertexSlots];
            // Native device scalar defaults; SourceGraphicsDevice pushes its full pipeline before drawing.
            blend.blendFactor = uint.MaxValue;
            blend.multiSampleMask = -1;
        }

        public void SetBlendState(ref SourceNativeBlendState state)
        {
            if (UnitySourceEffectPipeline.BlendEquals(in blend, in state)) return;
            blend = state;
            PipelineVersion++;
        }

        public void SetDepthStencilState(ref SourceNativeDepthStencilState state)
        {
            if (UnitySourceEffectPipeline.DepthStencilEquals(in depthStencil, in state)) return;
            depthStencil = state;
            PipelineVersion++;
        }

        public void ApplyRasterizerState(ref SourceNativeRasterizerState state)
        {
            if (UnitySourceEffectPipeline.RasterizerEquals(in rasterizer, in state)) return;
            rasterizer = state;
            PipelineVersion++;
        }

        public void VerifySampler(int index, IntPtr texture, ref SourceNativeSamplerState state)
            => pixelSamplers[index] = new UnitySourceEffectSampler(texture, in state);

        public void VerifyVertexSampler(int index, IntPtr texture, ref SourceNativeSamplerState state)
            => vertexSamplers[index] = new UnitySourceEffectSampler(texture, in state);

        public void GetBlendFactor(out PackedColor color) =>
            color = new PackedColor { PackedValue = blend.blendFactor };

        public void SetBlendFactor(ref PackedColor color)
        {
            if (blend.blendFactor == color.PackedValue) return;
            blend.blendFactor = color.PackedValue;
            PipelineVersion++;
        }

        public int GetMultiSampleMask() => blend.multiSampleMask;

        public void SetMultiSampleMask(int mask)
        {
            if (blend.multiSampleMask == mask) return;
            blend.multiSampleMask = mask;
            PipelineVersion++;
        }

        public int GetReferenceStencil() => depthStencil.referenceStencil;

        public void SetReferenceStencil(int stencil)
        {
            if (depthStencil.referenceStencil == stencil) return;
            depthStencil.referenceStencil = stencil;
            PipelineVersion++;
        }
    }
}
