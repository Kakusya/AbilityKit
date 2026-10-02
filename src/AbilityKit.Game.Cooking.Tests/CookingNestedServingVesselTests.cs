using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingNestedServingVesselTests
{
    private static readonly PlayerId Player = new("chef");
    private static readonly ItemId Cup = new("cup"), Tray = new("tray"), Ingredient = new("ingredient"), Spare = new("spare");
    private static readonly DefinitionId Raw = new("raw"), Food = new("food"), CupDefinition = new("cup"), TrayDefinition = new("tray");
    private static readonly StationSlotId Stove = new("stove"), Counter = new("counter"), TraySlot = new("tray-slot");
    private static readonly OrderId Order = new("order");
    private static readonly OrderTemplateId Template = new("template");
    private static readonly RecipeId Recipe = new("recipe");
    private static readonly CookingScope Scope = new(new("nested-serving"), new("world"), new("match"));

    private static CookingRecipeFixture Fixture(bool disposable) => new(Scope,
        new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string> { "cook" }, new HashSet<string> { "stove", "counter", "tray-slot" }),
        },
        new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Raw] = new(Raw, new HashSet<string> { "cook" }),
            [Food] = new(Food, new HashSet<string> { "cook" }),
            [CupDefinition] = new(CupDefinition, new HashSet<string> { "cook" }, new(2, new HashSet<DefinitionId> { Food, Raw }, disposable)),
            [TrayDefinition] = new(TrayDefinition, new HashSet<string> { "cook" }, new(2, new HashSet<DefinitionId> { CupDefinition, Raw })),
        },
        new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Stove] = new(Stove, new HashSet<string> { "cook" }),
            [Counter] = new(Counter, new HashSet<string>()),
            [TraySlot] = new(TraySlot, new HashSet<string>()),
        },
        new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [Recipe] = new(Recipe, new[] { Raw }, Food, new("cook"), "cook", 1),
        },
        washableContainerDefinitions: disposable ? null : new HashSet<DefinitionId> { CupDefinition },
        orderTemplates: new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>
        {
            [Template] = new(Template, Recipe, CupDefinition, RequiresBinding: true),
        },
        spatial: new(-2000, -2000, 2000, 2000, 40, 1000,
            new[] { new CookingPlayerPose(Player, 0, 0, 1, 0) },
            new[]
            {
                new CookingSpatialAnchor(LocationKind.StationSlot, Stove.Value, 500, 0),
                new CookingSpatialAnchor(LocationKind.StationSlot, Counter.Value, 500, 300),
                new CookingSpatialAnchor(LocationKind.StationSlot, TraySlot.Value, 500, 600),
            }, Array.Empty<CookingSpatialObstacle>()));

    private static void Act(CookingRecipeSimulation simulation, CookingRecipeOperation operation, string id,
        ItemId item, ItemId? container = null, StationSlotId? station = null, OrderId? order = null)
    {
        var result = simulation.Submit(Command(simulation, operation, id, item, container, station, order));
        Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.Reason.ToString());
    }

    private static CookingRecipeCommand Command(CookingRecipeSimulation simulation, CookingRecipeOperation operation,
        string id, ItemId item, ItemId? container = null, StationSlotId? station = null, OrderId? order = null) =>
        new(Scope, 1, Player, new(id), operation, Item: item, Container: container, Station: station, Order: order,
            ExpectedItemVersion: simulation.Snapshot().Items.Single(value => value.Id == item).Version);

    private static (CookingRecipeSimulation Simulation, ItemId Product) Produce(bool disposable)
    {
        var simulation = new CookingRecipeSimulation(Fixture(disposable));
        simulation.AddItem(Cup, CupDefinition, ItemLocation.Station(Counter));
        simulation.AddItem(Tray, TrayDefinition, ItemLocation.Station(TraySlot));
        simulation.AddItem(Ingredient, Raw, ItemLocation.Station(Stove));
        simulation.AddItem(Spare, Raw, ItemLocation.Container(Tray, "spare"));
        Act(simulation, CookingRecipeOperation.StartProcess, "cook", Ingredient, station: Stove);
        simulation.AdvanceFixedTick(new(Scope, new(1), new("level"), 1), 1);
        var product = Assert.Single(simulation.Snapshot().Items, value => value.IsProduct).Id;
        Act(simulation, CookingRecipeOperation.Pickup, "pick-food", product);
        Act(simulation, CookingRecipeOperation.PutIn, "plate", product, container: Cup);
        Act(simulation, CookingRecipeOperation.Pickup, "pick-cup", Cup);
        Act(simulation, CookingRecipeOperation.PutIn, "nest", Cup, container: Tray);
        Assert.True(simulation.OpenOrder(Order, Template).Accepted);
        return (simulation, product);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Submission_detaches_nested_serving_vessel_and_preserves_tray_inventory_and_restore(bool disposable)
    {
        var (simulation, product) = Produce(disposable);
        Act(simulation, CookingRecipeOperation.BindOrder, "bind", product, order: Order);
        Act(simulation, CookingRecipeOperation.Pickup, "carry-tray", Tray);
        Act(simulation, CookingRecipeOperation.SubmitOrder, "submit", product, order: Order);
        Assert.Equal(Tray, simulation.ItemInHand(Player));
        Assert.Equal(new[] { Spare }, simulation.ItemsInContainer(Tray));
        Assert.Empty(simulation.ItemsInContainer(Cup));
        Assert.DoesNotContain(simulation.Snapshot().Items, value => value.Id == Cup || value.Id == product);
        Assert.Single(simulation.SettlementHistory);
        var checkpoint = simulation.ExportCheckpoint();
        Assert.Equal(!disposable, checkpoint.Items.Single(value => value.Id == Cup).IsDirty);
        Assert.Equal(disposable ? 0 : 1, simulation.DirtyBowlsAwaitingWash().Count);
        var restored = new CookingRecipeSimulation(Fixture(disposable));
        Assert.True(restored.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), restored.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Bound_nested_cup_rejects_extra_material_and_occupied_order_without_unbinding_or_consumption()
    {
        var (simulation, product) = Produce(disposable: true);
        Act(simulation, CookingRecipeOperation.BindOrder, "bind", product, order: Order);
        Act(simulation, CookingRecipeOperation.TakeOut, "take-spare", Spare, container: Tray);
        var before = simulation.Snapshot().CanonicalText();
        var rejected = simulation.Submit(Command(simulation, CookingRecipeOperation.PutIn, "extra", Spare, container: Cup));
        Assert.Equal(CookingRecipeRejectionReason.BindingConflict, rejected.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        rejected = simulation.Submit(Command(simulation, CookingRecipeOperation.BindOrder, "bind-again", product, order: Order));
        Assert.Equal(CookingRecipeRejectionReason.BindingConflict, rejected.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(Order, simulation.Snapshot().Items.Single(value => value.Id == product).BoundOrder);
        Assert.Empty(simulation.SettlementHistory);
    }
}
