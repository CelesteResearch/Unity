using System;
using Celeste.Core.Rendering.States;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public readonly struct UnitySourceEffectPipeline : IEquatable<UnitySourceEffectPipeline>
    {
        public readonly SourceNativeBlendState Blend;
        public readonly SourceNativeDepthStencilState DepthStencil;
        public readonly SourceNativeRasterizerState Rasterizer;

        public UnitySourceEffectPipeline(in SourceNativeBlendState blend, in SourceNativeDepthStencilState depthStencil,
            in SourceNativeRasterizerState rasterizer)
        {
            Blend = blend;
            DepthStencil = depthStencil;
            Rasterizer = rasterizer;
        }

        public bool Equals(UnitySourceEffectPipeline other)
            => BlendEquals(in Blend, in other.Blend) && DepthStencilEquals(in DepthStencil, in other.DepthStencil) &&
               RasterizerEquals(in Rasterizer, in other.Rasterizer);

        public override bool Equals(object other) => other is UnitySourceEffectPipeline value && Equals(value);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Blend.colorSourceBlend);
            hash.Add(Blend.colorDestinationBlend);
            hash.Add(Blend.colorBlendFunction);
            hash.Add(Blend.alphaSourceBlend);
            hash.Add(Blend.alphaDestinationBlend);
            hash.Add(Blend.alphaBlendFunction);
            hash.Add(Blend.colorWriteEnable);
            hash.Add(Blend.colorWriteEnable1);
            hash.Add(Blend.colorWriteEnable2);
            hash.Add(Blend.colorWriteEnable3);
            hash.Add(Blend.blendFactor);
            hash.Add(Blend.multiSampleMask);
            hash.Add(DepthStencil.depthBufferEnable);
            hash.Add(DepthStencil.depthBufferWriteEnable);
            hash.Add(DepthStencil.depthBufferFunction);
            hash.Add(DepthStencil.stencilEnable);
            hash.Add(DepthStencil.stencilMask);
            hash.Add(DepthStencil.stencilWriteMask);
            hash.Add(DepthStencil.twoSidedStencilMode);
            hash.Add(DepthStencil.stencilFail);
            hash.Add(DepthStencil.stencilDepthBufferFail);
            hash.Add(DepthStencil.stencilPass);
            hash.Add(DepthStencil.stencilFunction);
            hash.Add(DepthStencil.ccwStencilFail);
            hash.Add(DepthStencil.ccwStencilDepthBufferFail);
            hash.Add(DepthStencil.ccwStencilPass);
            hash.Add(DepthStencil.ccwStencilFunction);
            hash.Add(DepthStencil.referenceStencil);
            hash.Add(Rasterizer.fillMode);
            hash.Add(Rasterizer.cullMode);
            hash.Add(Rasterizer.depthBias);
            hash.Add(Rasterizer.slopeScaleDepthBias);
            hash.Add(Rasterizer.scissorTestEnable);
            hash.Add(Rasterizer.multiSampleAntiAlias);
            return hash.ToHashCode();
        }

        internal static bool BlendEquals(in SourceNativeBlendState left, in SourceNativeBlendState right)
            => left.colorSourceBlend == right.colorSourceBlend &&
               left.colorDestinationBlend == right.colorDestinationBlend &&
               left.colorBlendFunction == right.colorBlendFunction &&
               left.alphaSourceBlend == right.alphaSourceBlend &&
               left.alphaDestinationBlend == right.alphaDestinationBlend &&
               left.alphaBlendFunction == right.alphaBlendFunction &&
               left.colorWriteEnable == right.colorWriteEnable &&
               left.colorWriteEnable1 == right.colorWriteEnable1 &&
               left.colorWriteEnable2 == right.colorWriteEnable2 &&
               left.colorWriteEnable3 == right.colorWriteEnable3 &&
               left.blendFactor == right.blendFactor &&
               left.multiSampleMask == right.multiSampleMask;

        internal static bool DepthStencilEquals(in SourceNativeDepthStencilState left,
            in SourceNativeDepthStencilState right)
            => left.depthBufferEnable == right.depthBufferEnable &&
               left.depthBufferWriteEnable == right.depthBufferWriteEnable &&
               left.depthBufferFunction == right.depthBufferFunction &&
               left.stencilEnable == right.stencilEnable &&
               left.stencilMask == right.stencilMask &&
               left.stencilWriteMask == right.stencilWriteMask &&
               left.twoSidedStencilMode == right.twoSidedStencilMode &&
               left.stencilFail == right.stencilFail &&
               left.stencilDepthBufferFail == right.stencilDepthBufferFail &&
               left.stencilPass == right.stencilPass &&
               left.stencilFunction == right.stencilFunction &&
               left.ccwStencilFail == right.ccwStencilFail &&
               left.ccwStencilDepthBufferFail == right.ccwStencilDepthBufferFail &&
               left.ccwStencilPass == right.ccwStencilPass &&
               left.ccwStencilFunction == right.ccwStencilFunction &&
               left.referenceStencil == right.referenceStencil;

        internal static bool RasterizerEquals(in SourceNativeRasterizerState left, in SourceNativeRasterizerState right)
            => left.fillMode == right.fillMode &&
               left.cullMode == right.cullMode &&
               left.depthBias.Equals(right.depthBias) &&
               left.slopeScaleDepthBias.Equals(right.slopeScaleDepthBias) &&
               left.scissorTestEnable == right.scissorTestEnable &&
               left.multiSampleAntiAlias == right.multiSampleAntiAlias;
    }
}
