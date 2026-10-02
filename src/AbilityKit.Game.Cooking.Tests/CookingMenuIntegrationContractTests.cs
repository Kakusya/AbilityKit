using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingMenuIntegrationContractTests
{
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), Pot = new("pot"), Bowl = new("bowl");
    private static readonly RecipeId Recipe = new("cook");
    private static CookingConfigurationCandidate Candidate() => new(new[] { "cook" },
        new CookingItemDefinition[] {
            new(Raw, new HashSet<string> { "carry" }), new(Product, new HashSet<string> { "carry" }),
            new(Pot, new HashSet<string> { "carry" }, new(2, new HashSet<DefinitionId> { Raw, Product })),
            new(Bowl, new HashSet<string> { "carry" }, new(2, new HashSet<DefinitionId> { Raw, Product })) },
        new[] { new CookingApplianceDefinition(new("stove"), new HashSet<string> { "cook" }) },
        new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Product, new("cook"), "cook", 2,
            RequiredProcessingContainerDefinition: Pot) });

    private static CookingContentProvenance Provenance(string hash = "a") => new("cooking-menu-catalog-v1", new string(hash[0], 64),
        new[] { new CookingContentSourceIdentity("menu.md", new string('b', 64)) }, new[] { "F31" });

    [Fact]
    public void Carrier_filters_matching_and_is_part_of_configuration_identity()
    {
        var candidate = Candidate();
        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, CookingRecipeMatcher.Match(new[] { Raw }, null, candidate.Recipes, Bowl).Outcome);
        Assert.Equal(CookingRecipeMatchOutcome.Matched, CookingRecipeMatcher.Match(new[] { Raw }, null, candidate.Recipes, Pot).Outcome);
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(candidate).Accepted);
        var first = registry.Current!.Identity;
        Assert.True(registry.Submit(candidate with { Recipes = new[] { candidate.Recipes[0] with { RequiredProcessingContainerDefinition = Bowl } } }).Accepted);
        Assert.NotEqual(first, registry.Current!.Identity);
    }

    [Fact]
    public void Wrong_carrier_start_and_preview_leave_gameplay_unchanged()
    {
        var candidate = Candidate();
        var player = new PlayerId("player");
        var scope = new CookingScope(new("s"), new("w"), new("m"));
        var fixture = new CookingRecipeFixture(scope,
            new Dictionary<PlayerId, CookingPlayerConfig> { [player] = new(player, new HashSet<string> { "carry" }, new HashSet<string> { "stove" }) },
            candidate.Items.ToDictionary(item => item.Id), candidate.Appliances.ToDictionary(item => item.Station), candidate.Recipes.ToDictionary(item => item.Id));
        var simulation = new CookingRecipeSimulation(fixture);
        var carrier = new ItemId("carrier");
        simulation.AddItem(carrier, Bowl, ItemLocation.Station(new("stove")));
        simulation.AddItem(new("ingredient"), Raw, ItemLocation.Container(carrier, "slot-0"));
        var before = simulation.Snapshot().CanonicalText();
        var preview = simulation.PreviewInteraction(player, 1, new("preview"));
        Assert.DoesNotContain(preview, item => item.Command.Operation == CookingRecipeOperation.StartProcess);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        var result = simulation.Submit(new(scope, 1, player, new("wrong-carrier"), CookingRecipeOperation.StartProcess,
            Item: carrier, Station: new("stove"), Recipe: Recipe, ExpectedItemVersion: simulation.Snapshot().Items.Single(item => item.Id == carrier).Version));
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, result.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("raw")]
    public void Missing_or_noncontainer_carrier_rejects_configuration(string carrier)
    {
        var candidate = Candidate();
        var registry = new CookingConfigurationRegistry();
        Assert.False(registry.Submit(candidate with { Recipes = new[] { candidate.Recipes[0] with { RequiredProcessingContainerDefinition = new(carrier) } } }).Accepted);
        Assert.Null(registry.Current);
    }

    [Fact]
    public void Processing_carrier_must_fit_every_input_and_accept_each_definition()
    {
        var candidate = Candidate();
        var registry = new CookingConfigurationRegistry();
        var insufficient = candidate with { Recipes = new[] { candidate.Recipes[0] with { Inputs = new[] { Raw, Raw, Raw } } } };
        Assert.False(registry.Submit(insufficient).Accepted);
        var incompatible = candidate with { Items = candidate.Items.Select(item => item.Id == Pot
            ? item with { Container = new(2, new HashSet<DefinitionId> { Product }) } : item).ToArray() };
        Assert.False(registry.Submit(incompatible).Accepted);
        Assert.Null(registry.Current);
    }

    [Fact]
    public void Formal_content_loader_preserves_carrier_and_provenance_in_actual_snapshot()
    {
        var candidate = Candidate();
        var document = new CookingContentDocument(CookingConfigurationIdentity.CurrentSchema,
            candidate.SupportedApplianceCapabilities,
            candidate.Items.Select(item => new CookingContentItem(item.Id.Value, item.AllowedPlayerCapabilities.ToArray(),
                item.Container is null ? null : new(item.Container.Capacity, item.Container.AcceptedDefinitions.Select(id => id.Value).ToArray()))).ToArray(),
            candidate.Appliances.Select(item => new CookingContentAppliance(item.Station.Value, item.Capabilities.ToArray())).ToArray(),
            new[] { new CookingContentRecipe("cook", new[] { "raw" }, "product", "cook", "cook", 2,
                RequiredProcessingContainerDefinition: "pot") },
            Array.Empty<CookingContentOrderTemplate>(), Array.Empty<CookingContentSupplyEntry>()) { ContentProvenance = Provenance() };
        var loaded = CookingContentCatalog.Load(document);
        Assert.Equal(Pot, loaded.Recipes[Recipe].RequiredProcessingContainerDefinition);
        Assert.Equal("F31", Assert.Single(loaded.Snapshot.ContentProvenance!.SelectedMenus));
        var changed = CookingContentCatalog.Load(document with { ContentProvenance = Provenance() with {
            Sources = new[] { new CookingContentSourceIdentity("menu.md", new string('c', 64)) } } });
        Assert.NotEqual(loaded.Identity, changed.Identity);
    }

    [Fact]
    public void Provenance_is_frozen_and_catalog_changes_change_identity()
    {
        var selected = new[] { "F31" };
        var candidate = Candidate() with { ContentProvenance = Provenance() with { SelectedMenus = selected } };
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(candidate).Accepted);
        var first = registry.Current!.Identity;
        selected[0] = "F32";
        Assert.Equal("F31", Assert.Single(registry.Current.ContentProvenance!.SelectedMenus));
        Assert.Equal(first, registry.Current.Identity);
        Assert.True(registry.Submit(candidate with { ContentProvenance = Provenance("c") }).Accepted);
        Assert.NotEqual(first, registry.Current!.Identity);
    }

    [Fact]
    public void Invalid_source_hash_or_duplicate_menu_rejects_without_replacing_current()
    {
        var candidate = Candidate() with { ContentProvenance = Provenance() };
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(candidate).Accepted);
        var first = registry.Current!.Identity;
        Assert.False(registry.Submit(candidate with { ContentProvenance = Provenance() with { CatalogSha256 = new string('A', 64) } }).Accepted);
        Assert.False(registry.Submit(candidate with { ContentProvenance = Provenance() with { SelectedMenus = new[] { "F31", "F31" } } }).Accepted);
        Assert.Equal(first, registry.Current!.Identity);
    }
}
