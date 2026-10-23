using System;
using Celeste.Core.Rendering.States;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public readonly struct UnitySourceEffectSampler : IEquatable<UnitySourceEffectSampler>
    {
        public readonly IntPtr Texture;
        public readonly SourceNativeSamplerState State;

        public UnitySourceEffectSampler(IntPtr texture, in SourceNativeSamplerState state)
        {
            Texture = texture;
            State = state;
        }

        public bool Equals(UnitySourceEffectSampler other) =>
            Texture == other.Texture && StateEquals(in State, in other.State);

        public override bool Equals(object other) => other is UnitySourceEffectSampler value && Equals(value);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Texture);
            hash.Add(State.filter);
            hash.Add(State.addressU);
            hash.Add(State.addressV);
            hash.Add(State.addressW);
            hash.Add(State.mipMapLevelOfDetailBias);
            hash.Add(State.maxAnisotropy);
            hash.Add(State.maxMipLevel);
            return hash.ToHashCode();
        }

        internal static bool StateEquals(in SourceNativeSamplerState left, in SourceNativeSamplerState right)
            => left.filter == right.filter &&
               left.addressU == right.addressU &&
               left.addressV == right.addressV &&
               left.addressW == right.addressW &&
               left.mipMapLevelOfDetailBias.Equals(right.mipMapLevelOfDetailBias) &&
               left.maxAnisotropy == right.maxAnisotropy &&
               left.maxMipLevel == right.maxMipLevel;
    }
}
