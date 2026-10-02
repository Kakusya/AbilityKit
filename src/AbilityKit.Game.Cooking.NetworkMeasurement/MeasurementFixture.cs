using AbilityKit.Game.Cooking.EtRuntime;

namespace AbilityKit.Game.Cooking.NetworkMeasurement;

internal sealed class MeasurementFixture : ICookingPreparationGameplayFactory
{
    internal static readonly PlayerId[] Players = { new("measure-a"), new("measure-b") };
    internal static readonly ItemId[] Tools = { new("tool-a"), new("tool-b") };
    internal static readonly CookingScope Match = new(new("measurement"), new("world"), new("match"));
    internal static readonly CookingLevelScope Scope = new(Match, new(1), new("preparing"), 1);
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), Box = new("box"), Machine = new("machine");
    private static readonly StationSlotId Stove = new("stove");
    private readonly CookingRecipeFixture _fixture;
    internal CookingConfigurationSnapshot Configuration { get; }
    internal int CreateCount { get; private set; }

    internal MeasurementFixture()
    {
        var capabilities = new HashSet<string> { "cook" };
        var items = new[] { new CookingItemDefinition(Raw, capabilities), new(Product, capabilities),
            new(Box, capabilities, new(1, new HashSet<DefinitionId> { Raw })) };
        var appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
        var recipes = new[] { new CookingRecipeDefinition(new("cook"), new[] { Raw }, Product, new("heat"), "heat", 3) };
        var spatial = new CookingSpatialConfiguration(0, 0, 6000, 6000, 40, 800,
            new[] { new CookingPlayerPose(Players[0], 1500, 1500, 1, 0), new CookingPlayerPose(Players[1], 4500, 1500, 1, 0) },
            new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, Tools[0].Value, 1500, 1500),
                new(LocationKind.WorldPosition, Tools[1].Value, 4500, 1500),
                new(LocationKind.StationSlot, Stove.Value, 3500, 3500) }, Array.Empty<CookingSpatialObstacle>(), 1000);
        var registry = new CookingConfigurationRegistry();
        var registered = registry.Submit(new(new[] { "heat" }, items, appliances, recipes, Spatial: spatial));
        if (!registered.Accepted) throw new InvalidOperationException("Measurement registry rejected configuration: " + string.Join(",", registered.Validation.Diagnostics));
        Configuration = registry.Current!;
        _fixture = new(Match, Players.ToDictionary(p => p, p => new CookingPlayerConfig(p, capabilities, new HashSet<string> { Stove.Value })),
            items.ToDictionary(i => i.Id), appliances.ToDictionary(a => a.Station), recipes.ToDictionary(r => r.Id), spatial: spatial);
    }

    public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        if (scope != Scope || configuration.Identity != Configuration.Identity || ++CreateCount != 1)
            throw new InvalidOperationException("Untrusted or repeated measurement authority creation.");
        var simulation = new CookingRecipeSimulation(_fixture);
        foreach (var tool in Tools) simulation.AddItem(tool, Box, ItemLocation.World(tool.Value));
        return simulation;
    }

    public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        var layout = new CookingRestaurantLayout(new("measurement-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 6, 6) },
            new[] { new CookingEquipmentPlacement(Stove, Machine, new(3, 4), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
            new[] { new CookingLayoutTarget("entrance", CookingLayoutTargetKind.PlayerEntrance, new(1, 1)),
                new(Tools[0].Value, CookingLayoutTargetKind.Storage, new(1, 1)), new(Tools[1].Value, CookingLayoutTargetKind.Storage, new(4, 1)),
                new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)),
                new("queue", CookingLayoutTargetKind.Queue, new(0, 4)), new("exit", CookingLayoutTargetKind.Exit, new(5, 3)),
                new("table-1", CookingLayoutTargetKind.Table, new(2, 4)) }) { ActorRadius = 40 };
        return new(layout, new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Machine] = new(Machine, 1, 1, 0, -1) },
            new(40, 800, 1000), new HashSet<DefinitionId> { Machine }, new HashSet<DefinitionId> { Machine },
            new Dictionary<StationSlotId, DefinitionId> { [Stove] = Machine }, 2, 3);
    }

    internal CookingLevelEtHost CreateHost()
    {
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(Scope, Configuration, this));
        try {
            if (!host.BeginPreparation(new(Scope.Level, new("map"), new(new("measurement-layout"), new[] { Stove }, new[] { Box }), Configuration.Identity)).Accepted)
                throw new InvalidOperationException("Real Preparing initialization rejected.");
            return host;
        } catch { host.Dispose(); throw; }
    }
}
