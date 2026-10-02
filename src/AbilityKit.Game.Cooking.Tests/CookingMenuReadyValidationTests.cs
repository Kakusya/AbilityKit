using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingMenuReadyValidationTests
{
    private static readonly CookingScope Scope = new(new("ready"), new("world"), new("match"));
    private static readonly PlayerId Chef = new("chef");
    private static CookingMenuCatalog Catalog() => CookingMenuCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));
    private static CookingContentDocument Baseline() => JsonSerializer.Deserialize<CookingContentDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        CookingContentCatalog.ContentFileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    private static (CookingMenuCatalog Catalog, CookingRecipeSimulation Simulation, CookingLevelMenuConfiguration Policy) Kitchen(params string[] menus)
    {
        var catalog = Catalog(); var content = catalog.LoadContent(Baseline(), menus);
        var player = new CookingPlayerConfig(Chef, content.Items.Values.SelectMany(i => i.AllowedPlayerCapabilities).ToHashSet(),
            content.Appliances.Keys.Select(s => s.Value).ToHashSet());
        var sim = new CookingRecipeSimulation(CookingContentCatalog.BuildFixture(content, Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = player }));
        CookingContentCatalog.ApplyStandardInitialSupply(sim, content);
        var policy = new CookingLevelMenuConfiguration(catalog.Sha256, menus, content.Items.Keys.ToHashSet(), new HashSet<DefinitionId>(), content.Items.Keys.ToHashSet(), true);
        return (catalog, sim, policy);
    }
    public static IEnumerable<object[]> Menus() => Enumerable.Range(1, 44).Select(i => new object[] { $"F{i:00}" })
        .Concat(Enumerable.Range(1, 12).Select(i => new object[] { $"S{i:00}" })).Concat(Enumerable.Range(1, 31).Select(i => new object[] { $"D{i:00}" }));
    [Theory, MemberData(nameof(Menus))]
    public void All_87_existing_menu_closures_validate_actual_kitchen(string menu)
    {
        var (catalog, sim, policy) = Kitchen(menu); var before = sim.ExportCheckpoint().CanonicalText();
        var result = CookingMenuReadyValidation.Validate(catalog, policy, sim.DescribeManufacturingAvailability());
        Assert.True(result.IsReady, string.Join(" | ", result.Diagnostics));
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText());
    }
    [Fact]
    public void Entire_candidate_catalog_reuses_shared_sources_and_stations_without_summing_stock()
    {
        var all = Menus().Select(row => (string)row[0]).ToArray(); var (catalog, sim, policy) = Kitchen(all);
        var result = CookingMenuReadyValidation.Validate(catalog, policy, sim.DescribeManufacturingAvailability());
        Assert.True(result.IsReady, string.Join(" | ", result.Diagnostics));
    }
    [Fact]
    public void Unknown_menu_and_foreign_catalog_are_structured_diagnostics()
    {
        var (catalog, sim, policy) = Kitchen("F01");
        var result = CookingMenuReadyValidation.Validate(catalog, policy with { SelectedMenuIds = new[] { "unknown" }, CatalogIdentity = "foreign" }, sim.DescribeManufacturingAvailability());
        Assert.False(result.IsReady); Assert.Contains(result.Diagnostics, d => d.Code == "UnknownMenu" && d.MenuId == "unknown");
        Assert.Contains(result.Diagnostics, d => d.Code == "CatalogMismatch");
    }
    [Theory]
    [InlineData("input")][InlineData("output")][InlineData("carrier")][InlineData("yield")][InlineData("completion")][InlineData("ticks")][InlineData("default")][InlineData("execution")][InlineData("process")][InlineData("capability")]
    public void Actual_runtime_node_must_match_every_catalog_execution_field(string field)
    {
        var (catalog, sim, policy) = Kitchen("F01"); var view = sim.DescribeManufacturingAvailability();
        var id = catalog.Requirements(new[] { "F01" }).Recipes.First(); var recipe = view.Recipes[id];
        var changed = field switch {
            "input" => recipe with { Inputs = recipe.Inputs.Concat(recipe.Inputs).ToArray() },
            "output" => recipe with { ProductDefinition = recipe.Inputs[0] },
            "carrier" => recipe with { RequiredProcessingContainerDefinition = null },
            "yield" => recipe with { YieldPortions = recipe.YieldPortions + 1 },
            "completion" => recipe with { Completion = recipe.Completion == CookingRecipeCompletionKind.ConsumeInputs ? CookingRecipeCompletionKind.RetainInputs : CookingRecipeCompletionKind.ConsumeInputs },
            "ticks" => recipe with { RequiredTicks = recipe.RequiredTicks + 1 },
            "default" => recipe with { DefaultInputs = recipe.Inputs.Concat(recipe.Inputs).ToArray() },
            "execution" => recipe with { Execution = recipe.Execution == CookingRecipeExecutionKind.Automatic ? CookingRecipeExecutionKind.Manual : CookingRecipeExecutionKind.Automatic },
            "process" => recipe with { Process = new("wrong-process") },
            _ => recipe with { RequiredApplianceCapability = "wrong-capability" } };
        var recipes = view.Recipes.ToDictionary(); recipes[id] = changed;
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, view with { Recipes = recipes }).Diagnostics,
            d => d.Code == "RecipeMismatch" && d.NodeId == id.Value);
    }
    [Theory][InlineData("supply")][InlineData("stage")][InlineData("container")]
    public void Material_permission_is_base_plus_confirmed_unlock_intersect_allowed(string kind)
    {
        var (catalog, sim, policy) = Kitchen("F01"); var req = catalog.Requirements(new[] { "F01" });
        var definition = kind switch { "supply" => req.Supplies.First(), "container" => req.Containers.First(), _ => catalog.Document.Steps.Single(s => s.Id == req.Recipes.First()).Output };
        var without = policy.BaseAuthorizedMaterialDefinitions.Where(d => d != definition).ToHashSet();
        var unlocked = policy with { BaseAuthorizedMaterialDefinitions = without, ConfirmedMaterialUnlocks = new HashSet<DefinitionId> { definition } };
        Assert.True(CookingMenuReadyValidation.Validate(catalog, unlocked, sim.DescribeManufacturingAvailability()).IsReady);
        var forbidden = unlocked with { AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Where(d => d != definition).ToHashSet() };
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, forbidden, sim.DescribeManufacturingAvailability()).Diagnostics,
            d => d.Code == "MaterialNotAuthorized" && d.Relation == definition.Value);
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy with { BaseAuthorizedMaterialDefinitions = without }, sim.DescribeManufacturingAvailability()).Diagnostics,
            d => d.Code == "MaterialNotAuthorized" && d.Relation == definition.Value);
    }
    [Fact]
    public void Capability_vocabulary_cannot_replace_installed_station_or_one_eligible_worker()
    {
        var (catalog, sim, policy) = Kitchen("F01"); var view = sim.DescribeManufacturingAvailability();
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, view with { Appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>() }).Diagnostics,
            d => d.Code == "MissingAppliance");
        var incapable = view.Players.ToDictionary(p => p.Key, p => p.Value with { Capabilities = new HashSet<string>() });
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, view with { Players = incapable }).Diagnostics, d => d.Code == "NoEligiblePlayer");
        var far = view.Players.ToDictionary(p => p.Key, p => p.Value with { ReachableStations = new HashSet<string>() });
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, view with { Players = far }).Diagnostics, d => d.Code == "UnreachableAppliance");
    }
    [Fact]
    public void Definition_only_container_is_not_a_source_and_capacity_is_real()
    {
        var (catalog, sim, policy) = Kitchen("F01"); var view = sim.DescribeManufacturingAvailability();
        var carrier = catalog.Document.Steps.Single(s => s.Id == catalog.Requirements(new[] { "F01" }).Recipes.Last()).Carrier;
        var seeds = view.RegisteredSeeds.Where(s => s.Definition != carrier).ToArray();
        var current = view.Current with { Items = view.Current.Items.Where(i => i.Definition != carrier).ToArray(), Containers = view.Current.Containers.Where(c => view.Current.Items.Single(i => i.Id == c.Id).Definition != carrier).ToArray() };
        var noSource = view with { RegisteredSeeds = seeds, Current = current,
            CleanContainerSupply = view.CleanContainerSupply.Where(p => p.Key != carrier).ToDictionary() };
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, noSource).Diagnostics, d => d.Code == "MissingSource" && d.Relation == carrier.Value);
        var definitions = view.ItemDefinitions.ToDictionary(); definitions[carrier] = definitions[carrier] with {
            Container = definitions[carrier].Container! with { AcceptedDefinitions = new HashSet<DefinitionId>() } };
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, view with { ItemDefinitions = definitions }).Diagnostics,
            d => d.Code == "ContainerIncompatible");
    }
    [Fact]
    public void Binding_is_trusted_command_support_not_a_physical_station_requirement()
    {
        var (catalog, sim, policy) = Kitchen("D31"); var view = sim.DescribeManufacturingAvailability();
        var noTicketStation = view with { Appliances = view.Appliances.Where(p => !p.Value.Capabilities.Contains(CookingMenuCatalog.BindingCapability)).ToDictionary() };
        Assert.True(CookingMenuReadyValidation.Validate(catalog, policy, noTicketStation).IsReady);
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy with { BindingCommandsEnabled = false }, noTicketStation).Diagnostics, d => d.Code == "BindingDisabled");
        var meal = Kitchen("F01"); Assert.True(CookingMenuReadyValidation.Validate(meal.Catalog, meal.Policy with { BindingCommandsEnabled = false }, meal.Simulation.DescribeManufacturingAvailability()).IsReady);
    }
    [Fact]
    public void Zero_stock_finite_supplier_warns_but_does_not_fail_structural_readiness()
    {
        var (catalog, sim, policy) = Kitchen("F01"); var view = sim.DescribeManufacturingAvailability();
        var raw = catalog.Requirements(new[] { "F01" }).Supplies.First(); var package = new DefinitionId("test-package");
        var items = view.ItemDefinitions.ToDictionary(); items[package] = new(package, view.Players[Chef].Capabilities, new(3, new HashSet<DefinitionId> { raw }));
        var supplier = new CookingSupplyConfiguration(new[] { new CookingSupplierDefinition("supplier", "source", "receiving", raw, package, 2, 3, 0) });
        var seeds = catalog.Requirements(new[] { "F01" }).Supplies.Concat(catalog.Requirements(new[] { "F01" }).Containers).Distinct()
            .Where(d => d != raw && !view.CleanContainerSupply.ContainsKey(d)).ToArray();
        var anchors = seeds.Select((d, i) => new CookingSpatialAnchor(LocationKind.WorldPosition, $"seed-{d.Value}", 1000, 2000 + i * 700))
            .Concat(view.Appliances.Keys.Select((s, i) => new CookingSpatialAnchor(LocationKind.StationSlot, s.Value, 8000, 1000 + i * 700)))
            .Concat(new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 5000, 1000),
                new CookingSpatialAnchor(LocationKind.WorldPosition, "receiving", 6000, 1000),
                new CookingSpatialAnchor(LocationKind.WorldPosition, view.CleanPoolLocation, 1000, 1000) }).ToArray();
        var spatial = new CookingSpatialConfiguration(0, 0, 10000, 10000, 100, 500, new[] { new CookingPlayerPose(Chef, 500, 500, 1, 0) }, anchors, Array.Empty<CookingSpatialObstacle>());
        var actual = new CookingRecipeSimulation(new(Scope, view.Players, items, view.Appliances, view.Recipes,
            cleanContainerSupply: view.CleanContainerSupply, cleanPoolLocation: view.CleanPoolLocation, orderTemplates: view.OrderTemplates, spatial: spatial, supply: supplier));
        foreach (var definition in seeds) actual.AddItem(new($"seed-{definition.Value}"), definition, ItemLocation.World($"seed-{definition.Value}"));
        view = actual.DescribeManufacturingAvailability();
        policy = policy with { BaseAuthorizedMaterialDefinitions = policy.BaseAuthorizedMaterialDefinitions.Append(package).ToHashSet(),
            AllowedMaterialDefinitions = policy.AllowedMaterialDefinitions.Append(package).ToHashSet() };
        var result = CookingMenuReadyValidation.Validate(catalog, policy, view);
        Assert.True(result.IsReady, string.Join(" | ", result.Diagnostics));
        Assert.Contains(result.Diagnostics, d => d.Code == "SupplyExhausted" && !d.Blocking);
        Assert.Contains(result.Diagnostics, d => d.Code == "NoCurrentStock" && !d.Blocking);
    }
    [Fact]
    public void Frozen_nested_views_and_confirmed_unlock_identity_survive_mutable_input_changes()
    {
        var (catalog, sim, policy) = Kitchen("F01"); var view = sim.DescribeManufacturingAvailability();
        var capabilities = new HashSet<string> { "one" }; var inputs = new List<DefinitionId> { view.ItemDefinitions.Keys.First() };
        var recipes = view.Recipes.ToDictionary(); var id = recipes.Keys.First(); recipes[id] = recipes[id] with { Inputs = inputs };
        var items = view.ItemDefinitions.ToDictionary(); var definition = items.Keys.First(); items[definition] = items[definition] with { AllowedPlayerCapabilities = capabilities };
        var frozen = (view with { Recipes = recipes, ItemDefinitions = items }).Freeze();
        var unlocks = new HashSet<DefinitionId> { definition }; var trusted = (policy with { ConfirmedMaterialUnlocks = unlocks }).Freeze(); var identity = trusted.Identity();
        capabilities.Clear(); inputs.Clear(); recipes.Clear(); items.Clear(); unlocks.Clear();
        Assert.Single(frozen.Recipes[id].Inputs); Assert.Contains("one", frozen.ItemDefinitions[definition].AllowedPlayerCapabilities);
        Assert.Equal(identity, trusted.Identity()); Assert.Contains(definition, trusted.ConfirmedMaterialUnlocks);
        Assert.Equal(policy.Identity(), (policy with { SelectedMenuIds = policy.SelectedMenuIds.Reverse().ToArray(), BaseAuthorizedMaterialDefinitions = policy.BaseAuthorizedMaterialDefinitions.Reverse().ToHashSet() }).Identity());
        Assert.NotEqual(identity, (trusted with { ConfirmedMaterialUnlocks = new HashSet<DefinitionId>() }).Identity());
        Assert.Throws<NotSupportedException>(() => ((IList<CookingRecipeSnapshotItem>)frozen.Current.Items).Clear());
    }
    [Fact]
    public void Simulation_query_detaches_the_actual_fixture_collections_and_seed_registry()
    {
        var caps = new HashSet<string> { "cook" }; var accepted = new HashSet<DefinitionId> { new("raw") };
        var inputs = new List<DefinitionId> { new("raw") }; var recipeId = new RecipeId("recipe");
        var definitions = new Dictionary<DefinitionId, CookingItemDefinition> {
            [new("raw")] = new(new("raw"), caps), [new("out")] = new(new("out"), caps),
            [new("pot")] = new(new("pot"), caps, new(2, accepted)) };
        var player = new CookingPlayerConfig(Chef, caps, new HashSet<string> { "station" });
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition> { [recipeId] = new(recipeId, inputs, new("out"), new("process"), "heat", 1) };
        var sim = new CookingRecipeSimulation(new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = player }, definitions,
            new Dictionary<StationSlotId, CookingApplianceDefinition> { [new("station")] = new(new("station"), new HashSet<string> { "heat" }) }, recipes));
        sim.AddItem(new("seed"), new("raw"), ItemLocation.World("supply"));
        var frozen = sim.DescribeManufacturingAvailability();
        caps.Clear(); accepted.Clear(); inputs.Clear(); definitions.Clear(); recipes.Clear();
        Assert.Contains("cook", frozen.Players[Chef].Capabilities); Assert.Contains("cook", frozen.ItemDefinitions[new("raw")].AllowedPlayerCapabilities);
        Assert.Contains(new DefinitionId("raw"), frozen.ItemDefinitions[new("pot")].Container!.AcceptedDefinitions);
        Assert.Single(frozen.Recipes[recipeId].Inputs); Assert.Equal("seed", frozen.RegisteredSeeds.Single().Id.Value);
    }
    [Fact]
    public void Individual_worker_must_cover_all_inputs_and_carrier_for_a_node()
    {
        var (catalog, sim, policy) = Kitchen("F01"); var view = sim.DescribeManufacturingAvailability();
        var recipe = view.Recipes[catalog.Requirements(new[] { "F01" }).Recipes.First()];
        var items = view.ItemDefinitions.ToDictionary();
        items[recipe.Inputs.First()] = items[recipe.Inputs.First()] with { AllowedPlayerCapabilities = new HashSet<string> { "input-only" } };
        var carrier = recipe.RequiredProcessingContainerDefinition!.Value;
        items[carrier] = items[carrier] with { AllowedPlayerCapabilities = new HashSet<string> { "carrier-only" } };
        var players = new Dictionary<PlayerId, CookingPlayerConfig> {
            [Chef] = view.Players[Chef] with { Capabilities = new HashSet<string> { "input-only" } },
            [new("second")] = view.Players[Chef] with { Id = new("second"), Capabilities = new HashSet<string> { "carrier-only" } } };
        Assert.Contains(CookingMenuReadyValidation.Validate(catalog, policy, view with { ItemDefinitions = items, Players = players }).Diagnostics,
            d => d.Code == "NoEligiblePlayer" && d.NodeId == recipe.Id.Value);
    }
    [Theory][InlineData(100, false, true)][InlineData(100, true, false)][InlineData(400, false, false)]
    public void Static_actual_geometry_checks_far_targets_walls_and_actor_clearance(int radius, bool wall, bool reachable)
    {
        // Lower and upper obstacles leave a 500-unit corridor: radius 100 passes, radius 400 cannot.
        var obstacles = wall ? new[] { new CookingSpatialObstacle(4800, 0, 5200, 10000) }
            : new[] { new CookingSpatialObstacle(4000, 0, 6000, 4750), new CookingSpatialObstacle(4000, 5250, 6000, 10000) };
        var pose = new CookingPlayerPose(Chef, 1000, 5000, 1, 0); var anchor = new CookingSpatialAnchor(LocationKind.StationSlot, "station", 9000, 5000);
        var spatial = new CookingSpatialConfiguration(0, 0, 10000, 10000, radius, 500, new[] { pose }, new[] { anchor }, obstacles);
        Assert.Equal(reachable ? CookingStaticReachability.Reachable : CookingStaticReachability.Unreachable, CookingManufacturingPaths.Reach(spatial, pose, anchor));
    }
    [Fact]
    public void Anchor_interaction_face_is_reachable_even_if_anchor_center_is_not_walkable()
    {
        var pose = new CookingPlayerPose(Chef, 1000, 1000, 1, 0); var anchor = new CookingSpatialAnchor(LocationKind.StationSlot, "station", 5000, 4900);
        var spatial = new CookingSpatialConfiguration(0, 0, 10000, 10000, 200, 1000, new[] { pose }, new[] { anchor },
            new[] { new CookingSpatialObstacle(4500, 5000, 5500, 6000) });
        Assert.False(spatial.ValidPose(pose with { X = anchor.X, Y = anchor.Y }));
        Assert.Equal(CookingStaticReachability.Reachable, CookingManufacturingPaths.Reach(spatial, pose, anchor));
    }
    [Fact]
    public void Geometry_complexity_limit_is_explicit_and_cannot_return_reachable()
    {
        var pose = new CookingPlayerPose(Chef, 1000, 1000, 1, 0); var anchor = new CookingSpatialAnchor(LocationKind.StationSlot, "station", 90000, 90000);
        var obstacles = Enumerable.Range(1, 40).Select(i => new CookingSpatialObstacle(i * 2000, i * 2000, i * 2000 + 100, i * 2000 + 100)).ToArray();
        var spatial = new CookingSpatialConfiguration(0, 0, 100000, 100000, 100, 500, new[] { pose }, new[] { anchor }, obstacles);
        Assert.Equal(CookingStaticReachability.ComplexityLimit, CookingManufacturingPaths.Reach(spatial, pose, anchor));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Actual_spatial_kitchen_readiness_is_static_reach_not_current_distance(bool dividedByWall)
    {
        var (catalog, legacy, policy) = Kitchen("F01"); var view = legacy.DescribeManufacturingAvailability();
        var required = catalog.Requirements(new[] { "F01" }); var definitions = required.Supplies.Concat(required.Containers).Distinct().ToArray();
        var sourceAnchors = definitions.Select((d, i) => new CookingSpatialAnchor(LocationKind.WorldPosition, $"source-{i}", 1000, 1000 + i * 700)).ToArray();
        var stationAnchors = view.Appliances.Keys.Select((s, i) => new CookingSpatialAnchor(LocationKind.StationSlot, s.Value, 8000, 1000 + i * 700)).ToArray();
        var pose = new CookingPlayerPose(Chef, 1000, 1000, -1, 0);
        var spatial = new CookingSpatialConfiguration(0, 0, 10000, 10000, 100, 500, new[] { pose }, sourceAnchors.Concat(stationAnchors).ToArray(),
            dividedByWall ? new[] { new CookingSpatialObstacle(4500, 0, 5500, 10000) } : Array.Empty<CookingSpatialObstacle>());
        var fixture = new CookingRecipeFixture(Scope, view.Players, view.ItemDefinitions, view.Appliances, view.Recipes, orderTemplates: view.OrderTemplates, spatial: spatial);
        var actual = new CookingRecipeSimulation(fixture);
        for (var i = 0; i < definitions.Length; i++) actual.AddItem(new($"seed-{i}"), definitions[i], ItemLocation.World(sourceAnchors[i].Id));
        var before = actual.ExportCheckpoint().CanonicalText();
        var result = CookingMenuReadyValidation.Validate(catalog, policy, actual.DescribeManufacturingAvailability());
        Assert.Equal(!dividedByWall, result.IsReady);
        if (dividedByWall) Assert.Contains(result.Diagnostics, d => d.Code == "UnreachableAppliance");
        Assert.Equal(before, actual.ExportCheckpoint().CanonicalText());
    }
    [Fact]
    public void Committed_discard_keeps_historical_seed_witness_without_reviving_stock()
    {
        var (catalog, sim, policy) = Kitchen("F01"); var raw = catalog.Requirements(new[] { "F01" }).Supplies.First();
        foreach (var item in sim.Snapshot().Items.Where(i => i.Definition == raw).ToArray())
        {
            var result = sim.Submit(new(Scope, 1, Chef, new($"discard-{item.Id.Value}"), CookingRecipeOperation.DiscardItem,
                Item: item.Id, ExpectedItemVersion: item.Version));
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        }
        var before = sim.ExportCheckpoint().CanonicalText(); var view = sim.DescribeManufacturingAvailability();
        Assert.DoesNotContain(view.Current.Items, i => i.Definition == raw); Assert.Contains(view.RegisteredSeeds, s => s.Definition == raw);
        var resultReady = CookingMenuReadyValidation.Validate(catalog, policy, view);
        Assert.True(resultReady.IsReady); Assert.Contains(resultReady.Diagnostics, d => d.Code == "NoCurrentStock" && d.Relation == raw.Value && !d.Blocking);
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText());
    }
}
