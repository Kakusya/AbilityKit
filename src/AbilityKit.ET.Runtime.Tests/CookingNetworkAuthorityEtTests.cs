using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingNetworkAuthorityEtTests
{
    private static readonly PlayerId A = new("a"), Z = new("z");
    private static readonly DefinitionId Raw = new("raw"), Cooked = new("cooked");
    private static readonly ItemId Shared = new("shared"), Work = new("work");
    private static readonly StationSlotId Board = new("board");
    private static readonly CookingScope Scope = new(new("network"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);

    private sealed class Factory : ICookingLevelGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public Action? OnCreate { get; set; }
        private readonly CookingRecipeFixture _fixture;
        private readonly ICookingProductIdAllocator? _allocator;
        private readonly CookingMajorProgress? _progress;
        public Factory(ICookingProductIdAllocator? allocator = null, CookingMajorProgress? progress = null)
        {
            _allocator = allocator;
            _progress = progress;
            var caps = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, caps), new CookingItemDefinition(Cooked, caps),
                new CookingItemDefinition(new("plate"), caps, new(1, new HashSet<DefinitionId> { Cooked })) };
            var stations = new[] { new CookingApplianceDefinition(Board, new HashSet<string> { "work" }) };
            var recipes = new[] { new CookingRecipeDefinition(new("manual"), new[] { Raw }, Cooked, new("work"), "work", 4,
                Execution: CookingRecipeExecutionKind.Manual) };
            var registry = new CookingConfigurationRegistry();
            var orders = new[] { new CookingOrderTemplateDefinition(new("order"), new("manual"), new("plate")) };
            Assert.True(registry.Submit(new(new[] { "work" }, items, stations, recipes, OrderTemplates: orders)).Accepted);
            Config = registry.Current!;
            _fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> {
                [A] = new(A, caps, new HashSet<string> { Board.Value }), [Z] = new(Z, caps, new HashSet<string> { Board.Value }) },
                items.ToDictionary(i => i.Id), stations.ToDictionary(s => s.Station), recipes.ToDictionary(r => r.Id), orderTemplates: orders.ToDictionary(o => o.Id));
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            OnCreate?.Invoke();
            Simulation = new(_fixture, _allocator); Simulation.AddItem(Shared, Raw, ItemLocation.World("shared"));
            Simulation.AddItem(Work, Raw, ItemLocation.Station(Board));
            if (_progress is not null) Simulation.UseMajorProgress(_progress);
            return Simulation;
        }
        public CookingLevelEtHost Host(bool start = true, bool front = false)
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(Level, Config, this));
            try
            {
            if (front) host.UseFrontOfHouse(new(new(1, 20, 2, 1, 2, 2, 10)), new CookingFrontOfHouseMenu(new[] { new OrderTemplateId("order") }));
            if (start)
            {
                Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board }, Array.Empty<DefinitionId>()), Config.Identity)).Accepted);
                Assert.True(host.Start().Accepted);
            }
            return host;
            }
            catch { host.Dispose(); throw; }
        }
    }
    private static CookingNetworkMappedCommand Mapped(CookingLevelEtHost host, PlayerId player, string id, long ordinal = 1,
        string source = "peer-1-generation-1", string? correlation = null, CookingRecipeOperation operation = CookingRecipeOperation.Pickup,
        ItemId? item = null, long? batch = null, ProcessId? process = null) => new(new(Level,
            new(Scope, batch ?? host.HostFrameSequence + 1, player, new(id), operation,
                Item: operation is CookingRecipeOperation.Pickup or CookingRecipeOperation.StartProcess ? item ?? Shared : null,
                Station: operation == CookingRecipeOperation.StartProcess ? Board : null,
                Process: process, ExpectedItemVersion: operation is CookingRecipeOperation.ContinueProcess or CookingRecipeOperation.StopProcess ? 0 : item == Work ? host.Observe().Recipe!.Items.Single(i => i.Id == Work).Version : 1),
            source, correlation ?? id), ordinal, "wire-" + id);

    [Fact]
    public void Trusted_ordinal_wins_opposing_player_race_in_one_tick()
    {
        var f = new Factory(); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        var frame = port.ConsumeFrame(new[] { Mapped(host, Z, "z-first", 1), Mapped(host, A, "a-second", 2) }, Array.Empty<PlayerId>());
        Assert.True(frame.Accepted); Assert.Equal(1, host.HostFrameSequence); Assert.Equal(1, f.Simulation.Snapshot().LogicalTick);
        Assert.Equal(Z.Value, f.Simulation.Snapshot().Items.Single(i => i.Id == Shared).Location.OwnerId);
        Assert.Equal(CookingRecipeOutcome.Accepted, frame.Dispositions.Single(d => d.Participant == Z).Result!.Outcome);
        Assert.NotEqual(CookingRecipeOutcome.Accepted, frame.Dispositions.Single(d => d.Participant == A).Result!.Outcome);
    }

    [Fact]
    public void Default_singleplayer_still_sorts_by_player()
    {
        var f = new Factory(); using var host = f.Host();
        foreach (var mapped in new[] { Mapped(host, Z, "z-first"), Mapped(host, A, "a-second", 2) })
            Assert.True(host.TryEnqueue(new(Level, mapped.Envelope.Command, mapped.Envelope.SourceConnectionId, mapped.Envelope.CorrelationId)).Accepted);
        host.Tick(); Assert.Equal(A.Value, f.Simulation.Snapshot().Items.Single(i => i.Id == Shared).Location.OwnerId);
    }

    [Fact]
    public void Partial_source_cancel_keeps_duplicate_group_and_true_execution_receipt()
    {
        var f = new Factory(); using var host = f.Host(); _ = new CookingNetworkAuthorityAdapter(host);
        var first = Mapped(host, A, "same", source: "old-generation-1", correlation: "old");
        var live = first with { Envelope = first.Envelope with { SourceConnectionId = "new-generation-2", CorrelationId = "live" } };
        Assert.True(host.TryEnqueueNetwork(first).Accepted); Assert.True(host.TryEnqueueNetwork(live).Accepted);
        var before = f.Simulation.ExportCheckpoint().CanonicalText();
        var cancelled = host.CancelPendingSources(new[] { new CookingNetworkCancellation("old-generation-1", 1, CookingNetworkCallerCancellationReason.ConnectionClosed) });
        Assert.True(cancelled.Accepted); Assert.Equal("old", Assert.Single(cancelled.CancelledCallers).CorrelationId);
        Assert.Equal(before, f.Simulation.ExportCheckpoint().CanonicalText()); Assert.Empty(host.DrainNewTerminalDispositions());
        Assert.Equal("live", Assert.Single(host.Tick().Dispositions).Envelope.CorrelationId);
        var repeated = live with { Envelope = live.Envelope with { CorrelationId = "repeat" } };
        var result = host.TryEnqueueNetwork(repeated);
        Assert.True(result.Accepted); Assert.Equal(CookingLevelDispositionKind.Duplicate, result.TerminalDisposition!.Kind);
        Assert.Equal(CookingRecipeOutcome.Accepted, result.TerminalDisposition.Result!.Outcome);
    }

    [Fact]
    public void All_source_cancel_has_no_fake_logical_terminal_and_new_generation_is_not_cancelled()
    {
        var f = new Factory(); using var host = f.Host(); _ = new CookingNetworkAuthorityAdapter(host);
        Assert.True(host.TryEnqueueNetwork(Mapped(host, A, "old", source: "connection-generation-1")).Accepted);
        var before = f.Simulation.ExportCheckpoint().CanonicalText();
        Assert.Single(host.CancelPendingSources(new[] { new CookingNetworkCancellation("connection-generation-1", 1, CookingNetworkCallerCancellationReason.ConnectionClosed) }).CancelledCallers);
        Assert.Equal(0, host.PendingCommandIdentityCount); Assert.Equal(before, f.Simulation.ExportCheckpoint().CanonicalText());
        Assert.Empty(host.DispositionHistory);
        Assert.True(host.TryEnqueueNetwork(Mapped(host, A, "new", source: "connection-generation-2")).Accepted);
        Assert.Empty(host.CancelPendingSources(new[] { new CookingNetworkCancellation("connection-generation-1", 1, CookingNetworkCallerCancellationReason.ConnectionClosed) }).CancelledCallers);
        Assert.Equal(CookingRecipeOutcome.Accepted, Assert.Single(host.Tick().Dispositions).Result!.Outcome);
    }

    [Fact]
    public void Pending_conflict_notifies_prior_and_new_caller_without_execution()
    {
        var f = new Factory(); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        var first = Mapped(host, A, "identity", correlation: "first");
        var conflict = first with { Envelope = first.Envelope with { CorrelationId = "conflict", Command = first.Envelope.Command with { Item = Work } } };
        var frame = port.ConsumeFrame(new[] { first, conflict }, Array.Empty<PlayerId>());
        Assert.Equal(2, frame.Dispositions.Count); Assert.All(frame.Dispositions, d => Assert.Equal(CookingNetworkDispositionKind.Conflicted, d.Kind));
        Assert.Equal(LocationKind.WorldPosition, f.Simulation.Snapshot().Items.Single(i => i.Id == Shared).Location.Kind);
        Assert.Empty(host.DrainNewTerminalDispositions());
    }

    [Fact]
    public void Created_and_paused_capture_are_readonly_full_state_not_resumable_images()
    {
        var f = new Factory(); using var host = f.Host(start: false); var port = new CookingNetworkAuthorityAdapter(host);
        var created = port.CaptureFullState(); Assert.True(created.Accepted); Assert.Null(created.State!.FullRecipe);
        Assert.Equal(CookingNetworkCheckpointUnavailableReason.NotInitialized, created.State.CheckpointUnavailableReason);
        Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board }, Array.Empty<DefinitionId>()), f.Config.Identity)).Accepted);
        Assert.True(host.Start().Accepted); host.Tick(); Assert.True(host.Pause().Accepted);
        var before = f.Simulation.ExportCheckpoint().CanonicalText(); var frame = host.HostFrameSequence;
        var paused = port.CaptureFullState(); Assert.True(paused.Accepted); Assert.Equal(before, paused.State!.FullRecipe!.CanonicalText());
        Assert.Null(paused.State.ResumableCheckpoint); Assert.Equal(CookingNetworkCheckpointUnavailableReason.LevelPaused, paused.State.CheckpointUnavailableReason);
        Assert.Equal(frame, host.HostFrameSequence); Assert.Equal(before, port.CaptureFullState().State!.FullRecipe!.CanonicalText());
    }

    [Fact]
    public void Disconnect_cleanup_precedes_user_ordinal_one_and_releases_manual_worker()
    {
        var f = new Factory(); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        Assert.True(port.ConsumeFrame(new[] { Mapped(host, A, "start", operation: CookingRecipeOperation.StartProcess, item: Work) }, Array.Empty<PlayerId>()).Accepted);
        var process = Assert.Single(f.Simulation.Snapshot().Processes); Assert.Equal(1, process.ElapsedTicks);
        var frame = port.ConsumeFrame(new[] { Mapped(host, Z, "continue", operation: CookingRecipeOperation.ContinueProcess, process: process.Id) }, new[] { A });
        Assert.Equal(CookingRecipeOutcome.Accepted, frame.Dispositions.Single(d => d.Participant == Z).Result!.Outcome);
        Assert.Equal(Z, Assert.Single(f.Simulation.Snapshot().Processes).ActiveWorker); Assert.Equal(2, Assert.Single(f.Simulation.Snapshot().Processes).ElapsedTicks);
    }

    private sealed class ThrowingAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long productSequence) => throw new IOException("actual allocation fault");
    }

    [Fact]
    public void Actual_fixed_tick_fault_capture_returns_failure_null_and_dispose_releases_owner()
    {
        var f = new Factory(new ThrowingAllocator());
        using (var host = f.Host())
        {
            var port = new CookingNetworkAuthorityAdapter(host);
            port.ConsumeFrame(new[] { Mapped(host, A, "start", operation: CookingRecipeOperation.StartProcess, item: Work) }, Array.Empty<PlayerId>());
            for (var i = 0; i < 3; i++) port.ConsumeFrame(Array.Empty<CookingNetworkMappedCommand>(), Array.Empty<PlayerId>());
            Assert.True(host.IsFaulted);
            var failed = port.CaptureFullState(); Assert.False(failed.Accepted);
            Assert.Equal(CookingNetworkCaptureReason.AuthorityFaulted, failed.Reason); Assert.Null(failed.State);
        }
        using var fresh = new Factory().Host(); Assert.False(fresh.IsFaulted);
    }

    [Fact]
    public void Mixed_batches_reject_before_admission_tick_or_cleanup()
    {
        var f = new Factory(); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        var before = port.CaptureFullState().State!.FullRecipe!.CanonicalText();
        var result = port.ConsumeFrame(new[] { Mapped(host, A, "one", batch: 1), Mapped(host, Z, "two", 2, batch: 2) }, Array.Empty<PlayerId>());
        Assert.False(result.Accepted); Assert.Equal(0, host.HostFrameSequence); Assert.Equal(0, host.PendingCommandIdentityCount);
        Assert.Equal(before, port.CaptureFullState().State!.FullRecipe!.CanonicalText());
    }

    [Fact]
    public void Full_capture_preserves_hidden_tombstone_allocator_and_front_while_paused()
    {
        var f = new Factory(); using var host = f.Host(front: true); var port = new CookingNetworkAuthorityAdapter(host);
        port.ConsumeFrame(new[] { Mapped(host, A, "pickup") }, Array.Empty<PlayerId>());
        var item = f.Simulation.Snapshot().Items.Single(i => i.Id == Shared);
        var discard = Mapped(host, A, "discard") with { Envelope = Mapped(host, A, "discard").Envelope with {
            Command = new(Scope, host.HostFrameSequence + 1, A, new("discard"), CookingRecipeOperation.DiscardItem, Item: Shared, ExpectedItemVersion: item.Version) } };
        Assert.Equal(CookingRecipeOutcome.Accepted, Assert.Single(port.ConsumeFrame(new[] { discard }, Array.Empty<PlayerId>()).Dispositions).Result!.Outcome);
        Assert.True(host.Pause().Accepted);
        var before = f.Simulation.ExportCheckpoint().CanonicalText();
        var capture = port.CaptureFullState().State!;
        Assert.DoesNotContain(capture.Observation.Items, i => i.Id == Shared);
        Assert.True(capture.FullRecipe!.Items.Single(i => i.Id == Shared).Removed);
        Assert.Equal(before, capture.FullRecipe.CanonicalText()); Assert.NotNull(capture.FullFront);
        var front = capture.FullFront!.CanonicalText();
        Assert.Equal(front, port.CaptureFullState().State!.FullFront!.CanonicalText());
        Assert.Null(capture.ResumableCheckpoint); Assert.Equal(CookingNetworkCheckpointUnavailableReason.LevelPaused, capture.CheckpointUnavailableReason);
    }

    [Fact]
    public void Paused_disconnect_defers_cleanup_until_resume_without_clock_or_worker_mutation()
    {
        var f = new Factory(); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        port.ConsumeFrame(new[] { Mapped(host, A, "start", operation: CookingRecipeOperation.StartProcess, item: Work) }, Array.Empty<PlayerId>());
        Assert.True(host.Pause().Accepted); var before = f.Simulation.ExportCheckpoint().CanonicalText(); var frame = host.HostFrameSequence;
        Assert.False(port.ConsumeFrame(Array.Empty<CookingNetworkMappedCommand>(), new[] { A }).Accepted);
        Assert.Equal(before, f.Simulation.ExportCheckpoint().CanonicalText()); Assert.Equal(frame, host.HostFrameSequence);
        Assert.True(port.ApplyControl(new(CookingNetworkControlKind.Resume, "owner-resume")).Accepted);
        Assert.True(port.ConsumeFrame(Array.Empty<CookingNetworkMappedCommand>(), Array.Empty<PlayerId>()).Accepted);
        var process = Assert.Single(f.Simulation.Snapshot().Processes); Assert.Null(process.ActiveWorker); Assert.Equal(1, process.ElapsedTicks);
    }

    [Fact]
    public void Cleanup_admission_failure_is_structured_and_participant_stays_unavailable_until_recovery()
    {
        var f = new Factory(); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        port.ConsumeFrame(new[] { Mapped(host, A, "start", operation: CookingRecipeOperation.StartProcess, item: Work) }, Array.Empty<PlayerId>());
        var failed = port.ConsumeFrame(new[] { Mapped(host, Z, "stale", batch: 1) }, new[] { A });
        Assert.False(failed.Accepted); Assert.Equal(CookingNetworkFrameReason.CleanupRejected, failed.Reason);
        Assert.Equal(A, Assert.Single(f.Simulation.Snapshot().Processes).ActiveWorker);
        var next = port.ConsumeFrame(new[] { Mapped(host, A, "blocked") }, Array.Empty<PlayerId>());
        Assert.Equal(CookingNetworkAdmissionReason.Unauthorized, next.Admissions.Single(a => a.CommandId.Value == "blocked").Reason);
        Assert.True(next.Accepted); Assert.Null(Assert.Single(f.Simulation.Snapshot().Processes).ActiveWorker);
        Assert.Equal(LocationKind.WorldPosition, f.Simulation.Snapshot().Items.Single(i => i.Id == Shared).Location.Kind);
    }

    [Fact]
    public void Notification_capacity_reserves_accepted_callers_and_conflict_newcomer_returns_immediately()
    {
        var f = new Factory(); using var host = f.Host(); _ = new CookingNetworkAuthorityAdapter(host);
        for (var group = 0; group < 256; group++)
            for (var caller = 0; caller < 8; caller++)
                Assert.True(host.TryEnqueueNetwork(Mapped(host, A, "id-" + group, group + 1,
                    source: "source-" + caller, correlation: "group-" + group + "-caller-" + caller)).Accepted);
        var conflict = Mapped(host, A, "id-0", source: "newcomer", correlation: "conflict", item: Work);
        var rejected = host.TryEnqueueNetwork(conflict);
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, rejected.Reason);
        Assert.Equal("conflict", rejected.TerminalDisposition!.Envelope.CorrelationId);
        Assert.Equal(CookingLevelAdmissionReason.QueueFull, host.TryEnqueueNetwork(Mapped(host, A, "fresh", 300, source: "fresh")).Reason);
        var drained = host.DrainNewTerminalDispositions(); Assert.Equal(8, drained.Count);
        Assert.All(drained, d => Assert.Equal(CookingLevelDispositionKind.Conflicted, d.Kind));
        Assert.DoesNotContain(drained, d => d.Envelope.SourceConnectionId == "newcomer");
        Assert.True(host.TryEnqueueNetwork(Mapped(host, A, "fresh", 300, source: "fresh")).Accepted);
    }

    [Fact]
    public void Trusted_major_progress_is_frozen_display_and_roundtrips_global_future_unlock_without_grant()
    {
        var progress = new CookingMajorProgress(); Assert.True(progress.EnableCookFaster().Accepted);
        Assert.True(progress.Unlock(new("future-not-in-current-config")).Accepted);
        Assert.True(progress.ChooseDecoration(new[] { new CookingStationReplacement(Board, new("future-board")) }).Accepted);
        var f = new Factory(progress: progress); using var host = f.Host(); var port = new CookingNetworkAuthorityAdapter(host);
        var before = f.Simulation.ExportCheckpoint().CanonicalText();
        var capture = port.CaptureFullState().State!; var display = capture.MajorProgress!;
        Assert.True(display.CookFaster); Assert.False(display.Locked);
        Assert.Equal("future-not-in-current-config", Assert.Single(display.Unlocks).Value);
        Assert.Equal(Board, Assert.Single(display.Decoration).From);
        Assert.DoesNotContain(capture.Observation.Items, i => i.DefinitionKey == "future-not-in-current-config");
        Assert.Equal(before, f.Simulation.ExportCheckpoint().CanonicalText());
        Assert.True(progress.Unlock(new("another-future-choice")).Accepted); progress.Lock();
        Assert.False(display.Locked); Assert.Single(display.Unlocks); Assert.True(port.CaptureFullState().State!.MajorProgress!.Locked);
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var json = System.Text.Json.JsonSerializer.Serialize(capture, options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<CookingNetworkAuthorityCapture>(json, options)!;
        Assert.Equal(display.CookFaster, restored.MajorProgress!.CookFaster);
        Assert.Equal(display.Unlocks, restored.MajorProgress.Unlocks); Assert.Equal(display.Decoration, restored.MajorProgress.Decoration);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!; node.AsObject().Remove("majorProgress");
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<CookingNetworkAuthorityCapture>(node.ToJsonString(), options));
    }

    [Fact]
    public void Reentrant_capture_in_real_lifecycle_factory_callback_is_busy_without_reading_intermediate_world()
    {
        var f = new Factory(); using var host = f.Host(start: false); var port = new CookingNetworkAuthorityAdapter(host);
        Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board }, Array.Empty<DefinitionId>()), f.Config.Identity)).Accepted);
        CookingNetworkCaptureResult? captured = null;
        f.OnCreate = () => captured = port.CaptureFullState();
        Assert.True(host.Start().Accepted); Assert.NotNull(captured);
        Assert.False(captured.Accepted); Assert.Equal(CookingNetworkCaptureReason.Busy, captured.Reason); Assert.Null(captured.State);
        Assert.True(port.CaptureFullState().Accepted);
    }
}
