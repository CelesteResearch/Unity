using System;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Celeste.Core.Geometry;
using Celeste.Graphics.Diagnostics.Presentation;
using Celeste.Graphics.Rendering.Batches;

namespace Celeste.Unity.Editor
{
    // One renderer synchronously lends a completed capture; workers read only Quad and Transform values.
    internal sealed class SceneCaptureGeometryWorkers : IDisposable
    {
        internal const int MinimumQuadCount = 8192;
        private const int WorkerCount = 4;

        internal struct Result
        {
            internal SourceSpriteQuad Quad;
            internal Bounds Bounds;
            internal bool FiniteUv;
            internal Bounds SecondTriangleBounds;
            internal byte PrimitiveFiniteUv;
            internal byte AtlasUnitUv;
        }

        private sealed class Worker
        {
            internal readonly ManualResetEventSlim Wake = new(false, 0);
            internal Thread Thread;
            internal int Begin, End;
            internal bool Started, Stop;
        }

        private readonly Worker[] workers = new Worker[WorkerCount];
        private readonly CountdownEvent completed = new(0);
        private ReadOnlyMemory<SourceSpritePresentationQuad> records;
        private SourceSpritePresentationCapture borrowedCapture;
        private Result[] output = Array.Empty<Result>();
        private Matrix4x4 camera, inverse;
        private Exception failure, deferredFailure;
        private bool running;
        private SourceSpritePresentationCapture jobCapture;
        private long jobRevision;
        private bool disposed;
        private SourceSpritePresentationCapture preparedCapture;
        private long preparedRevision;

        internal SceneCaptureGeometryWorkers()
        {
            try
            {
                for (int i = 0; i < workers.Length; i++)
                {
                    var worker = new Worker();
                    workers[i] = worker;
                    worker.Thread = new Thread(() => Run(worker))
                    {
                        IsBackground = true,
                        Name = "Celeste Scene geometry " + i
                    };
                    worker.Thread.Start();
                    worker.Started = true;
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // Owner-thread only. Failures are latched for Process, never thrown into the producer callback.
        internal void Start(SourceSpritePresentationCapture capture, Matrix4x4 camera, Matrix4x4 inverse)
        {
            int awakened = 0;
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(SceneCaptureGeometryWorkers));
                Wait();
                long revision = capture.Revision;
                if (ReferenceEquals(preparedCapture, capture) && preparedRevision == revision &&
                    SameBits(in this.camera, in camera) && SameBits(in this.inverse, in inverse)) return;
                preparedCapture = null;
                var records = capture.CompletedQuadMemory;
                int count = records.Length;
                if (output.Length < count)
                    Array.Resize(ref output, Math.Max(count, Math.Max(256, output.Length * 2)));
                this.records = records;
                borrowedCapture = capture;
                jobCapture = capture;
                jobRevision = revision;
                this.camera = camera;
                this.inverse = inverse;
                failure = null;
                completed.Reset(workers.Length);
                running = true;
                for (int i = 0; i < workers.Length; i++)
                {
                    var worker = workers[i];
                    worker.Begin = count * i / workers.Length;
                    worker.End = count * (i + 1) / workers.Length;
                    worker.Wake.Set();
                    awakened++;
                }
            }
            catch (Exception exception)
            {
                deferredFailure ??= exception;
                if (running)
                {
                    Interlocked.CompareExchange(ref failure, exception, null);
                    int unstarted = workers.Length - awakened;
                    if (unstarted != 0) completed.Signal(unstarted);
                    Wait();
                }
                else
                {
                    records = default;
                    borrowedCapture = null;
                    jobCapture = null;
                }
            }
        }

        // Fences readers without observing their exceptions in the source render/mutation path.
        internal void Wait()
        {
            if (!running) return;
            completed.Wait();
            if (failure != null) deferredFailure ??= failure;
            else
            {
                preparedRevision = jobRevision;
                preparedCapture = jobCapture;
            }

            running = false;
            records = default;
            borrowedCapture = null;
            jobCapture = null;
        }

        internal ReadOnlySpan<Result> Process(SourceSpritePresentationCapture capture,
            Matrix4x4 camera, Matrix4x4 inverse)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneCaptureGeometryWorkers));
            Wait();
            ThrowDeferredFailure();
            Start(capture, camera, inverse);
            Wait();
            ThrowDeferredFailure();
            return output.AsSpan(0, capture.Count);
        }

        private void ThrowDeferredFailure()
        {
            var exception = deferredFailure;
            if (exception == null) return;
            deferredFailure = null;
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        private static unsafe bool SameBits(in Matrix4x4 a, in Matrix4x4 b)
        {
            fixed (Matrix4x4* left = &a)
            fixed (Matrix4x4* right = &b)
                for (int i = 0; i < sizeof(Matrix4x4) / sizeof(ulong); i++)
                    if (((ulong*)left)[i] != ((ulong*)right)[i])
                        return false;
            return true;
        }

        private void Run(Worker worker)
        {
            while (true)
            {
                worker.Wake.Wait();
                worker.Wake.Reset();
                if (worker.Stop) return;
                try
                {
                    var records = this.records.Span;
                    var batchGroups = borrowedCapture.CompletedBatchGroups;
                    var camera = this.camera;
                    var inverse = this.inverse;
                    int lastGroup = 0;
                    bool sameCamera = false;
                    Matrix4x4 relative = default;
                    for (int i = worker.Begin; i < worker.End; i++)
                    {
                        ref readonly var source = ref records[i];
                        ref readonly var transform = ref source.State.Transform;
                        int group = batchGroups[i];
                        if (group == 0 || group != lastGroup)
                        {
                            sameCamera = transform.Equals(camera);
                            if (!sameCamera) relative = transform * inverse;
                            lastGroup = group;
                        }

                        var quad = source.Quad;
                        if (!sameCamera)
                        {
                            quad.Position0 = Vector3.Transform(quad.Position0, relative);
                            quad.Position1 = Vector3.Transform(quad.Position1, relative);
                            quad.Position2 = Vector3.Transform(quad.Position2, relative);
                            quad.Position3 = Vector3.Transform(quad.Position3, relative);
                        }

                        ref var result = ref output[i];
                        result.Quad = quad;
                        result.AtlasUnitUv = AtlasUvMask(in quad, source.IsPrimitiveTrianglePair);
                        if (source.IsPrimitiveTrianglePair)
                        {
                            bool commonUv = FiniteUv(quad.TextureCoordinate1) && FiniteUv(quad.TextureCoordinate2);
                            bool firstUv = commonUv && FiniteUv(quad.TextureCoordinate0);
                            bool secondUv = commonUv && FiniteUv(quad.TextureCoordinate3);
                            result.PrimitiveFiniteUv = (byte)((firstUv ? 1 : 0) | (secondUv ? 2 : 0));
                            result.FiniteUv = firstUv && secondUv;
                            result.Bounds = ScenePresentationMeshQuad.TriangleBounds(in quad, false);
                            result.SecondTriangleBounds = ScenePresentationMeshQuad.TriangleBounds(in quad, true);
                        }
                        else
                        {
                            result.FiniteUv = FiniteUv(quad.TextureCoordinate0) && FiniteUv(quad.TextureCoordinate1) &&
                                              FiniteUv(quad.TextureCoordinate2) && FiniteUv(quad.TextureCoordinate3);
                            result.Bounds = result.FiniteUv ? QuadBounds(in quad) : default;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(ref failure, exception, null);
                }
                finally
                {
                    completed.Signal();
                }
            }
        }

        internal static byte AtlasUvMask(in SourceSpriteQuad quad, bool pair)
        {
            if (!pair)
                return AtlasUv(quad.TextureCoordinate0) && AtlasUv(quad.TextureCoordinate1) &&
                       AtlasUv(quad.TextureCoordinate2) && AtlasUv(quad.TextureCoordinate3)
                    ? (byte)1
                    : (byte)0;
            bool first = AtlasUv(quad.TextureCoordinate0) && AtlasUv(quad.TextureCoordinate1) &&
                         AtlasUv(quad.TextureCoordinate2) && AtlasUv(quad.TextureCoordinate2);
            bool second = AtlasUv(quad.TextureCoordinate2) && AtlasUv(quad.TextureCoordinate1) &&
                          AtlasUv(quad.TextureCoordinate3) && AtlasUv(quad.TextureCoordinate3);
            return (byte)((first ? 1 : 0) | (second ? 2 : 0));
        }

        private static bool AtlasUv(Vector2 uv) =>
            uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f;

        internal static SourceSpriteQuad TransformWorldQuad(in SourceSpriteQuad source,
            in Matrix4x4 transform, in Matrix4x4 inverse)
        {
            var quad = source;
            var relative = transform * inverse;
            quad.Position0 = Vector3.Transform(quad.Position0, relative);
            quad.Position1 = Vector3.Transform(quad.Position1, relative);
            quad.Position2 = Vector3.Transform(quad.Position2, relative);
            quad.Position3 = Vector3.Transform(quad.Position3, relative);
            return quad;
        }

        internal static bool FiniteUv(Vector2 uv) =>
            (BitConverter.SingleToInt32Bits(uv.X) & 0x7f800000) != 0x7f800000 &&
            (BitConverter.SingleToInt32Bits(uv.Y) & 0x7f800000) != 0x7f800000;

        internal static Bounds QuadBounds(in SourceSpriteQuad quad)
        {
            float left = Math.Min(Math.Min(quad.Position0.X, quad.Position1.X),
                Math.Min(quad.Position2.X, quad.Position3.X));
            float top = Math.Min(Math.Min(quad.Position0.Y, quad.Position1.Y),
                Math.Min(quad.Position2.Y, quad.Position3.Y));
            float right = Math.Max(Math.Max(quad.Position0.X, quad.Position1.X),
                Math.Max(quad.Position2.X, quad.Position3.X));
            float bottom = Math.Max(Math.Max(quad.Position0.Y, quad.Position1.Y),
                Math.Max(quad.Position2.Y, quad.Position3.Y));
            return new Bounds(left, top, right - left, bottom - top);
        }

        public void Dispose()
        {
            if (disposed) return;
            Wait();
            disposed = true;
            foreach (var worker in workers)
            {
                if (worker == null || !worker.Started) continue;
                worker.Stop = true;
                worker.Wake.Set();
            }

            foreach (var worker in workers)
            {
                if (worker == null) continue;
                if (worker.Started) worker.Thread.Join();
                worker.Wake.Dispose();
            }

            completed.Dispose();
            records = default;
            borrowedCapture = null;
            preparedCapture = null;
            output = Array.Empty<Result>();
        }
    }
}
