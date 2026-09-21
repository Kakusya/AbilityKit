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
        var recipe = new RecipeId("soup");
        var plate = new ContainerId("plate");
        var orders = new AcceptingOrders();
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(
            scope,
            new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [player] = new(player, new HashSet<string> { "cook" }, new HashSet<string> { station.Value }),
            },
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [raw] = new(raw, new HashSet<string> { "cook" }),
                [cooked] = new(cooked, new HashSet<string> { "cook" }),
            },
            new Dictionary<StationSlotId, CookingApplianceDefinition>
            {
                [station] = new(station, new HashSet<string> { "heat" }),
            },
            new Dictionary<RecipeId, CookingRecipeDefinition>
            {
                [recipe] = new(recipe, new[] { raw }, cooked, new ProcessId("boil"), "heat", 3),
            },
            new Dictionary<ContainerId, CookingContainerDefinition> { [plate] = new(plate, 1) }), orders);
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
        var product = Assert.Single(simulation.Snapshot().Items);
        Assert.True(product.IsProduct);
        Assert.Empty(simulation.Snapshot().Processes);
        Assert.True(Execute(advance).IsDuplicate);
        Assert.Equal(3, simulation.LogicalTick);
        Execute(new(scope, 4, player, new RecipeCommandId("plate"), CookingRecipeOperation.Plate,
            Item: product.Id, Container: plate, ExpectedItemVersion: 1));
        var submit = new CookingRecipeCommand(scope, 5, player, new RecipeCommandId("order"),
            CookingRecipeOperation.SubmitOrder, Item: product.Id, Order: new OrderId("order-1"), ExpectedItemVersion: 2);
        Execute(submit);
        Assert.True(Execute(submit).IsDuplicate);
        Assert.Equal(1, orders.Submissions);
        Assert.Empty(simulation.Snapshot().Items);
        Assert.Single(simulation.Snapshot().AcceptedOrders);
        host.Dispose();
        Assert.Throws<ObjectDisposedException>(() => host.Enqueue(submit));
        Assert.Throws<ObjectDisposedException>(() => host.Tick());
    }

    private sealed class AcceptingOrders : ICookingOrderPort
    {
        public int Submissions { get; private set; }

        public CookingOrderAcceptance Submit(CookingOrderSubmission submission)
        {
            Submissions++;
            return new(true);
        }
    }
}
