using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingLanSession")]
public sealed class CookingLanSessionContractTests
{
    private static readonly SessionId Session = new("session-p1");
    private static readonly WorldId World = new("world-p1");
    private static readonly MatchId Match = new("match-p1");
    private static readonly PlayerId PlayerOne = new("player-a");
    private static readonly PlayerId PlayerTwo = new("player-b");
    private static readonly ItemId Item = new("item-a");
    private static readonly DefinitionId Definition = new("ingredient-a");
    private static readonly StationSlotId Station = new("station-a");
    private static readonly ConnectionId HostConnection = new("host-local");
    private static readonly ConnectionId RemoteConnection = new("remote-in-process");
    private static readonly ConnectionId SecondRemoteConnection = new("remote-in-process-two");
    private static readonly ConnectionId MalformedConnection = new("malformed-connection");

    [Fact]
    public void L01_host_local_and_remote_in_process_share_one_authority_batch()
    {
        using var evidence = CreateEvidence("L01");
        var simulation = CreateSimulation();
        using var authority = CreateAuthority(simulation, queueCapacity: 4);
        Bind(authority, HostConnection, PlayerOne, CookingConnectionOrigin.HostLocal);
        Bind(authority, RemoteConnection, PlayerTwo, CookingConnectionOrigin.RemoteInProcess);

        var hostQueued = authority.EnqueueCommand(HostConnection, Command(PlayerOne, "host-command"), "l01-host");
        var remoteQueued = authority.EnqueueCommand(RemoteConnection, Command(PlayerTwo, "remote-command"), "l01-remote");
        Assert.Equal(CookingSessionCommandDisposition.Queued, hostQueued.Disposition);
        Assert.Equal(CookingSessionCommandDisposition.Queued, remoteQueued.Disposition);

        var executions = authority.ExecuteNextBatch();
        Assert.Equal(2, executions.Count);
        Assert.Single(executions, result => result.AuthorityResult?.Outcome == CommandOutcome.Accepted);
        Assert.Single(simulation.EventHistory);
        Assert.Equal(PlayerOne, simulation.EventHistory.Single().Player);
        Assert.Equal(0, simulation.SubmittedEnvelopeCount);
        Assert.Equal(0, authority.QueueDepth);

        WriteDiagnostics(evidence.Path, authority);
        AssertDiagnostics(evidence.Path, "command-enqueued", "command-executed");
    }

    [Fact]
    public void L02_claimed_foreign_identity_and_scope_are_rejected_without_mutation()
    {
        using var evidence = CreateEvidence("L02");
        var simulation = CreateSimulation();
        using var authority = CreateAuthority(simulation, queueCapacity: 2);
        Bind(authority, RemoteConnection, PlayerOne, CookingConnectionOrigin.RemoteInProcess);
        var before = simulation.Snapshot().Sha256();

        var foreignPlayer = authority.EnqueueCommand(RemoteConnection, Command(PlayerTwo, "foreign-player"), "l02-player");
        var foreignScope = authority.EnqueueCommand(RemoteConnection,
            Command(PlayerOne, "foreign-scope", new CookingScope(new SessionId("other"), World, Match)), "l02-scope");

        Assert.Equal(CookingSessionReason.PlayerMismatch, foreignPlayer.Reason);
        Assert.Equal(CookingSessionReason.ScopeMismatch, foreignScope.Reason);
        Assert.Equal(0, authority.QueueDepth);
        Assert.Equal(before, simulation.Snapshot().Sha256());
        Assert.Empty(simulation.EventHistory);

        WriteDiagnostics(evidence.Path, authority);
        AssertDiagnostics(evidence.Path, "command-rejected");
    }

    [Fact]
    public void L03_handshake_rejections_do_not_create_gameplay_binding()
    {
        using var authority = CreateAuthority(CreateSimulation(), queueCapacity: 2);
        Assert.True(authority.OpenConnection(RemoteConnection, CookingConnectionOrigin.RemoteInProcess, "l03-open"));

        var rejected = authority.CompleteHandshake(RemoteConnection, PlayerOne,
            Handshake(protocolVersion: 999), "l03-bad-version");

        Assert.False(rejected.Accepted);
        Assert.Equal(CookingSessionReason.ProtocolVersionUnsupported, rejected.Reason);
        Assert.Null(rejected.Binding);
        var command = authority.EnqueueCommand(RemoteConnection, Command(PlayerOne, "after-reject"), "l03-command");
        Assert.Equal(CookingSessionReason.ConnectionNotBound, command.Reason);
        Assert.Equal(0, authority.QueueDepth);
    }

    [Fact]
    public void L03_rejects_duplicate_player_binding_unknown_assignment_and_malformed_handshake()
    {
        using var authority = CreateAuthority(CreateSimulation(), queueCapacity: 2);
        Bind(authority, RemoteConnection, PlayerOne, CookingConnectionOrigin.RemoteInProcess);

        Assert.True(authority.OpenConnection(SecondRemoteConnection, CookingConnectionOrigin.RemoteInProcess, "l03-second-open"));
        var duplicate = authority.CompleteHandshake(SecondRemoteConnection, PlayerOne, Handshake(), "l03-duplicate-player");
        Assert.False(duplicate.Accepted);
        Assert.Equal(CookingSessionReason.PlayerAlreadyBound, duplicate.Reason);

        Assert.True(authority.OpenConnection(MalformedConnection, CookingConnectionOrigin.RemoteInProcess, "l03-malformed-open"));
        var malformed = authority.CompleteHandshake(MalformedConnection, PlayerTwo, null!, "l03-malformed");
        Assert.False(malformed.Accepted);
        Assert.Equal(CookingSessionReason.InvalidHandshake, malformed.Reason);

        var unknownConnection = new ConnectionId("unknown-assignment");
        Assert.True(authority.OpenConnection(unknownConnection, CookingConnectionOrigin.RemoteInProcess, "l03-unknown-open"));
        var unknown = authority.CompleteHandshake(unknownConnection, new PlayerId("not-in-fixture"), Handshake(), "l03-unknown");
        Assert.False(unknown.Accepted);
        Assert.Equal(CookingSessionReason.AssignedPlayerNotFound, unknown.Reason);
    }

    [Fact]
    public void L04_and_L05_baseline_precedes_delta_and_sequence_failures_become_unsynchronized()
    {
        using var evidence = CreateEvidence("L04-L05");
        using var authority = CreateAuthority(CreateSimulation(), queueCapacity: 2);
        Bind(authority, RemoteConnection, PlayerOne, CookingConnectionOrigin.RemoteInProcess);
        var descriptor = authority.Descriptor;

        var beforeBaseline = authority.ApplyDelta(RemoteConnection,
            new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 2, 1, descriptor.ConfigIdentity, "hash-2"), "l04-before");
        Assert.False(beforeBaseline.Accepted);
        Assert.Equal(CookingSessionReason.BaselineRequired, beforeBaseline.Reason);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, beforeBaseline.State);

        var baseline = authority.InstallBaseline(RemoteConnection, authority.CreateBaseline(1), "l04-baseline");
        Assert.True(baseline.Accepted);

        var duplicate = authority.ApplyDelta(RemoteConnection,
            new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 1, 1, descriptor.ConfigIdentity, "hash-duplicate"), "l05-duplicate");
        Assert.Equal(CookingSessionReason.SnapshotSequenceDuplicate, duplicate.Reason);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, duplicate.State);

        var recovered = authority.InstallBaseline(RemoteConnection, authority.CreateBaseline(10), "l05-recover");
        Assert.True(recovered.Accepted);
        var gap = authority.ApplyDelta(RemoteConnection,
            new CookingSnapshotDelta(descriptor.Scope, descriptor.Epoch, 12, 10, descriptor.ConfigIdentity, "hash-12"), "l05-gap");
        Assert.Equal(CookingSessionReason.SnapshotSequenceGap, gap.Reason);

        var wrongEpoch = authority.InstallBaseline(RemoteConnection,
            new CookingSnapshotBaseline(descriptor.Scope, descriptor.Epoch - 1, 11, descriptor.ConfigIdentity, "old"), "l05-epoch");
        Assert.Equal(CookingSessionReason.EpochMismatch, wrongEpoch.Reason);
        var staleBaseline = authority.InstallBaseline(RemoteConnection, authority.CreateBaseline(9), "l05-stale-baseline");
        Assert.Equal(CookingSessionReason.SnapshotSequenceStale, staleBaseline.Reason);
        var invalidBaseline = authority.InstallBaseline(RemoteConnection,
            new CookingSnapshotBaseline(descriptor.Scope, descriptor.Epoch, 0, descriptor.ConfigIdentity, ""), "l05-invalid");
        Assert.Equal(CookingSessionReason.InvalidBaseline, invalidBaseline.Reason);

        WriteDiagnostics(evidence.Path, authority);
        AssertDiagnostics(evidence.Path, "delta-rejected", "baseline-installed", "synchronization-rejected");
    }

    [Fact]
    public void L06_duplicate_and_conflicting_command_identity_are_mutation_safe()
    {
        using var authority = CreateAuthority(CreateSimulation(), queueCapacity: 2);
        Bind(authority, HostConnection, PlayerOne, CookingConnectionOrigin.HostLocal);

        var command = Command(PlayerOne, "dedup");
        Assert.Equal(CookingSessionCommandDisposition.Queued, authority.EnqueueCommand(HostConnection, command, "l06-first").Disposition);
        var duplicate = authority.EnqueueCommand(HostConnection, command, "l06-duplicate");
        Assert.Equal(CookingSessionCommandDisposition.Duplicate, duplicate.Disposition);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(1, authority.QueueDepth);
        Assert.Equal(1, authority.DeduplicationCount);

        var conflict = authority.EnqueueCommand(HostConnection,
            command with { Operation = CookingOperation.Drop, TargetStation = Station }, "l06-conflict");
        Assert.Equal(CookingSessionReason.CommandIdentityConflict, conflict.Reason);
        Assert.Equal(1, authority.QueueDepth);

        var result = Assert.Single(authority.ExecuteNextBatch());
        Assert.Equal(CommandOutcome.Accepted, result.AuthorityResult?.Outcome);
        Assert.Single(result.AuthorityResult!.Events);
    }

    [Fact]
    public void L07_queue_cancellation_close_and_dispose_stop_new_ingress_and_emit_unbind_trace()
    {
        using var evidence = CreateEvidence("L07");
        var simulation = CreateSimulation();
        var authority = CreateAuthority(simulation, queueCapacity: 1);
        Bind(authority, HostConnection, PlayerOne, CookingConnectionOrigin.HostLocal);
        Bind(authority, RemoteConnection, PlayerTwo, CookingConnectionOrigin.RemoteInProcess);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancellation = authority.EnqueueCommand(RemoteConnection, Command(PlayerTwo, "cancelled"), "l07-cancel", cancelled.Token);
        Assert.Equal(CookingSessionReason.Cancelled, cancellation.Reason);
        var malformed = authority.EnqueueCommand(RemoteConnection, null!, "l07-malformed");
        Assert.Equal(CookingSessionReason.MalformedCommand, malformed.Reason);

        Assert.Equal(CookingSessionCommandDisposition.Queued,
            authority.EnqueueCommand(HostConnection, Command(PlayerOne, "queued"), "l07-queued").Disposition);
        var full = authority.EnqueueCommand(RemoteConnection, Command(PlayerTwo, "full"), "l07-full");
        Assert.Equal(CookingSessionReason.QueueFull, full.Reason);

        Assert.Equal(1, authority.CloseConnection(HostConnection, "l07-close"));
        Assert.Equal(0, authority.QueueDepth);
        Assert.Empty(simulation.EventHistory);
        var closed = authority.EnqueueCommand(HostConnection, Command(PlayerOne, "closed"), "l07-after-close");
        Assert.Equal(CookingSessionReason.ConnectionClosed, closed.Reason);

        authority.Dispose();
        var disposed = authority.EnqueueCommand(RemoteConnection, Command(PlayerTwo, "disposed"), "l07-after-dispose");
        Assert.Equal(CookingSessionReason.SessionDisposed, disposed.Reason);
        Assert.Empty(simulation.EventHistory);

        WriteDiagnostics(evidence.Path, authority);
        AssertDiagnostics(evidence.Path, "command-cancelled", "connection-closed", "resource-unbound", "command-rejected");
    }

    [Fact]
    public void L09_transport_loss_marks_the_connection_unsynchronized_without_authority_mutation()
    {
        using var evidence = CreateEvidence("L09");
        var simulation = CreateSimulation();
        using var authority = CreateAuthority(simulation, queueCapacity: 2);
        Bind(authority, RemoteConnection, PlayerOne, CookingConnectionOrigin.RemoteInProcess);
        var descriptor = authority.Descriptor;
        var before = simulation.Snapshot().Sha256();
        var mismatchedBaseline = authority.InstallBaseline(RemoteConnection,
            new CookingSnapshotBaseline(descriptor.Scope, descriptor.Epoch, 3, descriptor.ConfigIdentity, "forged"), "l09-forged");
        Assert.Equal(CookingSessionReason.AuthoritySnapshotMismatch, mismatchedBaseline.Reason);
        Assert.True(authority.InstallBaseline(RemoteConnection, authority.CreateBaseline(3), "l09-baseline").Accepted);
        before = simulation.Snapshot().Sha256();

        Assert.True(authority.EnqueueCommand(RemoteConnection, Command(PlayerOne, "queued-before-loss"), "l09-queued").Disposition ==
            CookingSessionCommandDisposition.Queued);
        Assert.Equal(1, authority.QueueDepth);
        var loss = authority.ReportTransportLoss(RemoteConnection, "l09-loss");

        Assert.False(loss.Accepted);
        Assert.Equal(CookingSessionReason.ConnectionClosed, loss.Reason);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, loss.State);
        Assert.Equal(0, authority.QueueDepth);
        Assert.Empty(authority.ExecuteNextBatch());
        Assert.Equal(before, simulation.Snapshot().Sha256());
        Assert.Empty(simulation.EventHistory);
        var afterLoss = authority.EnqueueCommand(RemoteConnection, Command(PlayerOne, "after-loss"), "l09-command");
        Assert.Equal(CookingSessionReason.ConnectionClosed, afterLoss.Reason);

        WriteDiagnostics(evidence.Path, authority);
        AssertDiagnostics(evidence.Path, "transport-loss", "resource-unbound", "command-rejected");
    }

    [Fact]
    public void Descriptor_captures_immutable_capabilities_and_policy()
    {
        var capabilities = new HashSet<string>(StringComparer.Ordinal) { "cook" };
        var policy = new Dictionary<string, string>(StringComparer.Ordinal) { ["mode"] = "cooperative" };
        var descriptor = new CookingSessionDescriptor(new CookingScope(Session, World, Match), 1,
            new CookingProtocolIdentity("cooking-session", 1, 2), "config-sha256-a", capabilities, policy);

        capabilities.Clear();
        policy["mode"] = "changed";

        Assert.Contains("cook", descriptor.RequiredCapabilities);
        Assert.Equal("cooperative", descriptor.Policy["mode"]);
        Assert.False(descriptor.RequiredCapabilities is HashSet<string>);
        Assert.False(descriptor.Policy is Dictionary<string, string>);
    }

    [Fact]
    public void L08_diagnostics_jsonl_round_trip_and_d1_report_remains_non_decisional()
    {
        using var evidence = CreateEvidence("L08");
        using var authority = CreateAuthority(CreateSimulation(), queueCapacity: 2);
        Bind(authority, HostConnection, PlayerOne, CookingConnectionOrigin.HostLocal);
        authority.EnqueueCommand(HostConnection, Command(PlayerOne, "diagnostic"), "l08-command");
        authority.ExecuteNextBatch();

        WriteDiagnostics(evidence.Path, authority);
        var diagnostics = CookingSessionDiagnosticWriter.ReadAll(evidence.Path);
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(Session.Value, diagnostic.SessionId);
            Assert.Equal(1, diagnostic.Epoch);
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.CorrelationId));
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.TimestampUtc));
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.State));
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.Direction));
        });
        Assert.Contains(diagnostics, diagnostic => diagnostic.EventType == "command-executed" &&
            !string.IsNullOrWhiteSpace(diagnostic.BeforeStateHash) && !string.IsNullOrWhiteSpace(diagnostic.AfterStateHash));

        var report = new CookingTransportSpikeReport("d1-template", new CookingTransportSpikeCandidate(
            "candidate-not-selected", "0.0", "unknown", "win-x64", new Dictionary<string, string>()),
            new[] { new CookingTransportSpikeObservation("framing", "not-run", "template only") },
            DateTimeOffset.UtcNow.ToString("O"));
        Assert.False(report.IsProductionDecision);
        Assert.Throws<ArgumentException>(() => new CookingTransportSpikeReport("d1-template", report.Candidate,
            report.Observations, report.GeneratedAtUtc, isProductionDecision: true));
    }

    private static CookingSessionAuthority CreateAuthority(CookingSimulation simulation, int queueCapacity) =>
        new(simulation, new CookingSessionDescriptor(new CookingScope(Session, World, Match), 1,
            new CookingProtocolIdentity("cooking-session", 1, 2), "config-sha256-a",
            new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>()), queueCapacity);

    private static void Bind(CookingSessionAuthority authority, ConnectionId connection, PlayerId player, CookingConnectionOrigin origin)
    {
        Assert.True(authority.OpenConnection(connection, origin, $"open-{connection.Value}"));
        var result = authority.CompleteHandshake(connection, player, Handshake(), $"handshake-{connection.Value}");
        Assert.True(result.Accepted);
        Assert.Equal(player, result.Binding?.Player);
    }

    private static CookingHandshake Handshake(int protocolVersion = 1) => new(
        new CookingScope(Session, World, Match), "cooking-session", protocolVersion, "config-sha256-a",
        new HashSet<string>(StringComparer.Ordinal) { "cook" });

    private static CookingCommand Command(PlayerId player, string commandId, CookingScope? scope = null) =>
        new(scope ?? new CookingScope(Session, World, Match), 10, player, new CommandId(commandId),
            CookingOperation.Pickup, Item, 1);

    private static CookingSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [PlayerOne] = new(PlayerOne, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
            [PlayerTwo] = new(PlayerTwo, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
        };
        var stations = new Dictionary<StationSlotId, CookingStationConfig> { [Station] = new(Station, 2) };
        var definitions = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Definition] = new(Definition, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
        };
        var simulation = new CookingSimulation(new CookingFixture(scope, players, stations, definitions));
        simulation.AddItem(Item, Definition, ItemLocation.World("counter-a"));
        return simulation;
    }

    private static void WriteDiagnostics(string path, CookingSessionAuthority authority)
    {
        foreach (var diagnostic in authority.Diagnostics)
            CookingSessionDiagnosticWriter.Append(path, diagnostic);
    }

    private static void AssertDiagnostics(string path, params string[] eventTypes)
    {
        var diagnostics = CookingSessionDiagnosticWriter.ReadAll(path);
        Assert.All(eventTypes, eventType => Assert.Contains(diagnostics, diagnostic => diagnostic.EventType == eventType));
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_SESSION_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts
                ? System.IO.Path.GetFullPath(requestedRoot!)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "session");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "session-diagnostics.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);
}
