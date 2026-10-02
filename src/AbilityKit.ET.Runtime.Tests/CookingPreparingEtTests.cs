using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingPreparingEtTests
{
    private static readonly CookingScope Scope = new(new("preparing"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);
    private static readonly PlayerId Player = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Box = new("box"), Product = new("product"), Plate = new("plate"), Machine = new("machine");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly RecipeId Recipe = new("cook");
    private static readonly OrderTemplateId Template = new("template");
    private class Factory : ICookingFrontOfHouseGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public int CreateCount { get; private set; }
        public Action? DuringCreate { get; set; }
        public bool ReuseSimulation { get; set; }
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; } = new(new(1, 20, 3, 1, 2, 2, 100), new[] { Template });
        public Factory()
        {
            var caps = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, caps), new(Product, caps), new(Box, caps, new(3, new HashSet<DefinitionId> { Raw })),
                new(Plate, caps, new(1, new HashSet<DefinitionId> { Product })) };
            var appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Product, new("heat"), "heat", 3) };
            var orders = new[] { new CookingOrderTemplateDefinition(Template, Recipe, Plate) };
            var spatial = new CookingSpatialConfiguration(0, 0, 6000, 6000, 250, 5000,
                new[] { new CookingPlayerPose(Player, 500, 1500, 1, 0) }, new[] {
                    new CookingSpatialAnchor(LocationKind.StationSlot, "stove", 3500, 1500),
                    new(LocationKind.WorldPosition, "source", 1500, 1500), new(LocationKind.WorldPosition, "receiving", 2500, 1500),
                    new(LocationKind.WorldPosition, "storage", 3500, 1500), new(LocationKind.WorldPosition, "plate", 500, 1500) },
                Array.Empty<CookingSpatialObstacle>());
            var supply = new CookingSupplyConfiguration(new[] { new CookingSupplierDefinition("finite", "source", "receiving", Raw, Box, 3, 2, 9) });
            var candidate = new CookingConfigurationCandidate(new[] { "heat" }, items, appliances, recipes, OrderTemplates: orders, Spatial: spatial) { Supply = supply };
            var registry = new CookingConfigurationRegistry(); Assert.True(registry.Submit(candidate).Accepted); Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Player] = new(Player, caps, new HashSet<string> { "stove" }) },
                items.ToDictionary(i => i.Id), appliances.ToDictionary(a => a.Station), recipes.ToDictionary(r => r.Id),
                orderTemplates: orders.ToDictionary(o => o.Id), spatial: spatial, supply: supply);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++; DuringCreate?.Invoke();
            if (ReuseSimulation && Simulation is not null) return Simulation;
            Simulation = new(Fixture); Simulation.AddItem(new("plate"), Plate, ItemLocation.World("plate")); return Simulation;
        }
        public CookingLevelEtHost Host() => new(new CookingLevelLifecycle(Level, Config, this));
    }
    private sealed class PreparingFactory : Factory, ICookingPreparationGameplayFactory
    {
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var layout = new CookingRestaurantLayout(new("initial"), new[] { new CookingFloorRegion("floor", 0, 0, 6, 6) },
                new[] { new CookingEquipmentPlacement(Stove, Machine, new(3, 2), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
                new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 1)),
                    new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)), new("exit", CookingLayoutTargetKind.Exit, new(5, 3)) });
            return new(layout, new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Machine] = new(Machine, 1, 1, 0, -1) },
                new(250, 5000, 1000), new HashSet<DefinitionId> { Machine }, new HashSet<DefinitionId> { Machine },
                new Dictionary<StationSlotId, DefinitionId> { [Stove] = Machine }, 2, 3);
        }
    }
    private sealed class PreparingWithoutFrontFactory(PreparingFactory source) : ICookingPreparationGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => source.Create(scope, configuration);
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            source.CreatePreparationConfiguration(scope, configuration);
    }
    private static CookingLevelPreparation Preparation() => new(Level.Level, new("map"), new(new("layout"), new[] { Stove }, new[] { Plate, Box }), new Factory().Config.Identity);
    private static CookingRecipeCommand Command(CookingLevelEtHost host, CookingRecipeOperation op, string? request = null, string? delivery = null) =>
        new(Scope, host.HostFrameSequence + 1, Player, new($"{op}-{host.HostFrameSequence}"), op,
            SupplierId: request is null ? null : "finite", SupplyRequestId: request, DeliveryId: delivery);
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(Level, command, "local", command.Command.Value)).Accepted);
        var frame = host.Tick(); Assert.True(frame.Accepted); var result = Assert.Single(frame.Dispositions).Result!;
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome); return result;
    }
    private static void Item(Factory factory, CookingLevelEtHost host, CookingRecipeOperation op, ItemId item,
        ItemId? container = null, StationSlotId? station = null, string? world = null) => Execute(host,
            Command(host, op) with { Item = item, Container = container, Station = station, WorldAnchor = world,
                ExpectedItemVersion = factory.Simulation.Snapshot().Items.Single(i => i.Id == item).Version });

    [Fact]
    public void Preparation_stock_and_partial_process_survive_start_in_the_single_factory_kitchen_with_zero_front_clock()
    {
        var f = new PreparingFactory(); using var host = f.Host(); Assert.True(host.BeginPreparation(Preparation()).Accepted);
        var kitchen = f.Simulation; Assert.Equal(1, f.CreateCount); Assert.Same(kitchen, host.Driver.Simulation);
        var delivery = Execute(host, Command(host, CookingRecipeOperation.RequestSupply, "r")).Supply!.DeliveryId;
        host.Tick(); var received = Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: delivery)).Supply!;
        Assert.Equal(3, received.Units.Count); var box = received.Package!.Value;
        Item(f, host, CookingRecipeOperation.Pickup, box); Item(f, host, CookingRecipeOperation.Drop, box, world: "storage");
        var raw = received.Units[0]; Item(f, host, CookingRecipeOperation.TakeOut, raw, container: box);
        Item(f, host, CookingRecipeOperation.Drop, raw, station: Stove); Item(f, host, CookingRecipeOperation.StartProcess, raw, station: Stove);
        Assert.Equal(1, Assert.Single(kitchen.Snapshot().Processes).ElapsedTicks);
        Assert.Equal(0, host.FrontOfHouseSnapshot!.ServiceTicks); Assert.Empty(host.FrontOfHouseSnapshot.Customers);
        Assert.False(host.Pause().Accepted); var preparedTick = kitchen.LogicalTick;
        Assert.True(host.CompletePreparation().Accepted); Assert.False(host.Tick().Accepted);
        Assert.Equal(preparedTick, kitchen.LogicalTick); Assert.True(host.Start().Accepted);
        Assert.Same(kitchen, f.Simulation); Assert.Same(kitchen, host.Driver.Simulation); Assert.Equal(1, f.CreateCount);
        Assert.True(host.Tick().Accepted); Assert.Equal(1, host.FrontOfHouseSnapshot.ServiceTicks);
        Assert.Equal(2, Assert.Single(kitchen.Snapshot().Processes).ElapsedTicks);
        var checkpoint = host.ExportCheckpoint().Checkpoint!; Assert.Equal(preparedTick, checkpoint.ServiceStartLogicalTick);
        Assert.NotNull(checkpoint.PreparationConfigurationIdentity);
        Assert.True(host.Pause().Accepted); var paused = kitchen.ExportCheckpoint().CanonicalText(); Assert.False(host.Tick().Accepted);
        Assert.Equal(paused, kitchen.ExportCheckpoint().CanonicalText()); Assert.True(host.Resume().Accepted);
        host.Tick(); Assert.Single(kitchen.Snapshot().Items, i => i.IsProduct); Assert.Equal(2, kitchen.ItemsInContainer(box).Count);
    }

    [Fact]
    public void Preparing_disallows_delivery_binding_front_work_and_legacy_clock_without_queueing_them()
    {
        var f = new PreparingFactory(); using var host = f.Host(); Assert.True(host.BeginPreparation(Preparation()).Accepted);
        foreach (var op in new[] { CookingRecipeOperation.SubmitOrder, CookingRecipeOperation.BindOrder, CookingRecipeOperation.UnbindOrder,
                     CookingRecipeOperation.RebindOrder, CookingRecipeOperation.ClaimFrontWork, CookingRecipeOperation.ContinueFrontWork,
                     CookingRecipeOperation.StopFrontWork, CookingRecipeOperation.AdvanceTicks })
        {
            var c = Command(host, op) with { Item = new("item"), Order = new("order"), ExpectedItemVersion = 1 };
            if (op is CookingRecipeOperation.ClaimFrontWork or CookingRecipeOperation.ContinueFrontWork or CookingRecipeOperation.StopFrontWork)
                c = Command(host, op) with { WorldAnchor = "inquiry:customer-1" };
            if (op == CookingRecipeOperation.UnbindOrder) c = c with { Order = null };
            if (op == CookingRecipeOperation.AdvanceTicks) c = Command(host, op) with { TickCount = 1 };
            Assert.False(host.TryEnqueue(new(Level, c, "local", op.ToString())).Accepted);
        }
        Assert.Equal(0, host.PendingCommandIdentityCount); Assert.Equal(0, f.Simulation.LogicalTick);
        Assert.Throws<InvalidOperationException>(() => f.Simulation.AddItem(new("outside"), Raw, ItemLocation.World("source")));
        Assert.True(host.Tick().Accepted); Assert.Equal(0, host.FrontOfHouseSnapshot!.ServiceTicks);
    }

    [Fact]
    public void Legacy_factories_still_create_only_at_start_and_prepare_convenience_is_preserved()
    {
        var f = new Factory(); using var host = f.Host(); Assert.True(host.BeginPreparation(Preparation()).Accepted);
        Assert.Equal(0, f.CreateCount); Assert.False(host.Tick().Accepted); Assert.True(host.CompletePreparation().Accepted);
        Assert.True(host.Start().Accepted); Assert.Equal(1, f.CreateCount);
        var g = new PreparingFactory(); host.Dispose(); using var next = g.Host(); Assert.True(next.Prepare(Preparation()).Accepted);
        Assert.Equal(1, g.CreateCount); Assert.True(next.Start().Accepted); Assert.Equal(1, g.CreateCount);
    }

    [Fact]
    public void Invalid_input_factory_reentry_and_runtime_ownership_do_not_publish_a_second_kitchen()
    {
        var f = new PreparingFactory(); using var host = f.Host();
        Assert.False(host.BeginPreparation(Preparation() with { ConfigIdentity = new("wrong", "hash") }).Accepted);
        Assert.Equal(0, f.CreateCount); Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        f.DuringCreate = () => host.BeginPreparation(Preparation());
        Assert.False(host.BeginPreparation(Preparation()).Accepted); Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        f.DuringCreate = null; Assert.True(host.BeginPreparation(Preparation()).Accepted); f.ReuseSimulation = true;
        Assert.Throws<InvalidOperationException>(() => f.Host());
        Assert.True(host.Tick().Accepted);
        Assert.Equal(0, host.FrontOfHouseSnapshot!.ServiceTicks);
    }

    [Fact]
    public void Running_restore_preserves_preparation_identity_service_offset_and_exact_checkpoint()
    {
        var f = new PreparingFactory(); CookingLevelCheckpoint saved;
        using (var host = f.Host())
        {
            Assert.True(host.BeginPreparation(Preparation()).Accepted); host.Tick(); host.Tick();
            Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted); host.Tick();
            saved = host.ExportCheckpoint().Checkpoint!;
        }
        var fresh = new PreparingFactory(); var restored = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(restored.Accepted, restored.ToString()); using var active = restored.Host!;
        Assert.Equal(saved.CanonicalText(), active.ExportCheckpoint().Checkpoint!.CanonicalText()); Assert.Equal(1, fresh.CreateCount);
        Assert.False(CookingLevelEtHost.Restore(saved with { ServiceStartLogicalTick = -1 }, fresh.Config, fresh).Accepted);
        Assert.False(CookingLevelEtHost.Restore(saved with { PreparationConfigurationIdentity = "forged" }, fresh.Config, fresh).Accepted);
        Assert.False(CookingLevelEtHost.Restore(saved with { ServiceStartLogicalTick = 0 }, fresh.Config, fresh).Accepted);
        active.Tick(); Assert.Equal(2, active.FrontOfHouseSnapshot!.ServiceTicks);
    }

    private static CookingLevelCheckpoint ContinuePreparation(PreparingFactory factory, CookingLevelEtHost host, string pendingDelivery)
    {
        var received = Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: pendingDelivery)).Supply!;
        Assert.Equal(3, received.Units.Count);
        Assert.Equal(2, Assert.Single(factory.Simulation.Snapshot().Processes).ElapsedTicks);
        Assert.Equal(0, host.FrontOfHouseSnapshot!.ServiceTicks);
        Assert.True(host.CompletePreparation().Accepted);
        Assert.Equal(CookingLevelState.Ready, host.Lifecycle.State);
        Assert.True(host.Start().Accepted); Assert.Equal(1, factory.CreateCount);
        host.Tick(); Assert.Equal(1, host.FrontOfHouseSnapshot.ServiceTicks);
        Assert.Single(factory.Simulation.Snapshot().Items, i => i.IsProduct);
        return host.ExportCheckpoint().Checkpoint!;
    }

    [Fact]
    public void Preparing_checkpoint_preserves_pending_delivery_partial_process_and_final_running_canonical()
    {
        var factory = new PreparingFactory(); CookingLevelCheckpoint saved, uninterrupted; string pending;
        using (var host = factory.Host())
        {
            Assert.True(host.BeginPreparation(Preparation()).Accepted);
            Assert.True(host.ExportCheckpoint().Accepted); // Publication already binds this generation, even before its first tick.
            var first = Execute(host, Command(host, CookingRecipeOperation.RequestSupply, "first")).Supply!.DeliveryId;
            host.Tick();
            var received = Execute(host, Command(host, CookingRecipeOperation.ReceiveSupply, delivery: first)).Supply!;
            var box = received.Package!.Value;
            Item(factory, host, CookingRecipeOperation.Pickup, box);
            Item(factory, host, CookingRecipeOperation.Drop, box, world: "storage");
            Item(factory, host, CookingRecipeOperation.TakeOut, received.Units[0], container: box);
            Item(factory, host, CookingRecipeOperation.Drop, received.Units[0], station: Stove);
            pending = Assert.IsType<string>(Execute(host, Command(host, CookingRecipeOperation.RequestSupply, "pending")).Supply!.DeliveryId);
            Item(factory, host, CookingRecipeOperation.StartProcess, received.Units[0], station: Stove);
            saved = host.ExportCheckpoint().Checkpoint!;
            Assert.Equal(CookingLevelState.Preparing, saved.State); Assert.Equal(0, saved.ServiceStartLogicalTick);
            Assert.Equal(1, Assert.Single(factory.Simulation.Snapshot().Processes).ElapsedTicks);
            uninterrupted = ContinuePreparation(factory, host, pending);
        }
        var fresh = new PreparingFactory(); var recovered = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(recovered.Accepted, recovered.ToString()); using var restored = recovered.Host!;
        Assert.Equal(CookingLevelState.Preparing, restored.Lifecycle.State); Assert.Equal(1, fresh.CreateCount);
        Assert.Same(fresh.Simulation, restored.Driver.Simulation);
        Assert.Equal(saved.CanonicalText(), restored.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.Equal(uninterrupted.CanonicalText(), ContinuePreparation(fresh, restored, pending).CanonicalText());
    }

    [Fact]
    public void Preparing_restore_rejects_unsupported_state_outcome_layout_and_clock_before_kitchen_creation()
    {
        var factory = new PreparingFactory(); CookingLevelCheckpoint saved;
        using (var host = factory.Host())
        {
            Assert.True(host.BeginPreparation(Preparation()).Accepted); host.Tick();
            saved = host.ExportCheckpoint().Checkpoint!;
        }
        var layout = factory.CreatePreparationConfiguration(Level, factory.Config).InitialLayout;
        var invalid = new[] {
            saved with { State = CookingLevelState.Ready },
            saved with { State = CookingLevelState.Created },
            saved with { Outcome = CookingLevelOutcome.Success },
            saved with { InstalledLayout = new(layout, Array.Empty<CookingPlayerPose>()) },
            saved with { ServiceStartLogicalTick = 1 },
            saved with { FrontOfHouse = saved.FrontOfHouse! with { State = saved.FrontOfHouse.State with { ServiceTicks = 1 } } }
        };
        foreach (var checkpoint in invalid)
        {
            var fresh = new PreparingFactory();
            Assert.False(CookingLevelEtHost.Restore(checkpoint, fresh.Config, fresh).Accepted);
            Assert.Equal(0, fresh.CreateCount);
        }
        foreach (var state in Enum.GetValues<CookingLevelState>().Where(s => s is not CookingLevelState.Running and not CookingLevelState.Preparing))
        {
            var fresh = new PreparingFactory();
            Assert.False(CookingLevelEtHost.Restore(saved with { State = state }, fresh.Config, fresh).Accepted);
            Assert.Equal(0, fresh.CreateCount);
        }
    }

    [Fact]
    public void Zero_tick_preparing_checkpoint_is_bound_and_restores_without_starting_service()
    {
        var factory = new PreparingFactory(); CookingLevelCheckpoint saved;
        using (var host = factory.Host())
        {
            Assert.True(host.BeginPreparation(Preparation()).Accepted);
            saved = host.ExportCheckpoint().Checkpoint!;
            Assert.Equal(0, saved.Recipe.LogicalTick); Assert.Equal(Level, saved.Recipe.LevelScope);
        }
        var fresh = new PreparingFactory(); var recovery = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(recovery.Accepted, recovery.ToString()); using var active = recovery.Host!;
        Assert.Equal(saved.CanonicalText(), active.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.Equal(CookingLevelState.Preparing, active.Lifecycle.State);
        Assert.Equal(0, active.FrontOfHouseSnapshot!.ServiceTicks); Assert.Equal(1, fresh.CreateCount);
    }

    [Fact]
    public void Preparing_restore_rejects_zero_clock_customer_and_occupied_table_before_creating_kitchen()
    {
        var factory = new PreparingFactory(); CookingLevelCheckpoint saved;
        using (var host = factory.Host())
        {
            Assert.True(host.BeginPreparation(Preparation()).Accepted); host.Tick();
            saved = host.ExportCheckpoint().Checkpoint!;
        }
        var front = saved.FrontOfHouse!;
        var forged = saved with { FrontOfHouse = front with { State = front.State with {
            NextCustomerSequence = 1,
            Customers = new[] { new CookingCustomerSnapshot(new("customer-1"), "table-1", 1,
                CookingTablePhase.WaitingForInquiry, 0, null) },
            Tables = new[] { new CookingFrontTableSnapshot("table-1", CookingFrontTableState.Occupied, 0) }
        } } };
        var fresh = new PreparingFactory();
        var rejected = CookingLevelEtHost.Restore(forged, fresh.Config, fresh);
        rejected.Host?.Dispose();
        Assert.False(rejected.Accepted, rejected.ToString()); Assert.Equal(0, fresh.CreateCount);
        var variants = new[] {
            front with { State = front.State with { TicksUntilNextGuest = front.State.TicksUntilNextGuest + 1 } },
            front with { State = front.State with { Companion = front.State.Companion with { CompletedTaskCount = 1 } } },
            front with { State = front.State with { Tables = new[] { new CookingFrontTableSnapshot("table-1", CookingFrontTableState.Free, 1) } } },
            front with { State = front.State with { Closing = true } }
        };
        foreach (var variant in variants)
        {
            var candidate = new PreparingFactory();
            Assert.False(CookingLevelEtHost.Restore(saved with { FrontOfHouse = variant }, candidate.Config, candidate).Accepted);
            Assert.Equal(0, candidate.CreateCount);
        }
        var legacy = new PreparingFactory();
        Assert.False(CookingLevelEtHost.Restore(saved with { FrontOfHouseConfigurationIdentity = null }, legacy.Config,
            new PreparingWithoutFrontFactory(legacy)).Accepted);
        Assert.Equal(0, legacy.CreateCount);
    }
}
