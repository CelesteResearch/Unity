using System;
using System.Collections.Generic;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnitySourceEffectMaterials : IUnitySourceEffectDrawBindings, IDisposable
    {
        private readonly UnitySourceEffectSamplers samplers;
        private readonly Func<SourceDepthFormat> getDepthFormat;
        private readonly bool swapGlStencilFaces;

        private readonly Dictionary<(string Vertex, string Pixel, UnitySourceEffectPipeline Pipeline, SourceDepthFormat
                DepthFormat), Material>
            materials = new();

        private UnitySourceEffectStage lastVertex, lastPixel;
        private ulong lastVersion;
        private SourceDepthFormat lastDepthFormat;
        private Material lastMaterial;

        private static class UniformProperties
        {
            internal static readonly int Vertex = Shader.PropertyToID("_CelesteVS");
            internal static readonly int Pixel = Shader.PropertyToID("_CelestePS");
        }

        public UnitySourceEffectMaterials(UnitySourceEffectSamplers samplers, Func<SourceDepthFormat> getDepthFormat)
        {
            this.samplers = samplers ?? throw new ArgumentNullException(nameof(samplers));
            this.getDepthFormat = getDepthFormat ?? throw new ArgumentNullException(nameof(getDepthFormat));
            swapGlStencilFaces = SystemInfo.graphicsDeviceType is GraphicsDeviceType.OpenGLCore or
                GraphicsDeviceType.OpenGLES3;
        }

        public Material Prepare(UnitySourceEffectStage vertex, UnitySourceEffectStage pixel,
            UnitySourceEffectState state,
            MaterialPropertyBlock properties)
        {
            var material = BorrowMaterial(vertex, pixel, state);
            properties.Clear();
            if (vertex.Constants.Length != 0) properties.SetVectorArray(UniformProperties.Vertex, vertex.Constants);
            if (pixel.Constants.Length != 0) properties.SetVectorArray(UniformProperties.Pixel, pixel.Constants);
            samplers.Bind(vertex, pixel, state, properties);
            return material;
        }

        private Material BorrowMaterial(UnitySourceEffectStage vertex, UnitySourceEffectStage pixel,
            UnitySourceEffectState state)
        {
            var pipeline = state.Pipeline;
            var depthFormat = pipeline.Rasterizer.depthBias == 0 ? SourceDepthFormat.None : getDepthFormat();
            if (ReferenceEquals(vertex, lastVertex) && ReferenceEquals(pixel, lastPixel) &&
                state.PipelineVersion == lastVersion && depthFormat == lastDepthFormat) return lastMaterial;
            var key = (vertex.Key, pixel.Key, pipeline, depthFormat);
            if (!materials.TryGetValue(key, out var material))
            {
                if (!UnityEffectShaderCatalog.TryGetProgram(vertex.Key, pixel.Key, out var program))
                    throw new NotSupportedException("The actual bound shader pair has no cooked program: " +
                                                    vertex.Key + "/" + pixel.Key);
                var shader = Resources.Load<Shader>(program.ShaderResource);
                if (shader == null || !shader.isSupported)
                    throw new NotSupportedException("The actual generated shader is absent or unsupported: " +
                                                    program.ShaderResource);
                material = new Material(shader);
                try
                {
                    Configure(material, in pipeline, depthFormat);
                    materials.Add(key, material);
                }
                catch
                {
                    Object.Destroy(material);
                    throw;
                }
            }

            lastVertex = vertex;
            lastPixel = pixel;
            lastVersion = state.PipelineVersion;
            lastDepthFormat = depthFormat;
            lastMaterial = material;
            return material;
        }

        private void Configure(Material material, in UnitySourceEffectPipeline pipeline,
            SourceDepthFormat depthFormat)
        {
            ref readonly var blend = ref pipeline.Blend;
            ref readonly var depth = ref pipeline.DepthStencil;
            ref readonly var raster = ref pipeline.Rasterizer;
            if (raster.fillMode != SourceFillMode.Solid)
                throw new NotSupportedException("The generated program currently has no wireframe raster consumer.");
            var opaque = blend.colorSourceBlend == SourceBlendFactor.One &&
                         blend.colorDestinationBlend == SourceBlendFactor.Zero &&
                         blend.alphaSourceBlend == SourceBlendFactor.One &&
                         blend.alphaDestinationBlend == SourceBlendFactor.Zero;
            material.SetInteger("_SrcBlend", (int)Factor(blend.colorSourceBlend, false));
            material.SetInteger("_DstBlend", (int)Factor(blend.colorDestinationBlend, false));
            material.SetInteger("_SrcBlendAlpha", (int)Factor(blend.alphaSourceBlend, true));
            material.SetInteger("_DstBlendAlpha", (int)Factor(blend.alphaDestinationBlend, true));
            material.SetInteger("_BlendOp",
                (int)Operation(opaque ? SourceBlendFunction.Add : blend.colorBlendFunction));
            material.SetInteger("_BlendOpAlpha",
                (int)Operation(opaque ? SourceBlendFunction.Add : blend.alphaBlendFunction));
            material.SetInteger("_ColorMask", Mask(blend.colorWriteEnable));
            if (material.HasProperty("_ColorMask1"))
            {
                material.SetInteger("_ColorMask1", Mask(blend.colorWriteEnable1));
                material.SetInteger("_ColorMask2", Mask(blend.colorWriteEnable2));
                material.SetInteger("_ColorMask3", Mask(blend.colorWriteEnable3));
            }

            material.SetInteger("_Cull", raster.cullMode switch
            {
                SourceCullMode.None => (int)CullMode.Off,
                SourceCullMode.CullClockwiseFace => (int)(swapGlStencilFaces ? CullMode.Front : CullMode.Back),
                SourceCullMode.CullCounterClockwiseFace => (int)(swapGlStencilFaces ? CullMode.Back : CullMode.Front),
                _ => throw new ArgumentOutOfRangeException(nameof(raster.cullMode))
            });
            material.SetInteger("_ZWrite", depth.depthBufferEnable != 0 && depth.depthBufferWriteEnable != 0 ? 1 : 0);
            material.SetInteger("_ZTest",
                (int)Compare(depth.depthBufferEnable != 0 ? depth.depthBufferFunction : SourceCompareFunction.Always));
            material.SetFloat("_DepthBias", DepthBias(raster.depthBias, depthFormat));
            material.SetFloat("_SlopeScaleBias", raster.slopeScaleDepthBias);
            material.SetInteger("_StencilRef", depth.referenceStencil);
            material.SetInteger("_StencilReadMask", depth.stencilMask & 255);
            material.SetInteger("_StencilWriteMask", depth.stencilEnable != 0 ? depth.stencilWriteMask & 255 : 0);
            var swapFaces = swapGlStencilFaces && depth.twoSidedStencilMode != 0;
            material.SetInteger(swapFaces ? "_StencilCompBack" : "_StencilCompFront",
                (int)Compare(depth.stencilEnable != 0 ? depth.stencilFunction : SourceCompareFunction.Always));
            material.SetInteger(swapFaces ? "_StencilCompFront" : "_StencilCompBack",
                (int)Compare(depth.stencilEnable == 0 ? SourceCompareFunction.Always :
                    depth.twoSidedStencilMode != 0 ? depth.ccwStencilFunction : depth.stencilFunction));
            material.SetInteger(swapFaces ? "_StencilPassBack" : "_StencilPassFront",
                (int)Stencil(depth.stencilEnable != 0 ? depth.stencilPass : SourceStencilOperation.Keep));
            material.SetInteger(swapFaces ? "_StencilPassFront" : "_StencilPassBack",
                (int)Stencil(depth.stencilEnable == 0 ? SourceStencilOperation.Keep :
                    depth.twoSidedStencilMode != 0 ? depth.ccwStencilPass : depth.stencilPass));
            material.SetInteger(swapFaces ? "_StencilFailBack" : "_StencilFailFront",
                (int)Stencil(depth.stencilEnable != 0 ? depth.stencilFail : SourceStencilOperation.Keep));
            material.SetInteger(swapFaces ? "_StencilFailFront" : "_StencilFailBack",
                (int)Stencil(depth.stencilEnable == 0 ? SourceStencilOperation.Keep :
                    depth.twoSidedStencilMode != 0 ? depth.ccwStencilFail : depth.stencilFail));
            material.SetInteger(swapFaces ? "_StencilZFailBack" : "_StencilZFailFront",
                (int)Stencil(depth.stencilEnable != 0 ? depth.stencilDepthBufferFail : SourceStencilOperation.Keep));
            material.SetInteger(swapFaces ? "_StencilZFailFront" : "_StencilZFailBack",
                (int)Stencil(depth.stencilEnable == 0 ? SourceStencilOperation.Keep :
                    depth.twoSidedStencilMode != 0 ? depth.ccwStencilDepthBufferFail : depth.stencilDepthBufferFail));
        }

        internal static int DepthBias(float bias, SourceDepthFormat depthFormat)
        {
            var scale = depthFormat switch
            {
                SourceDepthFormat.None => 0f,
                SourceDepthFormat.Depth16 => 65535f,
                SourceDepthFormat.Depth24 or SourceDepthFormat.Depth24Stencil8 => 16777215f,
                _ => throw new ArgumentOutOfRangeException(nameof(depthFormat))
            };
            return unchecked((int)(bias * scale));
        }

        internal static BlendMode Factor(SourceBlendFactor value, bool alpha) => value switch
        {
            SourceBlendFactor.One => BlendMode.One,
            SourceBlendFactor.Zero => BlendMode.Zero,
            SourceBlendFactor.SourceColor => alpha ? BlendMode.SrcAlpha : BlendMode.SrcColor,
            SourceBlendFactor.InverseSourceColor => alpha ? BlendMode.OneMinusSrcAlpha : BlendMode.OneMinusSrcColor,
            SourceBlendFactor.SourceAlpha => BlendMode.SrcAlpha,
            SourceBlendFactor.InverseSourceAlpha => BlendMode.OneMinusSrcAlpha,
            SourceBlendFactor.DestinationColor => alpha ? BlendMode.DstAlpha : BlendMode.DstColor,
            SourceBlendFactor.InverseDestinationColor =>
                alpha ? BlendMode.OneMinusDstAlpha : BlendMode.OneMinusDstColor,
            SourceBlendFactor.DestinationAlpha => BlendMode.DstAlpha,
            SourceBlendFactor.InverseDestinationAlpha => BlendMode.OneMinusDstAlpha,
            SourceBlendFactor.SourceAlphaSaturation => BlendMode.SrcAlphaSaturate,
            SourceBlendFactor.BlendFactor or SourceBlendFactor.InverseBlendFactor => throw new NotSupportedException(
                "Unity ShaderLab has no constant blend-color factor binding in this consumer."),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        internal static BlendOp Operation(SourceBlendFunction value) => value switch
        {
            SourceBlendFunction.Add => BlendOp.Add,
            SourceBlendFunction.Subtract => BlendOp.Subtract,
            SourceBlendFunction.ReverseSubtract => BlendOp.ReverseSubtract,
            SourceBlendFunction.Min => BlendOp.Min,
            SourceBlendFunction.Max => BlendOp.Max,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        internal static CompareFunction Compare(SourceCompareFunction value) => value switch
        {
            SourceCompareFunction.Always => CompareFunction.Always,
            SourceCompareFunction.Never => CompareFunction.Never,
            SourceCompareFunction.Less => CompareFunction.Less,
            SourceCompareFunction.LessEqual => CompareFunction.LessEqual,
            SourceCompareFunction.Equal => CompareFunction.Equal,
            SourceCompareFunction.GreaterEqual => CompareFunction.GreaterEqual,
            SourceCompareFunction.Greater => CompareFunction.Greater,
            SourceCompareFunction.NotEqual => CompareFunction.NotEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        internal static StencilOp Stencil(SourceStencilOperation value) => value switch
        {
            SourceStencilOperation.Keep => StencilOp.Keep,
            SourceStencilOperation.Zero => StencilOp.Zero,
            SourceStencilOperation.Replace => StencilOp.Replace,
            SourceStencilOperation.Increment => StencilOp.IncrementWrap,
            SourceStencilOperation.Decrement => StencilOp.DecrementWrap,
            SourceStencilOperation.IncrementSaturation => StencilOp.IncrementSaturate,
            SourceStencilOperation.DecrementSaturation => StencilOp.DecrementSaturate,
            SourceStencilOperation.Invert => StencilOp.Invert,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        internal static int Mask(SourceColorWriteChannels value)
            => ((value & SourceColorWriteChannels.Red) != 0 ? (int)ColorWriteMask.Red : 0) |
               ((value & SourceColorWriteChannels.Green) != 0 ? (int)ColorWriteMask.Green : 0) |
               ((value & SourceColorWriteChannels.Blue) != 0 ? (int)ColorWriteMask.Blue : 0) |
               ((value & SourceColorWriteChannels.Alpha) != 0 ? (int)ColorWriteMask.Alpha : 0);

        public void Dispose()
        {
            foreach (var material in materials.Values) Object.Destroy(material);
            materials.Clear();
            lastVertex = lastPixel = null;
            lastMaterial = null;
        }
    }
}
