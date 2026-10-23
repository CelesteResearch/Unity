using System;
using Celeste.Graphics.Effects;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Core.Rendering.States;
using Celeste.Unity.Graphics.SourceDevice.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Graphics.SourceDevice.Effects
{
    public sealed class UnitySourceEffectSubmission : IUnitySourceGeometrySubmission, IDisposable
    {
        private readonly UnitySourceGraphicsTargets targets;
        private readonly Action<SourceClearOptions, uint> markAttachmentsWritten;
        private SourceClearOptions pendingWrites;
        private uint pendingColorAttachments;
        private readonly CommandBuffer commands;
        private readonly MaterialPropertyBlock properties;
        private readonly UnitySourceEffectSamplers samplers;
        private readonly UnitySourceEffectMaterials materials;
        private readonly bool invertSourceTargetY;
        private bool externalInvertCulling;
        private bool disposed;
        private bool pending;
        private bool bindingsRecorded;
        private int targetVersion, resourceVersion;

        public ulong SubmissionVersion { get; private set; }
        public UnitySourceEffectIO Effects { get; }
        public UnitySourceEffectState State => Effects.State;

        // The final DeviceIO flushes before changing targets/viewport/scissor, texture contents, or presentation.
        // All draws in a prefix therefore write the attachments identified by markAttachmentsWritten.
        public UnitySourceEffectSubmission(ICpuEffectNativeBinding binding, UnitySourceEffectState state,
            UnitySourceTextureIO textures, UnitySourceGraphicsTargets targets,
            Action<SourceClearOptions, uint> markAttachmentsWritten,
            Func<SourceDepthFormat> getDepthFormat)
        {
            this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
            this.markAttachmentsWritten = markAttachmentsWritten ??
                                          throw new ArgumentNullException(nameof(markAttachmentsWritten));
            invertSourceTargetY = SystemInfo.graphicsDeviceType is GraphicsDeviceType.OpenGLCore or
                GraphicsDeviceType.OpenGLES3;
            commands = new CommandBuffer { name = "Source effect draw stream" };
            properties = new MaterialPropertyBlock();
            samplers = new UnitySourceEffectSamplers(textures, Flush);
            materials = new UnitySourceEffectMaterials(samplers, getDepthFormat);
            Effects = new UnitySourceEffectIO(binding, state, materials);
        }

        public void BeforeGeometryMutation() => Flush();

        public bool Submit(Mesh mesh, int subMesh)
        {
            // Current physical resources use one sample. Mask bit zero controls that sample's entire coverage.
            if ((State.GetMultiSampleMask() & 1) == 0) return false;
            var material = Effects.PrepareDraw(properties);
            // Sampler mutation/copy above may flush or temporarily change physical state. Bind afterwards.
            if (!bindingsRecorded || targetVersion != targets.SubmissionStateVersion ||
                resourceVersion != targets.ResourceStateVersion)
            {
                targets.RecordSubmissionBindings(commands);
                targetVersion = targets.SubmissionStateVersion;
                resourceVersion = targets.ResourceStateVersion;
                bindingsRecorded = true;
            }

            // Shader code already computes source clip positions; no Unity camera/object transform is added.
            if (invertSourceTargetY && !pending)
            {
                externalInvertCulling = GL.invertCulling;
                // Source clip positions reflect Y independently of the ambient Unity camera state.
                commands.SetInvertCulling(true);
            }

            commands.DrawMesh(mesh, Matrix4x4.identity, material, subMesh, 0, properties);
            var pipeline = State.Pipeline;
            var colorAttachments = PossibleColorWrites(in pipeline) & Effects.ColorOutputMask;
            pendingColorAttachments |= colorAttachments;
            pendingWrites |= PossibleWrites(in pipeline, colorAttachments);
            pending = true;
            return true;
        }

        internal static uint PossibleColorWrites(in UnitySourceEffectPipeline pipeline)
            // Source blend masks are intersected with the cooked pixel output mask before invalidating attachments.
            => (pipeline.Blend.colorWriteEnable != SourceColorWriteChannels.None ? 1u : 0u) |
               (pipeline.Blend.colorWriteEnable1 != SourceColorWriteChannels.None ? 2u : 0u) |
               (pipeline.Blend.colorWriteEnable2 != SourceColorWriteChannels.None ? 4u : 0u) |
               (pipeline.Blend.colorWriteEnable3 != SourceColorWriteChannels.None ? 8u : 0u);

        private static SourceClearOptions PossibleWrites(in UnitySourceEffectPipeline pipeline, uint colorAttachments)
        {
            var written = colorAttachments != 0
                ? SourceClearOptions.Target
                : 0;
            ref readonly var depth = ref pipeline.DepthStencil;
            if (depth.depthBufferEnable != 0 && depth.depthBufferWriteEnable != 0)
                written |= SourceClearOptions.DepthBuffer;
            if (depth.stencilEnable != 0 && (depth.stencilWriteMask & 255) != 0 &&
                (depth.stencilFail != SourceStencilOperation.Keep ||
                 depth.stencilDepthBufferFail != SourceStencilOperation.Keep ||
                 depth.stencilPass != SourceStencilOperation.Keep ||
                 depth.twoSidedStencilMode != 0 && (depth.ccwStencilFail != SourceStencilOperation.Keep ||
                                                    depth.ccwStencilDepthBufferFail != SourceStencilOperation.Keep ||
                                                    depth.ccwStencilPass != SourceStencilOperation.Keep)))
                written |= SourceClearOptions.Stencil;
            return written;
        }

        public void Flush()
        {
            if (pending)
            {
                if (invertSourceTargetY) commands.SetInvertCulling(externalInvertCulling);
                try
                {
                    UnityEngine.Graphics.ExecuteCommandBuffer(commands);
                }
                finally
                {
                    if (invertSourceTargetY && GL.invertCulling != externalInvertCulling)
                        GL.invertCulling = externalInvertCulling;
                }

                commands.Clear();
                pending = false;
                bindingsRecorded = false;
                SubmissionVersion++;
                var written = pendingWrites;
                pendingWrites = 0;
                var colorAttachments = pendingColorAttachments;
                pendingColorAttachments = 0;
                markAttachmentsWritten(written, colorAttachments);
                return;
            }

            commands.Clear();
            bindingsRecorded = false;
        }

        // Fatal render/update shutdown only: drop borrowed draw references without replaying the failed prefix.
        public void DiscardPendingPrefix()
        {
            if (disposed) return;
            commands.Clear();
            properties.Clear();
            pending = false;
            pendingWrites = 0;
            pendingColorAttachments = 0;
            bindingsRecorded = false;
        }

        public void DrainDisposals()
        {
            Flush();
            Effects.DrainDisposals();
        }

        public void Dispose()
        {
            if (disposed) return;
            try
            {
                Flush();
            }
            finally
            {
                try
                {
                    DiscardPendingPrefix();
                }
                finally
                {
                    try
                    {
                        Effects.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            materials.Dispose();
                        }
                        finally
                        {
                            try
                            {
                                samplers.Dispose();
                            }
                            finally
                            {
                                try
                                {
                                    commands.Dispose();
                                }
                                finally
                                {
                                    disposed = true;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
