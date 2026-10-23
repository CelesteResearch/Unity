using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public interface IUnitySourceEffectDrawBindings
    {
        // Read the current stage constants/state synchronously and freeze them into the actual draw properties.
        // Returned materials are immutable program/pipeline variants, never mutated while queued draws reference them.
        Material Prepare(UnitySourceEffectStage vertex, UnitySourceEffectStage pixel, UnitySourceEffectState state,
            MaterialPropertyBlock properties);
    }
}
