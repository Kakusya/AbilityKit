using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingNetworkMeasurement")]
public sealed class CookingNetworkMeasurementTests
{
    private static readonly SessionId Session = new("session-p6");
    private static readonly WorldId World = new("world-p6");
    private static readonly MatchId Match = new("match-p6");
    private static readonly PlayerId Player = new("player-p6");
    private static readonly ItemId Item = new("item-p6");
    private static readonly DefinitionId Definition = new("ingredient-p6");
    private static readonly StationSlotId Station = new("station-p6");
    private static readonly ConnectionId Connection = new("in-process-p6");

    [Fact]
    public void N01_fixed_in_process_workload_emits_comparable_json_csv_and_jsonl_evidence()
    {
        using var evidence = CreateEvidence("N01");
        var workload = Workload(sampleCount: 3, invocationsPerSample: 2);
        var endpoint = new CookingNetworkMeasurementEndpoint(CookingNetworkMeasurementTopology.InProcess,
            CookingNetworkEndpointRole.Host, "host-authority-in-process", "not-applicable", "not-applicable");

        var first = CookingNetworkBaselineMeasurementRunner.Run(workload, new FreshAuthorityFixture(), endpoint);
        var second = CookingNetworkBaselineMeasurementRunner.Run(workload, new FreshAuthorityFixture(), endpoint);
        CookingNetworkMeasurementArtifactWriter.WriteReport(evidence.Directory, first);
        CookingNetworkMeasurementArtifactWriter.AppendEvidence(evidence.EvidencePath, new CookingNetworkMeasurementEvidence(
            "N01", "baseline-run", first.WorkloadSha256, first.Endpoint.Topology.ToString(), "none", "passed",
            first.Samples.First().BeforeStateHash, first.Samples.Last().AfterStateHash,
            "Fixed workload schema and in-process metadata are comparable; this artifact is not LAN evidence.",
            "dotnet test AbilityKit.Game.Cooking.Tests --filter Gate=CookingNetworkMeasurement", DateTimeOffset.UtcNow.ToString("O")));

        Assert.Equal(CookingNetworkMeasurementReport.Schema, first.SchemaVersion);
        Assert.Equal("baseline-only", first.MeasurementMode);
        Assert.Equal(CookingNetworkMeasurementTopology.InProcess, first.Endpoint.Topology);
        Assert.False(string.IsNullOrWhiteSpace(first.Environment.BuildIdentity));
        Assert.Equal("not-applicable", first.Environment.NicStatus);
        Assert.Equal(CookingNetworkThresholdStatus.Unset, first.ThresholdStatus);
        Assert.Equal(workload.Sha256(), first.WorkloadSha256);
        Assert.Equal(first.WorkloadSha256, second.WorkloadSha256);
        Assert.Equal(first.Endpoint, second.Endpoint);
        Assert.Equal(first.MetricDefinitions, second.MetricDefinitions);
        Assert.False(first.MetricDefinitions is Dictionary<string, string>);
        Assert.False(first.Samples is List<CookingNetworkMeasurementSample>);
        Assert.Equal(first.Samples.Count, second.Samples.Count);
        Assert.All(first.Samples, sample =>
        {
            Assert.Equal(2, sample.Operations);
            Assert.True(sample.IngressToCommitNanosecondsPerOperation >= 0);
            Assert.Equal(0, sample.QueueDepthBefore);
            Assert.Equal(1, sample.MaximumQueueDepth);
            Assert.Equal(sample.Operations, sample.OperationTrace.Count);
            Assert.False(sample.OperationTrace is CookingNetworkMeasurementOperation[]);
            Assert.False(sample.OperationTrace is List<CookingNetworkMeasurementOperation>);
            Assert.All(sample.OperationTrace, operation =>
            {
                Assert.True(operation.IngressToCommitNanoseconds >= 0);
                Assert.False(string.IsNullOrWhiteSpace(operation.BeforeStateHash));
                Assert.False(string.IsNullOrWhiteSpace(operation.AfterStateHash));
            });
            Assert.False(string.IsNullOrWhiteSpace(sample.BeforeStateHash));
            Assert.False(string.IsNullOrWhiteSpace(sample.AfterStateHash));
        });
        Assert.Equal(first.Samples.SelectMany(sample => sample.OperationTrace)
                .Select(operation => (operation.BeforeStateHash, operation.AfterStateHash)),
            second.Samples.SelectMany(sample => sample.OperationTrace)
                .Select(operation => (operation.BeforeStateHash, operation.AfterStateHash)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CookingNetworkBaselineMeasurementRunner.Run(workload,
            new FreshAuthorityFixture(), endpoint with { Role = CookingNetworkEndpointRole.Client }));
        var reportPath = Path.Combine(evidence.Directory, "baseline-report.json");
        var csvPath = Path.Combine(evidence.Directory, "baseline-summary.csv");
        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(csvPath));
        var serialized = File.ReadAllText(reportPath);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<CookingNetworkMeasurementReport>(serialized,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        Assert.NotNull(roundTrip);
        Assert.Equal(first.WorkloadSha256, roundTrip.WorkloadSha256);
        Assert.Equal(first.Samples.Count, roundTrip.Samples.Count);
        var csv = File.ReadAllLines(csvPath);
        Assert.Equal(first.Samples.Count + 1, csv.Length);
        Assert.StartsWith("sampleIndex,operations,", csv[0], StringComparison.Ordinal);
        Assert.Single(CookingNetworkMeasurementArtifactWriter.ReadEvidence(evidence.EvidencePath));
    }

    [Fact]
    public void N04_application_faults_are_traceable_and_do_not_create_authority_mutations()
    {
        using var evidence = CreateEvidence("N04");
        var simulation = CreateSimulation();
        using var authority = CreateAuthority(simulation);
        Bind(authority);
        var before = simulation.Snapshot().Sha256();
        var descriptor = authority.Descriptor;
        var baseline = authority.CreateBaseline(1);
        var deltas = new[]
        {
            new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 2, 1, descriptor.ConfigIdentity, "hash-2"),
            new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 3, 1, descriptor.ConfigIdentity, "hash-3"),
        };

        var profile = new CookingApplicationFaultProfile(DelayTicks: 2, JitterTicks: 1, DuplicateEveryNthMessage: 1, Reorder: true);
        var result = CookingNetworkFaultMeasurementRunner.ApplyDeltas(authority, Connection, baseline, deltas, profile, "n04");
        using var repeatedAuthority = CreateAuthority(CreateSimulation());
        Bind(repeatedAuthority);
        var repeated = CookingNetworkFaultMeasurementRunner.ApplyDeltas(repeatedAuthority, Connection,
            repeatedAuthority.CreateBaseline(1), deltas, profile, "n04-repeat");
        CookingNetworkMeasurementArtifactWriter.AppendEvidence(evidence.EvidencePath, new CookingNetworkMeasurementEvidence(
            "N04", "fault-run", Workload().Sha256(), "InProcess", "delay=2;jitter=1;duplicate=1;reorder=true", "passed",
            before, simulation.Snapshot().Sha256(),
            "Application-level logical faults are traceable and sequence discontinuity becomes unsynchronized without authority mutation.",
            "dotnet test AbilityKit.Game.Cooking.Tests --filter Gate=CookingNetworkMeasurement", DateTimeOffset.UtcNow.ToString("O")));

        Assert.True(result.DeliveredCount >= 2);
        Assert.True(result.DuplicateCount >= 1);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, result.FinalSynchronizationState);
        Assert.Equal(
            result.Trace.Select(trace => (trace.MessageOrdinal, trace.LogicalDeliveryTick, trace.Disposition, trace.Faults, trace.Reason)),
            repeated.Trace.Select(trace => (trace.MessageOrdinal, trace.LogicalDeliveryTick, trace.Disposition, trace.Faults, trace.Reason)));
        Assert.Contains(result.Trace, trace => trace.Faults.Contains("reorder", StringComparison.Ordinal));
        Assert.Contains(result.Trace, trace => trace.Reason is CookingSessionReason.SnapshotSequenceGap or CookingSessionReason.SnapshotSequenceDuplicate);
        Assert.Equal(before, simulation.Snapshot().Sha256());
        Assert.Empty(simulation.EventHistory);
        Assert.Single(CookingNetworkMeasurementArtifactWriter.ReadEvidence(evidence.EvidencePath));
    }

    [Fact]
    public void N04_loss_followed_by_a_later_sequence_marks_the_delta_path_unsynchronized_without_authority_mutation()
    {
        var simulation = CreateSimulation();
        using var authority = CreateAuthority(simulation);
        Bind(authority);
        var before = simulation.Snapshot().Sha256();
        var descriptor = authority.Descriptor;

        var result = CookingNetworkFaultMeasurementRunner.ApplyDeltas(authority, Connection, authority.CreateBaseline(1), new[]
            {
                new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 2, 1, descriptor.ConfigIdentity, "hash-2"),
                new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 3, 1, descriptor.ConfigIdentity, "hash-3"),
                new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 4, 1, descriptor.ConfigIdentity, "hash-4"),
            },
            new CookingApplicationFaultProfile(DropEveryNthMessage: 2), "n04-loss");

        Assert.Equal(2, result.DeliveredCount);
        Assert.Equal(1, result.DroppedCount);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, result.FinalSynchronizationState);
        Assert.Contains(result.Trace, trace => trace.Reason == CookingSessionReason.SnapshotSequenceGap);
        Assert.Equal(before, simulation.Snapshot().Sha256());
        Assert.Empty(simulation.EventHistory);
    }

    [Fact]
    public void N05_disconnect_stops_following_delta_delivery_and_keeps_synchronization_unsynchronized()
    {
        using var evidence = CreateEvidence("N05");
        var simulation = CreateSimulation();
        using var authority = CreateAuthority(simulation);
        Bind(authority);
        var before = simulation.Snapshot().Sha256();
        var descriptor = authority.Descriptor;

        var result = CookingNetworkFaultMeasurementRunner.ApplyDeltas(authority, Connection, authority.CreateBaseline(1), new[]
            {
                new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 2, 1, descriptor.ConfigIdentity, "hash-2"),
                new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 3, 1, descriptor.ConfigIdentity, "hash-3"),
            },
            new CookingApplicationFaultProfile(DropEveryNthMessage: 2, DisconnectAfterDeliveries: 1), "n05");
        CookingNetworkMeasurementArtifactWriter.AppendEvidence(evidence.EvidencePath, new CookingNetworkMeasurementEvidence(
            "N05", "disconnect", Workload().Sha256(), "InProcess", "disconnectAfterDeliveries=1", "passed", before,
            simulation.Snapshot().Sha256(),
            "Disconnect stops later logical message delivery and retains the unsynchronized result; no reconnect is implied.",
            "dotnet test AbilityKit.Game.Cooking.Tests --filter Gate=CookingNetworkMeasurement", DateTimeOffset.UtcNow.ToString("O")));

        Assert.True(result.Disconnected);
        Assert.Equal(0, result.DroppedCount);
        Assert.False(result.Trace is List<CookingNetworkFaultTrace>);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, result.FinalSynchronizationState);
        Assert.Contains(result.Trace, trace => trace.Disposition == CookingNetworkFaultDisposition.NotDeliveredAfterDisconnect &&
            trace.Faults.Contains("loss", StringComparison.Ordinal));
        Assert.Equal(before, simulation.Snapshot().Sha256());
        Assert.Empty(simulation.EventHistory);
        Assert.Single(CookingNetworkMeasurementArtifactWriter.ReadEvidence(evidence.EvidencePath));
    }

    [Fact]
    public void N10_owner_approval_lan_evidence_fallback_and_runtime_prerequisites_each_keep_optimization_blocked()
    {
        var baseline = CookingNetworkOptimizationGate.Evaluate(CookingNetworkOptimizationStrategy.Baseline,
            new CookingNetworkOptimizationPrerequisites(CookingNetworkThresholdStatus.Unset, false, false, false));
        var unset = CookingNetworkOptimizationGate.Evaluate(CookingNetworkOptimizationStrategy.Interpolation,
            new CookingNetworkOptimizationPrerequisites(CookingNetworkThresholdStatus.Unset, false, false, false));
        var missingOwner = CookingNetworkOptimizationGate.Evaluate(CookingNetworkOptimizationStrategy.Interpolation,
            new CookingNetworkOptimizationPrerequisites(CookingNetworkThresholdStatus.Approved, false, false, false));
        var missingLan = CookingNetworkOptimizationGate.Evaluate(CookingNetworkOptimizationStrategy.LocalPrediction,
            new CookingNetworkOptimizationPrerequisites(CookingNetworkThresholdStatus.Approved, true, false, false));
        var missingFallback = CookingNetworkOptimizationGate.Evaluate(CookingNetworkOptimizationStrategy.Correction,
            new CookingNetworkOptimizationPrerequisites(CookingNetworkThresholdStatus.Approved, true, true, false));
        var runtimeMissing = CookingNetworkOptimizationGate.Evaluate(CookingNetworkOptimizationStrategy.Correction,
            new CookingNetworkOptimizationPrerequisites(CookingNetworkThresholdStatus.Approved, true, true, true));

        Assert.Equal(CookingNetworkOptimizationGateStatus.BaselineOnly, baseline.Status);
        Assert.Equal(CookingNetworkOptimizationBlockReason.ThresholdsUnset, unset.Reason);
        Assert.Contains("UNSET", unset.Message, StringComparison.Ordinal);
        Assert.Equal(CookingNetworkOptimizationBlockReason.OwnerApprovalMissing, missingOwner.Reason);
        Assert.Equal(CookingNetworkOptimizationBlockReason.RealLanEvidenceMissing, missingLan.Reason);
        Assert.Equal(CookingNetworkOptimizationBlockReason.FallbackPolicyMissing, missingFallback.Reason);
        Assert.Equal(CookingNetworkOptimizationBlockReason.OptimizationRuntimeNotImplemented, runtimeMissing.Reason);
        Assert.All(new[] { unset, missingOwner, missingLan, missingFallback, runtimeMissing },
            result => Assert.Equal(CookingNetworkOptimizationGateStatus.Blocked, result.Status));
    }

    private static CookingNetworkWorkload Workload(int sampleCount = 1, int invocationsPerSample = 1) => new(
        "p6-in-process-baseline", "v1", "config-p6", "cooking-session:1-2",
        new CookingNetworkSamplingWindow(sampleCount, invocationsPerSample));

    private static CookingSessionAuthority CreateAuthority(CookingSimulation simulation) =>
        new(simulation, new CookingSessionDescriptor(new CookingScope(Session, World, Match), 1,
            new CookingProtocolIdentity("cooking-session", 1, 2), "config-p6",
            new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>()), 4);

    private static void Bind(CookingSessionAuthority authority)
    {
        Assert.True(authority.OpenConnection(Connection, CookingConnectionOrigin.RemoteInProcess, "p6-open"));
        var handshake = authority.CompleteHandshake(Connection, Player, new CookingHandshake(
            new CookingScope(Session, World, Match), "cooking-session", 1, "config-p6",
            new HashSet<string>(StringComparer.Ordinal) { "cook" }), "p6-handshake");
        Assert.True(handshake.Accepted);
    }

    private static CookingSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
        };
        var stations = new Dictionary<StationSlotId, CookingStationConfig> { [Station] = new(Station, 2) };
        var definitions = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Definition] = new(Definition, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
        };
        var simulation = new CookingSimulation(new CookingFixture(scope, players, stations, definitions));
        simulation.AddItem(Item, Definition, ItemLocation.World("counter-p6"));
        return simulation;
    }

    private sealed class FreshAuthorityFixture : ICookingNetworkMeasurementFixture
    {
        public CookingNetworkMeasurementInvocation CreateInvocation(CookingNetworkWorkload workload, int sampleIndex, int invocationIndex)
        {
            var simulation = CreateSimulation();
            var authority = CreateAuthority(simulation);
            Bind(authority);
            return new CookingNetworkMeasurementInvocation(simulation, authority, Connection,
                new CookingCommand(new CookingScope(Session, World, Match), 1, Player,
                    new CommandId($"p6-{sampleIndex}-{invocationIndex}"), CookingOperation.Pickup, Item, 1),
                $"p6-{sampleIndex}-{invocationIndex}");
        }
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_NETWORK_MEASUREMENT_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts
                ? Path.GetFullPath(requestedRoot!)
                : Path.Combine(Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "network-measurement");
            Directory = Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            EvidencePath = Path.Combine(Directory, "measurement-evidence.jsonl");
        }

        public string Directory { get; }
        public string EvidencePath { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);
}
