using AbilityKit.Game.Cooking.EtRuntime;

namespace AbilityKit.Game.Cooking.NetworkMeasurement;

internal sealed class MeasurementFixture : ICookingPreparationGameplayFactory
{
    internal PlayerId[] Players { get; }
    internal ItemId[] Tools { get; }
    internal static readonly CookingScope Match = new(new("measurement"), new("world"), new("match"));
    internal static readonly CookingLevelScope Scope = new(Match, new(1), new("preparing"), 1);
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), Box = new("box"), Machine = new("machine");
    private static readonly StationSlotId Stove = new("stove");
    private readonly CookingRecipeFixture _fixture;
    internal CookingConfigurationSnapshot Configuration { get; }
    internal int CreateCount { get; private set; }

    internal MeasurementFixture(int participants = 2)
    {
        if (participants is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(participants));
        Players = Enumerable.Range(0, participants).Select(i => new PlayerId("measure-" + (char)('a' + i))).ToArray();
        Tools = Enumerable.Range(0, participants).Select(i => new ItemId("tool-" + (char)('a' + i))).ToArray();
        var capabilities = new HashSet<string> { "cook" };
        var items = new[] { new CookingItemDefinition(Raw, capabilities), new(Product, capabilities),
            new(Box, capabilities, new(1, new HashSet<DefinitionId> { Raw })) };
        var appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
        var recipes = new[] { new CookingRecipeDefinition(new("cook"), new[] { Raw }, Product, new("heat"), "heat", 3) };
        var spatial = new CookingSpatialConfiguration(0, 0, 6000, 6000, 40, 800,
            Players.Select((player, i) => new CookingPlayerPose(player, i % 2 == 0 ? 1500 : 4500, i < 2 ? 1500 : 4500, 1, 0)).ToArray(),
            Tools.Select((tool, i) => new CookingSpatialAnchor(LocationKind.WorldPosition, tool.Value, i % 2 == 0 ? 1500 : 4500, i < 2 ? 1500 : 4500))
                .Append(new CookingSpatialAnchor(LocationKind.StationSlot, Stove.Value, 3500, 3500)).ToArray(), Array.Empty<CookingSpatialObstacle>(), 1000);
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
            new[] { new CookingLayoutTarget("entrance", CookingLayoutTargetKind.PlayerEntrance, new(1, 1)) }
                .Concat(Tools.Select((tool, i) => new CookingLayoutTarget(tool.Value, CookingLayoutTargetKind.Storage, new(i % 2 == 0 ? 1 : 4, i < 2 ? 1 : 4))))
                .Concat(new CookingLayoutTarget[] {
                new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 3)),
                new("queue", CookingLayoutTargetKind.Queue, new(0, 4)), new("exit", CookingLayoutTargetKind.Exit, new(5, 3)),
                new("table-1", CookingLayoutTargetKind.Table, new(2, 4)) }).ToArray()) { ActorRadius = 40 };
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
