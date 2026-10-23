using System;
using System.Collections.Generic;
using Celeste.Core.Rendering.States;
using Celeste.Graphics.Rendering.Textures;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed unsafe class UnitySourceEffectSamplers : IDisposable
    {
        private sealed class Alias
        {
            public IntPtr Texture;
            public ulong ContentVersion;
            public bool Current;
        }

        private readonly UnitySourceTextureIO textures;
        private readonly Action flush;
        private readonly IntPtr nullTexture;
        private readonly Dictionary<IntPtr, SourceNativeSamplerState> applied = new();
        private readonly Dictionary<IntPtr, SourceNativeSamplerState> drawStates = new();
        private readonly Dictionary<UnitySourceEffectSampler, Alias> aliases = new();
        private readonly Dictionary<string, int> properties = new(StringComparer.Ordinal);

        public UnitySourceEffectSamplers(UnitySourceTextureIO textures, Action flush)
        {
            this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
            this.flush = flush ?? throw new ArgumentNullException(nameof(flush));
            // Native unbound sampled resources return zero, not ShaderLab's default white texture.
            nullTexture = textures.CreateTexture2D(SourceSurfaceFormat.Color, 1, 1, 1, 0);
            uint zero = 0;
            textures.SetTextureData2D(nullTexture, 0, 0, 1, 1, 0, (IntPtr)(&zero), 4);
        }

        public void Bind(UnitySourceEffectStage vertex, UnitySourceEffectStage pixel, UnitySourceEffectState state,
            MaterialPropertyBlock block)
        {
            drawStates.Clear();
            BindStage(vertex.Shader, state.VertexSamplers, block);
            BindStage(pixel.Shader, state.PixelSamplers, block);
        }

        private void BindStage(UnityEffectShaderStage shader, ReadOnlySpan<UnitySourceEffectSampler> samplers,
            MaterialPropertyBlock block)
        {
            foreach (var binding in shader.Textures)
            {
                if (binding.Type != 0)
                    throw new NotSupportedException("The current physical resource owner provides 2D textures only.");
                var sampler = samplers[binding.Slot];
                var handle = sampler.Texture;
                Texture texture = textures.BorrowTexture(nullTexture);
                if (handle != IntPtr.Zero)
                {
                    if (drawStates.TryGetValue(handle, out var first) &&
                        !UnitySourceEffectSampler.StateEquals(in first, in sampler.State))
                        handle = BorrowAlias(in sampler);
                    else
                        drawStates[handle] = sampler.State;
                    texture = textures.BorrowTexture(handle);
                    Apply(handle, texture, in sampler.State);
                }

                if (!properties.TryGetValue(binding.Property, out var property))
                    properties.Add(binding.Property, property = Shader.PropertyToID(binding.Property));
                block.SetTexture(property, texture);
            }
        }

        private IntPtr BorrowAlias(in UnitySourceEffectSampler sampler)
        {
            if (!aliases.TryGetValue(sampler, out var alias))
            {
                flush();
                var source = textures.BorrowTexture(sampler.Texture);
                alias = new Alias
                {
                    // RenderTexture destinations take the existing GPU-only CopyGpu path, never CPU readback.
                    Texture = textures.CreateTexture2D(SourceSurfaceFormat.Color, source.width, source.height, 1, 1)
                };
                aliases.Add(sampler, alias);
            }

            // Commit before inspecting generation: a recorded write is not yet included in content version.
            flush();
            var version = textures.GetContentVersion(sampler.Texture);
            if (!alias.Current || alias.ContentVersion != version)
            {
                textures.CopyTexture2D(sampler.Texture, alias.Texture);
                alias.ContentVersion = version;
                alias.Current = true;
            }

            return alias.Texture;
        }

        private void Apply(IntPtr handle, Texture texture, in SourceNativeSamplerState state)
        {
            if (applied.TryGetValue(handle, out var prior) &&
                UnitySourceEffectSampler.StateEquals(in prior, in state)) return;
            var filter = state.filter switch
            {
                SourceTextureFilter.Point => FilterMode.Point,
                SourceTextureFilter.Linear => FilterMode.Trilinear,
                SourceTextureFilter.Anisotropic => FilterMode.Trilinear,
                SourceTextureFilter.LinearMipPoint => FilterMode.Bilinear,
                // The current resource owner has exactly one mip, so this has no mip interpolation to distinguish.
                SourceTextureFilter.PointMipLinear => FilterMode.Point,
                _ => throw new NotSupportedException(
                    "Unity texture-backed samplers cannot express separate minification and magnification filters.")
            };
            var u = Address(state.addressU);
            var v = Address(state.addressV);
            var w = Address(state.addressW);
            flush();
            texture.filterMode = filter;
            texture.wrapModeU = u;
            texture.wrapModeV = v;
            texture.wrapModeW = w;
            texture.anisoLevel = state.filter == SourceTextureFilter.Anisotropic ? state.maxAnisotropy : 1;
            texture.mipMapBias = state.mipMapLevelOfDetailBias;
            applied[handle] = state;
        }

        private static TextureWrapMode Address(SourceTextureAddressMode address) => address switch
        {
            SourceTextureAddressMode.Wrap => TextureWrapMode.Repeat,
            SourceTextureAddressMode.Clamp => TextureWrapMode.Clamp,
            SourceTextureAddressMode.Mirror => TextureWrapMode.Mirror,
            _ => throw new ArgumentOutOfRangeException(nameof(address))
        };

        public void Dispose()
        {
            flush();
            foreach (var alias in aliases.Values) textures.AddDisposeTexture(alias.Texture);
            textures.AddDisposeTexture(nullTexture);
            aliases.Clear();
            applied.Clear();
            drawStates.Clear();
            properties.Clear();
        }
    }
}
