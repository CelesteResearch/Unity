using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Celeste.Graphics.Effects;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnitySourceEffectIO : IDisposable
    {
        private readonly CpuEffectFrontend frontend;
        private readonly IUnitySourceEffectDrawBindings drawBindings;
        private readonly HashSet<IntPtr> effects = new();
        private readonly Dictionary<IntPtr, UnitySourceEffectStage> stages = new();
        private readonly ConcurrentQueue<IntPtr> disposals = new();
        private IntPtr activeEffect;

        public UnitySourceEffectState State { get; }
        public uint ColorOutputMask { get; private set; }

        public UnitySourceEffectIO(ICpuEffectNativeBinding binding, UnitySourceEffectState state,
            IUnitySourceEffectDrawBindings drawBindings)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.drawBindings = drawBindings ?? throw new ArgumentNullException(nameof(drawBindings));
            frontend = new CpuEffectFrontend("hlsl", binding);
        }

        public void CreateEffect(byte[] code, int length, out IntPtr effect, out IntPtr effectData)
        {
            frontend.CreateEffect(code, length, out effect, out effectData);
            try
            {
                RegisterStages(effect);
                effects.Add(effect);
            }
            catch
            {
                frontend.DestroyEffect(effect);
                throw;
            }
        }

        private void RegisterStages(IntPtr effect)
        {
            var count = frontend.GetShaderCount(effect);
            for (uint index = 0; index < count; index++)
            {
                var lease = frontend.AcquireShader(effect, index);
                try
                {
                    var description = lease.Describe();
                    if (stages.ContainsKey(description.Identity))
                    {
                        lease.Dispose();
                        continue;
                    }

                    stages.Add(description.Identity, new UnitySourceEffectStage(lease, in description));
                }
                catch
                {
                    lease.Dispose();
                    throw;
                }
            }
        }

        public void CloneEffect(IntPtr original, out IntPtr effect, out IntPtr effectData)
        {
            // The real native clone shares retained shader descriptions while copying parameter storage.
            frontend.CloneEffect(original, out effect, out effectData);
            effects.Add(effect);
        }

        public void SetEffectTechnique(IntPtr effect, IntPtr technique) =>
            frontend.SetEffectTechnique(effect, technique);

        public void AddDisposeEffect(IntPtr effect) => disposals.Enqueue(effect);

        public void ApplyEffect(IntPtr effect, uint pass, IntPtr changes)
        {
            frontend.ApplyEffect(effect, pass, changes);
            activeEffect = effect;
            // SourceEffect consumes changes after this return. Sample the resulting pipeline at Draw, not here.
        }

        // Returned buffers belong to this owner and are valid until the next FreezeDraw. Queued draws must copy them.
        public void FreezeDraw(out UnitySourceEffectStage vertex, out UnitySourceEffectStage pixel)
        {
            var view = frontend.BorrowRegisters(activeEffect);
            if (view.VertexIdentity == IntPtr.Zero || view.PixelIdentity == IntPtr.Zero)
                throw new InvalidOperationException(
                    "The actual native effect context does not have both shader stages bound.");
            vertex = stages[view.VertexIdentity];
            pixel = stages[view.PixelIdentity];
            vertex.CopyConstants(frontend, activeEffect, true);
            pixel.CopyConstants(frontend, activeEffect, false);
        }

        public Material PrepareDraw(MaterialPropertyBlock properties)
        {
            FreezeDraw(out var vertex, out var pixel);
            var material = drawBindings.Prepare(vertex, pixel, State, properties);
            if (ReferenceEquals(material, null))
                throw new InvalidOperationException("The physical effect binding did not return a drawable material.");
            ColorOutputMask = pixel.Shader.ColorOutputMask;
            return material;
        }

        // Finalizers only enqueue. The host drains on the device owner thread after committing pending draws.
        public void DrainDisposals()
        {
            while (disposals.TryDequeue(out var effect))
                if (effects.Remove(effect))
                {
                    frontend.DestroyEffect(effect);
                    if (activeEffect == effect) activeEffect = IntPtr.Zero;
                }
        }

        public void Dispose()
        {
            DrainDisposals();
            foreach (var effect in effects) frontend.DestroyEffect(effect);
            effects.Clear();
            activeEffect = IntPtr.Zero;
            foreach (var stage in stages.Values) stage.Dispose();
            stages.Clear();
            frontend.Dispose();
        }
    }
}
