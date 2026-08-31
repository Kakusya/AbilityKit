using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Replay.Validation
{
    /// <summary>
    /// Result of comparing two snapshots (recorded vs replayed) for a single frame.
    /// Pure data; no Unity dependencies.
    /// </summary>
    public sealed class ReplaySnapshotComparison
    {
        public ReplaySnapshotComparison()
        {
            ActorDiffs = new List<ReplayActorDiff>(16);
            MissingActorIds = new List<int>(8);
            ExtraActorIds = new List<int>(8);
        }

        public int Frame { get; set; }
        public int ReferenceFrame { get; set; }
        public bool FrameNumbersMatch => Frame == ReferenceFrame;

        /// <summary>
        /// Per-actor numerical diffs for actors present in both snapshots.
        /// </summary>
        public List<ReplayActorDiff> ActorDiffs { get; }

        /// <summary>
        /// ActorIds present in the recorded snapshot but missing from the replayed one.
        /// </summary>
        public List<int> MissingActorIds { get; }

        /// <summary>
        /// ActorIds present in the replayed snapshot but missing from the recorded one.
        /// </summary>
        public List<int> ExtraActorIds { get; }

        public bool HasDivergence
        {
            get
            {
                if (MissingActorIds.Count > 0) return true;
                if (ExtraActorIds.Count > 0) return true;
                if (!FrameNumbersMatch) return true;
                for (var i = 0; i < ActorDiffs.Count; i++)
                {
                    if (ActorDiffs[i].IsDivergent) return true;
                }
                return false;
            }
        }

        public int MaxPositionDelta => AggregateMax(d => d.MaxPositionDelta);
        public int MaxRotationDelta => AggregateMax(d => d.MaxRotationDelta);
        public int MaxHpDelta => AggregateMax(d => d.MaxHpDelta);

        private int AggregateMax(Func<ReplayActorDiff, int> selector)
        {
            var max = 0;
            for (var i = 0; i < ActorDiffs.Count; i++)
            {
                var v = selector(ActorDiffs[i]);
                if (v > max) max = v;
            }
            return max;
        }

        public static ReplaySnapshotComparison Match(int frame)
        {
            return new ReplaySnapshotComparison { Frame = frame, ReferenceFrame = frame };
        }
    }

    /// <summary>
    /// Per-actor numerical diff between a recorded snapshot and its replayed counterpart.
    /// All numeric fields are deltas in their respective units (positions in world units,
    /// rotation in radians, HP in HP units).
    /// </summary>
    public readonly struct ReplayActorDiff
    {
        public ReplayActorDiff(
            int actorId,
            int positionDeltaMilliUnits,
            int rotationDeltaMilliUnits,
            int hpDeltaMilliUnits,
            int velocityDeltaMilliUnits,
            bool teamMatches)
        {
            ActorId = actorId;
            PositionDelta = positionDeltaMilliUnits;
            RotationDelta = rotationDeltaMilliUnits;
            HpDelta = hpDeltaMilliUnits;
            VelocityDelta = velocityDeltaMilliUnits;
            TeamMatches = teamMatches;
        }

        public int ActorId { get; }
        public int PositionDelta { get; }
        public int RotationDelta { get; }
        public int HpDelta { get; }
        public int VelocityDelta { get; }
        public bool TeamMatches { get; }

        public int MaxPositionDelta => PositionDelta;
        public int MaxRotationDelta => RotationDelta;
        public int MaxHpDelta => HpDelta;

        public bool IsDivergent =>
            PositionDelta > 0 ||
            RotationDelta > 0 ||
            HpDelta > 0 ||
            VelocityDelta > 0 ||
            !TeamMatches;
    }

    /// <summary>
    /// Configuration controlling the validator's tolerance and stop conditions.
    /// </summary>
    public sealed class MobaReplayValidatorOptions
    {
        public MobaReplayValidatorOptions()
        {
            PositionToleranceMilliUnits = 1;       // 1 mm world units
            RotationToleranceMilliRadians = 1;    // ~0.057 degrees
            HpToleranceMilliUnits = 1;            // 1 milli-HP
            VelocityToleranceMilliUnits = 1;      // 1 mm/s
            StopOnFirstDivergence = false;
            MaxComparedFrames = 0;                // 0 = no limit
        }

        public int PositionToleranceMilliUnits { get; set; }
        public int RotationToleranceMilliRadians { get; set; }
        public int HpToleranceMilliUnits { get; set; }
        public int VelocityToleranceMilliUnits { get; set; }
        public bool StopOnFirstDivergence { get; set; }
        public int MaxComparedFrames { get; set; }
    }

    /// <summary>
    /// Result of running a full comparison between two streams.
    /// </summary>
    public sealed class MobaReplayValidatorReport
    {
        public MobaReplayValidatorReport()
        {
            Comparisons = new List<ReplaySnapshotComparison>(256);
        }

        public string ScenarioName { get; set; }
        public int TotalComparedFrames { get; set; }
        public int MatchedFrames { get; set; }
        public int DivergentFrames { get; set; }
        public int SkippedFrames { get; set; }
        public int TotalActorDiffs { get; set; }
        public int TotalMissingActors { get; set; }
        public int TotalExtraActors { get; set; }
        public int MaxPositionDelta { get; set; }
        public int MaxRotationDelta { get; set; }
        public int MaxHpDelta { get; set; }
        public int MaxVelocityDelta { get; set; }
        public List<ReplaySnapshotComparison> Comparisons { get; }

        public bool IsClean => DivergentFrames == 0;

        public float MatchRate =>
            TotalComparedFrames > 0 ? (float)MatchedFrames / TotalComparedFrames : 1f;

        public string ToCompactString()
        {
            return
                $"Scenario={ScenarioName ?? "?"} Compared={TotalComparedFrames} Matched={MatchedFrames} " +
                $"Divergent={DivergentFrames} Skipped={SkippedFrames} " +
                $"ActorDiffs={TotalActorDiffs} Missing={TotalMissingActors} Extra={TotalExtraActors} " +
                $"MaxPosΔ={MaxPositionDelta}mm MaxRotΔ={MaxRotationDelta}mRad MaxHpΔ={MaxHpDelta}mHp MaxVelΔ={MaxVelocityDelta}mm/s " +
                $"MatchRate={MatchRate:P2}";
        }
    }

    /// <summary>
    /// Compares two streams of <see cref="MobaWorldSnapshotPayload"/> and produces a validator report.
    /// Pure data, Unity-free, allocation-light. Suitable for unit tests and headless determinism checks.
    /// </summary>
    public sealed class MobaReplayValidator
    {
        private readonly MobaReplayValidatorOptions _options;
        private readonly Dictionary<int, MobaActorSnapshotEntry> _replayIndexBuffer;

        public MobaReplayValidator(MobaReplayValidatorOptions options = null)
        {
            _options = options ?? new MobaReplayValidatorOptions();
            _replayIndexBuffer = new Dictionary<int, MobaActorSnapshotEntry>(64);
        }

        public MobaReplayValidatorOptions Options => _options;

        /// <summary>
        /// Compare two parallel streams of recorded snapshots frame-by-frame.
        /// </summary>
        public MobaReplayValidatorReport Compare(
            IReadOnlyList<MobaWorldSnapshotPayload> recorded,
            IReadOnlyList<MobaWorldSnapshotPayload> replayed,
            string scenarioName = null)
        {
            var report = new MobaReplayValidatorReport { ScenarioName = scenarioName };
            if (recorded == null || replayed == null || recorded.Count == 0 || replayed.Count == 0)
            {
                return report;
            }

            var recordedCount = recorded.Count;
            var replayedCount = replayed.Count;
            var comparedCount = Math.Min(recordedCount, replayedCount);
            if (_options.MaxComparedFrames > 0 && comparedCount > _options.MaxComparedFrames)
            {
                comparedCount = _options.MaxComparedFrames;
            }

            for (var i = 0; i < comparedCount; i++)
            {
                var rec = recorded[i];
                var rep = replayed[i];
                var comp = CompareFrame(in rec, in rep);
                report.Comparisons.Add(comp);
                report.TotalComparedFrames++;

                if (!comp.FrameNumbersMatch)
                {
                    report.DivergentFrames++;
                    report.SkippedFrames++;
                    if (_options.StopOnFirstDivergence) break;
                    continue;
                }

                if (!comp.HasDivergence)
                {
                    report.MatchedFrames++;
                    continue;
                }

                report.DivergentFrames++;
                report.TotalMissingActors += comp.MissingActorIds.Count;
                report.TotalExtraActors += comp.ExtraActorIds.Count;
                report.TotalActorDiffs += comp.ActorDiffs.Count;
                for (var d = 0; d < comp.ActorDiffs.Count; d++)
                {
                    var diff = comp.ActorDiffs[d];
                    if (diff.PositionDelta > report.MaxPositionDelta) report.MaxPositionDelta = diff.PositionDelta;
                    if (diff.RotationDelta > report.MaxRotationDelta) report.MaxRotationDelta = diff.RotationDelta;
                    if (diff.HpDelta > report.MaxHpDelta) report.MaxHpDelta = diff.HpDelta;
                    if (diff.VelocityDelta > report.MaxVelocityDelta) report.MaxVelocityDelta = diff.VelocityDelta;
                }

                if (_options.StopOnFirstDivergence) break;
            }

            // Frames that exist in one stream but not the other are reported as skipped.
            if (recordedCount != replayedCount)
            {
                var skipped = Math.Abs(recordedCount - replayedCount);
                report.SkippedFrames += skipped;
            }

            return report;
        }

        /// <summary>
        /// Compare a single pair of snapshots.
        /// </summary>
        public ReplaySnapshotComparison CompareFrame(
            in MobaWorldSnapshotPayload recorded,
            in MobaWorldSnapshotPayload replayed)
        {
            var comp = new ReplaySnapshotComparison
            {
                Frame = recorded.Frame,
                ReferenceFrame = replayed.Frame
            };

            if (recorded.Frame != replayed.Frame)
            {
                return comp;
            }

            BuildReplayIndex(replayed, _replayIndexBuffer);

            // Walk recorded actors; for each present in replayed compute the diff.
            if (recorded.Actors != null)
            {
                for (var i = 0; i < recorded.Actors.Length; i++)
                {
                    var ra = recorded.Actors[i];
                    if (_replayIndexBuffer.TryGetValue(ra.ActorId, out var replayer))
                    {
                        var diff = BuildActorDiff(in ra, in replayer);
                        comp.ActorDiffs.Add(diff);
                    }
                    else
                    {
                        comp.MissingActorIds.Add(ra.ActorId);
                    }
                }
            }

            // Walk replayed actors to find extras.
            if (replayed.Actors != null)
            {
                for (var i = 0; i < replayed.Actors.Length; i++)
                {
                    var replayer = replayed.Actors[i];
                    if (!ContainsActor(in recorded, replayer.ActorId))
                    {
                        comp.ExtraActorIds.Add(replayer.ActorId);
                    }
                }
            }

            return comp;
        }

        private void BuildReplayIndex(
            in MobaWorldSnapshotPayload replayed,
            Dictionary<int, MobaActorSnapshotEntry> buffer)
        {
            buffer.Clear();
            if (replayed.Actors == null) return;
            for (var i = 0; i < replayed.Actors.Length; i++)
            {
                var a = replayed.Actors[i];
                buffer[a.ActorId] = a;
            }
        }

        private static bool ContainsActor(in MobaWorldSnapshotPayload snapshot, int actorId)
        {
            if (snapshot.Actors == null) return false;
            for (var i = 0; i < snapshot.Actors.Length; i++)
            {
                if (snapshot.Actors[i].ActorId == actorId) return true;
            }
            return false;
        }

        private ReplayActorDiff BuildActorDiff(in MobaActorSnapshotEntry recorded, in MobaActorSnapshotEntry replayed)
        {
            var posDelta = Max3(
                AbsMilliDelta(recorded.PositionX, replayed.PositionX),
                AbsMilliDelta(recorded.PositionY, replayed.PositionY),
                AbsMilliDelta(recorded.PositionZ, replayed.PositionZ));
            var rotDelta = AbsMilliDelta(recorded.Rotation, replayed.Rotation);
            var hpDelta = AbsMilliDelta(recorded.Hp, replayed.Hp);
            var velDelta = Max2(
                AbsMilliDelta(recorded.VelocityX, replayed.VelocityX),
                AbsMilliDelta(recorded.VelocityZ, replayed.VelocityZ));

            var teamMatches = recorded.TeamId == replayed.TeamId;

            return new ReplayActorDiff(
                recorded.ActorId,
                posDelta,
                rotDelta,
                hpDelta,
                velDelta,
                teamMatches);
        }

        private static int AbsMilliDelta(float a, float b)
        {
            var d = a - b;
            var abs = d < 0f ? -d : d;
            return (int)System.Math.Round(abs * 1000f);
        }

        private static int Max2(int a, int b) => a > b ? a : b;
        private static int Max3(int a, int b, int c)
        {
            var m = a > b ? a : b;
            return m > c ? m : c;
        }
    }
}