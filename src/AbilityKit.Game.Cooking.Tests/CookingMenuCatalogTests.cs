using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingMenuCatalogTests
{
    private static CookingMenuCatalog Catalog() => CookingMenuCatalog.Load(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));

    private static CookingContentDocument Baseline() => JsonSerializer.Deserialize<CookingContentDocument>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void All_87_menus_60_preparations_72_supplies_19_stations_have_unique_runtime_ids()
    {
        var catalog = Catalog();
        var doc = catalog.Document;
        Assert.Equal(87, doc.Menus.Count);
        Assert.Equal(44, doc.Menus.Count(x => x.Category == CookingMenuCategory.Meal));
        Assert.Equal(12, doc.Menus.Count(x => x.Category == CookingMenuCategory.Dessert));
        Assert.Equal(31, doc.Menus.Count(x => x.Category == CookingMenuCategory.Drink));
        Assert.Equal(60, doc.Materials.Count(x => x.Kind == CookingMenuMaterialKind.Preparation));
        Assert.Equal(72, doc.Materials.Count(x => x.Kind == CookingMenuMaterialKind.Supply));
        Assert.Equal(19, doc.Stations.Count);
        Assert.Empty(CookingMenuCatalog.Validate(doc));
        var expected = Enumerable.Range(1, 44).Select(i => $"F{i:00}")
            .Concat(Enumerable.Range(1, 12).Select(i => $"S{i:00}"))
            .Concat(Enumerable.Range(1, 31).Select(i => $"D{i:00}"));
        Assert.Equal(expected.Order(), doc.Menus.Select(x => x.SourceId).Order());
        Assert.All(doc.Materials, x => Assert.NotEqual(x.SourceId, x.Id.Value));
        var closure = catalog.Requirements(doc.Menus.Select(x => x.SourceId));
        Assert.Equal(72, closure.Supplies.Count);
        Assert.Equal(doc.Steps.Count, closure.Recipes.Count);
    }

    [Theory]
    [InlineData("D12", "menu-preparation-hot-water", 2)]
    [InlineData("D13", "menu-preparation-hot-milk", 2)]
    [InlineData("D14", "menu-preparation-hot-milk", 2)]
    [InlineData("D31", "menu-preparation-green-tea", 2)]
    [InlineData("S11", "menu-supply-biscuit", 2)]
    [InlineData("D28", "menu-supply-milk", 2)]
    public void Game_portions_survive_graph_and_runtime_projection(string menu, string definition, int portions)
    {
        var catalog = Catalog();
        var recipes = catalog.Requirements(new[] { menu }).Recipes;
        var step = Assert.Single(catalog.Document.Steps, x => x.SourceId == menu &&
            x.Inputs.Any(i => i.Definition.Value == definition));
        Assert.Equal(portions, step.Inputs.Single(x => x.Definition.Value == definition).Portions);
        Assert.Equal(portions, CookingMenuCatalog.ExpandInputs(step).Count(x => x == definition));
        // Projection contract only: the core adapter adds Manual/Yield fields when published.
        var projected = catalog.ToContentDocument(Baseline(), new[] { menu }, ProjectionShape);
        Assert.Equal(portions, projected.Recipes.Single(x => x.Id == step.Id.Value).Inputs.Count(x => x == definition));
        Assert.Contains(step.Id, recipes);
    }

    [Fact]
    public void Shared_preparation_is_not_a_serial_chain_of_unrelated_branches()
    {
        var catalog = Catalog();
        var required = catalog.Requirements(new[] { "F21" });
        var steps = catalog.Document.Steps.Where(x => required.Recipes.Contains(x.Id)).ToDictionary(x => x.Output);
        var final = steps[new DefinitionId("menu-finished-tomato-pasta")];
        Assert.Equal(2, final.Inputs.Count);
        var pasta = steps[new DefinitionId("menu-preparation-cooked-pasta")];
        var sauce = steps[new DefinitionId("menu-preparation-hot-tomato-sauce")];
        Assert.DoesNotContain(pasta.Inputs, x => x.Definition == sauce.Output);
        Assert.DoesNotContain(sauce.Inputs, x => x.Definition == pasta.Output);
        Assert.Equal(new[] { "menu-supply-dry-pasta" }, pasta.Inputs.Select(x => x.Definition.Value));
        Assert.Equal(new[] { "menu-preparation-cut-tomato" }, sauce.Inputs.Select(x => x.Definition.Value));
    }

    [Theory]
    [InlineData("D18", "menu-preparation-milk-foam")]
    [InlineData("D19", "menu-preparation-espresso")]
    [InlineData("D20", "menu-preparation-whipped-cream")]
    [InlineData("D31", "menu-preparation-cheese-cap")]
    [InlineData("D02", "menu-preparation-dispensed-soda")]
    public void Must_last_additions_require_explicit_previous_stage(string menuId, string addition)
    {
        var doc = Catalog().Document;
        var menu = doc.Menus.Single(x => x.SourceId == menuId);
        var final = doc.Steps.Single(x => x.Id == menu.FinalRecipe);
        Assert.True(final.MustLast);
        Assert.Contains(new DefinitionId(addition), menu.FinalAdditions);
        Assert.Contains(final.Inputs, x => x.Definition.Value == addition);
        var prior = Assert.Single(final.Inputs, x => doc.Materials.Single(m => m.Id == x.Definition).Kind == CookingMenuMaterialKind.Stage);
        var invalid = doc with { Steps = doc.Steps.Select(x => x.Id == final.Id
            ? x with { Inputs = x.Inputs.Where(i => i != prior).ToArray() } : x).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(invalid), x => x.Code == "MustLastViolation");
        Assert.Throws<ArgumentException>(() => CookingMenuCatalog.Load(invalid));
    }

    [Fact]
    public void Premature_topping_in_prior_stage_is_rejected()
    {
        var doc = Catalog().Document;
        var final = doc.Steps.Single(x => x.SourceId == "D31" && x.MustLast);
        var priorId = final.Inputs.Single(x => x.Definition.Value.Contains("stage-")).Definition;
        var prior = doc.Steps.Single(x => x.Output == priorId);
        var bad = doc with { Steps = doc.Steps.Select(x => x.Id == prior.Id ? x with
        {
            Inputs = x.Inputs.Append(new CookingMenuInput(new DefinitionId("menu-preparation-cheese-cap"), 1)).ToArray(),
        } : x).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(bad), x => x.Code == "MustLastViolation" && x.Record == "D31");
    }

    [Fact]
    public void Supplier_ketchup_fresh_sauce_instant_tea_and_fresh_milk_are_distinct()
    {
        var catalog = Catalog();
        Assert.Contains(new DefinitionId("menu-supply-ketchup"), catalog.Requirements(new[] { "F15" }).Supplies);
        Assert.DoesNotContain(new DefinitionId("menu-supply-ketchup"), catalog.Requirements(new[] { "F21" }).Supplies);
        Assert.Contains(new DefinitionId("menu-supply-tomato"), catalog.Requirements(new[] { "F21" }).Supplies);
        Assert.Contains(new DefinitionId("menu-supply-instant-milk-tea"), catalog.Requirements(new[] { "D12" }).Supplies);
        Assert.DoesNotContain(catalog.Requirements(new[] { "D12" }).Supplies, x => x.Value.Contains("leaf"));
        Assert.Contains(new DefinitionId("menu-supply-black-tea-leaf"), catalog.Requirements(new[] { "D25" }).Supplies);
        Assert.DoesNotContain(catalog.Requirements(new[] { "D28" }).Supplies, x => x.Value.Contains("tea"));
        Assert.All(catalog.Document.Menus, x => Assert.Equal(x.Category == CookingMenuCategory.Drink, x.RequiresBinding));
    }

    [Fact]
    public void Level_missing_supply_station_container_or_drink_binding_is_blocked()
    {
        var catalog = Catalog();
        var required = catalog.Requirements(new[] { "D31" });
        var complete = new CookingMenuLevelAvailability(required.Supplies.ToHashSet(), required.Capabilities.ToHashSet(), required.Containers.ToHashSet());
        Assert.Empty(catalog.ValidateLevel(new[] { "D31" }, complete));
        var missing = complete with
        {
            Supplies = complete.Supplies.Where(x => x.Value != "menu-supply-soft-cheese").ToHashSet(),
            Capabilities = complete.Capabilities.Where(x => x != CookingMenuCatalog.BindingCapability).ToHashSet(),
            Containers = complete.Containers.Where(x => x.Value != "menu-container-cold-cup").ToHashSet(),
        };
        var errors = catalog.ValidateLevel(new[] { "D31" }, missing);
        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, x => x.Code == "MissingSupply");
        Assert.Contains(errors, x => x.Code == "MissingCapability");
        Assert.Contains(errors, x => x.Code == "MissingContainer");
        Assert.Throws<ArgumentException>(() => catalog.Requirements(new[] { "F99" }));
    }

    [Fact]
    public void Original_nodes_are_part_of_validated_catalog_and_trace_to_production_or_delivery()
    {
        var doc = Catalog().Document;
        Assert.Equal(367, doc.SourceNodes.Count);
        Assert.Equal(367, doc.SourceNodes.Select(x => x.SourceLocator).Distinct().Count());
        Assert.Equal(87, doc.SourceNodes.Count(x => x.Classification == "delivery-operation"));
        Assert.All(doc.SourceNodes.Where(x => x.SourceNode != "SERVE"), x => Assert.NotEmpty(x.Recipes));
        var sourceNode = doc.SourceNodes.First(x => x.SourceNode == "FINAL");
        var bad = doc with { SourceNodes = doc.SourceNodes.Select(x => x == sourceNode
            ? x with { Recipes = new[] { new RecipeId("missing") } } : x).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(bad), x => x.Code == "InvalidProvenance");
        var missing = doc with { SourceNodes = doc.SourceNodes.Where(x => x != sourceNode).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(missing), x => x.Code == "MissingProvenance");
    }

    [Fact]
    public void Delivery_binding_is_separate_and_disposable_cups_require_refill_supply()
    {
        var catalog = Catalog();
        var required = catalog.Requirements(new[] { "D31" });
        Assert.DoesNotContain(CookingMenuCatalog.BindingCapability, required.ProductionCapabilities);
        Assert.Equal(new[] { CookingMenuCatalog.BindingCapability }, required.DeliveryCapabilities);
        Assert.Contains(new DefinitionId("menu-container-cold-cup"), required.RefillContainers);
        var projected = catalog.ToContentDocument(Baseline(), new[] { "D31" }, ProjectionShape);
        Assert.StartsWith("world:", projected.StandardInitialSupply.Single(x => x.Definition == "menu-container-cold-cup").Location);
        var meal = catalog.ToContentDocument(Baseline(), new[] { "F01" }, ProjectionShape);
        Assert.Equal(CookingContentCatalog.CleanPoolLocation,
            meal.StandardInitialSupply.Single(x => x.Definition == "menu-container-plate").Location);
        Assert.Empty(catalog.Requirements(new[] { "F01" }).DeliveryCapabilities);
    }

    [Fact]
    public void Graph_cycle_and_missing_material_producer_are_rejected_before_load()
    {
        var doc = Catalog().Document;
        var first = doc.Steps.First();
        var cyclic = doc with { Steps = doc.Steps.Select(x => x.Id == first.Id
            ? x with { Inputs = new[] { new CookingMenuInput(x.Output, 1) } } : x).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(cyclic), x => x.Code == "Cycle");
        var noProducer = doc with { Steps = doc.Steps.Where(x => x.Id != first.Id).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(noProducer), x => x.Code == "MissingProducer");
        var missingInput = doc with { Steps = doc.Steps.Select(x => x.Id == first.Id
            ? x with { Inputs = new[] { new CookingMenuInput(new DefinitionId("absent"), 1) } } : x).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(missingInput), x => x.Code == "MissingReference");
    }

    [Fact]
    public void Invalid_schema_duplicate_ids_portions_and_modes_are_diagnostic()
    {
        var doc = Catalog().Document;
        Assert.Contains(CookingMenuCatalog.Validate(doc with { Schema = "cooking-menu-catalog-v0" }), x => x.Code == "UnknownSchema");
        Assert.Contains(CookingMenuCatalog.Validate(doc with { Materials = doc.Materials.Append(doc.Materials[0]).ToArray() }), x => x.Code == "DuplicateId");
        var bad = doc with { Steps = doc.Steps.Select((x, i) => i == 0 ? x with { YieldPortions = 0, ExecutionKind = (CookingMenuExecutionKind)99 } : x).ToArray() };
        Assert.Contains(CookingMenuCatalog.Validate(bad), x => x.Code == "InvalidPortions");
        Assert.Contains(CookingMenuCatalog.Validate(bad), x => x.Code == "InvalidMode");
    }

    [Fact]
    public void Canonical_is_order_independent_and_defensively_owned_but_changes_with_semantics()
    {
        var catalog = Catalog();
        var doc = catalog.Document;
        var reordered = doc with
        {
            Materials = doc.Materials.Reverse().ToArray(), Steps = doc.Steps.Reverse().Select(x => x with { Inputs = x.Inputs.Reverse().ToArray() }).ToArray(),
            Menus = doc.Menus.Reverse().ToArray(), Stations = doc.Stations.Reverse().ToArray(), Containers = doc.Containers.Reverse().ToArray(),
        };
        Assert.Equal(catalog.Sha256, CookingMenuCatalog.Load(reordered).Sha256);
        var changed = doc with { Steps = doc.Steps.Select((x, i) => i == 0 ? x with { RequiredTicks = 2 } : x).ToArray() };
        Assert.NotEqual(catalog.Sha256, CookingMenuCatalog.Load(changed).Sha256);
        var sourceChanged = doc with { Sources = doc.Sources.Select((x, i) => i == 0 ? x with { Sha256 = new string('a', 64) } : x).ToArray() };
        Assert.NotEqual(catalog.Sha256, CookingMenuCatalog.Load(sourceChanged).Sha256);
        ((IList<CookingMenuStep>)doc.Steps)[0] = doc.Steps[0] with { YieldPortions = 999 };
        Assert.NotEqual(999, catalog.Document.Steps[0].YieldPortions);
    }

    [Fact]
    public void Formal_loader_maps_compatible_recipe_and_preserves_soup_and_toast_regression()
    {
        var catalog = Catalog();
        var original = CookingContentCatalog.Load(Baseline());
        var loaded = catalog.LoadContent(Baseline(), new[] { "F31" });
        Assert.Equal(3, loaded.OrderTemplates.Count);
        foreach (var id in new[] { new RecipeId("tomato-egg-soup"), new RecipeId("bake-bread") })
        {
            var before = original.Recipes[id];
            var after = loaded.Recipes[id];
            Assert.Equal(before.Inputs, after.Inputs);
            Assert.Equal(before.DefaultInputs, after.DefaultInputs);
            Assert.Equal(before with { Inputs = after.Inputs, DefaultInputs = after.DefaultInputs }, after);
        }
        Assert.Equal(original.OrderTemplates[new OrderTemplateId("toasted-bread-order")], loaded.OrderTemplates[new OrderTemplateId("toasted-bread-order")]);
        Assert.NotEqual(original.Identity, loaded.Identity);
        Assert.Equal(loaded.Identity, catalog.LoadContent(Baseline(), new[] { "F31" }).Identity);
        Assert.Contains(new DefinitionId("menu-finished-steamed-fish"), loaded.Items.Keys);
        Assert.DoesNotContain(new DefinitionId("menu-finished-cheese-pizza"), loaded.Items.Keys);
        Assert.Equal("menu-container-steam-plate", loaded.OrderTemplates[new OrderTemplateId("menu-order-steamed-fish")].RequiredContainerDefinition.Value);
    }

    [Fact]
    public void Unsupported_core_contract_cannot_silently_drop_manual_batch_or_repeated_inputs()
    {
        var catalog = Catalog();
        Assert.Throws<InvalidOperationException>(() => catalog.LoadContent(Baseline(), new[] { "F01" }));
        Assert.Throws<InvalidOperationException>(() => catalog.LoadContent(Baseline(), new[] { "D31" }));
        Assert.Throws<ArgumentException>(() => catalog.ToContentDocument(Baseline(), new[] { "D12" }, step =>
            ProjectionShape(step) with { Inputs = CookingMenuCatalog.ExpandInputs(step).Distinct().ToArray() }));
    }

    [Fact]
    public void Compatible_automatic_route_uses_real_public_commands_from_raw_supply_to_settlement()
    {
        // Limited legacy compatibility proof while the required new core hooks are pending.
        // This does not claim Manual/batch/binding or full spatial/all-87 acceptance.
        var catalog = Catalog();
        var menu = catalog.Document.Menus.Single(x => x.SourceId == "F31");
        var step = catalog.Document.Steps.Single(x => x.Id == menu.FinalRecipe);
        var projected = catalog.ToContentDocument(Baseline(), new[] { menu.SourceId });
        var content = CookingContentCatalog.Load(projected with
        {
            // No ordinary-slot stacks or runtime-injected products: one raw source unit/vessel.
            StandardInitialSupply = projected.StandardInitialSupply.Where(x => x.Definition.StartsWith("menu-"))
                .Select(x => x with { Count = 1 }).ToArray(),
        });
        var scope = new CookingScope(new SessionId("menu-route"), new WorldId("menu-world"), new MatchId("menu-match"));
        var player = new PlayerId("chef");
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [player] = new(player, new HashSet<string> { "cook" }, content.Appliances.Keys.Select(x => x.Value).ToHashSet()),
        };
        var simulation = new CookingRecipeSimulation(CookingContentCatalog.BuildFixture(content, scope, players));
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        var vessel = simulation.Snapshot().Items.Single(x => x.Definition == step.Carrier).Id;
        var plate = simulation.Snapshot().Items.Single(x => x.Definition == menu.ServingContainer).Id;
        var raw = simulation.Snapshot().Items.Single(x => x.Definition == step.Inputs.Single().Definition).Id;
        var station = content.Appliances.Values.Single(x => x.Capabilities.Contains(step.Capability)).Station;
        var order = new OrderId("real-menu-order");
        Assert.True(simulation.OpenOrder(order, menu.OrderTemplate).Accepted);
        var sequence = 0;
        void Command(CookingRecipeOperation operation, ItemId item, StationSlotId? targetStation = null,
            ItemId? container = null, RecipeId? recipe = null, OrderId? targetOrder = null)
        {
            var state = simulation.Snapshot().Items.Single(x => x.Id == item);
            var result = simulation.Submit(new(scope, ++sequence, player, new RecipeCommandId($"menu-command-{sequence}"),
                operation, Recipe: recipe, Item: item, Station: targetStation, Container: container,
                Order: targetOrder, ExpectedItemVersion: state.Version));
            Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, $"{operation}: {result.Reason}");
        }
        Command(CookingRecipeOperation.Pickup, vessel);
        Command(CookingRecipeOperation.Drop, vessel, station);
        Command(CookingRecipeOperation.Pickup, plate);
        Command(CookingRecipeOperation.Drop, plate, new StationSlotId("counter-a"));
        Command(CookingRecipeOperation.Pickup, raw);
        Command(CookingRecipeOperation.PutIn, raw, container: vessel);
        Command(CookingRecipeOperation.StartProcess, vessel, station, recipe: step.Id);
        var level = new CookingLevelScope(scope, new RestaurantRuntimeId(1), new LevelId("menu-level"), 1);
        var tick = simulation.AdvanceFixedTick(level, 1);
        Assert.Equal(1, tick.AfterLogicalTick);
        var product = simulation.Snapshot().Items.Single(x => x.Definition == menu.Product);
        Assert.Equal(step.Id, product.Recipe);
        Command(CookingRecipeOperation.TakeOut, product.Id, container: vessel);
        Command(CookingRecipeOperation.PutIn, product.Id, container: plate);
        Command(CookingRecipeOperation.SubmitOrder, product.Id, targetOrder: order);
        var settlement = Assert.Single(simulation.SettlementHistory);
        Assert.Equal(menu.OrderTemplate, settlement.Template);
        Assert.Equal(menu.FinalRecipe, settlement.Recipe);
        Assert.Equal(plate, settlement.Container);
        Assert.Equal(100, simulation.Snapshot().TotalScore);
        Assert.DoesNotContain(simulation.Snapshot().Items, x => x.Id == raw || x.Id == product.Id);
        Assert.Empty(simulation.ItemsInContainer(vessel));
    }

    private static CookingContentRecipe ProjectionShape(CookingMenuStep step) => new(step.Id.Value,
        CookingMenuCatalog.ExpandInputs(step), step.Output.Value, step.Process.Value, step.Capability,
        step.RequiredTicks, Completion: CookingMenuCatalog.Completion(step));
}
