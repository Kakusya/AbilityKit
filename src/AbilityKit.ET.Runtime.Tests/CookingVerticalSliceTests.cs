using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

public sealed class CookingVerticalSliceTests
{
    [Fact]
    public void Et_tick_drains_recipe_commands_through_the_authoritative_simulation()
    {
        var scope = new CookingScope(new SessionId("s1"), new WorldId("w1"), new MatchId("m1"));
        var player = new PlayerId("p1");
        var ingredient = new ItemId("tomato-1");
        var raw = new DefinitionId("tomato");
        var cooked = new DefinitionId("soup");
        var station = new StationSlotId("stove");
        var counter = new StationSlotId("counter");
        var recipe = new RecipeId("soup");
        var plate = new ItemId("plate");
        var plateDefinition = new DefinitionId("plate");
        var orderTemplate = new OrderTemplateId("order-template");
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(
            scope,
            new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [player] = new(player, new HashSet<string> { "cook" }, new HashSet<string> { station.Value, counter.Value }),
            },
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [raw] = new(raw, new HashSet<string> { "cook" }),
                [cooked] = new(cooked, new HashSet<string> { "cook" }),
                [plateDefinition] = new(plateDefinition, new HashSet<string> { "cook" },
                    new CookingItemContainerCapability(1, new HashSet<DefinitionId> { cooked })),
            },
            new Dictionary<StationSlotId, CookingApplianceDefinition>
            {
                [station] = new(station, new HashSet<string> { "heat" }),
                [counter] = new(counter, new HashSet<string>()),
            },
            new Dictionary<RecipeId, CookingRecipeDefinition>
            {
                [recipe] = new(recipe, new[] { raw }, cooked, new ProcessId("boil"), "heat", 3),
            },
            orderTemplates: new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>
            {
                [orderTemplate] = new(orderTemplate, recipe, plateDefinition),
            }));
        simulation.AddItem(plate, plateDefinition, ItemLocation.Station(counter));
        simulation.AddWorldIngredient(ingredient, raw, "spawn");
        using var host = new AbilityKit.Game.Cooking.EtRuntime.CookingRecipeTickHost(simulation);

        CookingRecipeCommandResult Execute(CookingRecipeCommand command)
        {
            var before = simulation.Snapshot().Sha256();
            host.Enqueue(command);
            Assert.Equal(before, simulation.Snapshot().Sha256());
            var result = Assert.Single(host.Tick());
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
            Assert.Empty(host.Tick());
            return result;
        }

        Execute(new(scope, 1, player, new RecipeCommandId("pickup"), CookingRecipeOperation.Pickup,
            Item: ingredient, ExpectedItemVersion: 1));
        Assert.Equal(ingredient, simulation.ItemInHand(player));
        Execute(new(scope, 2, player, new RecipeCommandId("start"), CookingRecipeOperation.StartProcess,
            Recipe: recipe, Item: ingredient, Station: station, ExpectedItemVersion: 2));
        var process = Assert.Single(simulation.Snapshot().Processes);
        var advance = new CookingRecipeCommand(scope, 3, player, new RecipeCommandId("advance"),
            CookingRecipeOperation.AdvanceTicks, Process: process.Id, TickCount: 3);
        Execute(advance);
        Assert.Equal(3, simulation.LogicalTick);
        var product = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.True(product.IsProduct);
        Assert.Empty(simulation.Snapshot().Processes);
        Assert.True(Execute(advance).IsDuplicate);
        Assert.Equal(3, simulation.LogicalTick);
        Execute(new(scope, 4, player, new RecipeCommandId("pickup-product"), CookingRecipeOperation.Pickup,
            Item: product.Id, ExpectedItemVersion: 1));
        Execute(new(scope, 5, player, new RecipeCommandId("put-in"), CookingRecipeOperation.PutIn,
            Item: product.Id, Container: plate, ExpectedItemVersion: 2));
        Assert.True(simulation.OpenOrder(new OrderId("order-1"), orderTemplate).Accepted);
        var submit = new CookingRecipeCommand(scope, 6, player, new RecipeCommandId("order"),
            CookingRecipeOperation.SubmitOrder, Item: product.Id, Order: new OrderId("order-1"), ExpectedItemVersion: 3);
        Execute(submit);
        Assert.True(Execute(submit).IsDuplicate);
        Assert.Single(simulation.SettlementHistory);
        Assert.Equal(new[] { plate }, simulation.Snapshot().Items.Select(item => item.Id).ToArray());
        Assert.Single(simulation.Snapshot().AcceptedOrders);
        host.Dispose();
        Assert.Throws<ObjectDisposedException>(() => host.Enqueue(submit));
        Assert.Throws<ObjectDisposedException>(() => host.Tick());
    }
}
