using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingMajorBaselineEtTests
{
    private static readonly CookingScope Scope = new(new("generation"), new("world"), new("match"));
    private static readonly CookingLevelScope First = new(Scope, new(1), new("first"), 1);
    private static readonly LevelId Next = new("next");
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Prepared = new("prepared"), Box = new("box"), Pan = new("pan"), Cup = new("cup"), Machine = new("machine");
    private static readonly ItemId PanA = new("pan-a"), PanB = new("pan-b"), CupItem = new("cup-item");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly RecipeId Auto = new("auto"), Manual = new("manual");
    private sealed class Injector : ICookingLevelEtHostFailureInjector
    {
        public CookingLevelEtHostFailurePoint? Armed { get; set; }
        public void ThrowIfRequested(CookingLevelEtHostFailurePoint point)
        {
            if (Armed == point) { Armed = null; throw new InvalidOperationException("publication test failure"); }
        }
    }
    private class Factory : ICookingPreparationGameplayFactory
    {
        private readonly CookingItemDefinition[] _items;
        private readonly CookingApplianceDefinition[] _appliances;
        private readonly CookingRecipeDefinition[] _recipes;
        public CookingConfigurationSnapshot Config { get; }
        public CookingConfigurationCandidate Candidate { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public string? Failure { get; set; }
        public CookingRecipeSimulation? Foreign { get; set; }
        public int CreateCount { get; private set; }
        public Factory()
        {
            var caps = new HashSet<string> { "cook" };
            _items = new[] { new CookingItemDefinition(Raw, caps), new(Prepared, caps), new(Box, caps, new(3, new HashSet<DefinitionId> { Raw })),
                new(Pan, caps, new(1, new HashSet<DefinitionId> { Raw })), new(Cup, caps, new(1, new HashSet<DefinitionId> { Prepared })) };
            _appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
            _recipes = new[] { new CookingRecipeDefinition(Auto, new[] { Raw }, Prepared, new("auto-process"), "heat", 3,
                    Completion: CookingRecipeCompletionKind.RetainInputs, YieldPortions: 3, RequiredProcessingContainerDefinition: Pan),
                new(Manual, new[] { Raw }, Prepared, new("manual-process"), "heat", 3,
                    Completion: CookingRecipeCompletionKind.RetainInputs, Execution: CookingRecipeExecutionKind.Manual, YieldPortions: 3, RequiredProcessingContainerDefinition: Pan) };
            Candidate = new(new[] { "heat" }, _items, _appliances, _recipes, Spatial: Spatial(false)) { Supply = Supply(9) };
            var registry = new CookingConfigurationRegistry(); Assert.True(registry.Submit(Candidate).Accepted); Config = registry.Current!;
        }
        private static CookingSupplyConfiguration Supply(int amount) => new(new[] { new CookingSupplierDefinition("finite", "source", "receiving", Raw, Box, 3, 4, amount) });
        private static CookingSpatialConfiguration Spatial(bool next) => new(0, 0, 8000, 8000, 250, 7000,
            new[] { new CookingPlayerPose(Chef, next ? 3500 : 500, 1500, 1, 0) },
            new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 1500, 1500), new(LocationKind.WorldPosition, "receiving", 2500, 1500),
                new(LocationKind.WorldPosition, "storage", 3500, 1500), new(LocationKind.WorldPosition, "pan-a", 500, 1500),
                new(LocationKind.WorldPosition, "pan-b", 1500, 1500), new(LocationKind.WorldPosition, "cup", 500, 1500),
                new(LocationKind.StationSlot, Stove.Value, next ? 6500 : 4500, 1500) }, Array.Empty<CookingSpatialObstacle>());
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++; if (Failure == "factory-io") throw new IOException("factory IO test failure");
            if (Failure == "factory") throw new InvalidOperationException("factory test failure");
            if (Failure == "same") return Simulation;
            if (Foreign is not null) return Foreign;
            var caps = new HashSet<string> { "cook" }; var next = scope.Level == Next;
            var fixture = new CookingRecipeFixture(Failure == "wrong-match" ? new CookingScope(new("other"), new("world"), new("match")) : Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, caps, new HashSet<string> { Stove.Value }) },
                _items.ToDictionary(i => i.Id), _appliances.ToDictionary(a => a.Station), _recipes.ToDictionary(r => r.Id),
                spatial: Spatial(next), supply: Supply(Failure == "supplier" ? 12 : 9));
            Simulation = new(fixture); Simulation.AddItem(PanA, Pan, ItemLocation.World("pan-a"));
            Simulation.AddItem(PanB, Pan, ItemLocation.World("pan-b")); Simulation.AddItem(CupItem, Cup, ItemLocation.World("cup")); return Simulation;
        }
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var next = scope.Level == Next;
            var layout = new CookingRestaurantLayout(new(next ? "next-layout" : "first-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 8, 8) },
                new[] { new CookingEquipmentPlacement(Stove, Machine, new(next ? 6 : 4, 2), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
                new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(next ? 3 : 0, 1)),
                    new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)), new("exit", CookingLayoutTargetKind.Exit, new(7, 4)),
                    new("source", CookingLayoutTargetKind.Storage, new(1, 1)), new("receiving", CookingLayoutTargetKind.Receiving, new(2, 1)),
                    new("storage", CookingLayoutTargetKind.Storage, new(3, 1)), new("pan-a", CookingLayoutTargetKind.Storage, new(0, 1)),
                    new("pan-b", CookingLayoutTargetKind.Storage, new(1, 1)), new("cup", CookingLayoutTargetKind.Storage, new(0, 1)) });
            if (Failure == "layout") layout = layout with { Targets = layout.Targets.Where(t => t.Id != "receiving").ToArray() };
            return new(layout, new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Machine] = new(Machine, 1, 1, 0, -1) },
                new(250, 7000, 1000), new HashSet<DefinitionId> { Machine }, new HashSet<DefinitionId> { Machine },
                new Dictionary<StationSlotId, DefinitionId> { [Stove] = Machine }, 2, 3);
        }
        public CookingLevelPreparation Preparation(LevelId level) => new(level, new("map"), new(new("logical"), new[] { Stove }, new[] { Pan, Cup, Box }), Config.Identity);
        public CookingContent RetryContent() => new(Candidate, new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>(),
            new[] { new CookingSupplyEntryDefinition(Raw, 1, "world:source") }, Config.Identity);
        public CookingLevelEtHost Host(Injector? injector = null) => new(new CookingLevelLifecycle(First, Config, this), failureInjector: injector);
    }
    private static CookingRecipeCommand Command(CookingLevelEtHost host, CookingRecipeOperation operation, ItemId? item = null,
        ItemId? container = null, StationSlotId? station = null, string? world = null, RecipeId? recipe = null, ProcessId? process = null,
        string? request = null, string? delivery = null, int move = 0) => new(Scope, host.HostFrameSequence + 1, Chef, new($"{operation}-{host.HostFrameSequence}"), operation,
            Item: item, Container: container, Station: station, WorldAnchor: world, Recipe: recipe, Process: process, MoveX: move,
            ExpectedItemVersion: item is null ? 0 : host.Observe().Recipe!.Items.Single(i => i.Id == item).Version,
            SupplierId: request is null ? null : "finite", SupplyRequestId: request, DeliveryId: delivery);
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(host.Binding.LevelScope, command, "local", command.Command.Value)).Accepted);
        var result = Assert.Single(host.Tick().Dispositions).Result!; Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.ToString()); return result;
    }
    private static CookingRecipeCheckpoint PrepareCarry(Factory factory, CookingLevelEtHost host)
    {
        Assert.True(host.BeginPreparation(factory.Preparation(First.Level)).Accepted);
        var first = Execute(host, Command(host, CookingRecipeOperation.RequestSupply, request: "first")).Supply!.DeliveryId;
        host.Tick(); host.Tick(); host.Tick();
        var receipt = Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: first)).Supply!;
        var box = receipt.Package!.Value;
        Execute(host, Command(host, CookingRecipeOperation.Pickup, box)); Execute(host, Command(host, CookingRecipeOperation.Drop, box, world: "storage"));
        Execute(host, Command(host, CookingRecipeOperation.TakeOut, receipt.Units[0], container: box));
        Execute(host, Command(host, CookingRecipeOperation.PutIn, receipt.Units[0], container: PanA));
        Execute(host, Command(host, CookingRecipeOperation.Pickup, PanA)); Execute(host, Command(host, CookingRecipeOperation.Drop, PanA, station: Stove));
        Execute(host, Command(host, CookingRecipeOperation.StartProcess, PanA, station: Stove, recipe: Auto)); host.Tick(); host.Tick();
        Execute(host, Command(host, CookingRecipeOperation.Pickup, CupItem));
        Execute(host, Command(host, CookingRecipeOperation.ServePortion, PanA, container: CupItem));
        Execute(host, Command(host, CookingRecipeOperation.Drop, CupItem, world: "cup"));
        Execute(host, Command(host, CookingRecipeOperation.Pickup, PanA)); Execute(host, Command(host, CookingRecipeOperation.Drop, PanA, world: "pan-a"));
        Execute(host, Command(host, CookingRecipeOperation.TakeOut, receipt.Units[1], container: box));
        Execute(host, Command(host, CookingRecipeOperation.PutIn, receipt.Units[1], container: PanB));
        Execute(host, Command(host, CookingRecipeOperation.Pickup, PanB)); Execute(host, Command(host, CookingRecipeOperation.Drop, PanB, station: Stove));
        Execute(host, Command(host, CookingRecipeOperation.StartProcess, PanB, station: Stove, recipe: Manual));
        var process = Assert.Single(host.Observe().Recipe!.Processes).Id;
        Execute(host, Command(host, CookingRecipeOperation.StopProcess, process: process));
        Execute(host, Command(host, CookingRecipeOperation.RequestSupply, request: "pending"));
        Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
        return factory.Simulation.ExportCheckpoint();
    }

    private static CookingLevelCheckpoint FinishNext(CookingLevelEtHost host, string pending)
    {
        while (host.Observe().Recipe!.Supply!.Ledger.Deliveries.Single(d => d.DeliveryId == pending).RemainingTicks > 0) host.Tick();
        Execute(host, Command(host, CookingRecipeOperation.Move) with { FacingX = -1 });
        Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: pending));
        Execute(host, Command(host, CookingRecipeOperation.Move) with { FacingX = 1 });
        var process = Assert.Single(host.Observe().Recipe!.Processes).Id;
        Execute(host, Command(host, CookingRecipeOperation.ContinueProcess, process: process)); host.Tick();
        Assert.Empty(host.Observe().Recipe!.Processes);
        var face = Command(host, CookingRecipeOperation.Move) with { FacingX = -1 };
        Execute(host, face);
        Execute(host, Command(host, CookingRecipeOperation.Pickup, CupItem));
        Execute(host, Command(host, CookingRecipeOperation.ClearContents, CupItem));
        Execute(host, Command(host, CookingRecipeOperation.ServePortion, PanA, container: CupItem));
        return host.ExportCheckpoint().Checkpoint!;
    }


    private sealed class ConfirmedFactory : Factory, ICookingConfirmedMajorChoicesGameplayFactory
    {
        public CookingMajorBaselineChoices CreateConfirmedMajorChoices(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            new(true, Array.Empty<CookingStationReplacement>(), Array.Empty<DefinitionId>(), true);
    }
    private sealed class TempStore : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "cooking-major-et-" + Guid.NewGuid().ToString("N"));
        public string FilePath => Path.Combine(Root, "major.checkpoint.json");
        public CookingMajorCheckpointStore Store => new(Root);
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static CookingMajorProgress Locked(bool faster = false)
    {
        var progress = new CookingMajorProgress(); if (faster) progress.EnableCookFaster(); progress.Lock(); return progress;
    }
    private static void EndSuccess(CookingLevelEtHost host)
    { Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted); }
    private static CookingMajorBaselinePayload WriteBaseline(TempStore files)
    {
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host); EndSuccess(host);
        var result = host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store);
        Assert.True(result.Accepted, result.ToString()); Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        var read = files.Store.ReadBaseline(Scope); Assert.True(read.Accepted); return read.Payload!;
    }
    [Fact]
    public void Durable_success_dispose_and_load_target_created_preserves_complete_continuation_and_pending_allocator()
    {
        using var files = new TempStore(); var factory = new Factory(); CookingLevelCheckpoint uninterrupted; string created, pending; long frame;
        using (var host = factory.Host())
        {
            PrepareCarry(factory, host); EndSuccess(host); frame = host.HostFrameSequence;
            Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store).Accepted);
            created = host.Observe().CanonicalText();
            var payload = files.Store.ReadBaseline(Scope).Payload!; Assert.Equal(frame, payload.HostFrameSequence);
            Assert.Equal(host.Driver.Simulation!.ExportSuccessHandoff().CanonicalText(), payload.Kitchen.CanonicalText());
            pending = payload.Kitchen.Supply!.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).DeliveryId;
            Assert.True(host.Prepare(factory.Preparation(Next)).Accepted); Assert.True(host.Start().Accepted);
            uninterrupted = FinishNext(host, pending);
        }
        var fresh = new Factory(); var loaded = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, fresh.Config, fresh);
        Assert.True(loaded.Accepted, loaded.ToString()); using var recovered = loaded.Host!;
        Assert.Equal(CookingLevelState.Created, recovered.Lifecycle.State); Assert.Equal(frame, recovered.HostFrameSequence);
        Assert.Equal(created, recovered.Observe().CanonicalText()); Assert.True(loaded.Progress!.Locked);
        Assert.True(recovered.Prepare(fresh.Preparation(Next)).Accepted); Assert.True(recovered.Start().Accepted);
        Assert.Equal(uninterrupted.CanonicalText(), FinishNext(recovered, pending).CanonicalText());
    }
    [Theory]
    [InlineData(CookingLevelEtHostFailurePoint.LevelCreated)]
    [InlineData(CookingLevelEtHostFailurePoint.DriverCreated)]
    [InlineData(CookingLevelEtHostFailurePoint.BeforeLevelPublish)]
    [InlineData(CookingLevelEtHostFailurePoint.SimulationOwnershipAcquired)]
    [InlineData(CookingLevelEtHostFailurePoint.BeforeSimulationPublish)]
    public void Every_publication_failure_precedes_file_commit_and_preserves_old_source_and_baseline(CookingLevelEtHostFailurePoint point)
    {
        using var files = new TempStore(); WriteBaseline(files); var bytes = File.ReadAllBytes(files.FilePath);
        var factory = new Factory(); var injector = new Injector(); using var host = factory.Host(injector);
        PrepareCarry(factory, host); EndSuccess(host); var before = host.Observe().CanonicalText(); var lifecycle = host.Lifecycle; var driver = host.Driver;
        injector.Armed = point; var result = host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store);
        Assert.False(result.Accepted); Assert.Equal(bytes, File.ReadAllBytes(files.FilePath));
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Same(lifecycle, host.Lifecycle); Assert.Same(driver, host.Driver);
        Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store).Accepted);
    }
    [Fact]
    public void Io_staging_failure_rolls_back_candidate_tree_and_keeps_previous_readable_bytes()
    {
        using var files = new TempStore(); WriteBaseline(files); var bytes = File.ReadAllBytes(files.FilePath);
        Directory.CreateDirectory(files.FilePath + ".next");
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host); EndSuccess(host);
        var before = host.Observe().CanonicalText(); var result = host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store);
        Assert.False(result.Accepted); Assert.Equal(CookingMajorBaselineReason.WriteFailed, result.BaselineReason);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(bytes, File.ReadAllBytes(files.FilePath)); Assert.True(files.Store.ReadBaseline(Scope).Accepted);
        Directory.Delete(files.FilePath + ".next"); Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store).Accepted);
    }
    [Fact]
    public void Retry_never_rewrites_the_latest_success_baseline()
    {
        using var files = new TempStore(); WriteBaseline(files); var bytes = File.ReadAllBytes(files.FilePath);
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted); Assert.True(host.CompleteEnd().Accepted);
        Assert.True(host.CreateRetry(2, factory.RetryContent()).Accepted); Assert.Equal(bytes, File.ReadAllBytes(files.FilePath));
    }
    [Fact]
    public void Rehashed_saved_choices_cannot_grant_buff_but_independent_trusted_provider_can_confirm_it()
    {
        using var files = new TempStore(); var payload = WriteBaseline(files);
        Assert.True(files.Store.WriteBaseline(payload with { Choices = payload.Choices with { CookFaster = true } }).Accepted);
        var rejected = new Factory(); var loaded = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, rejected.Config, rejected);
        Assert.False(loaded.Accepted); Assert.Equal(CookingMajorBaselineHostLoadReason.ChoicesRejected, loaded.Reason); Assert.Equal(0, rejected.CreateCount);
        var trusted = new ConfirmedFactory(); var allowed = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, trusted.Config, trusted);
        Assert.True(allowed.Accepted, allowed.ToString()); Assert.True(allowed.Progress!.CookFaster); allowed.Host!.Dispose();
    }
    [Theory]
    [InlineData("menu")][InlineData("truncated")][InlineData("hash")][InlineData("legacy")][InlineData("wrong-match")][InlineData("typed2")][InlineData("config")]
    public void Invalid_baseline_is_rejected_without_leaking_authority_and_valid_record_can_load(string defect)
    {
        using var files = new TempStore(); var payload = WriteBaseline(files); var bytes = File.ReadAllBytes(files.FilePath);
        if (defect == "menu") Assert.True(files.Store.WriteBaseline(payload with { MenuPolicyIdentity = "foreign" }).Accepted);
        if (defect == "config")
        {
            var identity = payload.ConfigIdentity with { Sha256 = "foreign" };
            Assert.True(files.Store.WriteBaseline(payload with { ConfigIdentity = identity, Preparation = payload.Preparation with { ConfigIdentity = identity } }).Accepted);
        }
        if (defect == "typed2")
        {
            var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(files.FilePath))!;
            record["formatVersion"] = 2; File.WriteAllText(files.FilePath, record.ToJsonString());
        }
        if (defect == "truncated") File.WriteAllText(files.FilePath, "{");
        if (defect == "hash")
        {
            var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(files.FilePath))!;
            record["integritySha256"] = new string('0', 64); File.WriteAllText(files.FilePath, record.ToJsonString());
        }
        if (defect == "legacy") Assert.True(files.Store.Write(Scope, Locked(), payload.Kitchen).Accepted);
        var factory = new Factory(); var match = defect == "wrong-match" ? new CookingScope(new("other"), Scope.World, Scope.Match) : Scope;
        var result = CookingLevelEtHost.LoadMajorBaseline(files.Store, match, factory.Config, factory);
        Assert.False(result.Accepted); Assert.Equal(0, factory.CreateCount);
        File.WriteAllBytes(files.FilePath, bytes);
        var valid = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory); Assert.True(valid.Accepted, valid.ToString()); valid.Host!.Dispose();
    }

    [Fact]
    public void Rehashed_legal_alternate_seed_cannot_replace_trusted_target_spawn_and_rejection_releases_authority()
    {
        using var files = new TempStore(); var payload = WriteBaseline(files);
        var original = payload.InstalledLayout!.GeometrySeedPoses.Single();
        var alternate = original with { X = 4500 }; // Valid free floor, but not the trusted target factory seed.
        var changed = payload with { InstalledLayout = payload.InstalledLayout with { GeometrySeedPoses = new[] { alternate } },
            Kitchen = payload.Kitchen with { Poses = new[] { alternate } } };
        Assert.True(files.Store.WriteBaseline(changed).Accepted);
        var factory = new Factory(); var result = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory);
        Assert.False(result.Accepted); Assert.Equal(CookingMajorBaselineHostLoadReason.GameplayRestoreRejected, result.Reason);
        Assert.True(files.Store.WriteBaseline(payload).Accepted);
        var valid = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory); Assert.True(valid.Accepted, valid.ToString()); valid.Host!.Dispose();
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Unlocked_or_unconfirmed_choices_reject_before_factory_stage_and_file_write(bool unconfirmedBuff)
    {
        using var files = new TempStore(); WriteBaseline(files); var bytes = File.ReadAllBytes(files.FilePath);
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host); EndSuccess(host);
        var before = host.Observe().CanonicalText(); var creates = factory.CreateCount;
        var result = host.CreateSuccessor(Next, 2, factory.Preparation(Next), unconfirmedBuff ? Locked(true) : new CookingMajorProgress(), files.Store);
        Assert.False(result.Accepted); Assert.Equal(creates, factory.CreateCount);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(bytes, File.ReadAllBytes(files.FilePath));
        Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next), Locked(), files.Store).Accepted);
    }
    [Fact]
    public void Baseline_load_rejects_foreign_owned_kitchen_without_mutation_or_close_and_releases_its_et_tree()
    {
        using var files = new TempStore(); var payload = WriteBaseline(files); var foreignFactory = new Factory();
        var foreign = foreignFactory.Create(payload.TargetScope, foreignFactory.Config); var owner = new object();
        CookingSimulationHostOwnership.Acquire(foreign, owner);
        try
        {
            var before = foreign.ExportCheckpoint().CanonicalText(); var factory = new Factory { Foreign = foreign };
            var loaded = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory); Assert.False(loaded.Accepted);
            Assert.Equal(before, foreign.ExportCheckpoint().CanonicalText());
            Assert.Equal(CookingRecipeOutcome.Accepted, foreign.Submit(new(Scope, 1, Chef, new("foreign-move"), CookingRecipeOperation.Move, FacingX: -1)).Outcome);
            factory.Foreign = null;
            var valid = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory); Assert.True(valid.Accepted, valid.ToString()); valid.Host!.Dispose();
        }
        finally { CookingSimulationHostOwnership.Release(foreign, owner); }
    }

    private sealed class LegacyFactory(Factory inner) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => inner.Create(scope, configuration);
    }
    [Fact]
    public void Legacy_factory_durable_transition_and_load_use_next_trusted_seed_without_preparation_policy()
    {
        using var files = new TempStore(); var inner = new Factory(); var legacy = new LegacyFactory(inner); string created;
        using (var host = new CookingLevelEtHost(new CookingLevelLifecycle(First, inner.Config, legacy)))
        {
            Assert.True(host.Prepare(inner.Preparation(First.Level)).Accepted); Assert.True(host.Start().Accepted);
            Execute(host, Command(host, CookingRecipeOperation.RequestSupply, request: "legacy-pending"));
            EndSuccess(host); Assert.True(host.CreateSuccessor(Next, 2, inner.Preparation(Next), Locked(), files.Store).Accepted);
            Assert.Equal(3500, Assert.Single(host.Observe().Players).Pose!.X); created = host.Observe().CanonicalText();
            Assert.Null(files.Store.ReadBaseline(Scope).Payload!.InstalledLayout);
        }
        var fresh = new Factory(); var loaded = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, fresh.Config, new LegacyFactory(fresh));
        Assert.True(loaded.Accepted, loaded.ToString()); using var recovered = loaded.Host!;
        Assert.Equal(created, recovered.Observe().CanonicalText());
        Assert.True(recovered.Prepare(fresh.Preparation(Next)).Accepted); Assert.True(recovered.Start().Accepted); Assert.Equal(1, fresh.CreateCount);
    }

    [Fact]
    public void Factory_io_failure_returns_structured_load_rejection_and_later_load_owns_a_clean_tree()
    {
        using var files = new TempStore(); WriteBaseline(files); var bytes = File.ReadAllBytes(files.FilePath);
        var factory = new Factory { Failure = "factory-io" };
        var rejected = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory);
        Assert.False(rejected.Accepted); Assert.Equal(CookingMajorBaselineHostLoadReason.InitializationFailed, rejected.Reason);
        Assert.Contains("factory IO test failure", rejected.Detail); Assert.Equal(bytes, File.ReadAllBytes(files.FilePath));
        factory.Failure = null;
        var recovered = CookingLevelEtHost.LoadMajorBaseline(files.Store, Scope, factory.Config, factory);
        Assert.True(recovered.Accepted, recovered.ToString()); Assert.Equal(CookingLevelState.Created, recovered.Host!.Lifecycle.State); recovered.Host.Dispose();
    }

}
