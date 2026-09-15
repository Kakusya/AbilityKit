using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingMatchLifecycle")]
public sealed class CookingMatchLifecycleTests
{
    private static readonly SessionId Session = new("lifecycle-session");
    private static readonly WorldId World = new("lifecycle-world");
    private static readonly MatchId Match = new("match-a");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Station = new("stove-a");
    private static readonly ContainerId Container = new("plate-a");
    private static readonly DefinitionId Raw = new("raw-a");
    private static readonly DefinitionId Product = new("product-a");
    private static readonly RecipeId Recipe = new("recipe-a");

    [Fact]
    public void M01_valid_fixture_layout_prepares_ready_with_configuration_identity()
    {
        using var evidence = CreateEvidence("M01");
        var lifecycle = CreateLifecycle();

        var prepared = Record(lifecycle, evidence, "M01", "prepare", () => lifecycle.Prepare(Preparation(CurrentIdentity(lifecycle))),
            "valid fixture-only logical references produce a Ready lifecycle context");

        AssertAccepted(prepared, CookingMatchState.Ready);
        Assert.Equal(CurrentIdentity(lifecycle).ToString(), lifecycle.Snapshot().ConfigIdentity);
        Assert.Equal(new LevelId("fixture-level"), lifecycle.Snapshot().Level);
        Assert.Equal(new MapId("fixture-map"), lifecycle.Snapshot().Map);
        Assert.Equal(new LayoutId("fixture-layout"), lifecycle.Snapshot().Layout);
        Assert.False(lifecycle.TryGetGameplay(out _));
        AssertEvidence(evidence.Path, "M01", 1);
    }

    [Fact]
    public void M02_invalid_layout_and_config_mismatch_do_not_prepare_or_create_gameplay()
    {
        using var evidence = CreateEvidence("M02");
        var duplicate = CreateLifecycle();
        var invalidLayout = Preparation(CurrentIdentity(duplicate)) with
        {
            Layout = new CookingLogicalLayout(new LayoutId("fixture-layout"), new[] { Station, Station }, new[] { Container }),
        };
        var duplicateRejected = Record(duplicate, evidence, "M02", "duplicate-layout",
            () => duplicate.Prepare(invalidLayout), "duplicate logical appliance reference leaves lifecycle Preparing");
        AssertRejected(duplicateRejected, CookingMatchLifecycleReason.DuplicateLayoutReference);
        Assert.Equal(CookingMatchState.Preparing, duplicate.State);
        Assert.False(duplicate.TryGetGameplay(out _));

        var missing = CreateLifecycle();
        var missingLayout = Preparation(CurrentIdentity(missing)) with
        {
            Layout = new CookingLogicalLayout(new LayoutId("fixture-layout"), new[] { new StationSlotId("missing-stove") }, new[] { Container }),
        };
        AssertRejected(Record(missing, evidence, "M02", "missing-appliance", () => missing.Prepare(missingLayout),
            "missing configuration appliance does not create gameplay"), CookingMatchLifecycleReason.ApplianceNotFound);

        var mismatch = CreateLifecycle();
        var wrongIdentity = CurrentIdentity(mismatch) with { Sha256 = new string('0', 64) };
        AssertRejected(Record(mismatch, evidence, "M02", "config-mismatch", () => mismatch.Prepare(Preparation(wrongIdentity)),
            "configuration identity mismatch does not create gameplay"), CookingMatchLifecycleReason.ConfigIdentityMismatch);

        var countingFactory = new CountingFactory();
        var malformed = new CookingMatchLifecycle(new CookingScope(Session, World, new MatchId("malformed-match")), 1,
            CreateConfiguration(), countingFactory);
        var malformedLayout = new CookingMatchPreparation(new LevelId("fixture-level"), new MapId("fixture-map"),
            new CookingLogicalLayout(new LayoutId("fixture-layout"), null!, null!), CurrentIdentity(malformed));
        AssertRejected(Record(malformed, evidence, "M02", "malformed-layout", () => malformed.Prepare(malformedLayout),
            "malformed layout is structurally rejected before gameplay factory invocation"), CookingMatchLifecycleReason.LayoutMalformed);
        Assert.Equal(0, countingFactory.CreateCount);
        AssertEvidence(evidence.Path, "M02", 4);
    }

    [Fact]
    public void M03_M04_lifecycle_transitions_restart_into_new_match_and_illegal_operations_do_not_mutate()
    {
        using var evidence = CreateEvidence("M03-M04");
        var lifecycle = CreateLifecycle();
        var before = lifecycle.Snapshot();
        var illegalStart = Record(lifecycle, evidence, "M03-M04", "start-before-ready", lifecycle.Start,
            "start before Ready is mutation-safe");
        AssertRejected(illegalStart, CookingMatchLifecycleReason.InvalidState);
        Assert.Equal(before, lifecycle.Snapshot());

        AssertAccepted(Record(lifecycle, evidence, "M03-M04", "prepare", () => lifecycle.Prepare(Preparation(CurrentIdentity(lifecycle))),
            "preparation reaches Ready"), CookingMatchState.Ready);
        AssertAccepted(Record(lifecycle, evidence, "M03-M04", "start", lifecycle.Start,
            "Ready creates one gameplay instance and opens admission"), CookingMatchState.Started);
        Assert.True(lifecycle.TryGetGameplay(out var closedGameplay));
        var repeatedStartBefore = lifecycle.Snapshot();
        AssertRejected(Record(lifecycle, evidence, "M03-M04", "repeat-start", lifecycle.Start,
            "repeated start preserves Started lifecycle snapshot"), CookingMatchLifecycleReason.InvalidState);
        Assert.Equal(repeatedStartBefore, lifecycle.Snapshot());
        AssertAccepted(Record(lifecycle, evidence, "M03-M04", "end", lifecycle.End,
            "Started match closes gameplay admission on Ended"), CookingMatchState.Ended);
        Assert.False(lifecycle.TryGetGameplay(out _));
        var closedSnapshot = closedGameplay.Snapshot();
        var closedCommand = closedGameplay.Submit(RecipeCommand(lifecycle.Scope, "closed-command", CookingRecipeOperation.AdvanceTicks,
            process: new ProcessId("missing-process"), ticks: 1));
        Assert.Equal(CookingRecipeOutcome.Rejected, closedCommand.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed, closedCommand.Reason);
        Assert.Equal(closedSnapshot.CanonicalText(), closedGameplay.Snapshot().CanonicalText());

        var oldScope = lifecycle.Scope;
        var restart = lifecycle.Restart(new CookingScope(Session, World, new MatchId("match-b")), 2);
        Assert.True(restart.Accepted);
        var next = Assert.IsType<CookingMatchLifecycle>(restart.NewMatch);
        Assert.NotEqual(oldScope.Match, next.Scope.Match);
        Assert.Equal(2, next.Epoch);
        Assert.Equal(CookingMatchState.Preparing, next.State);
        Assert.False(lifecycle.IsGameplayAdmissionOpen);
        AssertEvidence(evidence.Path, "M03-M04", 5);
    }

    [Fact]
    public void M05_parallel_matches_keep_recipe_ticks_products_orders_and_snapshots_isolated()
    {
        var first = CreateLifecycle(new MatchId("match-a"));
        var second = CreateLifecycle(new MatchId("match-b"));
        AssertAccepted(first.Prepare(Preparation(CurrentIdentity(first))), CookingMatchState.Ready);
        AssertAccepted(second.Prepare(Preparation(CurrentIdentity(second))), CookingMatchState.Ready);
        AssertAccepted(first.Start(), CookingMatchState.Started);
        AssertAccepted(second.Start(), CookingMatchState.Started);
        Assert.True(first.TryGetGameplay(out var firstGameplay));
        Assert.True(second.TryGetGameplay(out var secondGameplay));

        var firstIngredient = new ItemId("first-ingredient");
        firstGameplay.AddIngredient(firstIngredient, Raw, Player);
        AssertAccepted(firstGameplay.Submit(RecipeCommand(first.Scope, "first-start", CookingRecipeOperation.StartProcess, recipe: Recipe,
            item: firstIngredient, station: Station, expectedVersion: 1)));
        var firstProcess = Assert.Single(firstGameplay.Snapshot().Processes);
        AssertAccepted(firstGameplay.Submit(RecipeCommand(first.Scope, "first-tick", CookingRecipeOperation.AdvanceTicks,
            process: firstProcess.Id, ticks: 2)));

        Assert.Equal(2, firstGameplay.LogicalTick);
        Assert.Equal(0, secondGameplay.LogicalTick);
        Assert.Single(firstGameplay.Snapshot().Processes);
        Assert.Empty(secondGameplay.Snapshot().Processes);
        Assert.NotEqual(firstGameplay.Snapshot().Sha256(), secondGameplay.Snapshot().Sha256());
    }

    [Fact]
    public void M06_M07_old_scope_epoch_and_out_of_order_lifecycle_snapshots_do_not_replace_watermark()
    {
        var lifecycle = CreateLifecycle();
        AssertAccepted(lifecycle.Prepare(Preparation(CurrentIdentity(lifecycle))), CookingMatchState.Ready);
        var applier = new CookingLifecycleSnapshotApplier(lifecycle.Scope, lifecycle.Epoch, CurrentIdentity(lifecycle));
        var ready = lifecycle.Snapshot();
        Assert.True(applier.Apply(ready).Accepted);
        AssertAccepted(lifecycle.Start(), CookingMatchState.Started);
        var started = lifecycle.Snapshot();
        Assert.True(applier.Apply(started).Accepted);

        var duplicate = applier.Apply(started);
        Assert.False(duplicate.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.SnapshotSequenceDuplicate, duplicate.Reason);
        var stale = applier.Apply(ready);
        Assert.False(stale.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.SnapshotSequenceStale, stale.Reason);
        var gapped = applier.Apply(started with { Version = started.Version + 2 });
        Assert.False(gapped.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.SnapshotSequenceGap, gapped.Reason);
        var illegalState = applier.Apply(started with { Version = started.Version + 1, State = CookingMatchState.Ready });
        Assert.False(illegalState.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.SnapshotSequenceGap, illegalState.Reason);
        var foreign = applier.Apply(started with { Scope = new CookingScope(Session, World, new MatchId("old-match")) });
        Assert.False(foreign.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.ScopeMismatch, foreign.Reason);
        var oldEpoch = applier.Apply(started with { Epoch = lifecycle.Epoch - 1 });
        Assert.False(oldEpoch.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.EpochMismatch, oldEpoch.Reason);
        Assert.Equal(started, applier.Current);
    }

    [Fact]
    public void owner_decisions_remain_explicitly_blocked_without_default_exit_behavior()
    {
        var lifecycle = CreateLifecycle();
        var before = lifecycle.Snapshot();

        var result = lifecycle.RequireOwnerDecision(CookingOwnerDecision.HostExit);

        AssertRejected(result, CookingMatchLifecycleReason.BlockedByOwnerDecision);
        Assert.Equal(before, lifecycle.Snapshot());
    }

    private static CookingMatchLifecycle CreateLifecycle(MatchId? match = null) =>
        new(new CookingScope(Session, World, match ?? Match), 1, CreateConfiguration(), new RecipeGameplayFactory());

    private static CookingConfigurationIdentity CurrentIdentity(CookingMatchLifecycle lifecycle) =>
        new CookingConfigurationRegistry().Submit(Candidate()).AfterIdentity!;

    private static CookingMatchPreparation Preparation(CookingConfigurationIdentity identity) =>
        new(new LevelId("fixture-level"), new MapId("fixture-map"),
            new CookingLogicalLayout(new LayoutId("fixture-layout"), new[] { Station }, new[] { Container }), identity);

    private static CookingConfigurationSnapshot CreateConfiguration()
    {
        var registry = new CookingConfigurationRegistry();
        var result = registry.Submit(Candidate());
        Assert.True(result.Accepted);
        return Assert.IsType<CookingConfigurationSnapshot>(registry.Current);
    }

    private static CookingConfigurationCandidate Candidate() => new(
        new[] { "heat" },
        new[]
        {
            new CookingItemDefinition(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            new CookingItemDefinition(Product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
        },
        new[] { new CookingApplianceDefinition(Station, new HashSet<string>(StringComparer.Ordinal) { "heat" }) },
        new[] { new CookingRecipeDefinition(Recipe, Raw, Product, new ProcessId("process-a"), "heat", 3) },
        new[] { new CookingContainerDefinition(Container, 2) });

    private static CookingRecipeCommand RecipeCommand(CookingScope scope, string id, CookingRecipeOperation operation,
        RecipeId? recipe = null, ProcessId? process = null, ItemId? item = null, StationSlotId? station = null,
        int expectedVersion = 0, int ticks = 0) =>
        new(scope, 1, Player, new RecipeCommandId(id), operation, recipe, process, item, station, Container, null, expectedVersion, ticks);

    private static CookingMatchLifecycleResult Record(CookingMatchLifecycle lifecycle, EvidenceScope evidence, string testId,
        string operation, Func<CookingMatchLifecycleResult> action, string summary)
    {
        var before = lifecycle.Snapshot();
        var result = action();
        CookingMatchLifecycleAcceptanceEvidenceWriter.Append(evidence.Path, new CookingMatchLifecycleAcceptanceEvidence(
            testId, $"{lifecycle.Scope.Session}/{lifecycle.Scope.World}/{lifecycle.Scope.Match}", lifecycle.Epoch, operation,
            result.Accepted, result.Reason.ToString(), Serialize(before), Serialize(lifecycle.Snapshot()), summary,
            "dotnet test AbilityKit.Game.Cooking.Tests", DateTimeOffset.UtcNow.ToString("O")));
        if (!result.Accepted)
            Assert.Equal(before, lifecycle.Snapshot());
        return result;
    }

    private static string Serialize(CookingMatchLifecycleSnapshot snapshot) =>
        $"{snapshot.Scope.Session}/{snapshot.Scope.World}/{snapshot.Scope.Match}:{snapshot.Epoch}:{snapshot.State}:{snapshot.Version}";

    private static void AssertAccepted(CookingMatchLifecycleResult result, CookingMatchState state)
    {
        Assert.True(result.Accepted);
        Assert.Equal(CookingMatchLifecycleReason.None, result.Reason);
        Assert.Equal(state, result.State);
        Assert.Single(result.Events);
    }

    private static void AssertAccepted(CookingRecipeCommandResult result)
    {
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        Assert.Single(result.Events);
    }

    private static void AssertRejected(CookingMatchLifecycleResult result, CookingMatchLifecycleReason reason)
    {
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Reason);
        Assert.Empty(result.Events);
    }

    private static void AssertEvidence(string path, string testId, int count)
    {
        var records = CookingMatchLifecycleAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(count, records.Count);
        Assert.All(records, record => Assert.Equal(testId, record.TestId));
    }

    private sealed class CountingFactory : ICookingMatchGameplayFactory
    {
        public int CreateCount { get; private set; }

        public CookingRecipeSimulation Create(CookingScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++;
            throw new InvalidOperationException("Invalid preparation must not create gameplay.");
        }
    }

    private sealed class RecipeGameplayFactory : ICookingMatchGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingScope scope, CookingConfigurationSnapshot configuration)
        {
            var players = new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                    new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
            };
            return new CookingRecipeSimulation(new CookingRecipeFixture(scope, players, configuration.Items,
                configuration.Appliances, configuration.Recipes, configuration.Containers), new AcceptingOrderPort());
        }
    }

    private sealed class AcceptingOrderPort : ICookingOrderPort
    {
        public CookingOrderAcceptance Submit(CookingOrderSubmission submission) => new(true, "fixture-accepted");
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_MATCH_LIFECYCLE_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "match-lifecycle");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "match-lifecycle.jsonl");
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
