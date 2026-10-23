using System;
using Celeste.Unity.Graphics.SourceDevice.Effects;
using Celeste.Unity.Graphics.SourceDevice.Geometry;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed class UnitySourceGraphicsDeviceComponents : IDisposable
    {
        public UnitySourceGraphicsTargets Targets { get; }
        public UnitySourceEffectState State => Submission.State;
        public UnitySourceEffectIO Effects => Submission.Effects;
        public UnitySourceGeometryIO Geometry { get; }
        public UnitySourceEffectSubmission Submission { get; }

        public UnitySourceGraphicsDeviceComponents(UnitySourceGraphicsTargets targets,
            UnitySourceEffectSubmission submission, UnitySourceGeometryIO geometry)
        {
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Submission = submission ?? throw new ArgumentNullException(nameof(submission));
        }

        public void Flush() => Submission.Flush();
        public void AbortUnsubmittedDraws() => Submission.DiscardPendingPrefix();

        public void Dispose()
        {
            try
            {
                Flush();
            }
            finally
            {
                try
                {
                    AbortUnsubmittedDraws();
                }
                finally
                {
                    try
                    {
                        Geometry.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            // Submission owns the effect frontend, materials, samplers and command buffer.
                            Submission.Dispose();
                        }
                        finally
                        {
                            Targets.Dispose();
                        }
                    }
                }
            }
        }
    }
}
