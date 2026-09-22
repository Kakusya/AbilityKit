using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using global::ET;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

public sealed class CookingLevelEtHostTests
{
    private static readonly ItemId Plate = new("plate");
    private static readonly DefinitionId PlateDefinition = new("plate");

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Canonical_tree_uses_exact_ET_relations_and_recursively_releases()
    {
        AssertRelation<ComponentOfAttribute, CookingApplicationComponent, Scene>();
        AssertRelation<ComponentOfAttribute, CookingMatchRegistryComponent, CookingApplicationComponent>();
        AssertRelation<ChildOfAttribute, CookingMatchEntity, CookingMatchRegistryComponent>();
        AssertRelation<ComponentOfAttribute, CookingRestaurantRuntimeComponent, CookingMatchEntity>();
        AssertRelation<ComponentOfAttribute, CookingKitchenComponent, CookingRestaurantRuntimeComponent>();
        AssertRelation<ComponentOfAttribute, CookingLevelComponent, CookingRestaurantRuntimeComponent>();
        AssertRelation<ComponentOfAttribute, CookingLevelDriverComponent, CookingLevelComponent>();

        var fixture = CreateFixture();
        using var host = fixture.CreateHost();
        Assert.Same(host.Scene, host.Application.Parent);
        Assert.Same(host.Application, host.MatchRegistry.Parent);
        Assert.Same(host.MatchRegistry, host.Match.Parent);
        Assert.Same(host.Match, host.RestaurantRuntime.Parent);
        Assert.Same(host.RestaurantRuntime, host.Kitchen.Parent);
        Assert.Same(host.RestaurantRuntime, host.Level.Parent);
        Assert.Same(host.Level, host.Driver.Parent);
        Assert.Same(host.Application, host.Scene.GetComponent<CookingApplicationComponent>());
        Assert.Same(host.MatchRegistry, host.Application.GetComponent<CookingMatchRegistryComponent>());
        Assert.Same(host.RestaurantRuntime, host.Match.GetComponent<CookingRestaurantRuntimeComponent>());
        Assert.Same(host.Level, host.RestaurantRuntime.GetComponent<CookingLevelComponent>());

        var application = host.Application;
        var registry = host.MatchRegistry;
        var match = host.Match;
        var runtime = host.RestaurantRuntime;
        var kitchen = host.Kitchen;
        var level = host.Level;
        var driver = host.Driver;
        host.Dispose();

        Assert.All(new Entity[] { application, registry, match, runtime, kitchen, level, driver },
            entity => Assert.True(entity.IsDisposed));
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Canonical_host_blocks_raw_lifecycle_mutations_outside_host_operations()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost();
        var before = host.Lifecycle.Snapshot();

        Assert.Equal(CookingLevelLifecycleReason.InvalidState, host.Lifecycle.Pause().Reason);
        Assert.Equal(CookingLevelLifecycleReason.InvalidState,
            host.Lifecycle.BeginEnd(CookingLevelOutcome.Success).Reason);
        Assert.Equal(before, host.Lifecycle.Snapshot());

        Assert.True(host.Pause().Accepted);
        Assert.True(host.Resume().Accepted);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var terminal = host.Lifecycle.Snapshot();
        var rawSuccessor = host.Lifecycle.CreateSuccessor(new LevelId("raw-successor"), 2);
        Assert.False(rawSuccessor.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.InvalidState, rawSuccessor.Reason);
        Assert.Equal(terminal, host.Lifecycle.Snapshot());
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Canonical_host_blocks_raw_simulation_writes_outside_its_ET_frame()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"));
        var command = fixture.Command(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1);

        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed, fixture.Simulation.Submit(command).Reason);
        Assert.Throws<InvalidOperationException>(() => fixture.Simulation.AdvanceFixedTick(fixture.LevelScope, 1));
        Assert.True(host.TryEnqueue(new(fixture.LevelScope, command, "connection", "correlation")).Accepted);

        var frame = host.Tick();

        Assert.True(frame.Accepted);
        Assert.Equal(CookingLevelDispositionKind.Executed, Assert.Single(frame.Dispositions).Kind);
        Assert.Equal(fixture.Ingredient, fixture.Simulation.ItemInHand(fixture.Player));
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Running_frame_executes_one_minimum_batch_then_one_fixed_tick()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"));

        Assert.True(host.TryEnqueue(fixture.Envelope(2, "future", CookingRecipeOperation.StartProcess,
            recipe: fixture.Recipe, item: fixture.Ingredient, station: fixture.Station, expectedVersion: 2)).Accepted);
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1)).Accepted);

        var first = host.Tick();
        Assert.True(first.Accepted);
        Assert.Equal(1, first.SimulationBatch);
        Assert.Equal(1, first.HostFrameSequence);
        Assert.Equal(1, fixture.Simulation.LogicalTick);
        Assert.Equal(fixture.Ingredient, fixture.Simulation.ItemInHand(fixture.Player));
        Assert.Single(first.Dispositions);
        Assert.Equal(1, host.PendingCommandIdentityCount);

        var second = host.Tick();
        Assert.Equal(2, second.SimulationBatch);
        Assert.Equal(2, second.HostFrameSequence);
        Assert.Equal(2, fixture.Simulation.LogicalTick);
        var process = Assert.Single(fixture.Simulation.Snapshot().Processes);
        Assert.Equal(1, process.ElapsedTicks);
        Assert.Equal(2, host.LastCommittedSimulationBatch);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Conflicting_pending_group_rejects_every_envelope_without_entering_simulation_ledger()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"));
        var command = fixture.Command(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1);
        Assert.True(host.TryEnqueue(new(fixture.LevelScope, command, "b", "b")).Accepted);
        var conflictAdmission = host.TryEnqueue(new(fixture.LevelScope, command with { ExpectedItemVersion = 2 }, "a", "a"));
        Assert.False(conflictAdmission.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, conflictAdmission.Reason);

        var frame = host.Tick();
        Assert.Empty(frame.Dispositions);
        Assert.Equal(2, host.DispositionHistory.Count);
        Assert.All(host.DispositionHistory, disposition =>
            Assert.Equal(CookingLevelDispositionKind.Conflicted, disposition.Kind));
        Assert.Null(fixture.Simulation.ItemInHand(fixture.Player));
        Assert.Empty(fixture.Simulation.EventHistory);
        Assert.Equal(1, fixture.Simulation.LogicalTick);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fixed_tick_failure_is_rethrown_faults_host_and_does_not_publish_frame_sequence()
    {
        var fixture = CreateFixture(new ThrowingAllocator());
        using var host = fixture.CreateStartedHost(simulation =>
        {
            simulation.AddIngredient(fixture.Ingredient, fixture.Raw, fixture.Player);
            var start = fixture.Command(1, "start", CookingRecipeOperation.StartProcess,
                recipe: fixture.Recipe, item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1);
            Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(start).Outcome);
            simulation.Submit(fixture.Command(2, "legacy-progress", CookingRecipeOperation.AdvanceTicks,
                process: Assert.Single(simulation.Snapshot().Processes).Id, tickCount: 2));
        });
        var before = fixture.Simulation.Snapshot().Sha256();

        var failure = Assert.Throws<InvalidOperationException>(host.Tick);
        Assert.Contains("authority failed", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(host.IsFaulted);
        Assert.Equal(0, host.HostFrameSequence);
        Assert.Equal(before, fixture.Simulation.Snapshot().Sha256());
        Assert.Equal(CookingRecipeRejectionReason.LifecycleClosed,
            fixture.Simulation.Submit(fixture.Command(3, "after-fault", CookingRecipeOperation.Pickup,
                item: fixture.Ingredient, expectedVersion: 1)).Reason);
        Assert.Throws<InvalidOperationException>(() => fixture.Simulation.AdvanceFixedTick(fixture.LevelScope, 1));
        Assert.Throws<InvalidOperationException>(host.Tick);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Same_command_identity_in_future_batch_is_one_pending_identity_and_terminalizes_in_host_ledger()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"));
        var batchOne = fixture.Command(1, "same", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1);
        var batchTwo = batchOne with { SimulationBatch = 2 };
        Assert.True(host.TryEnqueue(new(fixture.LevelScope, batchTwo, "future", "future")).Accepted);
        var conflict = host.TryEnqueue(new(fixture.LevelScope, batchOne, "current", "current"));
        Assert.False(conflict.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, conflict.Reason);
        Assert.Equal(0, host.PendingCommandIdentityCount);

        var frame = host.Tick();
        Assert.Null(frame.SimulationBatch);
        Assert.Equal(0, host.PendingCommandIdentityCount);
        Assert.Empty(frame.Dispositions);
        Assert.Equal(2, host.DispositionHistory.Count);
        Assert.All(host.DispositionHistory, value => Assert.Equal(CookingLevelDispositionKind.Conflicted, value.Kind));

        var replay = host.TryEnqueue(new(fixture.LevelScope, batchTwo, "replay", "replay"));
        Assert.False(replay.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, replay.Reason);
        Assert.Equal(CookingLevelDispositionKind.Conflicted, replay.TerminalDisposition!.Kind);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Arbitration_is_transport_independent_collapses_duplicates_and_terminalizes_conflicts()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(
            simulation => simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"),
            queueCapacity: 1);
        var command = fixture.Command(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1);

        Assert.True(host.TryEnqueue(new(fixture.LevelScope, command, "z-connection", "z-correlation")).Accepted);
        Assert.True(host.TryEnqueue(new(fixture.LevelScope, command, "a-connection", "a-correlation")).Accepted);
        Assert.Equal(1, host.PendingCommandIdentityCount);

        var frame = host.Tick();
        Assert.Collection(frame.Dispositions,
            value =>
            {
                Assert.Equal(CookingLevelDispositionKind.Executed, value.Kind);
                Assert.Equal("a-connection", value.Envelope.SourceConnectionId);
            },
            value => Assert.Equal(CookingLevelDispositionKind.Duplicate, value.Kind));

        var replay = host.TryEnqueue(new(fixture.LevelScope, command, "b-connection", "b-correlation"));
        Assert.True(replay.Accepted);
        Assert.NotNull(replay.TerminalDisposition);
        Assert.Equal(CookingLevelDispositionKind.Duplicate, replay.TerminalDisposition!.Kind);
        Assert.Equal(0, host.PendingCommandIdentityCount);

        var conflicting = command with { ExpectedItemVersion = 2 };
        var conflict = host.TryEnqueue(new(fixture.LevelScope, conflicting, "c", "c"));
        Assert.False(conflict.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, conflict.Reason);
        Assert.Equal(0, host.PendingCommandIdentityCount);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Pause_retains_queue_rejects_new_ingress_and_does_not_advance_clock()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"));
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1)).Accepted);

        Assert.True(host.Pause().Accepted);
        var rejected = host.TryEnqueue(fixture.Envelope(2, "new", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1));
        Assert.False(rejected.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.LevelPaused, rejected.Reason);
        var paused = host.Tick();
        Assert.False(paused.Accepted);
        Assert.Equal(CookingLevelFrameReason.LevelPaused, paused.Reason);
        Assert.Equal(0, fixture.Simulation.LogicalTick);
        Assert.Equal(1, host.PendingCommandIdentityCount);
        Assert.Equal(0, host.HostFrameSequence);

        Assert.True(host.Resume().Accepted);
        var resumed = host.Tick();
        Assert.True(resumed.Accepted);
        Assert.Equal(1, fixture.Simulation.LogicalTick);
        Assert.Equal(1, host.HostFrameSequence);
        Assert.Equal(fixture.LevelScope.LevelEpoch, host.Binding.LevelScope.LevelEpoch);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Reserved_clock_and_stale_batches_do_not_consume_capacity()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(queueCapacity: 1);
        var advance = fixture.Envelope(1, "advance", CookingRecipeOperation.AdvanceTicks,
            process: new ProcessId("process-1"), tickCount: 1);
        var reserved = host.TryEnqueue(advance);
        Assert.False(reserved.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.ReservedClockOperation, reserved.Reason);
        Assert.Equal(0, host.PendingCommandIdentityCount);

        host.Tick();
        Assert.True(host.TryEnqueue(fixture.Envelope(3, "future", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1)).Accepted);
        host.Tick();
        var stale = host.TryEnqueue(fixture.Envelope(2, "stale", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1));
        Assert.False(stale.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.BatchStale, stale.Reason);
        Assert.Equal(0, host.PendingCommandIdentityCount);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Retry_cancels_old_epoch_queue_resets_local_watermarks_and_preserves_host_sequence()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost();
        host.Tick();
        Assert.True(host.TryEnqueue(fixture.Envelope(5, "old", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1)).Accepted);
        var ending = host.BeginEnd(CookingLevelOutcome.Failed);
        Assert.True(ending.Accepted);
        Assert.Equal(CookingLevelDispositionKind.Cancelled, Assert.Single(ending.Dispositions).Kind);
        Assert.True(host.CompleteEnd().Accepted);

        var oldLevel = host.Level;
        var oldDriver = host.Driver;
        var retry = host.CreateRetry(2, EmptyContent());
        Assert.True(retry.Accepted);
        Assert.Equal(fixture.LevelScope.Level, retry.NewScope!.Level);
        Assert.Equal(2, retry.NewScope.LevelEpoch);
        Assert.Equal(1, host.HostFrameSequence);
        Assert.Equal(0, host.LastCommittedSimulationBatch);
        Assert.Empty(retry.Dispositions);
        Assert.True(oldLevel.IsDisposed);
        Assert.True(oldDriver.IsDisposed);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fingerprint_uses_canonical_semantics_and_excludes_transport_metadata()
    {
        var fixture = CreateFixture();
        var command = fixture.Command(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1);
        var left = CookingCommandFingerprint.Create(new(fixture.LevelScope, command, "a", "one"));
        var right = CookingCommandFingerprint.Create(new(fixture.LevelScope, command, "z", "two"));
        var changed = CookingCommandFingerprint.Create(new(fixture.LevelScope,
            command with { ExpectedItemVersion = 2 }, "a", "one"));
        Assert.Equal(left, right);
        Assert.NotEqual(left, changed);
        Assert.Equal(64, left.Value.Length);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Dispose_materializes_ordered_final_history_and_host_misuse_throws()
    {
        var fixture = CreateFixture();
        var host = fixture.CreateStartedHost();
        Assert.True(host.TryEnqueue(fixture.Envelope(2, "b", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1, player: new PlayerId("z"))).Accepted);
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "a", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1)).Accepted);

        Exception? foreignThreadFailure = null;
        var thread = new Thread(() => foreignThreadFailure = Record.Exception(host.Tick));
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(foreignThreadFailure);

        host.Dispose();
        Assert.Equal(2, host.FinalDispositionHistory.Count);
        Assert.All(host.FinalDispositionHistory, disposition =>
            Assert.Equal(CookingLevelDispositionKind.Cancelled, disposition.Kind));
        Assert.Equal("p1", host.FinalDispositionHistory[0].Envelope.Command.Player.Value);
        Assert.Equal(CookingLevelState.Ended, host.Lifecycle.State);
        Assert.Equal(CookingLevelOutcome.Aborted, host.Lifecycle.Outcome);
        Assert.Throws<ObjectDisposedException>(() => host.Tick());
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fingerprint_has_exact_canonical_bytes_and_sha256_golden_vector()
    {
        using var evidence = CreateFingerprintEvidence("pickup");
        var fixture = CreateFixture();
        var envelope = fixture.Envelope(1, "pickup", CookingRecipeOperation.Pickup,
            item: fixture.Ingredient, expectedVersion: 1);

        Assert.Equal(
            "0000000773657373696F6E00000005776F726C64000000056D617463680000000000000001" +
            "000000076C6576656C2D310000000000000001000000000000000100000002703100000006" +
            "7069636B7570000000000000010000000C696E6772656469656E742D310000000000000100000000",
            Convert.ToHexString(CookingCommandFingerprint.CanonicalBytes(envelope)));
        Assert.Equal("6B91D5971AB4F9A4D32B27D892F20397D2D61B159C087A5166B121B3A043A6E6",
            CookingCommandFingerprint.Create(envelope).Value);

        AppendFingerprintEvidence(evidence, "Fingerprint_has_exact_canonical_bytes_and_sha256_golden_vector",
            envelope, "pickup", "existing golden vector is byte-for-byte unchanged after the v2 contract task: the command "
            + "record gained no field, so CanonicalBytes output is identical and this test remains the regression anchor");
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fingerprint_recipe_less_start_process_has_exact_canonical_bytes_and_sha256_golden_vector()
    {
        using var evidence = CreateFingerprintEvidence("start");
        var fixture = CreateFixture();
        var envelope = fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess,
            item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1);

        Assert.True(CookingRecipeCommandValidation.IsWellFormed(envelope.Command));
        Assert.Null(envelope.Command.Recipe);
        Assert.Equal(
            "0000000773657373696F6E00000005776F726C64000000056D617463680000000000000001" +
            "000000076C6576656C2D310000000000000001000000000000000100000002703100000005" +
            "7374617274000000010000010000000C696E6772656469656E742D31010000000573746F7665" +
            "00000000000100000000",
            Convert.ToHexString(CookingCommandFingerprint.CanonicalBytes(envelope)));
        Assert.Equal("10F9BAD37CEEB607ACA2860036F83244DD4E68D5DF35123C76D3016EAC8BE88C",
            CookingCommandFingerprint.Create(envelope).Value);

        AppendFingerprintEvidence(evidence, "Fingerprint_recipe_less_start_process_has_exact_canonical_bytes_and_sha256_golden_vector",
            envelope, "start", "new golden vector for the recipe-less StartProcess command shape introduced by the v2 "
            + "contract task; generated by running CookingCommandFingerprint.CanonicalBytes and .Create on the fixture "
            + "envelope and anchoring the real output, never by adjusting the fingerprint to fit an expectation");
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fingerprint_put_in_has_exact_canonical_bytes_and_sha256_golden_vector()
    {
        using var evidence = CreateFingerprintEvidence("put-in");
        var fixture = CreateFixture();
        var envelope = fixture.Envelope(1, "put-in", CookingRecipeOperation.PutIn,
            item: fixture.Ingredient, container: fixture.Container, expectedVersion: 1);

        Assert.True(CookingRecipeCommandValidation.IsWellFormed(envelope.Command));
        Assert.Equal(CookingRecipeOperation.PutIn, envelope.Command.Operation);
        Assert.Equal(
            "0000000773657373696F6E00000005776F726C64000000056D617463680000000000000001" +
            "000000076C6576656C2D310000000000000001000000000000000100000002703100000006" +
            "7075742D696E000000050000010000000C696E6772656469656E742D31000100000005706C61" +
            "7465000000000100000000",
            Convert.ToHexString(CookingCommandFingerprint.CanonicalBytes(envelope)));
        Assert.Equal("70181A96B7A5E8775A764083D4D4B30FB0739CB0E67B636976239A8E6FBBF703",
            CookingCommandFingerprint.Create(envelope).Value);

        AppendFingerprintEvidence(evidence, "Fingerprint_put_in_has_exact_canonical_bytes_and_sha256_golden_vector",
            envelope, "put-in", "new golden vector for the put-in movement command introduced by the kitchen-loop "
            + "task: the operation enum lost Plate and tail-appended Drop/PutIn/TakeOut/Pour, so the encoded "
            + "operation value changed and this vector re-anchors the new numbering from real output");
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fingerprint_three_surfaces_agree_on_command_identity_for_both_recipe_modes()
    {
        using var evidence = CreateFingerprintEvidence("identity");
        var fixture = CreateFixture();
        var explicitRecipe = fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess,
            recipe: fixture.Recipe, item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1);
        var recipeLess = fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess,
            item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1);

        Assert.True(CookingRecipeCommandValidation.IsWellFormed(explicitRecipe.Command));
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(recipeLess.Command));

        Assert.NotEqual(CookingCommandFingerprint.Create(explicitRecipe).Value,
            CookingCommandFingerprint.Create(recipeLess).Value);
        Assert.NotEqual(
            Convert.ToHexString(CookingCommandFingerprint.CanonicalBytes(explicitRecipe)),
            Convert.ToHexString(CookingCommandFingerprint.CanonicalBytes(recipeLess)));
        Assert.NotEqual(
            JsonSerializer.Serialize(explicitRecipe.Command),
            JsonSerializer.Serialize(recipeLess.Command));

        // Cross-surface agreement: over every pair of commands, the simulation JSON fingerprint
        // and the ET binary fingerprint must agree on identity. If either surface drops or adds
        // a field the other one still covers, the two verdicts diverge and this fails.
        var variants = new[]
        {
            explicitRecipe,
            recipeLess,
            fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess, recipe: fixture.Recipe,
                item: fixture.Ingredient, station: fixture.Station, expectedVersion: 2),
            fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess, recipe: fixture.Recipe,
                item: fixture.Ingredient, expectedVersion: 1),
            fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess,
                item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1,
                process: new ProcessId("carried-process")),
            fixture.Envelope(2, "start", CookingRecipeOperation.StartProcess,
                item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1),
            fixture.Envelope(1, "other", CookingRecipeOperation.StartProcess,
                item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1),
        };
        var jsonFingerprints = variants
            .Select(envelope => JsonSerializer.Serialize(envelope.Command))
            .ToArray();
        var binaryFingerprints = variants
            .Select(envelope => CookingCommandFingerprint.Create(envelope).Value)
            .ToArray();
        for (var i = 0; i < variants.Length; i++)
        {
            for (var j = 0; j < variants.Length; j++)
            {
                Assert.Equal(
                    StringComparer.Ordinal.Equals(jsonFingerprints[i], jsonFingerprints[j]),
                    StringComparer.Ordinal.Equals(binaryFingerprints[i], binaryFingerprints[j]));
            }
        }
        Assert.Equal(variants.Length, binaryFingerprints.Distinct(StringComparer.Ordinal).Count());

        AppendFingerprintEvidence(evidence, "Fingerprint_three_surfaces_agree_on_command_identity_for_both_recipe_modes",
            explicitRecipe, "start-explicit", "explicit-recipe mode: well formed, with its own JSON and binary fingerprints");
        AppendFingerprintEvidence(evidence, "Fingerprint_three_surfaces_agree_on_command_identity_for_both_recipe_modes",
            recipeLess, "start-recipe-less", "recipe-less mode: well formed, with a distinct JSON and binary fingerprint");
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Reversed_arrival_conflict_replay_returns_the_same_deterministic_disposition()
    {
        CookingLevelPendingDisposition Run(bool reversed)
        {
            var fixture = CreateFixture();
            using var host = fixture.CreateStartedHost(simulation =>
                simulation.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn"));
            var first = fixture.Command(1, "same", CookingRecipeOperation.Pickup,
                item: fixture.Ingredient, expectedVersion: 1);
            var second = first with { ExpectedItemVersion = 2 };
            var envelopes = new[]
            {
                new CookingLevelCommandEnvelope(fixture.LevelScope, first, "a", "a"),
                new CookingLevelCommandEnvelope(fixture.LevelScope, second, "b", "b"),
            };
            var firstAdmission = host.TryEnqueue(envelopes[reversed ? 1 : 0]);
            Assert.True(firstAdmission.Accepted);
            var secondAdmission = host.TryEnqueue(envelopes[reversed ? 0 : 1]);
            Assert.False(secondAdmission.Accepted);
            Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, secondAdmission.Reason);
            host.Tick();

            var replay = host.TryEnqueue(new(fixture.LevelScope, first, "replay", "replay"));
            Assert.False(replay.Accepted);
            Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, replay.Reason);
            return replay.TerminalDisposition!;
        }

        var left = Run(false);
        var right = Run(true);
        Assert.Equal(CookingLevelDispositionKind.Conflicted, left.Kind);
        Assert.Equal(left.Kind, right.Kind);
        Assert.Equal(left.Reason, right.Reason);
        Assert.Equal(left.Fingerprint, right.Fingerprint);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Throwing_wash_port_terminalizes_failing_and_remaining_admitted_envelopes_once()
    {
        var fixture = CreateFixture(washPort: new ThrowingWashPort());
        using var host = fixture.CreateStartedHost(simulation => PreparePlatedProduct(fixture, simulation));
        var product = Assert.Single(fixture.Simulation.Snapshot().Items, item => item.IsProduct);
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "order", CookingRecipeOperation.SubmitOrder,
            item: product.Id, order: new OrderId("order-1"), expectedVersion: product.Version)).Accepted);
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "remaining", CookingRecipeOperation.Pickup,
            item: new ItemId("missing"), expectedVersion: 1, player: new PlayerId("z"))).Accepted);
        Assert.True(host.TryEnqueue(fixture.Envelope(2, "future", CookingRecipeOperation.Pickup,
            item: new ItemId("future"), expectedVersion: 1)).Accepted);

        Assert.Throws<InvalidOperationException>(host.Tick);
        Assert.True(host.IsFaulted);
        Assert.Equal(3, host.FinalDispositionHistory.Count);
        Assert.Equal(3, host.FinalDispositionHistory.Select(value =>
            (value.Envelope.LevelScope, value.Envelope.Command.Player, value.Envelope.Command.Command)).Distinct().Count());
        Assert.All(host.FinalDispositionHistory, value => Assert.Equal(CookingLevelDispositionKind.Cancelled, value.Kind));
        Assert.Contains(host.FinalDispositionHistory, value => value.Reason == "command-submission-fault");
        Assert.Contains(host.FinalDispositionHistory, value => value.Reason == "command-lane-fault");
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Fixed_step_failure_after_accepted_command_preserves_the_command_effect_and_final_history()
    {
        var fixture = CreateFixture(new ThrowingAllocator(), requiredTicks: 1);
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddIngredient(fixture.Ingredient, fixture.Raw, fixture.Player));
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "start", CookingRecipeOperation.StartProcess,
            recipe: fixture.Recipe, item: fixture.Ingredient, station: fixture.Station, expectedVersion: 1)).Accepted);

        Assert.Throws<InvalidOperationException>(host.Tick);
        var process = Assert.Single(fixture.Simulation.Snapshot().Processes);
        Assert.Equal(0, process.ElapsedTicks);
        Assert.Null(fixture.Simulation.ItemInHand(fixture.Player));
        var command = Assert.Single(fixture.Simulation.EventHistory);
        Assert.Equal("process-started", command.Summary);
        Assert.Equal(0, fixture.Simulation.LogicalTick);
        Assert.Equal(0, host.HostFrameSequence);
        var disposition = Assert.Single(host.FinalDispositionHistory);
        Assert.Equal(CookingLevelDispositionKind.Executed, disposition.Kind);
        Assert.Equal(CookingRecipeOutcome.Accepted, disposition.Result!.Outcome);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Successful_successor_replaces_level_and_reports_source_generation_metadata()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost();
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var oldLevel = host.Level;
        var oldDriver = host.Driver;

        var successor = host.CreateSuccessor(new LevelId("level-2"), 2);
        Assert.True(successor.Accepted);
        Assert.Equal(CookingLevelOutcome.Success, successor.SourceOutcome);
        Assert.True(successor.SourceGameplayClosed);
        Assert.True(successor.SourceVersionAfter > successor.SourceVersionBefore);
        Assert.Equal("level-2", successor.NewScope!.Level.Value);
        Assert.Same(successor.Lifecycle, host.Lifecycle);
        Assert.Same(host.Level, host.RestaurantRuntime.GetComponent<CookingLevelComponent>());
        Assert.True(oldLevel.IsDisposed);
        Assert.True(oldDriver.IsDisposed);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void H02_successful_successor_keeps_the_kitchen_and_stays_created()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
        {
            simulation.OpenOrder(new OrderId("order-1"), new OrderTemplateId("order-template"));
            simulation.AddIngredient(fixture.Ingredient, fixture.Raw, fixture.Player);
            Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(fixture.Command(100, "setup-start",
                CookingRecipeOperation.StartProcess, recipe: fixture.Recipe, item: fixture.Ingredient,
                station: fixture.Station, expectedVersion: 1)).Outcome);
        });
        var before = fixture.Simulation.ExportCheckpoint();
        Assert.NotEmpty(before.Orders);
        Assert.NotEmpty(before.Processes);
        Assert.True(host.Tick().Accepted);
        var frameAfterPlay = host.HostFrameSequence;
        var ingredient = fixture.Ingredient;

        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var successor = host.CreateSuccessor(new LevelId("level-2"), 2);

        Assert.True(successor.Accepted, successor.Reason);
        Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        Assert.Equal(1, successor.RetainedProcessCount);
        Assert.Equal(1, successor.ClearedOrderCount);
        Assert.True(host.HostFrameSequence >= frameAfterPlay);
        var handed = fixture.Simulation.ExportCheckpoint();
        Assert.Contains(handed.Items, item => item.Id == ingredient);
        Assert.NotEmpty(handed.Processes);
        Assert.Empty(handed.Orders);
        Assert.Empty(handed.Settlements);
        Assert.Equal(0, handed.LogicalTick);
        Assert.Equal(CookingLevelFrameReason.LevelNotRunning, host.Tick().Reason);
        Assert.Equal(CookingLevelAdmissionReason.LevelNotRunning,
            host.TryEnqueue(fixture.Envelope(1, "early", CookingRecipeOperation.Pickup, item: ingredient, expectedVersion: 1)).Reason);

        var preparation = fixture.Preparation with { Level = new LevelId("level-2") };
        Assert.True(host.Prepare(preparation).Accepted);
        Assert.True(host.Start().Accepted);
        Assert.Equal(CookingLevelState.Running, host.Lifecycle.State);
        Assert.Contains(fixture.Simulation.ExportCheckpoint().Items, item => item.Id == ingredient);
        Assert.Empty(fixture.Simulation.ExportCheckpoint().Orders);
        Assert.True(host.Tick().Accepted);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void F02_success_cannot_retry_and_running_retry_is_rejected()
    {
        var content = EmptyContent();
        var running = CreateFixture();
        using var runningHost = running.CreateStartedHost(simulation =>
            simulation.AddIngredient(running.Ingredient, running.Raw, running.Player));
        var before = running.Simulation.ExportCheckpoint().CanonicalText();
        var rejected = runningHost.CreateRetry(2, content);
        Assert.False(rejected.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.InvalidState.ToString(), rejected.Reason);
        Assert.Equal(before, running.Simulation.ExportCheckpoint().CanonicalText());
        Assert.False(runningHost.Lifecycle.HasCreatedNextGeneration);
        runningHost.Dispose();

        var success = CreateFixture();
        using var successHost = success.CreateStartedHost();
        Assert.True(successHost.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(successHost.CompleteEnd().Accepted);
        var wrongOutcome = successHost.CreateRetry(2, content);
        Assert.False(wrongOutcome.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.RetryRequiresFailedOutcome.ToString(), wrongOutcome.Reason);
        Assert.False(successHost.Lifecycle.HasCreatedNextGeneration);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void H03_running_level_cannot_hand_off()
    {
        var fixture = CreateFixture();
        using var host = fixture.CreateStartedHost(simulation =>
            simulation.AddIngredient(fixture.Ingredient, fixture.Raw, fixture.Player));
        var before = fixture.Simulation.ExportCheckpoint().CanonicalText();

        var successor = host.CreateSuccessor(new LevelId("level-2"), 2);

        Assert.False(successor.Accepted);
        Assert.Equal(CookingLevelLifecycleReason.InvalidState.ToString(), successor.Reason);
        Assert.Equal(before, fixture.Simulation.ExportCheckpoint().CanonicalText());
        Assert.Equal(CookingLevelState.Running, host.Lifecycle.State);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Successor_install_failure_rolls_back_partial_tree_keeps_old_binding_and_faults_host()
    {
        var injector = new OneShotFailureInjector(CookingLevelEtHostFailurePoint.DriverCreated, triggerOnCall: 2);
        var fixture = CreateFixture(failureInjector: injector);
        using var host = fixture.CreateStartedHost();
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var oldBinding = host.Binding;
        var oldLifecycle = host.Lifecycle;
        var sourceBefore = oldLifecycle.Snapshot();

        Assert.Throws<InvalidOperationException>(() => host.CreateSuccessor(new LevelId("level-2"), 2));
        Assert.True(host.IsFaulted);
        Assert.Equal(oldBinding, host.Binding);
        Assert.Same(oldLifecycle, host.Lifecycle);
        Assert.Equal(sourceBefore, oldLifecycle.Snapshot());
        Assert.False(oldLifecycle.HasCreatedNextGeneration);
        Assert.True(host.Level.IsDisposed);
        Assert.Null(host.RestaurantRuntime.GetComponent<CookingLevelComponent>());
        Assert.Throws<InvalidOperationException>(() => host.CreateSuccessor(new LevelId("level-3"), 3));
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Start_binding_failure_destroys_level_driver_closes_gameplay_and_faults_host()
    {
        var injector = new OneShotFailureInjector(CookingLevelEtHostFailurePoint.BeforeSimulationPublish, triggerOnCall: 1);
        var fixture = CreateFixture(failureInjector: injector);
        using var host = fixture.CreateHost();
        Assert.True(host.Prepare(fixture.Preparation).Accepted);
        var level = host.Level;
        var driver = host.Driver;

        Assert.Throws<InvalidOperationException>(host.Start);
        Assert.True(host.IsFaulted);
        Assert.True(level.IsDisposed);
        Assert.True(driver.IsDisposed);
        Assert.Null(host.RestaurantRuntime.GetComponent<CookingLevelComponent>());
        Assert.Equal(CookingLevelState.Ended, host.Lifecycle.State);
        Assert.Equal(CookingLevelOutcome.Aborted, host.Lifecycle.Outcome);
        Assert.Throws<InvalidOperationException>(() => host.TryEnqueue(fixture.Envelope(1, "late",
            CookingRecipeOperation.Pickup, item: fixture.Ingredient, expectedVersion: 1)));
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Same_simulation_cannot_be_owned_by_legacy_and_level_hosts()
    {
        var fixture = CreateFixture();
        using var levelHost = fixture.CreateStartedHost();
        var failure = Assert.Throws<InvalidOperationException>(() => new CookingRecipeTickHost(fixture.Simulation));
        Assert.Contains("cannot be driven", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Failed_second_host_construction_does_not_bind_or_poison_its_lifecycle()
    {
        var activeFixture = CreateFixture();
        using var activeHost = activeFixture.CreateHost();
        var rejectedFixture = CreateFixture();

        Assert.Throws<InvalidOperationException>(() => rejectedFixture.CreateHost());

        Assert.True(rejectedFixture.Lifecycle.BeginPreparation(rejectedFixture.Preparation).Accepted);
        Assert.True(rejectedFixture.Lifecycle.CompletePreparation().Accepted);
        Assert.True(rejectedFixture.Lifecycle.Start().Accepted);
        Assert.True(rejectedFixture.Lifecycle.TryGetGameplay(out var gameplay));
        gameplay.AddWorldIngredient(rejectedFixture.Ingredient, rejectedFixture.Raw, "spawn");
        Assert.Equal(CookingRecipeOutcome.Accepted, gameplay.Submit(rejectedFixture.Command(1, "pickup",
            CookingRecipeOperation.Pickup, item: rejectedFixture.Ingredient, expectedVersion: 1)).Outcome);
        Assert.True(rejectedFixture.Lifecycle.BeginEnd(CookingLevelOutcome.Aborted).Accepted);
        Assert.True(rejectedFixture.Lifecycle.CompleteEnd().Accepted);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Level_host_rejects_an_already_owned_simulation_without_closing_or_rebinding_it()
    {
        var fixture = CreateFixture();
        var shared = fixture.EnsureSimulation();
        var existingOwner = new object();
        CookingSimulationHostOwnership.Acquire(shared, existingOwner);
        try
        {
            using var levelHost = fixture.CreateHost();
            Assert.True(levelHost.Prepare(fixture.Preparation).Accepted);

            var start = levelHost.Start();

            Assert.False(start.Accepted);
            Assert.Equal(CookingLevelLifecycleReason.GameplayInitializationFailed.ToString(), start.Reason);
            Assert.Equal(CookingLevelState.Ready, levelHost.Lifecycle.State);
            shared.AddWorldIngredient(fixture.Ingredient, fixture.Raw, "spawn");
            Assert.Equal(CookingRecipeOutcome.Accepted, shared.Submit(fixture.Command(1, "pickup",
                CookingRecipeOperation.Pickup, item: fixture.Ingredient, expectedVersion: 1)).Outcome);
        }
        finally
        {
            CookingSimulationHostOwnership.Release(shared, existingOwner);
        }
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Wash_port_reentry_is_rejected_and_faults_command_lane()
    {
        var port = new ReenteringWashPort();
        var fixture = CreateFixture(washPort: port);
        using var host = fixture.CreateStartedHost(simulation => PreparePlatedProduct(fixture, simulation));
        port.Callback = () => host.Tick();
        var product = Assert.Single(fixture.Simulation.Snapshot().Items, item => item.IsProduct);
        Assert.True(host.TryEnqueue(fixture.Envelope(1, "order", CookingRecipeOperation.SubmitOrder,
            item: product.Id, order: new OrderId("order-1"), expectedVersion: product.Version)).Accepted);

        Assert.Throws<InvalidOperationException>(host.Tick);
        Assert.True(host.IsFaulted);
        Assert.IsType<InvalidOperationException>(port.ReentryFailure);
        Assert.Equal("command-submission-fault", Assert.Single(host.FinalDispositionHistory).Reason);
    }

    private static void PreparePlatedProduct(Fixture fixture, CookingRecipeSimulation simulation)
    {
        Assert.True(simulation.OpenOrder(new OrderId("order-1"), new OrderTemplateId("order-template")).Accepted);
        simulation.AddIngredient(fixture.Ingredient, fixture.Raw, fixture.Player);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(fixture.Command(100, "setup-start",
            CookingRecipeOperation.StartProcess, recipe: fixture.Recipe, item: fixture.Ingredient,
            station: fixture.Station, expectedVersion: 1)).Outcome);
        var process = Assert.Single(simulation.Snapshot().Processes);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(fixture.Command(101, "setup-progress",
            CookingRecipeOperation.AdvanceTicks, process: process.Id, tickCount: 3)).Outcome);
        var product = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(fixture.Command(102, "setup-pickup",
            CookingRecipeOperation.Pickup, item: product.Id, expectedVersion: product.Version)).Outcome);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(fixture.Command(103, "setup-put-in",
            CookingRecipeOperation.PutIn, item: product.Id, container: fixture.Container,
            expectedVersion: product.Version + 1)).Outcome);
    }

    private static CookingContent EmptyContent() =>
        CookingContentCatalog.Load(new CookingContentDocument(
            "cooking-definition-v2",
            new[] { "heat" },
            new[]
            {
                new CookingContentItem("raw", new[] { "cook" }),
                new CookingContentItem("cooked", new[] { "cook" }),
                new CookingContentItem("plate", new[] { "cook" }, new CookingContentContainer(1, new[] { "cooked" })),
            },
            new[]
            {
                new CookingContentAppliance("stove", new[] { "heat" }),
                new CookingContentAppliance("counter", Array.Empty<string>()),
            },
            new[] { new CookingContentRecipe("soup", new[] { "raw" }, "cooked", "boil", "heat", 3) },
            new[] { new CookingContentOrderTemplate("order-template", "soup", "plate") },
            Array.Empty<CookingContentSupplyEntry>()));

    private static void AssertRelation<TAttribute, TEntity, TParent>()
        where TAttribute : Attribute
    {
        var attribute = Assert.Single(typeof(TEntity).GetCustomAttributes(typeof(TAttribute), false));
        var field = typeof(TAttribute).GetField("Type") ?? typeof(TAttribute).GetField("type")!;
        Assert.Equal(typeof(TParent), field.GetValue(attribute));
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(TEntity)));
    }

    private static Fixture CreateFixture(
        ICookingProductIdAllocator? productIdAllocator = null,
        ICookingBowlWashingPort? washPort = null,
        int requiredTicks = 3,
        ICookingLevelEtHostFailureInjector? failureInjector = null)
    {
        var matchScope = new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match"));
        var levelScope = new CookingLevelScope(matchScope, new RestaurantRuntimeId(1), new LevelId("level-1"), 1);
        var player = new PlayerId("p1");
        var ingredient = new ItemId("ingredient-1");
        var raw = new DefinitionId("raw");
        var cooked = new DefinitionId("cooked");
        var station = new StationSlotId("stove");
        var counter = new StationSlotId("counter");
        var recipe = new RecipeId("soup");
        var configuration = BuildConfiguration(station, raw, cooked, recipe);
        var orderTemplate = new OrderTemplateId("order-template");
        var simulationFixture = new CookingRecipeFixture(
            matchScope,
            new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [player] = new(player, new HashSet<string> { "cook" }, new HashSet<string> { station.Value, "counter" }),
            },
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [raw] = new(raw, new HashSet<string> { "cook" }),
                [cooked] = new(cooked, new HashSet<string> { "cook" }),
                [PlateDefinition] = new(PlateDefinition, new HashSet<string> { "cook" },
                    new CookingItemContainerCapability(1, new HashSet<DefinitionId> { cooked })),
            },
            new Dictionary<StationSlotId, CookingApplianceDefinition>
            {
                [station] = new(station, new HashSet<string> { "heat" }),
                [counter] = new(counter, new HashSet<string>()),
            },
            new Dictionary<RecipeId, CookingRecipeDefinition>
            {
                [recipe] = new(recipe, new[] { raw }, cooked, new ProcessId("boil"), "heat", requiredTicks),
            },
            washableContainerDefinitions: washPort is null
                ? null
                : new HashSet<DefinitionId> { PlateDefinition },
            cleanContainerSupply: washPort is null
                ? null
                : new Dictionary<DefinitionId, int> { [PlateDefinition] = 1 },
            orderTemplates: new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>
            {
                [orderTemplate] = new(orderTemplate, recipe, PlateDefinition),
            });
        var factory = new Factory(simulationFixture, productIdAllocator, washPort);
        var lifecycle = new CookingLevelLifecycle(levelScope, configuration, factory);
        var preparation = new CookingLevelPreparation(levelScope.Level, new MapId("map"),
            new CookingLogicalLayout(new LayoutId("layout"), new[] { station }, new[] { PlateDefinition }),
            configuration.Identity);
        return new Fixture(levelScope, player, ingredient, raw, station, recipe, Plate, lifecycle, preparation, factory,
            failureInjector);
    }

    private static CookingConfigurationSnapshot BuildConfiguration(
        StationSlotId station,
        DefinitionId raw,
        DefinitionId cooked,
        RecipeId recipe)
    {
        var registry = new CookingConfigurationRegistry();
        var result = registry.Submit(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[]
            {
                new CookingItemDefinition(raw, new HashSet<string> { "cook" }),
                new CookingItemDefinition(cooked, new HashSet<string> { "cook" }),
                new CookingItemDefinition(PlateDefinition, new HashSet<string> { "cook" },
                    new CookingItemContainerCapability(1, new HashSet<DefinitionId> { cooked })),
            },
            new[] { new CookingApplianceDefinition(station, new HashSet<string> { "heat" }) },
            new[] { new CookingRecipeDefinition(recipe, new[] { raw }, cooked, new ProcessId("boil"), "heat", 3) }));
        Assert.True(result.Accepted);
        return registry.Current!;
    }

    private sealed class Fixture(
        CookingLevelScope levelScope,
        PlayerId player,
        ItemId ingredient,
        DefinitionId raw,
        StationSlotId station,
        RecipeId recipe,
        ItemId container,
        CookingLevelLifecycle lifecycle,
        CookingLevelPreparation preparation,
        Factory factory,
        ICookingLevelEtHostFailureInjector? failureInjector)
    {
        public CookingLevelScope LevelScope { get; } = levelScope;
        public PlayerId Player { get; } = player;
        public ItemId Ingredient { get; } = ingredient;
        public DefinitionId Raw { get; } = raw;
        public StationSlotId Station { get; } = station;
        public RecipeId Recipe { get; } = recipe;
        public ItemId Container { get; } = container;
        public CookingLevelLifecycle Lifecycle { get; } = lifecycle;
        public CookingLevelPreparation Preparation { get; } = preparation;
        public CookingRecipeSimulation Simulation => factory.Simulation!;

        public CookingRecipeSimulation EnsureSimulation() => factory.EnsureSimulation(LevelScope);

        public CookingLevelEtHost CreateHost(int queueCapacity = 256) =>
            new(Lifecycle, queueCapacity, failureInjector);

        public CookingLevelEtHost CreateStartedHost(int queueCapacity = 256)
        {
            var host = CreateHost(queueCapacity);
            Assert.True(host.Prepare(Preparation).Accepted);
            Assert.True(host.Start().Accepted);
            return host;
        }

        public CookingLevelEtHost CreateStartedHost(
            Action<CookingRecipeSimulation> initialize,
            int queueCapacity = 256)
        {
            var host = CreateHost(queueCapacity);
            Assert.True(host.Prepare(Preparation).Accepted);
            initialize(factory.EnsureSimulation(LevelScope));
            Assert.True(host.Start().Accepted);
            return host;
        }

        public CookingRecipeCommand Command(
            long batch,
            string command,
            CookingRecipeOperation operation,
            RecipeId? recipe = null,
            ProcessId? process = null,
            ItemId? item = null,
            StationSlotId? station = null,
            ItemId? container = null,
            OrderId? order = null,
            int expectedVersion = 0,
            int tickCount = 0,
            PlayerId? player = null) =>
            new(LevelScope.MatchScope, batch, player ?? Player, new RecipeCommandId(command), operation,
                recipe, process, item, station, container, order, expectedVersion, tickCount);

        public CookingLevelCommandEnvelope Envelope(
            long batch,
            string command,
            CookingRecipeOperation operation,
            RecipeId? recipe = null,
            ProcessId? process = null,
            ItemId? item = null,
            StationSlotId? station = null,
            ItemId? container = null,
            OrderId? order = null,
            int expectedVersion = 0,
            int tickCount = 0,
            PlayerId? player = null) =>
            new(LevelScope, Command(batch, command, operation, recipe, process, item, station, container, order,
                expectedVersion, tickCount, player), "connection", command);
    }

    private sealed class Factory(
        CookingRecipeFixture fixture,
        ICookingProductIdAllocator? productIdAllocator = null,
        ICookingBowlWashingPort? washPort = null) : ICookingLevelGameplayFactory
    {
        private CookingRecipeSimulation? _precreated;
        public CookingRecipeSimulation? Simulation { get; private set; }

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Assert.Equal(scope.MatchScope, fixture.Scope);
            if (_precreated is not null)
            {
                Simulation = _precreated;
                _precreated = null;
                return Simulation;
            }
            return Simulation = NewSimulation();
        }

        public CookingRecipeSimulation EnsureSimulation(CookingLevelScope scope)
        {
            Assert.Equal(scope.MatchScope, fixture.Scope);
            return _precreated ??= NewSimulation();
        }

        private CookingRecipeSimulation NewSimulation()
        {
            var simulation = new CookingRecipeSimulation(fixture, productIdAllocator, washPort);
            simulation.AddItem(Plate, PlateDefinition, ItemLocation.Station(new StationSlotId("counter")));
            return simulation;
        }
    }

    private sealed class ThrowingAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long productSequence) =>
            throw new InvalidOperationException("fixed-tick-allocation-failure");
    }

    private static FingerprintEvidenceScope CreateFingerprintEvidence(string commandId) => new(commandId);

    private static void AppendFingerprintEvidence(FingerprintEvidenceScope evidence, string testId,
        CookingLevelCommandEnvelope envelope, string commandLabel, string assertionSummary)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(evidence.Path)!);
        var canonical = Convert.ToHexString(CookingCommandFingerprint.CanonicalBytes(envelope));
        File.AppendAllText(evidence.Path, JsonSerializer.Serialize(new
        {
            testId,
            commandLabel,
            operation = envelope.Command.Operation.ToString(),
            recipe = envelope.Command.Recipe?.Value,
            process = envelope.Command.Process?.Value,
            item = envelope.Command.Item?.Value,
            station = envelope.Command.Station?.Value,
            canonicalBytesHex = canonical,
            sha256 = CookingCommandFingerprint.Create(envelope).Value,
            wellFormed = CookingRecipeCommandValidation.IsWellFormed(envelope.Command),
            simulationJsonFingerprint = JsonSerializer.Serialize(envelope.Command),
            assertionSummary,
            runner = "dotnet test AbilityKit.ET.Runtime.Tests",
            timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
        }) + Environment.NewLine, Encoding.UTF8);
    }

    private sealed class FingerprintEvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public FingerprintEvidenceScope(string commandId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_FINGERPRINT_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.ET.Runtime.Tests", "fingerprint");
            _directory = System.IO.Path.Combine(root, commandId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "cooking-command-fingerprint.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && System.IO.Directory.Exists(_directory))
                System.IO.Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class ThrowingWashPort : ICookingBowlWashingPort
    {
        public void RequestWash(ItemId bowl, DefinitionId definition) =>
            throw new InvalidOperationException("wash-port-failure");
    }

    private sealed class ReenteringWashPort : ICookingBowlWashingPort
    {
        public Action Callback { get; set; } = null!;
        public Exception? ReentryFailure { get; private set; }

        public void RequestWash(ItemId bowl, DefinitionId definition)
        {
            ReentryFailure = Record.Exception(Callback);
            if (ReentryFailure is not null)
                throw ReentryFailure;
        }
    }

    private sealed class OneShotFailureInjector(
        CookingLevelEtHostFailurePoint point,
        int triggerOnCall) : ICookingLevelEtHostFailureInjector
    {
        private int _calls;

        public void ThrowIfRequested(CookingLevelEtHostFailurePoint candidate)
        {
            if (candidate == point && ++_calls == triggerOnCall)
                throw new InvalidOperationException($"injected-{point}");
        }
    }
}
