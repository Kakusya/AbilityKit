using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Replay.Validation
{
    /// <summary>
    /// Plug-in strategy used by the determinism harness to drive a snapshot from inputs.
    /// Production implementations feed a live <see cref="AbilityKit.Game.Battle.BattleLogicSession"/>
    /// or a headless determinism runner. Tests can plug a deterministic stub.
    /// </summary>
    public interface IMobaReplayDeterminismRunner
    {
        bool TryRun(
            IReadOnlyList<PlayerInputCommandBatch> inputSequence,
            out IReadOnlyList<MobaWorldSnapshotPayload> snapshots,
            out string failureReason);

        string RunnerName { get; }
    }

    /// <summary>
    /// A batch of player inputs that share the same frame index.
    /// </summary>
    public readonly struct PlayerInputCommandBatch
    {
        public PlayerInputCommandBatch(int frame, IReadOnlyList<byte[]> commands)
        {
            Frame = frame;
            Commands = commands ?? Array.Empty<byte[]>();
        }

        public int Frame { get; }
        public IReadOnlyList<byte[]> Commands { get; }
    }

    /// <summary>
    /// High-level harness that runs the same input sequence through two runners (typically
    /// "reference" and "candidate") and produces a <see cref="MobaReplayValidatorReport"/>.
    ///
    /// Use cases:
    ///   - Replaying a recorded run against itself to validate the harness.
    ///   - Comparing two engine builds for determinism divergence.
    ///   - Comparing a recorded server run against a candidate client-side prediction.
    /// </summary>
    public sealed class MobaReplayDeterminismHarness
    {
        private readonly MobaReplayValidator _validator;

        public MobaReplayDeterminismHarness(MobaReplayValidator validator = null)
        {
            _validator = validator ?? new MobaReplayValidator();
        }

        public MobaReplayValidator Validator => _validator;

        /// <summary>
        /// Run the same input sequence through both runners and compare their snapshots.
        /// </summary>
        public MobaReplayDeterminismHarnessResult Run(
            string scenarioName,
            IReadOnlyList<PlayerInputCommandBatch> inputSequence,
            IMobaReplayDeterminismRunner reference,
            IMobaReplayDeterminismRunner candidate)
        {
            var result = new MobaReplayDeterminismHarnessResult
            {
                ScenarioName = scenarioName,
                ReferenceRunnerName = reference?.RunnerName ?? "<null>",
                CandidateRunnerName = candidate?.RunnerName ?? "<null>"
            };

            if (reference == null || candidate == null)
            {
                result.FailureReason = "Both reference and candidate runners must be non-null.";
                return result;
            }

            if (!reference.TryRun(inputSequence, out var refShots, out var refFailure))
            {
                result.FailureReason = $"Reference runner failed: {refFailure}";
                return result;
            }
            result.ReferenceSnapshotCount = refShots?.Count ?? 0;

            if (!candidate.TryRun(inputSequence, out var candShots, out var candFailure))
            {
                result.FailureReason = $"Candidate runner failed: {candFailure}";
                return result;
            }
            result.CandidateSnapshotCount = candShots?.Count ?? 0;

            if (result.ReferenceSnapshotCount != result.CandidateSnapshotCount)
            {
                result.FailureReason =
                    $"Snapshot counts differ: reference={result.ReferenceSnapshotCount} candidate={result.CandidateSnapshotCount}";
                // Continue comparison — we still emit a partial report.
            }

            result.Report = _validator.Compare(refShots, candShots, scenarioName);
            result.IsSuccess = result.FailureReason == null;
            return result;
        }
    }

    /// <summary>
    /// Outcome of running the harness against a pair of runners.
    /// </summary>
    public sealed class MobaReplayDeterminismHarnessResult
    {
        public string ScenarioName { get; set; }
        public string ReferenceRunnerName { get; set; }
        public string CandidateRunnerName { get; set; }
        public int ReferenceSnapshotCount { get; set; }
        public int CandidateSnapshotCount { get; set; }
        public string FailureReason { get; set; }
        public bool IsSuccess { get; set; }
        public MobaReplayValidatorReport Report { get; set; }

        public bool IsDeterministic =>
            IsSuccess &&
            FailureReason == null &&
            Report != null &&
            Report.IsClean;

        public string ToCompactString()
        {
            var reportPart = Report != null ? Report.ToCompactString() : "<no report>";
            return
                $"Scenario={ScenarioName ?? "?"} Reference={ReferenceRunnerName}({ReferenceSnapshotCount}) " +
                $"Candidate={CandidateRunnerName}({CandidateSnapshotCount}) " +
                $"Deterministic={IsDeterministic} {(FailureReason ?? "")} | {reportPart}";
        }
    }
}