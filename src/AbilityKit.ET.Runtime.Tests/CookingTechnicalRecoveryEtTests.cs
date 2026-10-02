using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingTechnicalRecoveryEtTests
{
    private static readonly CookingScope Scope = new(new("technical-recovery"), new("world"), new("match"));
    private static readonly CookingLevelScope First = new(Scope, new(1), new("first"), 1);
    private static readonly LevelId Next = new("next");
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Prepared = new("prepared"), Box = new("box"), Pan = new("pan"), Cup = new("cup"), Machine = new("machine");
    private static readonly ItemId PanA = new("pan-a"), PanB = new("pan-b"), CupItem = new("cup-item");
    private static readonly StationSlotId Stove = new("stove"), Other = new("other");
    private static readonly RecipeId Auto = new("tomato-egg-soup"), Manual = new("manual"), FaultRecipe = new("fault-product");
    private static readonly OrderTemplateId Template = new("technical-order");
    private static readonly DefinitionId Future = new("future-global-material");
    private sealed class Factory : ICookingPreparationGameplayFactory, ICookingFrontOfHouseGameplayFactory, ICookingConfirmedMajorChoicesGameplayFactory
    {
        private readonly CookingItemDefinition[] _items;
        private readonly CookingApplianceDefinition[] _appliances;
        private readonly CookingRecipeDefinition[] _recipes;
        public CookingConfigurationSnapshot Config { get; }
        public CookingConfigurationCandidate Candidate { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public bool ArmAllocator { get; set; }
        public bool FutureUnlock { get; set; }
        public Factory()
        {
            var caps = new HashSet<string> { "cook" };
            _items = new[] { new CookingItemDefinition(Raw, caps), new(Prepared, caps), new(Box, caps, new(3, new HashSet<DefinitionId> { Raw })),
                new(Pan, caps, new(1, new HashSet<DefinitionId> { Raw, Prepared })), new(Cup, caps, new(1, new HashSet<DefinitionId> { Prepared })) };
            _appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }), new CookingApplianceDefinition(Other, new HashSet<string> { "heat" }) };
            _recipes = new[] { new CookingRecipeDefinition(Auto, new[] { Raw }, Prepared, new("auto-process"), "heat", 6, RequiredProcessingContainerDefinition: Pan),
                new(Manual, new[] { Raw }, Prepared, new("manual-process"), "heat", 100,
                    Completion: CookingRecipeCompletionKind.RetainInputs, Execution: CookingRecipeExecutionKind.Manual, YieldPortions: 3, RequiredProcessingContainerDefinition: Pan), new(FaultRecipe, new[] { Raw }, Prepared, new("fault-process"), "heat", 6, RequiredProcessingContainerDefinition: Pan) };
            Candidate = new(new[] { "heat" }, _items, _appliances, _recipes, OrderTemplates: new[] { new CookingOrderTemplateDefinition(Template, Auto, Cup) }, Spatial: Spatial(false)) { Supply = Supply(9) };
            var registry = new CookingConfigurationRegistry(); Assert.True(registry.Submit(Candidate).Accepted); Config = registry.Current!;
        }
        private static CookingSupplyConfiguration Supply(int amount) => new(new[] { new CookingSupplierDefinition("finite", "source", "receiving", Raw, Box, 3, 4, amount) });
        private static CookingSpatialConfiguration Spatial(bool next) => new(0, 0, 8000, 8000, 250, 7000,
            new[] { new CookingPlayerPose(Chef, next ? 3500 : 500, 1500, 1, 0) },
            new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 1500, 1500), new(LocationKind.WorldPosition, "receiving", 2500, 1500),
                new(LocationKind.WorldPosition, "storage", 3500, 1500), new(LocationKind.WorldPosition, "pan-a", 500, 1500),
                new(LocationKind.WorldPosition, "pan-b", 1500, 1500), new(LocationKind.WorldPosition, "cup", 500, 1500),
                new(LocationKind.StationSlot, Other.Value, 4500, 6500), new(LocationKind.WorldPosition, "queue", 500, 4500), new(LocationKind.WorldPosition, "table-1", 1500, 4500), new(LocationKind.WorldPosition, "washing", 2500, 4500), new(LocationKind.StationSlot, Stove.Value, next ? 6500 : 4500, 1500) }, Array.Empty<CookingSpatialObstacle>());
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var caps = new HashSet<string> { "cook" }; var next = scope.Level == Next;
            var fixture = new CookingRecipeFixture(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, caps, new HashSet<string> { Stove.Value, Other.Value }) },
                _items.ToDictionary(i => i.Id), _appliances.ToDictionary(a => a.Station), _recipes.ToDictionary(r => r.Id), new HashSet<DefinitionId> { Cup }, new Dictionary<DefinitionId,int> { [Cup] = 1 }, "washing", new Dictionary<OrderTemplateId,CookingOrderTemplateDefinition> { [Template] = new(Template, Auto, Cup) },
                spatial: Spatial(next), supply: Supply(9));
            Simulation = new(fixture, new ArmedAllocator(this)); Simulation.AddItem(PanA, Pan, ItemLocation.World("pan-a"));
            Simulation.AddItem(PanB, Pan, ItemLocation.World("pan-b")); Simulation.AddItem(CupItem, Cup, ItemLocation.World("cup")); return Simulation;
        }
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var next = scope.Level == Next;
            var layout = new CookingRestaurantLayout(new(next ? "next-layout" : "first-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 8, 8) },
                new[] { new CookingEquipmentPlacement(Stove, Machine, new(next ? 6 : 4, 2), 1, 1, 0, 0, -1), new CookingEquipmentPlacement(Other, Machine, new(4, 7), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
                new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(next ? 3 : 0, 1)),
                    new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)), new("exit", CookingLayoutTargetKind.Exit, new(7, 4)),
                    new("queue", CookingLayoutTargetKind.Queue, new(0, 4)), new("table-1", CookingLayoutTargetKind.Table, new(1, 4)), new("washing", CookingLayoutTargetKind.Storage, new(2, 4)),
                    new("source", CookingLayoutTargetKind.Storage, new(1, 1)), new("receiving", CookingLayoutTargetKind.Receiving, new(2, 1)),
                    new("storage", CookingLayoutTargetKind.Storage, new(3, 1)), new("pan-a", CookingLayoutTargetKind.Storage, new(0, 1)),
                    new("pan-b", CookingLayoutTargetKind.Storage, new(1, 1)), new("cup", CookingLayoutTargetKind.Storage, new(0, 1)) });
            return new(layout, new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Machine] = new(Machine, 1, 1, 0, -1) },
                new(250, 7000, 1000), new HashSet<DefinitionId> { Machine }, new HashSet<DefinitionId> { Machine },
                new Dictionary<StationSlotId, DefinitionId> { [Stove] = Machine, [Other] = Machine }, 2, 3);
        }
        public CookingLevelPreparation Preparation(LevelId level) => new(level, new("map"), new(new("logical"), new[] { Stove, Other }, new[] { Pan, Cup, Box }), Config.Identity);
        public CookingContent RetryContent() => new(Candidate, new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>(),
            new[] { new CookingSupplyEntryDefinition(Raw, 1, "world:source"), new CookingSupplyEntryDefinition(Cup, 1, CookingContentCatalog.CleanPoolLocation) }, Config.Identity);
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration => new(new(1, 100, 1, 50, 2, 2, 100), new[] { Template }, ManualPolicyIdentity: "technical-front-v1", WashingAnchor: "washing");
        public CookingMajorBaselineChoices CreateConfirmedMajorChoices(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            new(true, new[] { new CookingStationReplacement(Stove, Other) }, FutureUnlock ? new[] { Raw, Cup, Future } : new[] { Raw, Cup }, true);
        public CookingLevelEtHost Host() => new(new CookingLevelLifecycle(First, Config, this));
        private sealed class ArmedAllocator(Factory owner) : ICookingProductIdAllocator {
            public ItemId GetProductId(long sequence) {
                if (owner.ArmAllocator) throw new InvalidOperationException("armed fixed-tick product fault");
                return new("technical-allocated-" + sequence);
            }
        }
    }
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "technical-recovery-" + Guid.NewGuid().ToString("N"));
        public CookingMajorCheckpointStore NewStore() => new(Root);
        public byte[] Bytes => File.ReadAllBytes(Path.Combine(Root, "major.checkpoint.json"));
        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("technical-recovery-", StringComparison.Ordinal))
                throw new InvalidOperationException("Test cleanup target escaped the owned temporary directory.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }

    private static CookingRecipeCommand Command(CookingLevelEtHost host, CookingRecipeOperation operation,
        ItemId? item = null, ItemId? container = null, StationSlotId? station = null,
        RecipeId? recipe = null, ProcessId? process = null, string? world = null,
        string? request = null, string? delivery = null, int facingX = 0, int facingY = 0) =>
        new(Scope, host.HostFrameSequence + 1, Chef, new("technical-" + host.HostFrameSequence), operation,
            Item: item, Container: container, Station: station, Recipe: recipe, Process: process, WorldAnchor: world,
            ExpectedItemVersion: item is null ? 0 : host.Observe().Recipe!.Items.Single(i => i.Id == item).Version,
            SupplierId: request is null ? null : "finite", SupplyRequestId: request, DeliveryId: delivery,
            FacingX: facingX, FacingY: facingY);

    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(host.Binding.LevelScope, command, "local", command.Command.Value)).Accepted);
        var result = Assert.Single(host.Tick().Dispositions).Result!;
        Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.ToString());
        return result;
    }

    private static CookingMajorBaselinePayload SuccessfulBaseline(Files files, bool futureUnlock = false)
    {
        var factory = new Factory { FutureUnlock = futureUnlock }; using var host = factory.Host();
        Assert.True(host.Prepare(factory.Preparation(First.Level)).Accepted); Assert.True(host.Start().Accepted);
        for (var tick = 0; tick < 500; tick++) host.Tick();
        Assert.True(host.TryFinishService().Accepted); Assert.True(host.CompleteEnd().Accepted);
        var choices = factory.CreateConfirmedMajorChoices(new(Scope, First.RestaurantRuntime, Next, 2), factory.Config).CreateProgress();
        var result = host.CreateSuccessor(Next, 2, factory.Preparation(Next), choices, files.NewStore());
        Assert.True(result.Accepted, result.ToString());
        return files.NewStore().ReadBaseline(Scope).Payload!;
    }

    private static (CookingLevelEtHost Host, CookingMajorProgress Progress) Load(Files files, Factory factory)
    {
        var loaded = CookingLevelEtHost.LoadMajorBaseline(files.NewStore(), Scope, factory.Config, factory);
        Assert.True(loaded.Accepted, loaded.ToString());
        return (loaded.Host!, loaded.Progress!);
    }

    private static CookingSupplyPhysicalResult ReceivePackage(CookingLevelEtHost host, string request)
    {
        Execute(host, Command(host, CookingRecipeOperation.Move, facingX: -1));
        var id = Execute(host, Command(host, CookingRecipeOperation.RequestSupply, request: request)).Supply!.DeliveryId!;
        for (var tick = 0; tick < 4; tick++) host.Tick();
        return Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: id)).Supply!;
    }

    private static void PutPurchasedUnitIntoPan(CookingLevelEtHost host, CookingSupplyPhysicalResult delivery, ItemId pan)
    {
        var box = delivery.Package!.Value;
        Execute(host, Command(host, CookingRecipeOperation.Pickup, box));
        Execute(host, Command(host, CookingRecipeOperation.Drop, box, world: "storage"));
        Execute(host, Command(host, CookingRecipeOperation.TakeOut, delivery.Units[0], container: box));
        Execute(host, Command(host, CookingRecipeOperation.PutIn, delivery.Units[0], container: pan));
        Execute(host, Command(host, CookingRecipeOperation.Pickup, pan));
        Execute(host, Command(host, CookingRecipeOperation.Move, facingX: 1));
        Execute(host, Command(host, CookingRecipeOperation.Drop, pan, station: Stove));
    }

    [Fact]
    public void Healthy_owner_declared_failed_retry_discards_failed_work_purchases_and_layout_but_uses_trusted_choices()
    {
        using var files = new Files(); var baseline = SuccessfulBaseline(files); var bytes = files.Bytes;
        var factory = new Factory(); var loaded = Load(files, factory); using var host = loaded.Host;
        Assert.True(host.BeginPreparation(factory.Preparation(Next)).Accepted);
        var originalLayout = factory.CreatePreparationConfiguration(host.Binding.LevelScope, factory.Config).InitialLayout;
        var changed = originalLayout with { Equipment = originalLayout.Equipment.Select(e => e.Station == Stove ? e with { Cell = new(6, 3) } : e).ToArray() };
        Assert.True(host.TryInstallPreparedLayout(changed));
        Assert.NotEqual(originalLayout, host.Observe().InstalledLayout!);
        var received = ReceivePackage(host, "failed-received"); PutPurchasedUnitIntoPan(host, received, PanB);
        Execute(host, Command(host, CookingRecipeOperation.StartProcess, PanB, station: Stove, recipe: Manual));
        var manual = Assert.Single(host.Observe().Recipe!.Processes);
        Execute(host, Command(host, CookingRecipeOperation.StopProcess, process: manual.Id));
        Execute(host, Command(host, CookingRecipeOperation.Move, facingX: -1, facingY: 1));
        Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
        CookingFrontWorkSnapshot? inquiry = null;
        for (var tick = 0; tick < 100 && inquiry is null; tick++) {
            inquiry = host.FrontOfHouseSnapshot!.Work.FirstOrDefault(w => w.Kind == CookingCompanionWorkKind.Inquiring && w.Status == CookingFrontWorkStatus.Available);
            if (inquiry is null) host.Tick();
        }
        Assert.NotNull(inquiry);
        Execute(host, Command(host, CookingRecipeOperation.ClaimFrontWork, world: inquiry!.Id));
        Execute(host, Command(host, CookingRecipeOperation.RequestSupply, request: "failed-pending"));
        Assert.Contains(host.FrontOfHouseSnapshot!.Work, w => w.Player == Chef);
        var failed = host.Observe();
        Assert.NotEmpty(failed.Recipe!.Supply!.Ledger.Deliveries);
        Assert.Contains(failed.Recipe.Supply.Ledger.Deliveries, d => d.Phase == CookingDeliveryPhase.Pending);
        Assert.NotEmpty(failed.Recipe.Processes);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var retry = host.CreateRetry(3, factory.RetryContent(), loaded.Progress);
        Assert.True(retry.Accepted, retry.ToString());
        Assert.Equal(bytes, files.Bytes);
        var clean = host.Observe(); var checkpoint = factory.Simulation.ExportCheckpoint();
        Assert.Empty(checkpoint.Processes); Assert.Empty(checkpoint.Supply!.Deliveries); Assert.Empty(checkpoint.SupplyOrigins!);
        Assert.Equal(9, checkpoint.Supply.Balances.Single().AvailableUnits); Assert.Equal(0, checkpoint.NextProductId);
        Assert.All(checkpoint.Items, i => Assert.Null(i.SupplyProvenance));
        Assert.DoesNotContain(checkpoint.Items, i => received.Units.Contains(i.Id));
        Assert.Empty(clean.Front!.Customers); Assert.Empty(clean.Front.Work);
        Assert.Equal(baseline.Choices.Decoration, loaded.Progress.Decoration); Assert.Equal(baseline.Choices.Unlocks, loaded.Progress.Unlocks);
        Assert.True(loaded.Progress.Locked); Assert.True(loaded.Progress.CookFaster);
        Assert.Equal(factory.CreatePreparationConfiguration(host.Binding.LevelScope, factory.Config).InitialLayout.CanonicalText(),
            clean.InstalledLayout!.CanonicalText());
        // Raw unlock is the standard supply, once, not a second object in its ordinary slot.
        var raw = Assert.Single(checkpoint.Items, i => i.Definition == Raw && !i.Removed);
        Assert.Equal(new ItemId("raw-unlock-1"), raw.Id);
        Assert.Equal(ItemLocation.World("source"), raw.Location);
        Assert.Equal(1, checkpoint.CleanContainerCounts.Single(c => c.Definition == Cup).Count);
        Assert.True(host.Prepare(factory.Preparation(Next)).Accepted); Assert.True(host.Start().Accepted);
        var beforeOld = host.Observe().CanonicalText();
        var old = Command(host, CookingRecipeOperation.Move, facingX: 1);
        Assert.Equal(CookingLevelAdmissionReason.ScopeMismatch, host.TryEnqueue(new(baseline.TargetScope, old, "old", "old")).Reason);
        Assert.Equal(beforeOld, host.Observe().CanonicalText());
        Execute(host, Command(host, CookingRecipeOperation.Move, facingX: -1));
        Execute(host, Command(host, CookingRecipeOperation.Pickup, raw.Id));
        Execute(host, Command(host, CookingRecipeOperation.PutIn, raw.Id, container: PanA));
        Execute(host, Command(host, CookingRecipeOperation.Pickup, PanA));
        Execute(host, Command(host, CookingRecipeOperation.Move, facingX: 1));
        Execute(host, Command(host, CookingRecipeOperation.Drop, PanA, station: Stove));
        Execute(host, Command(host, CookingRecipeOperation.StartProcess, PanA, station: Stove, recipe: Auto));
        Assert.Equal(3, Assert.Single(factory.Simulation.Snapshot().Processes).RequiredTicks);
        host.Tick(); host.Tick(); Assert.Empty(factory.Simulation.Snapshot().Processes);
        Assert.Single(factory.Simulation.Snapshot().Items, i => i.IsProduct);
    }

    [Fact]
    public void Actual_fixed_tick_fault_is_quarantined_and_dispose_new_store_load_recovers_last_success_only()
    {
        using var files = new Files(); SuccessfulBaseline(files); var bytes = files.Bytes;
        var factory = new Factory(); var loaded = Load(files, factory); var faulted = loaded.Host;
        Assert.True(faulted.Prepare(factory.Preparation(Next)).Accepted); Assert.True(faulted.Start().Accepted);
        var delivery = ReceivePackage(faulted, "failed-purchase"); PutPurchasedUnitIntoPan(faulted, delivery, PanA);
        Execute(faulted, Command(faulted, CookingRecipeOperation.StartProcess, PanA, station: Stove, recipe: FaultRecipe));
        factory.ArmAllocator = true;
        var fault = Assert.Throws<InvalidOperationException>(() => { for (var tick = 0; tick < 6; tick++) faulted.Tick(); });
        Assert.Contains("fault", fault.ToString(), StringComparison.OrdinalIgnoreCase); Assert.True(faulted.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => faulted.BeginEnd(CookingLevelOutcome.Failed));
        Assert.Throws<InvalidOperationException>(() => faulted.CreateRetry(3, factory.RetryContent(), loaded.Progress));
        Assert.Throws<InvalidOperationException>(() => faulted.Tick()); Assert.Equal(bytes, files.Bytes);
        faulted.Dispose();
        string Continuation() {
            var fresh = new Factory(); var recovered = Load(files, fresh); using var host = recovered.Host;
            Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
            Assert.Empty(host.Observe().Recipe!.Supply!.Ledger.Deliveries);
            Assert.Empty(host.Observe().Recipe!.Processes); Assert.Empty(host.Observe().Front!.Work);
            Assert.True(host.Prepare(fresh.Preparation(Next)).Accepted); Assert.True(host.Start().Accepted);
            var received = ReceivePackage(host, "clean-purchase"); PutPurchasedUnitIntoPan(host, received, PanA);
            Execute(host, Command(host, CookingRecipeOperation.StartProcess, PanA, station: Stove, recipe: FaultRecipe));
            for (var tick = 0; tick < 6; tick++) host.Tick();
            Assert.Empty(fresh.Simulation.Snapshot().Processes); Assert.Single(fresh.Simulation.Snapshot().Items, i => i.IsProduct);
            return host.ExportCheckpoint().Checkpoint!.CanonicalText();
        }
        Assert.Equal(Continuation(), Continuation()); Assert.Equal(bytes, files.Bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Accelerated_inflight_process_survives_optional_same_level_restore_and_durable_created_cold_load(bool restoreRunning)
    {
        using var files = new Files(); SuccessfulBaseline(files);
        var factory = new Factory(); var loaded = Load(files, factory); var host = loaded.Host;
        var finalLevel = new LevelId("after-next"); string created, continued;
        try {
            Assert.True(host.BeginPreparation(factory.Preparation(Next)).Accepted);
            var received = ReceivePackage(host, "buff-carry"); PutPurchasedUnitIntoPan(host, received, PanA);
            Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
            // Finish actual Front service while prepared raw food waits at the station.
            // Starting the final process afterward retains real unfinished work at Success.
            for (var tick = 0; tick < 500; tick++) host.Tick();
            Execute(host, Command(host, CookingRecipeOperation.StartProcess, PanA, station: Stove, recipe: Auto));
            var process = Assert.Single(host.Observe().Recipe!.Processes);
            Assert.Equal(3, process.RequiredTicks); Assert.Equal(1, process.ElapsedTicks);
            if (restoreRunning) {
                var saved = host.ExportCheckpoint().Checkpoint!; host.Dispose();
                var reconstructed = new Factory();
                var restored = CookingLevelEtHost.Restore(saved, reconstructed.Config, reconstructed);
                Assert.True(restored.Accepted, restored.ToString()); host = restored.Host!; factory = reconstructed;
                Assert.Equal(saved.CanonicalText(), host.ExportCheckpoint().Checkpoint!.CanonicalText());
            }
            Assert.True(host.TryFinishService().Accepted); Assert.True(host.CompleteEnd().Accepted);
            var choices = factory.CreateConfirmedMajorChoices(new(Scope, First.RestaurantRuntime, finalLevel, 3), factory.Config).CreateProgress();
            var successor = host.CreateSuccessor(finalLevel, 3, factory.Preparation(finalLevel), choices, files.NewStore());
            Assert.True(successor.Accepted, successor.ToString());
            var carried = Assert.Single(host.Observe().Recipe!.Processes);
            Assert.Equal(3, carried.RequiredTicks); Assert.Equal(1, carried.ElapsedTicks);
            created = host.Observe().CanonicalText();
            Assert.True(host.Prepare(factory.Preparation(finalLevel)).Accepted); Assert.True(host.Start().Accepted);
            host.Tick(); host.Tick();
            Assert.Empty(host.Observe().Recipe!.Processes);
            Assert.Single(host.Observe().Recipe!.Items, i => i.IsProduct);
            continued = host.ExportCheckpoint().Checkpoint!.CanonicalText();
        }
        finally { host.Dispose(); }
        var fresh = new Factory(); var cold = Load(files, fresh); using var recovered = cold.Host;
        Assert.Equal(CookingLevelState.Created, recovered.Lifecycle.State);
        Assert.Equal(created, recovered.Observe().CanonicalText());
        Assert.Equal(3, Assert.Single(recovered.Observe().Recipe!.Processes).RequiredTicks);
        Assert.True(recovered.Prepare(fresh.Preparation(finalLevel)).Accepted); Assert.True(recovered.Start().Accepted);
        recovered.Tick(); recovered.Tick();
        Assert.Equal(continued, recovered.ExportCheckpoint().Checkpoint!.CanonicalText());
    }

    [Fact]
    public void Trusted_global_unlock_absent_from_current_level_survives_cold_load_and_retry_without_spawning()
    {
        using var files = new Files(); SuccessfulBaseline(files, futureUnlock: true); var bytes = files.Bytes;
        var factory = new Factory { FutureUnlock = true }; var loaded = Load(files, factory); using var host = loaded.Host;
        Assert.Contains(Future, loaded.Progress.Unlocks); Assert.True(loaded.Progress.Locked);
        Assert.DoesNotContain(Future, factory.Config.Items.Keys);
        Assert.DoesNotContain(factory.RetryContent().StandardInitialSupply, entry => entry.Definition == Future);
        Assert.True(host.Prepare(factory.Preparation(Next)).Accepted); Assert.True(host.Start().Accepted);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted); Assert.True(host.CompleteEnd().Accepted);
        var retry = host.CreateRetry(3, factory.RetryContent(), loaded.Progress);
        Assert.True(retry.Accepted, retry.ToString());
        Assert.Contains(Future, loaded.Progress.Unlocks);
        Assert.Equal(bytes, files.Bytes);
        var checkpoint = factory.Simulation.ExportCheckpoint();
        Assert.DoesNotContain(checkpoint.Items, item => item.Definition == Future);
        var raw = Assert.Single(checkpoint.Items, item => item.Definition == Raw && !item.Removed);
        Assert.Equal(new ItemId("raw-unlock-1"), raw.Id);
        Assert.Equal(1, checkpoint.CleanContainerCounts.Single(pool => pool.Definition == Cup).Count);
        Assert.True(host.Prepare(factory.Preparation(Next)).Accepted); Assert.True(host.Start().Accepted);
    }
}
