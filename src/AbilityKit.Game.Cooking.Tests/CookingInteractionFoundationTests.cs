using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingInteractionFoundation")]
public sealed class CookingInteractionFoundationTests
{
    private const string FixtureId = "p0-two-player-one-item";

    [Fact]
    public void T01_pickup_drop_and_repickup_preserve_unique_location()
    {
        using var evidence = CreateEvidence("T01");
        var simulation = CreateSimulation();

        var pickup = Submit(simulation, evidence, "T01", Command(CookingOperation.Pickup, "pickup-1", expectedVersion: 1),
            "pickup moves the only item from world to player-one hand");
        AssertAccepted(pickup);
        Assert.Equal(ItemLocation.Hand(PlayerOne), Item(simulation).Location);
        Assert.Equal(ItemOne, simulation.ItemInHand(PlayerOne));
        Assert.Equal(1, simulation.OccupancyCount(ItemOne));
        Assert.Null(simulation.ItemAtWorldPosition("counter-a"));

        var drop = Submit(simulation, evidence, "T01", Command(CookingOperation.Drop, "drop-1", expectedVersion: 2, station: StationOne),
            "drop moves the item from player-one hand to station-one");
        AssertAccepted(drop);
        Assert.Equal(ItemLocation.Station(StationOne), Item(simulation).Location);
        Assert.Equal(new[] { ItemOne }, simulation.ItemsAtStation(StationOne));
        Assert.Equal(1, simulation.OccupancyCount(ItemOne));
        Assert.Null(simulation.ItemInHand(PlayerOne));

        var repickup = Submit(simulation, evidence, "T01", Command(CookingOperation.Pickup, "pickup-2", expectedVersion: 3),
            "repickup restores the item to player-one hand exactly once");
        AssertAccepted(repickup);
        Assert.Equal(ItemLocation.Hand(PlayerOne), Item(simulation).Location);
        Assert.Equal(ItemOne, simulation.ItemInHand(PlayerOne));
        Assert.Equal(1, simulation.OccupancyCount(ItemOne));
        Assert.Empty(simulation.ItemsAtStation(StationOne));
        Assert.Equal(3, simulation.EventHistory.Count);
        Assert.Equal(3, simulation.SubmittedEnvelopeCount);
        AssertEvidence(evidence.Path, "T01", 3);
    }

    [Fact]
    public void T02_lifecycle_and_scope_rejections_do_not_mutate_authority()
    {
        using var evidence = CreateEvidence("T02");
        var simulation = CreateSimulation();

        var crossScope = Submit(simulation, evidence, "T02", Command(CookingOperation.Pickup, "cross-scope", expectedVersion: 1,
                scope: new CookingScope(new SessionId("other-session"), World, Match)),
            "cross-session command is rejected without mutation");
        AssertRejected(crossScope, RejectionReason.ScopeMismatch);

        var stale = Submit(simulation, evidence, "T02", Command(CookingOperation.Pickup, "stale", expectedVersion: 99),
            "stale item version is rejected without mutation");
        AssertRejected(stale, RejectionReason.ItemStale);

        simulation.RemoveItem(ItemOne);
        var removed = Submit(simulation, evidence, "T02", Command(CookingOperation.Pickup, "removed", expectedVersion: 2),
            "removed item is rejected without mutation");
        AssertRejected(removed, RejectionReason.ItemNotFound);
        Assert.Empty(simulation.EventHistory);
        AssertEvidence(evidence.Path, "T02", 3);
    }

    [Fact]
    public void T03_validation_rejections_have_stable_reasons_and_no_mutation()
    {
        using var evidence = CreateEvidence("T03");
        var noCapability = CreateSimulation(playerOneCapabilities: new HashSet<string>());
        var ineligible = Submit(noCapability, evidence, "T03", Command(CookingOperation.Pickup, "ineligible", expectedVersion: 1),
            "missing capability is rejected");
        AssertRejected(ineligible, RejectionReason.PlayerIneligible);

        var outOfRange = CreateSimulation(playerOneReachableStations: new HashSet<string>());
        AssertAccepted(outOfRange.Submit(Command(CookingOperation.Pickup, "setup-pickup", expectedVersion: 1)));
        var range = Submit(outOfRange, evidence, "T03", Command(CookingOperation.Drop, "out-of-range", expectedVersion: 2, station: StationOne),
            "unreachable station is rejected");
        AssertRejected(range, RejectionReason.TargetOutOfRange);

        var wrongLocation = CreateSimulation();
        var location = Submit(wrongLocation, evidence, "T03", Command(CookingOperation.Drop, "wrong-location", expectedVersion: 1, station: StationOne),
            "drop from a non-hand location is rejected");
        AssertRejected(location, RejectionReason.CurrentLocationMismatch);

        var unavailable = CreateSimulation(stationAvailable: false);
        AssertAccepted(unavailable.Submit(Command(CookingOperation.Pickup, "setup-pickup", expectedVersion: 1)));
        var target = Submit(unavailable, evidence, "T03", Command(CookingOperation.Drop, "unavailable", expectedVersion: 2, station: StationOne),
            "unavailable station is rejected");
        AssertRejected(target, RejectionReason.TargetUnavailable);

        var unknownPlayer = Submit(CreateSimulation(), evidence, "T03", Command(CookingOperation.Pickup, "unknown-player", expectedVersion: 1,
            player: new PlayerId("unknown-player")), "unknown player is rejected");
        AssertRejected(unknownPlayer, RejectionReason.PlayerNotFound);

        var unavailablePlayer = CreateSimulation(playerOneAvailable: false);
        var inactive = Submit(unavailablePlayer, evidence, "T03", Command(CookingOperation.Pickup, "unavailable-player", expectedVersion: 1),
            "configured unavailable player is rejected");
        AssertRejected(inactive, RejectionReason.PlayerUnavailable);
        AssertEvidence(evidence.Path, "T03", 6);
    }

    [Fact]
    public void T04_full_station_rejects_drop_without_losing_hand_item()
    {
        using var evidence = CreateEvidence("T04");
        var simulation = CreateSimulation(stationCapacity: 1);
        simulation.AddItem(new ItemId("item-on-station"), Definition, ItemLocation.Station(StationOne));
        AssertAccepted(simulation.Submit(Command(CookingOperation.Pickup, "setup-pickup", expectedVersion: 1)));

        var result = Submit(simulation, evidence, "T04", Command(CookingOperation.Drop, "full-slot", expectedVersion: 2, station: StationOne),
            "full station rejects without moving hand item");
        AssertRejected(result, RejectionReason.CapacityFull);
        Assert.Equal(ItemLocation.Hand(PlayerOne), Item(simulation).Location);
        Assert.Equal(ItemOne, simulation.ItemInHand(PlayerOne));
        Assert.Equal(new[] { new ItemId("item-on-station") }, simulation.ItemsAtStation(StationOne));
        Assert.Equal(1, simulation.OccupancyCount(ItemOne));
        AssertEvidence(evidence.Path, "T04", 1);
    }

    [Fact]
    public void T05_host_and_remote_adapters_share_the_same_authority_path()
    {
        using var evidence = CreateEvidence("T05");
        var localSimulation = CreateSimulation();
        var remoteSimulation = CreateSimulation();
        ICookingCommandAdapter local = new HostLocalAdapter(localSimulation);
        ICookingCommandAdapter remote = new RemoteInProcessAdapter(remoteSimulation);
        var command = Command(CookingOperation.Pickup, "same-envelope", expectedVersion: 1);

        var localBefore = localSimulation.Snapshot();
        var localResult = local.Submit(command);
        Write(evidence, "T05", localSimulation, command, localResult, localBefore, "host-local adapter result");
        var remoteBefore = remoteSimulation.Snapshot();
        var remoteResult = remote.Submit(command);
        Write(evidence, "T05", remoteSimulation, command, remoteResult, remoteBefore, "remote in-process adapter result");

        AssertAccepted(localResult);
        Assert.Equal(localResult.Outcome, remoteResult.Outcome);
        Assert.Equal(localResult.Reason, remoteResult.Reason);
        Assert.Equal(localSimulation.Snapshot().CanonicalText(), remoteSimulation.Snapshot().CanonicalText());
        Assert.Equal(localSimulation.EventHistory, remoteSimulation.EventHistory);
        Assert.Equal(1, localSimulation.SubmittedEnvelopeCount);
        Assert.Equal(1, remoteSimulation.SubmittedEnvelopeCount);
        AssertEvidence(evidence.Path, "T05", 2);
    }

    [Fact]
    public void T06_contention_selects_the_same_stable_winner_on_every_run()
    {
        using var evidence = CreateEvidence("T06");
        var first = CreateSimulation();
        var second = CreateSimulation();
        var commands = new[]
        {
            Command(CookingOperation.Pickup, "command-z", expectedVersion: 1, player: PlayerTwo),
            Command(CookingOperation.Pickup, "command-a", expectedVersion: 1, player: PlayerOne),
        };
        var firstExecutions = first.ExecuteBatch(commands);
        var firstResults = firstExecutions.Select(execution => execution.Result).ToArray();
        var secondResults = second.SubmitBatch(commands.Reverse());
        foreach (var execution in firstExecutions)
            Write(evidence, "T06", first, execution.Command, execution.Result, execution.Before,
                "contention result after deterministic batch ordering", execution.After);

        Assert.Single(firstResults, result => result.Outcome == CommandOutcome.Accepted);
        Assert.Single(secondResults, result => result.Outcome == CommandOutcome.Accepted);
        Assert.Equal(PlayerOne, first.EventHistory.Single().Player);
        Assert.Equal(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.Equal(first.EventHistory, second.EventHistory);
        AssertEvidence(evidence.Path, "T06", 2);
    }

    [Fact]
    public void T07_duplicate_command_is_idempotent()
    {
        using var evidence = CreateEvidence("T07");
        var simulation = CreateSimulation();
        var command = Command(CookingOperation.Pickup, "replay-me", expectedVersion: 1);

        var first = Submit(simulation, evidence, "T07", command, "first command commit");
        var duplicate = Submit(simulation, evidence, "T07", command, "duplicate command returns cached result without a new event");

        AssertAccepted(first);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(CommandOutcome.Accepted, duplicate.Outcome);
        Assert.Empty(duplicate.Events);
        Assert.Single(simulation.EventHistory);
        Assert.Equal(2, simulation.SubmittedEnvelopeCount);
        AssertEvidence(evidence.Path, "T07", 2);
    }

    [Fact]
    public void T08_reordered_ingress_has_identical_snapshot_and_event_sequence()
    {
        using var evidence = CreateEvidence("T08");
        var first = CreateSimulation();
        var second = CreateSimulation();
        var sameClosedBatch = new[]
        {
            Command(CookingOperation.Pickup, "command-z", expectedVersion: 1, player: PlayerTwo),
            Command(CookingOperation.Pickup, "command-a", expectedVersion: 1, player: PlayerOne),
        };
        var firstExecutions = first.ExecuteBatch(sameClosedBatch);
        var firstResults = firstExecutions.Select(execution => execution.Result).ToArray();
        var secondResults = second.SubmitBatch(sameClosedBatch.Reverse());
        foreach (var execution in firstExecutions)
            Write(evidence, "T08", first, execution.Command, execution.Result, execution.Before,
                "same closed batch executed after canonical ordering", execution.After);

        Assert.Equal(firstResults.Select(result => result.Outcome), secondResults.Select(result => result.Outcome));
        Assert.Equal(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.Equal(first.EventHistory, second.EventHistory);
        AssertEvidence(evidence.Path, "T08", 2);
    }

    [Fact]
    public void Invalid_initial_locations_are_rejected_and_canonical_snapshots_escape_identifier_delimiters()
    {
        var simulation = CreateSimulation();
        Assert.Throws<InvalidOperationException>(() => simulation.AddItem(new ItemId("duplicate-world"), Definition, ItemLocation.World("counter-a")));
        Assert.Throws<ArgumentException>(() => simulation.AddItem(new ItemId("unknown-hand"), Definition, ItemLocation.Hand(new PlayerId("missing-player"))));
        Assert.Throws<ArgumentException>(() => simulation.AddItem(new ItemId("unknown-station"), Definition, ItemLocation.Station(new StationSlotId("missing-station"))));

        var first = CreateSimulation(itemId: new ItemId("item|a"), worldPosition: "counter:one");
        var second = CreateSimulation(itemId: new ItemId("item"), worldPosition: "a:counter:one");
        Assert.NotEqual(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.NotEqual(first.Snapshot().Sha256(), second.Snapshot().Sha256());
    }

    [Fact]
    public void Command_identity_conflict_and_mixed_batches_are_rejected_without_mutation()
    {
        var simulation = CreateSimulation();
        var accepted = simulation.Submit(Command(CookingOperation.Pickup, "same-command", expectedVersion: 1));
        AssertAccepted(accepted);
        var beforeConflict = simulation.Snapshot();

        var conflict = simulation.Submit(Command(CookingOperation.Drop, "same-command", expectedVersion: 2, station: StationOne));
        AssertRejected(conflict, RejectionReason.CommandIdentityConflict);
        Assert.Equal(beforeConflict.CanonicalText(), simulation.Snapshot().CanonicalText());

        Assert.Throws<ArgumentException>(() => simulation.ExecuteBatch(new[]
        {
            Command(CookingOperation.Pickup, "batch-a", expectedVersion: 2),
            new CookingCommand(new CookingScope(Session, World, Match), 11, PlayerOne, new CommandId("batch-b"), CookingOperation.Pickup, ItemOne, 2),
        }));
    }


    private static readonly SessionId Session = new("session-a");
    private static readonly WorldId World = new("world-a");
    private static readonly MatchId Match = new("match-a");
    private static readonly PlayerId PlayerOne = new("player-a");
    private static readonly PlayerId PlayerTwo = new("player-b");
    private static readonly ItemId ItemOne = new("item-a");
    private static readonly DefinitionId Definition = new("ingredient-a");
    private static readonly StationSlotId StationOne = new("station-a");

    private static CookingSimulation CreateSimulation(
        IReadOnlySet<string>? playerOneCapabilities = null,
        IReadOnlySet<string>? playerOneReachableStations = null,
        int stationCapacity = 2,
        bool stationAvailable = true,
        bool playerOneAvailable = true,
        ItemId? itemId = null,
        string worldPosition = "counter-a")
    {
        var scope = new CookingScope(Session, World, Match);
        var capabilities = playerOneCapabilities ?? new HashSet<string>(StringComparer.Ordinal) { "cook" };
        var reachable = playerOneReachableStations ?? new HashSet<string>(StringComparer.Ordinal) { StationOne.Value };
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [PlayerOne] = new(PlayerOne, capabilities, reachable, playerOneAvailable),
            [PlayerTwo] = new(PlayerTwo, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { StationOne.Value }),
        };
        var stations = new Dictionary<StationSlotId, CookingStationConfig>
        {
            [StationOne] = new(StationOne, stationCapacity, stationAvailable),
        };
        var definitions = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Definition] = new(Definition, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
        };
        var simulation = new CookingSimulation(new CookingFixture(scope, players, stations, definitions));
        simulation.AddItem(itemId ?? ItemOne, Definition, ItemLocation.World(worldPosition));
        return simulation;
    }

    private static CookingCommand Command(
        CookingOperation operation,
        string commandId,
        int expectedVersion,
        StationSlotId? station = null,
        PlayerId? player = null,
        CookingScope? scope = null) =>
        new(scope ?? new CookingScope(Session, World, Match), 10, player ?? PlayerOne, new CommandId(commandId),
            operation, ItemOne, expectedVersion, station);

    private static CookingSnapshotItem Item(CookingSimulation simulation) => simulation.Snapshot().Items.Single(item => item.Id == ItemOne);

    private static CommandResult Submit(CookingSimulation simulation, EvidenceScope evidence, string testId,
        CookingCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        Write(evidence, testId, simulation, command, result, before, assertionSummary);
        if (result.Outcome == CommandOutcome.Rejected)
            Assert.Equal(before.CanonicalText(), simulation.Snapshot().CanonicalText());
        return result;
    }

    private static void AssertAccepted(CommandResult result)
    {
        Assert.Equal(CommandOutcome.Accepted, result.Outcome);
        Assert.Equal(RejectionReason.None, result.Reason);
        Assert.False(result.IsDuplicate);
        Assert.Single(result.Events);
    }

    private static void AssertRejected(CommandResult result, RejectionReason reason)
    {
        Assert.Equal(CommandOutcome.Rejected, result.Outcome);
        Assert.Equal(reason, result.Reason);
        Assert.Empty(result.Events);
    }

    private static void Write(EvidenceScope evidence, string testId, CookingSimulation simulation, CookingCommand command,
        CommandResult result, CookingSnapshot before, string assertionSummary, CookingSnapshot? after = null)
    {
        after ??= simulation.Snapshot();
        CookingAcceptanceEvidenceWriter.Append(evidence.Path, new CookingAcceptanceEvidence(
            testId,
            FixtureId,
            after.Scope.Session.Value,
            after.Scope.World.Value,
            after.Scope.Match.Value,
            command.SimulationBatch,
            $"{command.SimulationBatch}/{command.Player.Value}/{command.Command.Value}",
            command,
            result.Outcome.ToString(),
            result.Reason.ToString(),
            result.IsDuplicate,
            result.Events,
            before.Sha256(),
            after.Sha256(),
            assertionSummary,
            "dotnet test AbilityKit.Game.Cooking.Tests",
            DateTimeOffset.UtcNow.ToString("O")));
    }

    private static void AssertEvidence(string path, string testId, int expectedRecords)
    {
        var records = CookingAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(expectedRecords, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(testId, record.TestId);
            Assert.Equal("dotnet test AbilityKit.Game.Cooking.Tests", record.Runner);
            Assert.False(string.IsNullOrWhiteSpace(record.BeforeStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AfterStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AssertionSummary));
            Assert.False(string.IsNullOrWhiteSpace(record.SortKey));
            Assert.False(string.IsNullOrWhiteSpace(record.Command.Command.Value));
            Assert.False(string.IsNullOrWhiteSpace(record.Command.Item.Value));
            Assert.NotNull(record.Events);
        });
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var evidenceRoot = _keepArtifacts
                ? System.IO.Path.GetFullPath(requestedRoot!)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests");
            _directory = System.IO.Path.Combine(evidenceRoot, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "acceptance.jsonl");
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
