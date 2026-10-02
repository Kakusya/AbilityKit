using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingObservationEtTests
{
    private static readonly CookingScope Scope = new(new("observation"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 3);
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Box = new("box"), Meal = new("meal"), Cup = new("cup"), Machine = new("machine");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly OrderTemplateId Template = new("template");
    private static readonly RecipeId Recipe = new("cook");
    private static readonly ItemId CupItem = new("cup-item");
    private sealed class Factory : ICookingPreparationGameplayFactory, ICookingFrontOfHouseGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public int CreateCount { get; private set; }
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; } = new(new(1, 100, 3, 1, 2, 2, 100), new[] { Template });
        public Factory()
        {
            var caps = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, caps), new(Meal, caps),
                new(Box, caps, new(3, new HashSet<DefinitionId> { Raw })), new(Cup, caps, new(1, new HashSet<DefinitionId> { Meal })) };
            var appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Meal, new("heat"), "heat", 3) };
            var orders = new[] { new CookingOrderTemplateDefinition(Template, Recipe, Cup, RequiresBinding: true) };
            var spatial = new CookingSpatialConfiguration(0, 0, 8000, 8000, 250, 5000,
                new[] { new CookingPlayerPose(Chef, 500, 1500, 1, 0) }, new[] {
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 1500, 1500), new(LocationKind.WorldPosition, "receiving", 2500, 1500),
                    new(LocationKind.WorldPosition, "storage", 3500, 1500), new(LocationKind.WorldPosition, "cup", 500, 1500),
                    new(LocationKind.StationSlot, Stove.Value, 4500, 1500) }, Array.Empty<CookingSpatialObstacle>());
            var supply = new CookingSupplyConfiguration(new[] { new CookingSupplierDefinition("finite", "source", "receiving", Raw, Box, 3, 2, 9) });
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "heat" }, items, appliances, recipes, OrderTemplates: orders, Spatial: spatial) { Supply = supply }).Accepted);
            Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, caps, new HashSet<string> { Stove.Value }) },
                items.ToDictionary(i => i.Id), appliances.ToDictionary(a => a.Station), recipes.ToDictionary(r => r.Id),
                orderTemplates: orders.ToDictionary(o => o.Id), spatial: spatial, supply: supply);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++; Simulation = new(Fixture); Simulation.AddItem(CupItem, Cup, ItemLocation.World("cup")); return Simulation;
        }
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var layout = new CookingRestaurantLayout(new("installed-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 8, 8) },
                new[] { new CookingEquipmentPlacement(Stove, Machine, new(4, 2), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
                new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 1)),
                    new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)), new("queue", CookingLayoutTargetKind.Queue, new(0, 4)),
                    new("table-1", CookingLayoutTargetKind.Table, new(2, 4)), new("exit", CookingLayoutTargetKind.Exit, new(7, 4)),
                    new("source", CookingLayoutTargetKind.Storage, new(1, 1)), new("receiving", CookingLayoutTargetKind.Receiving, new(2, 1)),
                    new("storage", CookingLayoutTargetKind.Storage, new(3, 1)), new("cup", CookingLayoutTargetKind.Storage, new(0, 1)) });
            return new(layout, new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Machine] = new(Machine, 1, 1, 0, -1) },
                new(250, 5000, 1000), new HashSet<DefinitionId> { Machine }, new HashSet<DefinitionId> { Machine },
                new Dictionary<StationSlotId, DefinitionId> { [Stove] = Machine }, 2, 3);
        }
        public CookingLevelEtHost Host(CookingLevelScope? scope = null) => new(new CookingLevelLifecycle(scope ?? Level, Config, this));
        public CookingLevelPreparation Preparation() => new(Level.Level, new("map"), new(new("logical-layout"), new[] { Stove }, new[] { Cup, Box }), Config.Identity);
    }
    private static CookingRecipeCommand Command(CookingLevelEtHost host, CookingRecipeOperation op, ItemId? item = null,
        ItemId? container = null, StationSlotId? station = null, string? world = null, OrderId? order = null,
        string? request = null, string? delivery = null) => new(Scope, host.HostFrameSequence + 1, Chef, new($"{op}-{host.HostFrameSequence}"), op,
            Item: item, Container: container, Station: station, WorldAnchor: world, Order: order,
            ExpectedItemVersion: item is null ? 0 : host.Observe().Recipe!.Items.Single(i => i.Id == item).Version,
            SupplierId: request is null ? null : "finite", SupplyRequestId: request, DeliveryId: delivery);
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(host.Binding.LevelScope, command, "local", command.Command.Value)).Accepted);
        var result = Assert.Single(host.Tick().Dispositions).Result!; Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.ToString()); return result;
    }
    private static CookingLevelObservation StableRead(Factory factory, CookingLevelEtHost host)
    {
        var recipeBefore = factory.Simulation.ExportCheckpoint().CanonicalText();
        var lifecycleBefore = host.Lifecycle.Snapshot(); var frame = host.HostFrameSequence;
        var pending = host.PendingCommandIdentityCount; var dispositions = host.DispositionHistory.Count;
        var observation = host.Observe(); Assert.Equal(observation.CanonicalText(), host.Observe().CanonicalText());
        Assert.Equal(recipeBefore, factory.Simulation.ExportCheckpoint().CanonicalText());
        Assert.Equal(lifecycleBefore, host.Lifecycle.Snapshot()); Assert.Equal(frame, host.HostFrameSequence);
        Assert.Equal(pending, host.PendingCommandIdentityCount); Assert.Equal(dispositions, host.DispositionHistory.Count);
        Assert.Equal(factory.Simulation.Snapshot().CanonicalText(), observation.Recipe!.CanonicalText());
        Assert.Equal(host.FrontOfHouseSnapshot!.CanonicalText(), observation.Front!.CanonicalText()); return observation;
    }

    [Fact]
    public void Observation_reads_real_supply_container_progress_binding_and_pause_without_mutating_owners()
    {
        var factory = new Factory(); CookingLevelCheckpoint saved; CookingLevelObservation observed;
        using (var host = factory.Host())
        {
            var created = host.Observe(); Assert.Null(created.Recipe); Assert.Null(created.InstalledLayout); Assert.Empty(created.Items);
            Assert.Equal(CookingLevelState.Created, created.Lifecycle.State); Assert.Equal(0, factory.CreateCount);
            Assert.True(host.BeginPreparation(factory.Preparation()).Accepted);
            var initialized = StableRead(factory, host); var frozenBefore = initialized.CanonicalText();
            Assert.Equal(new LayoutId("installed-layout"), initialized.InstalledLayout!.Id);
            Assert.Equal(new LayoutId("logical-layout"), initialized.Lifecycle.Layout);
            Assert.Equal(CookingFrontOfHouseFlow.SpatialIdentity(factory.Simulation.SpatialConfiguration!), initialized.GeometryIdentity);
            var request = Command(host, CookingRecipeOperation.RequestSupply, request: "r");
            Assert.True(host.TryEnqueue(new(Level, request, "local", "r")).Accepted);
            Assert.Empty(StableRead(factory, host).Recipe!.Supply!.Ledger.Deliveries); // Admitted commands are not committed inventory.
            var delivery = Assert.Single(host.Tick().Dispositions).Result!.Supply!.DeliveryId;
            var pending = StableRead(factory, host); Assert.DoesNotContain(pending.Inventory, i => i.DefinitionKey == Raw.Value);
            Assert.Equal(6, pending.Recipe!.Supply!.Ledger.Balances.Single().AvailableUnits);
            host.Tick(); var receipt = Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: delivery)).Supply!;
            var box = receipt.Package!.Value;
            Execute(host, Command(host, CookingRecipeOperation.Pickup, box));
            var held = StableRead(factory, host); Assert.Equal(box, Assert.Single(held.Players).HeldItem);
            Assert.Equal(3, held.Items.Single(i => i.Id == box).Contents.Count);
            Assert.Equal(3, held.Inventory.Single(i => i.DefinitionKey == Raw.Value).SupplyUnitsInTrackedPackages);
            Assert.Throws<NotSupportedException>(() => ((IList<ItemId>)held.Items.Single(i => i.Id == box).Contents).Clear());
            Execute(host, Command(host, CookingRecipeOperation.Drop, box, world: "storage"));
            Execute(host, Command(host, CookingRecipeOperation.TakeOut, receipt.Units[0], container: box));
            Assert.Equal(1, StableRead(factory, host).Inventory.Single(i => i.DefinitionKey == Raw.Value).SupplyHandUnits);
            Execute(host, Command(host, CookingRecipeOperation.Drop, receipt.Units[0], station: Stove));
            Execute(host, Command(host, CookingRecipeOperation.StartProcess, receipt.Units[0], station: Stove));
            Assert.Equal(1, Assert.Single(StableRead(factory, host).Recipe!.Processes).ElapsedTicks);
            host.Tick(); host.Tick(); var meal = host.Observe().Items.Single(i => i.IsProduct).Id;
            Execute(host, Command(host, CookingRecipeOperation.Pickup, meal));
            Execute(host, Command(host, CookingRecipeOperation.PutIn, meal, container: CupItem));
            Execute(host, Command(host, CookingRecipeOperation.Pickup, CupItem));
            Assert.True(host.CompletePreparation().Accepted); Assert.Equal(CookingLevelState.Ready, StableRead(factory, host).Lifecycle.State);
            Assert.True(host.Start().Accepted);
            for (var i = 0; i < 40 && host.Observe().Recipe!.Orders.Count == 0; i++) host.Tick();
            var order = Assert.Single(host.Observe().Recipe!.Orders).Id;
            Execute(host, Command(host, CookingRecipeOperation.BindOrder, meal, order: order));
            var bound = StableRead(factory, host); Assert.Equal(order, bound.Items.Single(i => i.Id == meal).BoundOrder);
            Assert.Equal(meal, bound.Items.Single(i => i.Id == CupItem).Contents.Single());
            Assert.Equal(CupItem, Assert.Single(bound.Players).HeldItem);
            Assert.True(host.Pause().Accepted); Assert.False(host.Tick().Accepted);
            Assert.Equal(CookingLevelState.Paused, StableRead(factory, host).Lifecycle.State);
            Assert.True(host.Resume().Accepted); observed = StableRead(factory, host); saved = host.ExportCheckpoint().Checkpoint!;
            Assert.Equal(frozenBefore, initialized.CanonicalText()); Assert.Equal(Level.LevelEpoch, observed.Scope.LevelEpoch);
        }
        var fresh = new Factory(); var recovered = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(recovered.Accepted, recovered.ToString()); using var restored = recovered.Host!;
        Assert.Equal(observed.CanonicalText(), StableRead(fresh, restored).CanonicalText()); Assert.Equal(1, fresh.CreateCount);
    }

    [Fact]
    public void Captured_scope_and_state_remain_distinct_from_a_new_generation_and_read_does_not_create_kitchen()
    {
        var factory = new Factory(); CookingLevelObservation old;
        using (var host = factory.Host())
        {
            Assert.True(host.Prepare(factory.Preparation()).Accepted); Assert.True(host.Start().Accepted); host.Tick();
            old = StableRead(factory, host);
        }
        var fresh = new Factory(); var nextScope = new CookingLevelScope(Level.MatchScope, Level.RestaurantRuntime, Level.Level, Level.LevelEpoch + 1);
        using var next = fresh.Host(nextScope); var created = next.Observe();
        Assert.Equal(nextScope, created.Scope); Assert.NotEqual(old.Scope, created.Scope);
        Assert.Equal(CookingLevelState.Running, old.Lifecycle.State); Assert.Equal(CookingLevelState.Created, created.Lifecycle.State);
        Assert.Equal(0, created.HostFrameSequence); Assert.Null(created.Recipe); Assert.Equal(0, fresh.CreateCount);
    }
}
