using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-formal-content-and-orders</c>：正式内容目录加载、校验与实例化。
/// 内容是唯一正式来源：物品、工位、配方、订单模板与标准初始供应只存在于内容文档中。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingContentCatalogTests
{
    private static readonly SessionId Session = new("content-session");
    private static readonly WorldId World = new("content-world");
    private static readonly MatchId Match = new("content-match");

    private static CookingContent LoadContent() =>
        CookingContentCatalog.Load(File.ReadAllText(ContentPath()));

    private static string ContentPath() =>
        Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName);

    [Fact]
    public void C01_formal_content_loads_and_passes_v2_validation()
    {
        var first = LoadContent();
        var second = LoadContent();

        Assert.Equal(CookingConfigurationIdentity.CurrentSchema, first.Identity.Schema);
        Assert.False(string.IsNullOrWhiteSpace(first.Identity.Sha256));
        Assert.Equal(first.Identity, second.Identity);

        Assert.Equal(4, first.Recipes.Count);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(4, first.Appliances.Count);
        var template = Assert.Single(first.OrderTemplates);
        Assert.Equal("tomato-egg-soup-order", template.Key.Value);
        Assert.Equal("tomato-egg-soup", template.Value.RequiredRecipe.Value);
        Assert.Equal("bowl", template.Value.RequiredContainerDefinition.Value);

        Assert.Equal(5, first.StandardInitialSupply.Count);
        Assert.Contains(first.StandardInitialSupply, entry =>
            entry.Definition.Value == "pot" && entry.Location == "station:stove-a");
        Assert.Contains(first.StandardInitialSupply, entry =>
            entry.Definition.Value == "bowl" && entry.Location == CookingContentCatalog.CleanPoolLocation && entry.Count == 2);

        var soup = first.Recipes[new RecipeId("tomato-egg-soup")];
        Assert.Equal(CookingRecipeCompletionKind.RetainInputs, soup.Completion);
        Assert.Equal(6, soup.RequiredTicks);
        Assert.Equal(new[] { "water" }, soup.DefaultInputs!.Select(input => input.Value));

        var bake = first.Recipes[new RecipeId("bake-bread")];
        Assert.Equal("bread-slice", Assert.Single(bake.Inputs).Value);
        Assert.Equal("toasted-bread", bake.ProductDefinition.Value);

        var beat = first.Recipes[new RecipeId("beat-egg")];
        Assert.False(beat.RequiresStation);

        // 占位 dough 已退役：正式物品里不存在。
        Assert.DoesNotContain(first.Items.Keys, definition => definition.Value == "dough");
    }

    [Fact]
    public void C02_content_identity_is_independent_of_declaration_order()
    {
        var document = Deserialize(File.ReadAllText(ContentPath()));
        var reordered = document with
        {
            Items = document.Items.Reverse().ToArray(),
            Appliances = document.Appliances.Reverse().ToArray(),
            Recipes = document.Recipes.Reverse().ToArray(),
            OrderTemplates = document.OrderTemplates.Reverse().ToArray(),
            StandardInitialSupply = document.StandardInitialSupply.Reverse().ToArray(),
        };

        Assert.Equal(CookingContentCatalog.Load(document).Identity, CookingContentCatalog.Load(reordered).Identity);
    }

    [Fact]
    public void C03_bad_content_is_rejected_with_structured_diagnostics()
    {
        var document = Deserialize(File.ReadAllText(ContentPath()));

        var missingRecipe = document with
        {
            OrderTemplates = new[]
            {
                new CookingContentOrderTemplate("soup-order", "absent-recipe", "bowl"),
            },
        };
        var missingRecipeError = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(missingRecipe));
        Assert.Contains("OrderTemplate/soup-order/RequiredRecipe/MissingReference", missingRecipeError.Message);

        var containerRejectsProduct = document with
        {
            OrderTemplates = new[]
            {
                new CookingContentOrderTemplate("soup-order", "tomato-egg-soup", "pot"),
            },
        };
        var rejectsError = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(containerRejectsProduct));
        Assert.Contains("OrderTemplate/soup-order/RequiredContainerDefinition/MissingReference", rejectsError.Message);

        var missingSupplyItem = document with
        {
            StandardInitialSupply = new[]
            {
                new CookingContentSupplyEntry("absent-item", 1, "world:pantry"),
            },
        };
        var supplyError = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(missingSupplyItem));
        Assert.Contains("StandardInitialSupply/absent-item/Definition/MissingReference", supplyError.Message);

        var cleanPoolNotContainer = document with
        {
            StandardInitialSupply = new[]
            {
                new CookingContentSupplyEntry("tomato", 1, CookingContentCatalog.CleanPoolLocation),
            },
        };
        var poolError = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(cleanPoolNotContainer));
        Assert.Contains("StandardInitialSupply/tomato/Location/InvalidValue", poolError.Message);

        var zeroSupply = document with
        {
            StandardInitialSupply = new[]
            {
                new CookingContentSupplyEntry("tomato", 0, "world:pantry"),
            },
        };
        var zeroError = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(zeroSupply));
        Assert.Contains("StandardInitialSupply/tomato/Count/InvalidValue", zeroError.Message);

        var missingStation = document with
        {
            StandardInitialSupply = new[]
            {
                new CookingContentSupplyEntry("tomato", 1, "station:absent-station"),
            },
        };
        var stationError = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(missingStation));
        Assert.Contains("StandardInitialSupply/tomato/Location/MissingReference", stationError.Message);
    }

    [Fact]
    public void C04_foreign_schema_is_rejected_without_migration()
    {
        var document = Deserialize(File.ReadAllText(ContentPath())) with { Schema = "cooking-definition-v1" };
        var error = Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(document));
        Assert.Contains("cooking-definition-v1", error.Message);
    }

    [Fact]
    public void C05_fixture_and_standard_initial_supply_instantiate_from_content()
    {
        var content = LoadContent();
        var simulation = CreateSimulation(content);

        Assert.Equal(2, simulation.CleanContainerCount(new DefinitionId("bowl")));
        Assert.Equal(2, simulation.Snapshot().Items.Count(item => item.Definition.Value == "bowl"));
        Assert.Contains(simulation.Snapshot().Items,
            item => item.Id == new ItemId("pool-bowl-1") &&
                item.Location == ItemLocation.World(CookingContentCatalog.CleanPoolLocation));
        Assert.Contains(simulation.Snapshot().Items,
            item => item.Id == new ItemId("pot-1") && item.Location == ItemLocation.Station(new StationSlotId("stove-a")));
        Assert.Contains(simulation.Snapshot().Items,
            item => item.Id == new ItemId("tomato-2") && item.Location == ItemLocation.World("pantry"));
        Assert.Contains(simulation.Snapshot().Items,
            item => item.Id == new ItemId("bread-slice-1") && item.Location == ItemLocation.World("pantry"));

        // 内容数值只来自文档：切 2、打蛋 2、煮 6、烤 2。
        Assert.Equal(2, content.Recipes[new RecipeId("chop-tomato")].RequiredTicks);
        Assert.Equal(2, content.Recipes[new RecipeId("beat-egg")].RequiredTicks);
        Assert.Equal(6, content.Recipes[new RecipeId("tomato-egg-soup")].RequiredTicks);
        Assert.Equal(2, content.Recipes[new RecipeId("bake-bread")].RequiredTicks);
    }

    [Fact]
    public void C06_pot_accepts_only_declared_definitions_and_counter_has_no_capability()
    {
        var content = LoadContent();
        var pot = content.Items[new DefinitionId("pot")];
        Assert.NotNull(pot.Container);
        Assert.Equal(4, pot.Container.Capacity);
        Assert.Contains(new DefinitionId("chopped-tomato"), pot.Container.AcceptedDefinitions);
        Assert.Contains(new DefinitionId("beaten-egg"), pot.Container.AcceptedDefinitions);
        Assert.DoesNotContain(new DefinitionId("tomato"), pot.Container.AcceptedDefinitions);

        var counter = content.Appliances[new StationSlotId("counter-a")];
        Assert.Empty(counter.Capabilities);
        Assert.True(counter.IsAvailable);
    }

    private static CookingRecipeSimulation CreateSimulation(CookingContent content)
    {
        var scope = new CookingScope(Session, World, Match);
        var player = new PlayerId("chef-a");
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [player] = new(player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "board-a", "stove-a", "oven-a", "counter-a",
                }),
        };
        var simulation = new CookingRecipeSimulation(CookingContentCatalog.BuildFixture(content, scope, players));
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        return simulation;
    }

    private static CookingContentDocument Deserialize(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<CookingContentDocument>(json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            })!;
}
