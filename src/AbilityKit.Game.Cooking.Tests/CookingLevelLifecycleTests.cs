using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingLevelLifecycleTests
{
    private static readonly SessionId Session = new("level-session");
    private static readonly WorldId World = new("level-world");
    private static readonly MatchId Match = new("level-match");
    private static readonly RestaurantRuntimeId Runtime = new(7);
    private static readonly LevelId Level = new("level-a");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Station = new("stove-a");
    private static readonly DefinitionId Container = new("plate-a");
    private static readonly DefinitionId Raw = new("raw-a");
    private static readonly DefinitionId Product = new("product-a");
    private static readonly RecipeId Recipe = new("recipe-a");

    [Fact]
    public void scope_requires_explicit_parent_runtime_level_and_epoch_identity()
    {
        Assert.Throws<ArgumentNullException>(() => new CookingLevelScope(null!, Runtime, Level, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CookingLevelScope(MatchScope(), new RestaurantRuntimeId(0), Level, 1));
        Assert.Throws<ArgumentException>(() => new CookingLevelScope(MatchScope(), Runtime, new LevelId(" "), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CookingLevelScope(MatchScope(), Runtime, Level, 0));

        var scope = LevelScope();
        Assert.Equal(MatchScope(), scope.MatchScope);
        Assert.Equal(Runtime, scope.RestaurantRuntime);
        Assert.Equal(Level, scope.Level);
        Assert.Equal(1, scope.LevelEpoch);
    }

    [Fact]
    public void canonical_transition_table_locks_outcome_and_rejects_illegal_transitions_without_mutation()
    {
        var lifecycle = CreateLifecycle();
        AssertMutationFree(lifecycle, lifecycle.Start, CookingLevelLifecycleReason.InvalidState);
        AssertMutationFree(lifecycle, () => lifecycle.BeginEnd(CookingLevelOutcome.Success), CookingLevelLifecycleReason.InvalidState);

        AssertAccepted(lifecycle.BeginPreparation(Preparation()), CookingLevelState.Preparing);
        AssertMutationFree(lifecycle, () => lifecycle.BeginPreparation(Preparation()), CookingLevelLifecycleReason.InvalidState);
        AssertAccepted(lifecycle.CompletePreparation(), CookingLevelState.Ready);
        AssertAccepted(lifecycle.Start(), CookingLevelState.Running);
        AssertAccepted(lifecycle.Pause(), CookingLevelState.Paused);
        AssertMutationFree(lifecycle, () => lifecycle.BeginEnd(CookingLevelOutcome.Success), CookingLevelLifecycleReason.InvalidState);
        AssertMutationFree(lifecycle, () => lifecycle.BeginEnd(CookingLevelOutcome.Failed), CookingLevelLifecycleReason.InvalidState);
        AssertAccepted(lifecycle.Resume(), CookingLevelState.Running);
        AssertAccepted(lifecycle.BeginEnd(CookingLevelOutcome.Failed), CookingLevelState.Ending);
        Assert.Equal(CookingLevelOutcome.Failed, lifecycle.Outcome);
        AssertMutationFree(lifecycle, () => lifecycle.BeginEnd(CookingLevelOutcome.Success), CookingLevelLifecycleReason.InvalidState);
        AssertAccepted(lifecycle.CompleteEnd(), CookingLevelState.Ended);
        Assert.Equal(CookingLevelOutcome.Failed, lifecycle.Outcome);
        AssertMutationFree(lifecycle, lifecycle.CompleteEnd, CookingLevelLifecycleReason.InvalidState);
    }

    [Theory]
    [InlineData(CookingLevelState.Created)]
    [InlineData(CookingLevelState.Preparing)]
    [InlineData(CookingLevelState.Ready)]
    [InlineData(CookingLevelState.Running)]
    [InlineData(CookingLevelState.Paused)]
    public void aborted_is_allowed_from_every_pre_terminal_state(CookingLevelState target)
    {
        var lifecycle = CreateLifecycle();
        MoveTo(lifecycle, target);

        AssertAccepted(lifecycle.BeginEnd(CookingLevelOutcome.Aborted), CookingLevelState.Ending);
        Assert.Equal(CookingLevelOutcome.Aborted, lifecycle.Outcome);
        AssertAccepted(lifecycle.CompleteEnd(), CookingLevelState.Ended);
    }

    [Fact]
    public void preparation_validation_is_complete_and_invalid_completion_is_mutation_free()
    {
        var lifecycle = CreateLifecycle();
        var wrongLevel = Preparation() with { Level = new LevelId("other-level") };
        AssertAccepted(lifecycle.BeginPreparation(wrongLevel), CookingLevelState.Preparing);
        AssertMutationFree(lifecycle, lifecycle.CompletePreparation, CookingLevelLifecycleReason.LevelIdMismatch);

        var malformed = CreateLifecycle();
        AssertAccepted(malformed.BeginPreparation(Preparation() with
        {
            Layout = new CookingLogicalLayout(new LayoutId("layout-a"), null!, null!),
        }), CookingLevelState.Preparing);
        AssertMutationFree(malformed, malformed.CompletePreparation, CookingLevelLifecycleReason.LayoutMalformed);
    }

    [Fact]
    public void start_creates_one_gameplay_owner_and_pause_resume_preserves_it()
    {
        var factory = new CountingFactory();
        var lifecycle = CreateLifecycle(factory);
        PrepareAndStart(lifecycle);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(lifecycle.TryGetGameplay(out var gameplay));
        Assert.Same(factory.Gameplay, gameplay);

        AssertAccepted(lifecycle.Pause(), CookingLevelState.Paused);
        Assert.False(lifecycle.IsGameplayAdmissionOpen);
        Assert.False(lifecycle.TryGetGameplay(out _));
        AssertAccepted(lifecycle.Resume(), CookingLevelState.Running);
        Assert.True(lifecycle.TryGetGameplay(out var resumed));
        Assert.Same(gameplay, resumed);
        AssertMutationFree(lifecycle, lifecycle.Start, CookingLevelLifecycleReason.InvalidState);
        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public void retained_gameplay_reference_cannot_mutate_during_pause_or_ending()
    {
        var lifecycle = CreateLifecycle();
        PrepareAndStart(lifecycle);
        Assert.True(lifecycle.TryGetGameplay(out var gameplay));
        var beforePause = gameplay.Snapshot();

        AssertAccepted(lifecycle.Pause(), CookingLevelState.Paused);
        Assert.False(lifecycle.TryGetGameplay(out _));
        Assert.False(lifecycle.IsGameplayAdmissionOpen);
        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed,
            gameplay.Submit(RecipeCommand(lifecycle.Scope.MatchScope)).Reason);
        Assert.Throws<InvalidOperationException>(() => gameplay.AdvanceFixedTick(lifecycle.Scope, 1));
        Assert.Throws<InvalidOperationException>(() => gameplay.AddIngredient(new ItemId("paused-item"), Raw, Player));
        Assert.Equal(beforePause.CanonicalText(), gameplay.Snapshot().CanonicalText());

        AssertAccepted(lifecycle.Resume(), CookingLevelState.Running);
        Assert.True(lifecycle.TryGetGameplay(out var resumed));
        Assert.Same(gameplay, resumed);
        AssertAccepted(lifecycle.BeginEnd(CookingLevelOutcome.Aborted), CookingLevelState.Ending);
        Assert.False(lifecycle.TryGetGameplay(out _));
        Assert.False(lifecycle.IsGameplayAdmissionOpen);
        var beforeEndingWrite = gameplay.Snapshot();
        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed,
            gameplay.Submit(RecipeCommand(lifecycle.Scope.MatchScope)).Reason);
        Assert.Throws<InvalidOperationException>(() => gameplay.AdvanceFixedTick(lifecycle.Scope, 1));
        Assert.Throws<InvalidOperationException>(() => gameplay.AddIngredient(new ItemId("ending-item"), Raw, Player));
        Assert.Equal(beforeEndingWrite.CanonicalText(), gameplay.Snapshot().CanonicalText());
    }

    [Fact]
    public void reentrant_start_factory_cannot_abort_lifecycle_and_returned_gameplay_is_closed()
    {
        var factory = new ReentrantFactory();
        var lifecycle = CreateLifecycle(factory);
        factory.Lifecycle = lifecycle;
        AssertAccepted(lifecycle.BeginPreparation(Preparation()), CookingLevelState.Preparing);
        AssertAccepted(lifecycle.CompletePreparation(), CookingLevelState.Ready);

        var result = lifecycle.Start();

        Assert.False(result.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.GameplayInitializationFailed, result.Reason);
        Assert.Equal(CookingLevelLifecycleReason.InvalidState, factory.ReentrantResult!.Reason);
        Assert.Equal(CookingLevelState.Ready, lifecycle.State);
        Assert.False(lifecycle.TryGetGameplay(out _));
        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed,
            factory.ReturnedGameplay!.Submit(RecipeCommand(lifecycle.Scope.MatchScope)).Reason);
    }

    [Fact]
    public void invalid_outcome_is_reserved_for_illegal_enum_values()
    {
        var lifecycle = CreateLifecycle();
        AssertMutationFree(lifecycle,
            () => lifecycle.BeginEnd((CookingLevelOutcome)999),
            CookingLevelLifecycleReason.InvalidOutcome);
    }

    [Fact]
    public void events_are_exposed_through_read_only_collections()
    {
        var lifecycle = CreateLifecycle();
        var result = lifecycle.BeginPreparation(Preparation());

        Assert.Throws<NotSupportedException>(() => ((IList<CookingLevelLifecycleEvent>)result.Events)
            .Add(result.Events[0]));
        Assert.Throws<NotSupportedException>(() => ((IList<CookingLevelLifecycleEvent>)lifecycle.EventHistory)
            .Add(result.Events[0]));
    }

    [Fact]
    public void failed_end_creates_retry_with_same_level_and_higher_epoch_only()
    {
        var lifecycle = CreateLifecycle();
        PrepareAndStart(lifecycle);
        Assert.True(lifecycle.TryGetGameplay(out var gameplay));
        End(lifecycle, CookingLevelOutcome.Failed);
        var terminal = lifecycle.Snapshot();

        AssertMutationFreeSuccessor(lifecycle, lifecycle.CreateRetry(1), CookingLevelLifecycleReason.EpochNotAdvanced, terminal);
        var retry = lifecycle.CreateRetry(2);

        Assert.True(retry.Accepted);
        Assert.Equal(Level, retry.SourceScope.Level);
        Assert.Equal(CookingLevelOutcome.Failed, retry.SourceOutcome);
        Assert.Equal(Level, retry.NewScope!.Level);
        Assert.Equal(2, retry.NewScope.LevelEpoch);
        Assert.Equal(MatchScope(), retry.NewScope.MatchScope);
        Assert.Equal(Runtime, retry.NewScope.RestaurantRuntime);
        Assert.True(retry.SourceGameplayClosed);
        Assert.Equal(CookingLevelState.Ended, lifecycle.State);
        Assert.Equal(CookingLevelState.Created, retry.NewLevel!.State);
        Assert.Equal(0, retry.NewLevel.Version);
        Assert.False(retry.NewLevel.TryGetGameplay(out _));

        var closed = gameplay.Submit(RecipeCommand(lifecycle.Scope.MatchScope));
        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed, closed.Reason);
        AssertMutationFreeSuccessor(lifecycle, lifecycle.CreateRetry(3), CookingLevelLifecycleReason.GenerationAlreadyCreated, lifecycle.Snapshot());
    }

    [Fact]
    public void successful_end_creates_different_level_successor_with_higher_epoch_only()
    {
        var lifecycle = CreateLifecycle();
        PrepareAndStart(lifecycle);
        End(lifecycle, CookingLevelOutcome.Success);
        var terminal = lifecycle.Snapshot();

        AssertMutationFreeSuccessor(lifecycle,
            lifecycle.CreateSuccessor(new LevelId(" "), 2),
            CookingLevelLifecycleReason.SuccessorLevelIdMissing,
            terminal);
        AssertMutationFreeSuccessor(lifecycle,
            lifecycle.CreateSuccessor(Level, 2),
            CookingLevelLifecycleReason.SuccessorLevelIdUnchanged,
            terminal);
        AssertMutationFreeSuccessor(lifecycle,
            lifecycle.CreateSuccessor(new LevelId("level-b"), 1),
            CookingLevelLifecycleReason.EpochNotAdvanced,
            terminal);

        var successor = lifecycle.CreateSuccessor(new LevelId("level-b"), 2);
        Assert.True(successor.Accepted);
        Assert.Equal(new LevelId("level-b"), successor.NewScope!.Level);
        Assert.Equal(2, successor.NewScope.LevelEpoch);
        Assert.Equal(MatchScope(), successor.NewScope.MatchScope);
        Assert.Equal(Runtime, successor.NewScope.RestaurantRuntime);
        Assert.Equal(CookingLevelState.Created, successor.NewLevel!.State);
    }

    [Fact]
    public void retry_and_successor_require_matching_terminal_outcome()
    {
        var failed = CreateLifecycle();
        PrepareAndStart(failed);
        End(failed, CookingLevelOutcome.Failed);
        AssertMutationFreeSuccessor(failed,
            failed.CreateSuccessor(new LevelId("level-b"), 2),
            CookingLevelLifecycleReason.SuccessorRequiresSuccessOutcome,
            failed.Snapshot());

        var success = CreateLifecycle();
        PrepareAndStart(success);
        End(success, CookingLevelOutcome.Success);
        AssertMutationFreeSuccessor(success,
            success.CreateRetry(2),
            CookingLevelLifecycleReason.RetryRequiresFailedOutcome,
            success.Snapshot());
    }

    [Fact]
    public void snapshot_applier_rejects_old_scope_epoch_and_out_of_order_versions()
    {
        var lifecycle = CreateLifecycle();
        var applier = new CookingLevelLifecycleSnapshotApplier(lifecycle.Scope, Configuration().Identity);
        Assert.True(applier.Apply(lifecycle.Snapshot()).Accepted);

        AssertAccepted(lifecycle.BeginPreparation(Preparation()), CookingLevelState.Preparing);
        var preparing = lifecycle.Snapshot();
        Assert.True(applier.Apply(preparing).Accepted);
        Assert.False(applier.Apply(preparing).Accepted);
        Assert.Equal(CookingLevelLifecycleReason.SnapshotSequenceDuplicate, applier.Apply(preparing).Reason);

        var foreignScope = new CookingLevelScope(MatchScope(), Runtime, Level, 2);
        var foreign = applier.Apply(preparing with { Scope = foreignScope, Version = preparing.Version + 1 });
        Assert.False(foreign.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.ScopeMismatch, foreign.Reason);

        var gap = applier.Apply(preparing with { Version = preparing.Version + 2 });
        Assert.False(gap.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.SnapshotSequenceGap, gap.Reason);
        Assert.Equal(preparing, applier.Current);
    }

    [Fact]
    public void snapshot_applier_rejects_impossible_snapshots_without_moving_current_watermark()
    {
        var lifecycle = CreateLifecycle();
        var applier = new CookingLevelLifecycleSnapshotApplier(lifecycle.Scope, Configuration().Identity);
        var created = lifecycle.Snapshot();
        Assert.True(applier.Apply(created).Accepted);

        var impossible = new[]
        {
            created with { Version = 1, State = CookingLevelState.Running },
            created with { Version = 1, State = CookingLevelState.Ending, Outcome = CookingLevelOutcome.Success },
            created with { Version = 1, State = CookingLevelState.Ended, Outcome = CookingLevelOutcome.Failed },
            created with { Version = 2, State = CookingLevelState.Ready, Outcome = CookingLevelOutcome.Aborted },
            created with { Version = 1, State = CookingLevelState.Ending },
            created with { Version = 1, State = CookingLevelState.Ending, Outcome = (CookingLevelOutcome)999 },
            created with { Version = 1, HasCreatedNextGeneration = true },
            created with { Version = 1, Map = new MapId("map-a") },
        };
        foreach (var snapshot in impossible)
        {
            var result = applier.Apply(snapshot);
            Assert.False(result.Accepted);
            Assert.Equal(CookingLevelLifecycleReason.SnapshotSequenceGap, result.Reason);
            Assert.Equal(created, applier.Current);
        }

        AssertAccepted(lifecycle.BeginPreparation(Preparation()), CookingLevelState.Preparing);
        var preparing = lifecycle.Snapshot();
        Assert.True(applier.Apply(preparing).Accepted);
        var changedFields = preparing with
        {
            Version = preparing.Version + 1,
            State = CookingLevelState.Ready,
            Map = new MapId("other-map"),
        };
        var changedResult = applier.Apply(changedFields);
        Assert.False(changedResult.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.SnapshotSequenceGap, changedResult.Reason);
        Assert.Equal(preparing, applier.Current);
    }

    [Fact]
    public void obsolete_facade_and_canonical_lifecycle_share_gameplay_version_and_event_owner()
    {
#pragma warning disable CS0618
        var factory = new LegacyCountingFactory();
        var facade = new CookingMatchLifecycle(MatchScope(), 1, Configuration(), factory);

        Assert.Equal(CookingMatchState.Preparing, facade.State);
        Assert.Null(facade.CanonicalLifecycle);
        Assert.Equal(0, facade.Version);
        Assert.Empty(facade.EventHistory);

        var prepared = facade.Prepare(new CookingMatchPreparation(Level, new MapId("map-a"),
            new CookingLogicalLayout(new LayoutId("layout-a"), new[] { Station }, new[] { Container }),
            Configuration().Identity));
        Assert.True(prepared.Accepted);
        var inner = Assert.IsType<CookingLevelLifecycle>(facade.CanonicalLifecycle);
        Assert.Equal(Level, inner.Scope.Level);
        Assert.Equal(CookingLevelState.Ready, inner.State);
        Assert.Equal(facade.Version, facade.EventHistory.Count);

        Assert.True(facade.Start().Accepted);
        Assert.Equal(CookingLevelState.Running, inner.State);
        Assert.True(facade.TryGetGameplay(out var legacyGameplay));
        Assert.True(inner.TryGetGameplay(out var canonicalGameplay));
        Assert.Same(legacyGameplay, canonicalGameplay);
        Assert.Same(factory.Gameplay, canonicalGameplay);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(facade.Version, facade.EventHistory.Count);

        Assert.True(facade.End().Accepted);
        Assert.Equal(CookingLevelState.Ended, inner.State);
        Assert.Equal(CookingLevelOutcome.Success, inner.Outcome);
        Assert.Equal(facade.Version, facade.EventHistory.Count);

        var restart = facade.Restart(new CookingScope(Session, World, new MatchId("legacy-next")), 2);
        Assert.True(restart.Accepted);
        Assert.Equal(CookingLevelState.Ended, inner.State);
        Assert.Equal(facade.Version, facade.EventHistory.Count);
        Assert.Equal(CookingMatchState.Preparing, restart.NewMatch!.State);
        Assert.Equal(0, restart.NewMatch.Version);
        Assert.Equal(Level, restart.NewMatch.CanonicalLifecycle!.Scope.Level);
        Assert.Equal(CookingLevelState.Created, restart.NewMatch.CanonicalLifecycle.State);
#pragma warning restore CS0618
    }

    [Fact]
    public void obsolete_facade_rejects_operations_during_unsupported_paused_and_ending_states()
    {
#pragma warning disable CS0618
        var facade = new CookingMatchLifecycle(MatchScope(), 1, Configuration(), new LegacyCountingFactory());
        Assert.True(facade.Prepare(new CookingMatchPreparation(Level, new MapId("map-a"),
            new CookingLogicalLayout(new LayoutId("layout-a"), new[] { Station }, new[] { Container }),
            Configuration().Identity)).Accepted);
        Assert.True(facade.Start().Accepted);

        Assert.True(facade.CanonicalLifecycle!.Pause().Accepted);
        Assert.Equal(CookingMatchState.Unsupported, facade.State);
        Assert.Equal(CookingMatchState.Unsupported, facade.Snapshot().State);
        var pausedVersion = facade.CanonicalLifecycle.Version;
        Assert.False(facade.Start().Accepted);
        Assert.False(facade.End().Accepted);
        Assert.Equal(pausedVersion, facade.CanonicalLifecycle.Version);
        Assert.False(facade.IsGameplayAdmissionOpen);

        Assert.True(facade.CanonicalLifecycle.BeginEnd(CookingLevelOutcome.Aborted).Accepted);
        Assert.Equal(CookingMatchState.Unsupported, facade.State);
        Assert.Equal(CookingMatchState.Unsupported, facade.Snapshot().State);
        var endingVersion = facade.CanonicalLifecycle.Version;
        Assert.False(facade.Start().Accepted);
        Assert.False(facade.End().Accepted);
        Assert.Equal(endingVersion, facade.CanonicalLifecycle.Version);
        Assert.False(facade.IsGameplayAdmissionOpen);
#pragma warning restore CS0618
    }

    private static void MoveTo(CookingLevelLifecycle lifecycle, CookingLevelState target)
    {
        if (target == CookingLevelState.Created)
            return;
        AssertAccepted(lifecycle.BeginPreparation(Preparation()), CookingLevelState.Preparing);
        if (target == CookingLevelState.Preparing)
            return;
        AssertAccepted(lifecycle.CompletePreparation(), CookingLevelState.Ready);
        if (target == CookingLevelState.Ready)
            return;
        AssertAccepted(lifecycle.Start(), CookingLevelState.Running);
        if (target == CookingLevelState.Running)
            return;
        AssertAccepted(lifecycle.Pause(), CookingLevelState.Paused);
    }

    private static void PrepareAndStart(CookingLevelLifecycle lifecycle)
    {
        AssertAccepted(lifecycle.BeginPreparation(Preparation()), CookingLevelState.Preparing);
        AssertAccepted(lifecycle.CompletePreparation(), CookingLevelState.Ready);
        AssertAccepted(lifecycle.Start(), CookingLevelState.Running);
    }

    private static void End(CookingLevelLifecycle lifecycle, CookingLevelOutcome outcome)
    {
        AssertAccepted(lifecycle.BeginEnd(outcome), CookingLevelState.Ending);
        AssertAccepted(lifecycle.CompleteEnd(), CookingLevelState.Ended);
    }

    private static void AssertAccepted(CookingLevelLifecycleResult result, CookingLevelState state)
    {
        Assert.True(result.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.None, result.Reason);
        Assert.Equal(state, result.State);
        Assert.Single(result.Events);
    }

    private static void AssertMutationFree(
        CookingLevelLifecycle lifecycle,
        Func<CookingLevelLifecycleResult> action,
        CookingLevelLifecycleReason reason)
    {
        var before = lifecycle.Snapshot();
        var eventsBefore = lifecycle.EventHistory.ToArray();
        var result = action();

        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Reason);
        Assert.Empty(result.Events);
        Assert.Equal(before, lifecycle.Snapshot());
        Assert.Equal(eventsBefore, lifecycle.EventHistory);
    }

    private static void AssertMutationFreeSuccessor(
        CookingLevelLifecycle lifecycle,
        CookingLevelSuccessorResult result,
        CookingLevelLifecycleReason reason,
        CookingLevelLifecycleSnapshot before)
    {
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Reason);
        Assert.Null(result.NewLevel);
        Assert.Equal(before, lifecycle.Snapshot());
        Assert.Equal(result.SourceVersionBefore, result.SourceVersionAfter);
        Assert.Empty(result.SourceResult.Events);
    }

    [Fact]
    public void Initialized_preparation_kitchen_is_created_once_and_reused_by_start()
    {
        var factory = new CountingFactory();
        var lifecycle = CreateLifecycle(factory);
        var guard = new PreparationPublicationGuard(true);
        Assert.True(lifecycle.InitializePreparationKitchen(guard).Accepted);
        Assert.True(lifecycle.InitializePreparationKitchen(guard).Accepted);
        Assert.True(lifecycle.TryPeekBoundKitchen(out var prepared));
        Assert.False(lifecycle.TryGetGameplay(out _));
        Assert.Throws<InvalidOperationException>(() => prepared!.AddWorldIngredient(new("closed-prep-item"), Raw, "source"));
        Assert.True(lifecycle.BeginPreparation(Preparation()).Accepted);
        Assert.True(lifecycle.CompletePreparation().Accepted);
        Assert.True(lifecycle.Start(guard).Accepted);
        Assert.True(lifecycle.TryGetGameplay(out var running));
        Assert.Same(prepared, running);
        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public void Rejected_preparation_publication_does_not_bind_or_close_a_foreign_candidate()
    {
        var factory = new CountingFactory();
        var lifecycle = CreateLifecycle(factory);
        var before = lifecycle.Snapshot();
        Assert.False(lifecycle.InitializePreparationKitchen(new PreparationPublicationGuard(false)).Accepted);
        Assert.False(lifecycle.TryPeekBoundKitchen(out _));
        Assert.Equal(before, lifecycle.Snapshot());
        factory.Gameplay!.AddWorldIngredient(new("still-unbound"), Raw, "source");
    }

    private sealed class PreparationPublicationGuard(bool accepted) : ICookingLevelGameplayPublicationGuard
    {
        public bool TryAcquire(CookingRecipeSimulation gameplay) => accepted;
        public void Release(CookingRecipeSimulation gameplay) { }
    }

    private static CookingLevelLifecycle CreateLifecycle(ICookingLevelGameplayFactory? factory = null) =>
        new(LevelScope(), Configuration(), factory ?? new CountingFactory());

    private static CookingLevelScope LevelScope() => new(MatchScope(), Runtime, Level, 1);
    private static CookingScope MatchScope() => new(Session, World, Match);

    private static CookingLevelPreparation Preparation() =>
        new(Level, new MapId("map-a"),
            new CookingLogicalLayout(new LayoutId("layout-a"), new[] { Station }, new[] { Container }),
            Configuration().Identity);

    private static CookingConfigurationSnapshot Configuration()
    {
        var registry = new CookingConfigurationRegistry();
        var result = registry.Submit(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[]
            {
                new CookingItemDefinition(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
                new CookingItemDefinition(Product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
                new CookingItemDefinition(Container, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                    new CookingItemContainerCapability(2, new HashSet<DefinitionId> { Product })),
            },
            new[] { new CookingApplianceDefinition(Station, new HashSet<string>(StringComparer.Ordinal) { "heat" }) },
            new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Product, new ProcessId("process-a"), "heat", 3) }));
        Assert.True(result.Accepted);
        return Assert.IsType<CookingConfigurationSnapshot>(registry.Current);
    }

    private static CookingRecipeSimulation CreateGameplay(CookingScope scope, CookingConfigurationSnapshot configuration)
    {
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
        };
        return new CookingRecipeSimulation(new CookingRecipeFixture(scope, players, configuration.Items,
            configuration.Appliances, configuration.Recipes));
    }

    private static CookingRecipeCommand RecipeCommand(CookingScope scope) =>
        new(scope, 1, Player, new RecipeCommandId("closed-command"), CookingRecipeOperation.AdvanceTicks,
            null, new ProcessId("missing-process"), null, null, null, null, 0, 1);

    private sealed class ReentrantFactory : ICookingLevelGameplayFactory
    {
        public CookingLevelLifecycle? Lifecycle { get; set; }
        public CookingLevelLifecycleResult? ReentrantResult { get; private set; }
        public CookingRecipeSimulation? ReturnedGameplay { get; private set; }

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            ReentrantResult = Lifecycle!.BeginEnd(CookingLevelOutcome.Aborted);
            ReturnedGameplay = CreateGameplay(scope.MatchScope, configuration);
            return ReturnedGameplay;
        }
    }

    private sealed class CountingFactory : ICookingLevelGameplayFactory
    {
        public int CreateCount { get; private set; }
        public CookingRecipeSimulation? Gameplay { get; private set; }

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++;
            Gameplay = CreateGameplay(scope.MatchScope, configuration);
            return Gameplay;
        }
    }

#pragma warning disable CS0618
    private sealed class LegacyCountingFactory : ICookingMatchGameplayFactory
    {
        public int CreateCount { get; private set; }
        public CookingRecipeSimulation? Gameplay { get; private set; }

        public CookingRecipeSimulation Create(CookingScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++;
            Gameplay = CreateGameplay(scope, configuration);
            return Gameplay;
        }
    }
#pragma warning restore CS0618

}
