#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// Drives a <see cref="FastReconnectSession"/> toward an externally selected phase while collecting
    /// the health events emitted by each legal session transition.
    /// </summary>
    public sealed class FastReconnectPhaseDriver
    {
        private const int EventBufferCapacity = 16;

        private readonly FastReconnectSession _session;
        private readonly SyncHealthEventBuffer _events =
            new SyncHealthEventBuffer(EventBufferCapacity);

        public FastReconnectPhaseDriver(int resumeWindowFrames)
            : this(new FastReconnectSession(resumeWindowFrames < 1 ? 1 : resumeWindowFrames))
        {
        }

        public FastReconnectPhaseDriver(FastReconnectSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public FastReconnectPhase Phase => _session.Phase;

        public int ResumeWindowFrames => _session.ResumeWindowFrames;

        public IReadOnlyList<SyncHealthEvent> CollectedEvents => _events;

        public void ResetEventBuffer()
        {
            _events.Reset();
        }

        public void Heartbeat(int authoritativeFrame)
        {
            var phase = _session.Phase;
            if (phase == FastReconnectPhase.Connected || phase == FastReconnectPhase.Recovered)
            {
                TryObserve(authoritativeFrame);
            }
        }

        public void Reconcile(FastReconnectPhase target, int authoritativeFrame, int gapHint)
        {
            if (target == FastReconnectPhase.Disconnected)
            {
                return;
            }

            var frame = authoritativeFrame < 0 ? 0 : authoritativeFrame;
            var gap = gapHint < 0 ? -gapHint : gapHint;

            for (var guard = 0; guard < 8; guard++)
            {
                var phase = _session.Phase;
                if (phase == target)
                {
                    return;
                }

                if (!Step(phase, target, frame, gap))
                {
                    return;
                }
            }
        }

        private bool Step(FastReconnectPhase current, FastReconnectPhase target, int frame, int gap)
        {
            switch (current)
            {
                case FastReconnectPhase.Connected:
                    return TryDisconnect();

                case FastReconnectPhase.Recovered:
                    return target == FastReconnectPhase.Connected
                        ? TryObserve(frame)
                        : TryDisconnect();

                case FastReconnectPhase.Disconnected:
                    return target == FastReconnectPhase.AwaitingFullSnapshot
                        ? TryReconnect(LargeGapFrame(gap))
                        : TryReconnect(SmallGapFrame(gap));

                case FastReconnectPhase.Resuming:
                case FastReconnectPhase.AwaitingFullSnapshot:
                    return TryComplete();

                default:
                    return false;
            }
        }

        private int SmallGapFrame(int gap)
        {
            var window = _session.ResumeWindowFrames;
            var bounded = gap;
            if (bounded < 0) bounded = 0;
            if (bounded > window) bounded = window;
            return _session.LastAckedServerFrame + bounded;
        }

        private int LargeGapFrame(int gap)
        {
            var window = _session.ResumeWindowFrames;
            var forced = gap > window ? gap : window + 1;
            return _session.LastAckedServerFrame + forced;
        }

        private bool TryObserve(int frame)
        {
            var safe = frame < _session.LastAckedServerFrame ? _session.LastAckedServerFrame : frame;
            try
            {
                Collect(_session.ObserveServerFrame(safe));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryDisconnect()
        {
            try
            {
                Collect(_session.Disconnect());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryReconnect(int currentServerFrame)
        {
            var safe = currentServerFrame < _session.LastAckedServerFrame
                ? _session.LastAckedServerFrame
                : currentServerFrame;
            try
            {
                Collect(_session.Reconnect(safe));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryComplete()
        {
            try
            {
                Collect(_session.CompleteRecovery());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Collect(in FastReconnectStepReport report)
        {
            _events.Publish(report.HealthEvents);
        }
    }
}
