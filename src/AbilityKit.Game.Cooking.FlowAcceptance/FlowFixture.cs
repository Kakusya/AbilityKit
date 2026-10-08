using AbilityKit.Game.Cooking.EtRuntime;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

// Test configuration only: after Start, all writes belong to the existing ET host.
public sealed class FlowFixture : ICookingLevelGameplayFactory
{
    public static FixtureSpec Spec { get; } = new("one-item", "1.0", Array.AsReadOnly(new[] { "A", "B" }),
        "flow-item", "source", "drop");
    public CookingLevelScope Scope { get; }
    public CookingConfigurationSnapshot Configuration { get; }
    public CookingLevelPreparation Preparation { get; }
    private readonly CookingRecipeFixture recipe;

    public FlowFixture(RunIdentity run)
    {
        Scope = new(new(new(run.RunId), new("flow-world"), new("flow-match")), new(1), new("flow-level"), run.RunGeneration);
        var definition = new CookingItemDefinition(new("flow-ingredient"), new HashSet<string> { "cook" });
        var appliances = new[] { new CookingApplianceDefinition(new("source"), new HashSet<string>()),
            new CookingApplianceDefinition(new("drop"), new HashSet<string>()) };
        var players = new[] { new PlayerId("A"), new PlayerId("B") }.ToDictionary(p => p,
            p => new CookingPlayerConfig(p, new HashSet<string> { "cook" }, new HashSet<string> { "source", "drop" }));
        var spatial = new CookingSpatialConfiguration(0, 0, 3000, 3000, 40, 800,
            new[] { new CookingPlayerPose(new("A"), 800, 1000, 1, 0), new CookingPlayerPose(new("B"), 1200, 1000, -1, 0) },
            new[] { new CookingSpatialAnchor(LocationKind.StationSlot, "source", 1000, 1000),
                new CookingSpatialAnchor(LocationKind.StationSlot, "drop", 1000, 1200) }, Array.Empty<CookingSpatialObstacle>());
        var registry = new CookingConfigurationRegistry();
        var admitted = registry.Submit(new(Array.Empty<string>(), new[] { definition }, appliances,
            Array.Empty<CookingRecipeDefinition>(), Spatial: spatial));
        if (!admitted.Accepted || registry.Current is null) throw new InvalidOperationException("Invalid fixed fixture configuration.");
        Configuration = registry.Current;
        recipe = new(Scope.MatchScope, players, new Dictionary<DefinitionId, CookingItemDefinition> { [definition.Id] = definition },
            appliances.ToDictionary(a => a.Station), new Dictionary<RecipeId, CookingRecipeDefinition>(), spatial: spatial);
        Preparation = new(Scope.Level, new("flow-map"), new(new("flow-layout"), appliances.Select(a => a.Station).ToArray(),
            Array.Empty<DefinitionId>()), Configuration.Identity);
    }

    public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        if (scope != Scope || configuration.Identity != Configuration.Identity) throw new InvalidOperationException("Fixture binding mismatch.");
        var simulation = new CookingRecipeSimulation(recipe);
        simulation.AddItem(new(Spec.ItemId), new("flow-ingredient"), ItemLocation.Station(new(Spec.InitialSlot)));
        return simulation;
    }

    public CookingLevelEtHost CreateHost(int queueCapacity = 256)
    {
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(Scope, Configuration, this), queueCapacity);
        try
        {
            if (!host.Prepare(Preparation).Accepted || !host.Start().Accepted) throw new InvalidOperationException("Fixture startup rejected.");
            return host;
        }
        catch { host.Dispose(); throw; }
    }
}
