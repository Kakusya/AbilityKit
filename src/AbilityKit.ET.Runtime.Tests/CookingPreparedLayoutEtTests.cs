using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingPreparedLayoutEtTests
{
    private static readonly CookingScope Scope = new(new("layout"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);
    private static readonly PlayerId Player = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), Plate = new("plate"), Machine = new("machine");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly ItemId Item = new("raw-item");
    private static readonly OrderTemplateId Template = new("template");
    private sealed class Factory : ICookingPreparationGameplayFactory, ICookingFrontOfHouseGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public int CreateCount { get; private set; }
        public CookingRestaurantLayout? LayoutOverride { get; init; }
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; } = new(new(1, 30, 3, 1, 2, 2, 100), new[] { Template });
        public Factory()
        {
            var capabilities = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, capabilities), new(Product, capabilities),
                new(Plate, capabilities, new(1, new HashSet<DefinitionId> { Product })) };
            var appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(new("cook"), new[] { Raw }, Product, new("heat"), "heat", 3,
                Execution: CookingRecipeExecutionKind.Manual) };
            var orders = new[] { new CookingOrderTemplateDefinition(Template, new("cook"), Plate) };
            var spatial = new CookingSpatialConfiguration(0, 0, 2000, 2000, 250, 1100,
                new[] { new CookingPlayerPose(Player, 500, 1500, 1, 0) },
                new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, "old", 1500, 1500),
                    new(LocationKind.StationSlot, Stove.Value, 1500, 500) }, Array.Empty<CookingSpatialObstacle>());
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "heat" }, items, appliances, recipes, OrderTemplates: orders, Spatial: spatial)).Accepted);
            Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Player] = new(Player, capabilities, new HashSet<string> { Stove.Value }) },
                items.ToDictionary(x => x.Id), appliances.ToDictionary(x => x.Station), recipes.ToDictionary(x => x.Id),
                orderTemplates: orders.ToDictionary(x => x.Id), spatial: spatial);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++; Simulation = new(Fixture); Simulation.AddItem(Item, Raw, ItemLocation.World("old")); return Simulation;
        }
        public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            new(LayoutOverride ?? Layout(), new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Machine] = new(Machine, 1, 1, 0, -1) },
                new(250, 1100, 1000), new HashSet<DefinitionId> { Machine }, new HashSet<DefinitionId> { Machine },
                new Dictionary<StationSlotId, DefinitionId> { [Stove] = Machine }, 2, 3);
        public CookingLevelEtHost Host() => new(new CookingLevelLifecycle(Level, Config, this));
        public CookingLevelPreparation Preparation() => new(Level.Level, new("map"), new(new("layout"), new[] { Stove }, new[] { Plate }), Config.Identity);
    }
    private static CookingRestaurantLayout Layout() => new(new("initial"), new[] { new CookingFloorRegion("floor", 0, 0, 8, 8) },
        new[] { new CookingEquipmentPlacement(Stove, Machine, new(4, 2), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
        new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 1)),
            new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)), new("queue", CookingLayoutTargetKind.Queue, new(0, 4)),
            new("exit", CookingLayoutTargetKind.Exit, new(7, 4)), new("table-1", CookingLayoutTargetKind.Table, new(6, 6)),
            new("old", CookingLayoutTargetKind.Storage, new(1, 1)), new("new", CookingLayoutTargetKind.Storage, new(2, 1)) });
    private static CookingRecipeCommandResult Execute(Factory factory, CookingLevelEtHost host, CookingRecipeOperation op,
        string? world = null, StationSlotId? station = null, int x = 0, int y = 0)
    {
        var move = op == CookingRecipeOperation.Move;
        var command = new CookingRecipeCommand(Scope, host.HostFrameSequence + 1, Player, new($"{op}-{host.HostFrameSequence}"), op,
            Item: move ? null : Item, Station: station, WorldAnchor: world,
            ExpectedItemVersion: move ? 0 : factory.Simulation.Snapshot().Items.Single(i => i.Id == Item).Version,
            MoveX: x * 1000, MoveY: y * 1000);
        Assert.True(host.TryEnqueue(new(Level, command, "local", command.Command.Value)).Accepted);
        return Assert.Single(host.Tick().Dispositions).Result!;
    }
    private static void Accepted(CookingRecipeCommandResult result) => Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);

    [Fact]
    public void Initial_layout_is_installed_with_real_bounds_obstacles_and_derived_front_flow()
    {
        var factory = new Factory(); using var host = factory.Host(); Assert.True(host.BeginPreparation(factory.Preparation()).Accepted);
        var checkpoint = host.ExportCheckpoint().Checkpoint!;
        Assert.NotNull(checkpoint.InstalledLayout); Assert.Equal(8000, factory.Simulation.SpatialConfiguration!.MaxX);
        Assert.Contains(factory.Simulation.SpatialConfiguration.Obstacles, o => o.MinX == 4000 && o.MinY == 2000);
        Assert.NotNull(host.FrontOfHouseSnapshot!.Flow);
        Assert.Equal(CookingFrontOfHouseFlow.SpatialIdentity(factory.Simulation.SpatialConfiguration), host.FrontOfHouseSnapshot.Flow.GeometryIdentity);
        Assert.All(checkpoint.InstalledLayout!.GeometrySeedPoses, p => Assert.Equal(-1, p.LastMovementTick));
        var before = checkpoint.CanonicalText(); Assert.True(host.TryInstallPreparedLayout(Layout()));
        Assert.Equal(before, host.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted);
        before = host.ExportCheckpoint().Checkpoint!.CanonicalText(); Assert.False(host.TryInstallPreparedLayout(Layout()));
        Assert.Equal(before, host.ExportCheckpoint().Checkpoint!.CanonicalText()); Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public void Invalid_live_anchor_or_front_route_rejects_without_any_checkpoint_change()
    {
        var factory = new Factory(); using var host = factory.Host(); Assert.True(host.BeginPreparation(factory.Preparation()).Accepted);
        var before = host.ExportCheckpoint().Checkpoint!.CanonicalText();
        Assert.False(host.TryInstallPreparedLayout(Layout() with { Targets = Layout().Targets.Where(t => t.Id != "old").ToArray() }));
        Assert.Equal(before, host.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.False(host.TryInstallPreparedLayout(Layout() with { Targets = Layout().Targets.Where(t => t.Id != "table-1").ToArray() }));
        Assert.Equal(before, host.ExportCheckpoint().Checkpoint!.CanonicalText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Changed_layout_restores_new_references_before_recipe_with_exact_canonical(bool running)
    {
        var factory = new Factory(); CookingLevelCheckpoint saved, uninterrupted;
        using (var host = factory.Host())
        {
            Assert.True(host.BeginPreparation(factory.Preparation()).Accepted);
            Accepted(Execute(factory, host, CookingRecipeOperation.Pickup));
            Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
            Accepted(Execute(factory, host, CookingRecipeOperation.Drop, world: "new"));
            var changed = Layout() with { Id = new("changed"), Targets = Layout().Targets.Where(t => t.Id != "old").ToArray() };
            Assert.True(host.TryInstallPreparedLayout(changed));
            saved = host.ExportCheckpoint().Checkpoint!;
            Assert.True(Assert.Single(saved.Recipe.Poses!).LastMovementTick >= 0);
            Assert.All(saved.InstalledLayout!.GeometrySeedPoses, p => Assert.Equal(-1, p.LastMovementTick));
            if (running) { Assert.True(host.CompletePreparation().Accepted); Assert.True(host.Start().Accepted); host.Tick(); saved = host.ExportCheckpoint().Checkpoint!; }
            Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1)); host.Tick();
            uninterrupted = host.ExportCheckpoint().Checkpoint!;
        }
        var fresh = new Factory(); var recovered = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(recovered.Accepted, recovered.ToString()); using var restored = recovered.Host!;
        Assert.Equal(saved.CanonicalText(), restored.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.DoesNotContain(fresh.Simulation.SpatialConfiguration!.Anchors, a => a.Id == "old");
        Assert.Equal(ItemLocation.World("new"), fresh.Simulation.Snapshot().Items.Single(i => i.Id == Item).Location);
        Accepted(Execute(fresh, restored, CookingRecipeOperation.Move, x: 1)); restored.Tick();
        Assert.Equal(uninterrupted.CanonicalText(), restored.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.Equal(1, fresh.CreateCount);
    }

    [Fact]
    public void Layout_restore_rejects_self_granted_equipment_radius_seed_and_flow()
    {
        var factory = new Factory(); CookingLevelCheckpoint saved;
        using (var host = factory.Host()) { Assert.True(host.BeginPreparation(factory.Preparation()).Accepted); saved = host.ExportCheckpoint().Checkpoint!; }
        var installed = saved.InstalledLayout!;
        var invalid = new[] {
            saved with { InstalledLayout = installed with { Layout = installed.Layout with { ActorRadius = 1 } } },
            saved with { InstalledLayout = installed with { Layout = installed.Layout with { Equipment = new[] { installed.Layout.Equipment[0] with { Definition = new("unauthorized") } } } } },
            saved with { InstalledLayout = installed with { GeometrySeedPoses = installed.GeometrySeedPoses.Select(p => p with { LastMovementTick = 0 }).ToArray() } },
            saved with { FrontOfHouse = saved.FrontOfHouse! with { State = saved.FrontOfHouse.State with { Flow = saved.FrontOfHouse.State.Flow! with { GeometryIdentity = "forged" } } } }
        };
        foreach (var checkpoint in invalid)
        {
            var fresh = new Factory(); Assert.False(CookingLevelEtHost.Restore(checkpoint, fresh.Config, fresh).Accepted); Assert.Equal(0, fresh.CreateCount);
        }
    }

    [Fact]
    public void Live_install_preserves_partial_manual_work_and_movement_watermark_but_releases_worker()
    {
        var factory = new Factory(); using var host = factory.Host(); Assert.True(host.BeginPreparation(factory.Preparation()).Accepted);
        Accepted(Execute(factory, host, CookingRecipeOperation.Pickup));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Drop, station: Stove));
        Accepted(Execute(factory, host, CookingRecipeOperation.StartProcess, station: Stove));
        var before = host.ExportCheckpoint().Checkpoint!; var process = Assert.Single(before.Recipe.Processes);
        Assert.Equal(1, process.ElapsedTicks); Assert.Equal(Player, process.ActiveWorker);
        Assert.True(host.TryInstallPreparedLayout(Layout())); // Exact repeat must preserve claim and version.
        Assert.Equal(before.CanonicalText(), host.ExportCheckpoint().Checkpoint!.CanonicalText());
        var changed = Layout() with { Id = new("table-moved"), Targets = Layout().Targets.Select(t => t.Id == "table-1" ? t with { Cell = new(7, 6) } : t).ToArray() };
        Assert.True(host.TryInstallPreparedLayout(changed));
        var after = host.ExportCheckpoint().Checkpoint!; var continued = Assert.Single(after.Recipe.Processes);
        Assert.Equal(process.ElapsedTicks, continued.ElapsedTicks); Assert.Null(continued.ActiveWorker);
        Assert.Equal(process.LockedInputs, continued.LockedInputs);
        Assert.Equal(before.Recipe.Poses, after.Recipe.Poses); Assert.Equal(before.Recipe.NextProductId, after.Recipe.NextProductId);
        Assert.Equal(before.Recipe.Items.Single().Version, after.Recipe.Items.Single().Version);
        host.Tick(); Assert.Equal(1, Assert.Single(factory.Simulation.Snapshot().Processes).ElapsedTicks);
        var command = new CookingRecipeCommand(Scope, host.HostFrameSequence + 1, Player, new("resume"), CookingRecipeOperation.ContinueProcess, Process: process.Id);
        Assert.True(host.TryEnqueue(new(Level, command, "local", "resume")).Accepted);
        Accepted(Assert.Single(host.Tick().Dispositions).Result!);
        Assert.Equal(2, Assert.Single(factory.Simulation.Snapshot().Processes).ElapsedTicks);
        host.Tick(); Assert.Single(factory.Simulation.Snapshot().Items, i => i.IsProduct);
    }

    [Theory]
    [InlineData("old")]
    [InlineData("table-1")]
    public void Invalid_trusted_initial_layout_faults_without_a_published_driver_and_releases_host(string omitted)
    {
        var factory = new Factory { LayoutOverride = Layout() with { Targets = Layout().Targets.Where(t => t.Id != omitted).ToArray() } };
        using (var host = factory.Host())
        {
            Assert.Throws<InvalidOperationException>(() => host.BeginPreparation(factory.Preparation()));
            Assert.True(host.IsFaulted); Assert.Null(host.Driver.Simulation);
            Assert.Throws<InvalidOperationException>(() => host.Tick());
        }
        var fresh = new Factory(); using var next = fresh.Host(); Assert.True(next.BeginPreparation(fresh.Preparation()).Accepted);
        Assert.True(next.Tick().Accepted);
    }

    [Fact]
    public void Moving_occupied_station_changes_actual_reach_and_preserves_manual_process_until_reclaimed_near_new_station()
    {
        var factory = new Factory(); using var host = factory.Host(); Assert.True(host.BeginPreparation(factory.Preparation()).Accepted);
        Accepted(Execute(factory, host, CookingRecipeOperation.Pickup));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Drop, station: Stove));
        Accepted(Execute(factory, host, CookingRecipeOperation.StartProcess, station: Stove));
        var before = host.ExportCheckpoint().Checkpoint!; var process = Assert.Single(before.Recipe.Processes);
        Assert.Equal(1, process.ElapsedTicks); Assert.Equal(Player, process.ActiveWorker);
        var moved = Layout() with { Id = new("station-moved"), Equipment = new[] { Layout().Equipment[0] with { Cell = new(6, 2) } } };
        Assert.True(host.TryInstallPreparedLayout(moved));
        var after = host.ExportCheckpoint().Checkpoint!; var retained = Assert.Single(after.Recipe.Processes);
        Assert.Equal(process.Id, retained.Id); Assert.Equal(Stove, retained.Station);
        Assert.Equal(Item, retained.Anchor); Assert.Equal(process.LockedInputs, retained.LockedInputs);
        Assert.Equal(1, retained.ElapsedTicks); Assert.Null(retained.ActiveWorker);
        Assert.Equal(ItemLocation.Station(Stove), after.Recipe.Items.Single().Location);
        Assert.Equal(before.Recipe.Items.Single().Version, after.Recipe.Items.Single().Version);
        CookingRecipeCommandResult Continue()
        {
            var command = new CookingRecipeCommand(Scope, host.HostFrameSequence + 1, Player, new($"continue-{host.HostFrameSequence}"),
                CookingRecipeOperation.ContinueProcess, Process: process.Id);
            Assert.True(host.TryEnqueue(new(Level, command, "local", command.Command.Value)).Accepted);
            return Assert.Single(host.Tick().Dispositions).Result!;
        }
        var remote = Continue(); Assert.Equal(CookingRecipeOutcome.Rejected, remote.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange, remote.Reason);
        var pickup = Execute(factory, host, CookingRecipeOperation.Pickup);
        Assert.Equal(CookingRecipeOutcome.Rejected, pickup.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange, pickup.Reason);
        Assert.Equal(1, Assert.Single(factory.Simulation.Snapshot().Processes).ElapsedTicks);
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Execute(factory, host, CookingRecipeOperation.Move, x: 1));
        Accepted(Continue()); Assert.Equal(2, Assert.Single(factory.Simulation.Snapshot().Processes).ElapsedTicks);
        host.Tick(); Assert.Empty(factory.Simulation.Snapshot().Processes);
        Assert.Single(factory.Simulation.Snapshot().Items, i => i.IsProduct && i.Location == ItemLocation.Station(Stove));
    }
}
