using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingGenerationTransactionEtTests
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
    private sealed class Factory : ICookingPreparationGameplayFactory
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
            CreateCount++; if (Failure == "factory") throw new InvalidOperationException("factory test failure");
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

    [Fact]
    public void Wrong_match_retry_candidate_is_rejected_before_mutation_or_generation_commit()
    {
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var before = host.Observe().CanonicalText(); var lifecycle = host.Lifecycle; var driver = host.Driver;
        factory.Failure = "wrong-match";
        Assert.False(host.CreateRetry(2, factory.RetryContent()).Accepted);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Same(lifecycle, host.Lifecycle); Assert.Same(driver, host.Driver);
        factory.Failure = null;
        Assert.True(host.CreateRetry(2, factory.RetryContent()).Accepted);
    }

    [Fact]
    public void Foreign_owned_candidate_is_rejected_without_mutation_or_closing_its_owner()
    {
        var foreignFactory = new Factory();
        var foreign = foreignFactory.Create(new(Scope, new(1), Next, 2), foreignFactory.Config);
        var owner = new object(); CookingSimulationHostOwnership.Acquire(foreign, owner);
        try
        {
            var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host);
            Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
            var before = foreign.ExportCheckpoint().CanonicalText(); var source = host.Observe().CanonicalText();
            factory.Foreign = foreign;
            Assert.False(host.CreateSuccessor(Next, 2, factory.Preparation(Next)).Accepted);
            Assert.Equal(before, foreign.ExportCheckpoint().CanonicalText());
            Assert.Equal(source, host.Observe().CanonicalText());
            Assert.Equal(CookingRecipeOutcome.Accepted, foreign.Submit(new(Scope, 1, Chef, new("foreign-move"),
                CookingRecipeOperation.Move, MoveX: -1000)).Outcome);
            factory.Foreign = null;
            Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next)).Accepted);
        }
        finally { CookingSimulationHostOwnership.Release(foreign, owner); }
    }

    [Fact]
    public void Successor_installs_trusted_next_seed_before_handoff_and_preserves_stock_portions_process_pending_and_allocator()
    {
        var factory = new Factory(); using var host = factory.Host(); var before = PrepareCarry(factory, host);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var result = host.CreateSuccessor(Next, 2, factory.Preparation(Next)); Assert.True(result.Accepted, result.ToString());
        var carried = host.Observe(); Assert.Equal(CookingLevelState.Created, carried.Lifecycle.State);
        Assert.Equal(3500, Assert.Single(carried.Players).Pose!.X); Assert.NotEqual(before.Poses!.Single().X, carried.Players.Single().Pose!.X);
        Assert.Equal(-1, carried.Players.Single().Pose!.LastMovementTick); Assert.Equal(new LayoutId("next-layout"), carried.InstalledLayout!.Id);
        var kitchen = host.Driver.Simulation!; var checkpoint = kitchen.ExportCheckpoint();
        Assert.Equal(before.NextProductId, checkpoint.NextProductId); Assert.Equal(before.NextProcessId, checkpoint.NextProcessId);
        Assert.Equal(before.Items.Count, checkpoint.Items.Count); Assert.Equal(2, checkpoint.Items.Single(i => i.Id == PanA).RemainingPortions);
        Assert.Equal(before.Processes.Single().ElapsedTicks, checkpoint.Processes.Single().ElapsedTicks); Assert.Null(checkpoint.Processes.Single().ActiveWorker);
        Assert.Equal(before.Supply!.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).RemainingTicks,
            checkpoint.Supply!.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).RemainingTicks);
        Assert.Equal(before.Supply.Balances, checkpoint.Supply.Balances); Assert.Empty(checkpoint.Deduplication); Assert.Empty(checkpoint.Orders);
        Assert.True(host.BeginPreparation(factory.Preparation(Next)).Accepted); host.Tick();
        Assert.Equal(checkpoint.Supply.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).RemainingTicks - 1,
            host.Observe().Recipe!.Supply!.Ledger.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).RemainingTicks);
        Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted); Assert.Equal(2, factory.CreateCount);
        Assert.False(host.TryEnqueue(new(First, Command(host, CookingRecipeOperation.Move, move: 1000), "old", "old")).Accepted);
    }

    [Theory]
    [InlineData(CookingLevelEtHostFailurePoint.LevelCreated)]
    [InlineData(CookingLevelEtHostFailurePoint.DriverCreated)]
    [InlineData(CookingLevelEtHostFailurePoint.BeforeLevelPublish)]
    [InlineData(CookingLevelEtHostFailurePoint.SimulationOwnershipAcquired)]
    [InlineData(CookingLevelEtHostFailurePoint.BeforeSimulationPublish)]
    public void Publication_failure_preserves_complete_source_observation_lifecycle_driver_and_retryability(CookingLevelEtHostFailurePoint point)
    {
        var factory = new Factory(); var injector = new Injector(); using var host = factory.Host(injector); PrepareCarry(factory, host);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var before = host.Observe().CanonicalText(); var lifecycle = host.Lifecycle; var driver = host.Driver; var pending = host.PendingCommandIdentityCount;
        var dispositions = host.DispositionHistory.ToArray(); injector.Armed = point;
        var rejected = host.CreateSuccessor(Next, 2, factory.Preparation(Next)); Assert.False(rejected.Accepted); Assert.False(host.IsFaulted);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Same(lifecycle, host.Lifecycle); Assert.Same(driver, host.Driver);
        Assert.False(lifecycle.HasCreatedNextGeneration); Assert.Equal(pending, host.PendingCommandIdentityCount); Assert.Equal(dispositions, host.DispositionHistory);
        Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next)).Accepted);
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("layout")]
    [InlineData("supplier")]
    [InlineData("same")]
    public void Candidate_failure_rejects_without_source_change_and_can_be_retried(string failure)
    {
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var before = host.Observe().CanonicalText(); factory.Failure = failure;
        Assert.False(host.CreateSuccessor(Next, 2, factory.Preparation(Next)).Accepted);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.False(host.IsFaulted);
        factory.Failure = null; Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next)).Accepted);
    }

    [Fact]
    public void Retry_uses_standard_stock_and_supplier_baseline_and_preserves_confirmed_choices_without_carry()
    {
        var factory = new Factory(); using var host = factory.Host(); PrepareCarry(factory, host);
        var progress = new CookingMajorProgress(); Assert.True(progress.EnableCookFaster().Accepted); progress.Lock();
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var retry = host.CreateRetry(2, factory.RetryContent(), progress); Assert.True(retry.Accepted, retry.ToString());
        var kitchen = host.Driver.Simulation!; var checkpoint = kitchen.ExportCheckpoint();
        Assert.Empty(checkpoint.Processes); Assert.Empty(checkpoint.Supply!.Deliveries); Assert.Empty(checkpoint.SupplyOrigins!);
        Assert.Equal(9, checkpoint.Supply.Balances.Single().AvailableUnits); Assert.Equal(0, checkpoint.NextProductId);
        Assert.Equal(1, checkpoint.Items.Count(i => i.Definition == Raw && !i.Removed));
        Assert.False(checkpoint.Items.Single(i => i.Id == PanA).ContainerCompleted); Assert.Equal(0, checkpoint.Items.Single(i => i.Id == PanA).RemainingPortions);
        Assert.True(progress.Locked); Assert.True(progress.CookFaster);
        Assert.True(host.BeginPreparation(factory.Preparation(First.Level)).Accepted); Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
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

    [Fact]
    public void Next_generation_restore_replays_pending_receipt_manual_continuation_and_next_allocator_identity_exactly()
    {
        var factory = new Factory(); CookingLevelCheckpoint saved, uninterrupted; string pending;
        using (var host = factory.Host())
        {
            PrepareCarry(factory, host); Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
            Assert.True(host.CreateSuccessor(Next, 2, factory.Preparation(Next)).Accepted);
            Assert.True(host.BeginPreparation(factory.Preparation(Next)).Accepted); Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
            host.Tick(); saved = host.ExportCheckpoint().Checkpoint!;
            pending = saved.Recipe.Supply!.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).DeliveryId;
            uninterrupted = FinishNext(host, pending);
            Assert.True(uninterrupted.Recipe.NextProductId > saved.Recipe.NextProductId);
        }
        var fresh = new Factory(); var recovery = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(recovery.Accepted, recovery.ToString()); using var restored = recovery.Host!;
        Assert.Equal(saved.CanonicalText(), restored.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.Equal(uninterrupted.CanonicalText(), FinishNext(restored, pending).CanonicalText());
    }

    [Theory]
    [InlineData(CookingLevelEtHostFailurePoint.DriverCreated)]
    [InlineData(CookingLevelEtHostFailurePoint.BeforeSimulationPublish)]
    public void Retry_publication_failure_preserves_failed_generation_and_confirmed_choices(CookingLevelEtHostFailurePoint point)
    {
        var factory = new Factory(); var injector = new Injector(); using var host = factory.Host(injector); PrepareCarry(factory, host);
        var progress = new CookingMajorProgress(); Assert.True(progress.EnableCookFaster().Accepted); progress.Lock();
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var before = host.Observe().CanonicalText(); var lifecycle = host.Lifecycle; var driver = host.Driver; injector.Armed = point;
        Assert.False(host.CreateRetry(2, factory.RetryContent(), progress).Accepted);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Same(lifecycle, host.Lifecycle); Assert.Same(driver, host.Driver);
        Assert.False(host.IsFaulted); Assert.True(progress.Locked); Assert.True(progress.CookFaster);
        Assert.True(host.CreateRetry(2, factory.RetryContent(), progress).Accepted);
    }
}
