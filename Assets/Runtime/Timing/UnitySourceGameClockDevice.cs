using System;
using Celeste.Core.Framework.Timing;
using UnityEngine;

namespace Celeste.Unity.Runtime.Timing
{
    public sealed class UnitySourceGameClockDevice : ISourceHostScheduledGameTickDevice, ISourceGameTimerStartDevice
    {
        private Action pollEvents;
        private Func<bool> sourceUpdateBlock;
        public bool SourceUpdatesBlocked => sourceUpdateBlock != null && sourceUpdateBlock();
        private bool started;
        private double startedAt;
        private double pausedAt;
        private double pausedDuration;
        public bool Paused { get; private set; }

        public TimeSpan Elapsed => ElapsedAt(Time.realtimeSinceStartupAsDouble);

        internal TimeSpan ElapsedAt(double now) => started
            ? TimeSpan.FromTicks((long)(((Paused ? pausedAt : now) - startedAt - pausedDuration) *
                                        TimeSpan.TicksPerSecond))
            : TimeSpan.Zero;

        public void StartGameTimer() => StartAt(Time.realtimeSinceStartupAsDouble);

        internal void StartAt(double now)
        {
            if (started) return;
            startedAt = now;
            if (Paused) pausedAt = startedAt;
            pausedDuration = 0;
            started = true;
        }

        public void Pause() => PauseAt(Time.realtimeSinceStartupAsDouble);

        internal void PauseAt(double now)
        {
            if (Paused) return;
            pausedAt = now;
            Paused = true;
        }

        public void Resume() => ResumeAt(Time.realtimeSinceStartupAsDouble);

        internal void ResumeAt(double now)
        {
            if (!Paused) return;
            if (started) pausedDuration += now - pausedAt;
            Paused = false;
        }

        public void BindSourceUpdateBlock(Func<bool> readBlocked)
        {
            if (sourceUpdateBlock != null)
                throw new InvalidOperationException("The source update block owner is already bound.");
            sourceUpdateBlock = readBlocked ?? throw new ArgumentNullException(nameof(readBlocked));
        }

        public void BindPollEvents(Action poll)
        {
            if (pollEvents != null)
                throw new InvalidOperationException("The actual source input poll is already bound.");
            pollEvents = poll ?? throw new ArgumentNullException(nameof(poll));
        }

        public void PollEvents()
        {
            if (pollEvents == null)
                throw new InvalidOperationException("Bind the actual source input poll before ticking.");
            pollEvents();
        }

        public void Sleep(int milliseconds)
            => throw new InvalidOperationException(
                "A host-scheduled clock must return to the host instead of sleeping.");

        public void SpinWait(int iterations)
            => throw new InvalidOperationException(
                "A host-scheduled clock must return to the host instead of spinning.");
    }
}
