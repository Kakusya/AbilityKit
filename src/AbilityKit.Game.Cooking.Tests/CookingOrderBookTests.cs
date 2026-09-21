using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-formal-content-and-orders</c>：order owner 移入领域后的订单簿与提交/结算契约。
/// 开单是前厅注入入口；提交校验（订单存在且 Open、产物 recipe 与容器定义匹配要求）完全在领域内执行；
/// 成功提交原子地消耗产物、订单转 Completed 并追加一条结算记录。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingOrderBookTests
{
    private static readonly SessionId Session = new("order-session");
    private static readonly WorldId World = new("order-world");
    private static readonly MatchId Match = new("order-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Station = new("stove-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly ItemId BowlA = new("bowl-a");
    private static readonly ItemId BowlB = new("bowl-b");
    private static readonly DefinitionId BowlADefinition = new("bowl-a");
    private static readonly DefinitionId BowlBDefinition = new("bowl-b");
    private static readonly DefinitionId Raw = new("order-raw");
    private static readonly DefinitionId Product = new("order-product");
    private static readonly RecipeId Recipe = new("order-recipe");
    private static readonly RecipeId OtherRecipe = new("order-other-recipe");
    private static readonly OrderTemplateId TemplateA = new("order-template-a");
    private static readonly OrderTemplateId TemplateB = new("order-template-b");
    private static readonly OrderId Order = new("order-1");

    [Fact]
    public void O01_open_order_validates_template_and_identity()
    {
        var simulation = CreateSimulation();

        var unknownTemplate = simulation.OpenOrder(Order, new OrderTemplateId("absent-template"));
        Assert.False(unknownTemplate.Accepted);
        Assert.Equal(CookingOrderResult.OrderTemplateNotFound, unknownTemplate.ReasonCode);
        Assert.Empty(simulation.Snapshot().Orders);

        Assert.True(simulation.OpenOrder(Order, TemplateA).Accepted);
        var duplicateIdentity = simulation.OpenOrder(Order, TemplateB);
        Assert.False(duplicateIdentity.Accepted);
        Assert.Equal(CookingOrderResult.OrderIdentityConflict, duplicateIdentity.ReasonCode);

        var opened = Assert.Single(simulation.Snapshot().Orders);
        Assert.Equal(Order, opened.Id);
        Assert.Equal(TemplateA, opened.Template);
        Assert.Equal(Recipe, opened.RequiredRecipe);
        Assert.Equal(BowlADefinition, opened.RequiredContainerDefinition);
        Assert.Equal(CookingOrderStatus.Open.ToString(), opened.Status);
        Assert.Null(opened.CompletedAtLogicalTick);
    }

    [Fact]
    public void O02_accepted_submission_completes_the_order_once_and_records_one_settlement()
    {
        var simulation = CreateSimulation();
        var product = CompleteAndPlate(simulation, BowlA);
        Assert.True(simulation.OpenOrder(Order, TemplateA).Accepted);
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product);

        var result = simulation.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit", item: product,
            order: Order, expectedVersion: plated.Version));

        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        Assert.Equal(new[] { Order }, simulation.Snapshot().AcceptedOrders);
        var completed = Assert.Single(simulation.Snapshot().Orders);
        Assert.Equal(CookingOrderStatus.Completed.ToString(), completed.Status);
        Assert.Equal(simulation.LogicalTick, completed.CompletedAtLogicalTick);
        Assert.Empty(simulation.ItemsInContainer(BowlA));
        Assert.DoesNotContain(simulation.Snapshot().Items, item => item.Id == product);

        var settlement = Assert.Single(simulation.SettlementHistory);
        Assert.Equal(1, settlement.Sequence);
        Assert.Equal(Order, settlement.Order);
        Assert.Equal(TemplateA, settlement.Template);
        Assert.Equal(Recipe, settlement.Recipe);
        Assert.Equal(product, settlement.Product);
        Assert.Equal(Player, settlement.Player);
        Assert.Equal(BowlA, settlement.Container);
        Assert.Equal(simulation.LogicalTick, settlement.LogicalTick);
    }

    [Fact]
    public void O03_rejections_are_structured_and_mutation_free()
    {
        var unopened = CreateSimulation();
        var unopenedProduct = CompleteAndPlate(unopened, BowlA);
        var unopenedPlated = unopened.Snapshot().Items.Single(item => item.Id == unopenedProduct);
        var unopenedBefore = unopened.Snapshot().CanonicalText();
        var unopenedResult = unopened.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-unopened",
            item: unopenedProduct, order: Order, expectedVersion: unopenedPlated.Version));
        AssertRejected(unopenedResult, CookingRecipeRejectionReason.OrderNotFound);
        Assert.Equal(unopenedBefore, unopened.Snapshot().CanonicalText());

        var wrongRecipe = CreateSimulation();
        var foreignProduct = CompleteForeignProduct(wrongRecipe, BowlA);
        Assert.True(wrongRecipe.OpenOrder(Order, TemplateA).Accepted);
        var foreignPlated = wrongRecipe.Snapshot().Items.Single(item => item.Id == foreignProduct);
        var recipeBefore = wrongRecipe.Snapshot().CanonicalText();
        var recipeResult = wrongRecipe.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-foreign-recipe",
            item: foreignProduct, order: Order, expectedVersion: foreignPlated.Version));
        AssertRejected(recipeResult, CookingRecipeRejectionReason.OrderRequirementMismatch);
        Assert.Equal(recipeBefore, wrongRecipe.Snapshot().CanonicalText());

        var wrongContainer = CreateSimulation();
        var product = CompleteAndPlate(wrongContainer, BowlB);
        Assert.True(wrongContainer.OpenOrder(Order, TemplateA).Accepted);
        var plated = wrongContainer.Snapshot().Items.Single(item => item.Id == product);
        var containerBefore = wrongContainer.Snapshot().CanonicalText();
        var containerResult = wrongContainer.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-wrong-container",
            item: product, order: Order, expectedVersion: plated.Version));
        AssertRejected(containerResult, CookingRecipeRejectionReason.OrderRequirementMismatch);
        Assert.Equal(containerBefore, wrongContainer.Snapshot().CanonicalText());
        Assert.Equal(new[] { product }, wrongContainer.ItemsInContainer(BowlB));

        var completed = CreateSimulation();
        var completedProduct = CompleteAndPlate(completed, BowlA);
        Assert.True(completed.OpenOrder(Order, TemplateA).Accepted);
        var completedPlated = completed.Snapshot().Items.Single(item => item.Id == completedProduct);
        Assert.Equal(CookingRecipeOutcome.Accepted, completed.Submit(Command(CookingRecipeOperation.SubmitOrder,
            "submit-once", item: completedProduct, order: Order, expectedVersion: completedPlated.Version)).Outcome);

        var secondProduct = CompleteAndPlate(completed, BowlA);
        var secondPlated = completed.Snapshot().Items.Single(item => item.Id == secondProduct);
        var secondBefore = completed.Snapshot().CanonicalText();
        var secondResult = completed.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-second",
            item: secondProduct, order: Order, expectedVersion: secondPlated.Version));
        AssertRejected(secondResult, CookingRecipeRejectionReason.OrderAlreadyCompleted);
        Assert.Equal(secondBefore, completed.Snapshot().CanonicalText());

        var thirdBefore = completed.Snapshot().CanonicalText();
        var thirdResult = completed.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-third",
            item: completedProduct, order: Order, expectedVersion: completedPlated.Version + 1));
        AssertRejected(thirdResult, CookingRecipeRejectionReason.ProductAlreadyConsumed);
        Assert.Equal(thirdBefore, completed.Snapshot().CanonicalText());

        Assert.Single(completed.SettlementHistory);
        Assert.Equal(new[] { Order }, completed.Snapshot().AcceptedOrders);
    }

    [Fact]
    public void O04_replayed_command_identity_returns_the_cached_result_without_a_second_settlement()
    {
        var simulation = CreateSimulation();
        var product = CompleteAndPlate(simulation, BowlA);
        Assert.True(simulation.OpenOrder(Order, TemplateA).Accepted);
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product);
        var command = Command(CookingRecipeOperation.SubmitOrder, "submit-once", item: product, order: Order,
            expectedVersion: plated.Version);

        var first = simulation.Submit(command);
        var replay = simulation.Submit(command);

        Assert.Equal(CookingRecipeOutcome.Accepted, first.Outcome);
        Assert.True(replay.IsDuplicate);
        Assert.Empty(replay.Events);
        Assert.Single(simulation.SettlementHistory);
        Assert.Single(simulation.EventHistory, @event => @event.Summary == "order-submitted");
    }

    [Fact]
    public void O05_settlements_accumulate_in_submission_order()
    {
        var simulation = CreateSimulation();
        var firstOrder = new OrderId("order-1");
        var secondOrder = new OrderId("order-2");
        Assert.True(simulation.OpenOrder(firstOrder, TemplateA).Accepted);
        Assert.True(simulation.OpenOrder(secondOrder, TemplateA).Accepted);

        var firstProduct = CompleteAndPlate(simulation, BowlA);
        var firstPlated = simulation.Snapshot().Items.Single(item => item.Id == firstProduct);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.SubmitOrder,
            "submit-first", item: firstProduct, order: firstOrder, expectedVersion: firstPlated.Version)).Outcome);

        var secondProduct = CompleteAndPlate(simulation, BowlA);
        var secondPlated = simulation.Snapshot().Items.Single(item => item.Id == secondProduct);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.SubmitOrder,
            "submit-second", item: secondProduct, order: secondOrder, expectedVersion: secondPlated.Version)).Outcome);

        Assert.Equal(new[] { firstOrder, secondOrder }, simulation.Snapshot().AcceptedOrders);
        Assert.Equal(new long[] { 1, 2 }, simulation.SettlementHistory.Select(settlement => settlement.Sequence));
        Assert.Equal(new[] { firstProduct, secondProduct },
            simulation.SettlementHistory.Select(settlement => settlement.Product));
        Assert.All(simulation.Snapshot().Orders, order => Assert.Equal(
            CookingOrderStatus.Completed.ToString(), order.Status));
    }

    [Fact]
    public void O06_canonical_snapshot_observes_the_order_book_and_settlements()
    {
        var simulation = CreateSimulation();
        var product = CompleteAndPlate(simulation, BowlA);
        Assert.True(simulation.OpenOrder(Order, TemplateA).Accepted);

        var opened = simulation.Snapshot().CanonicalText();
        Assert.Contains("orders", opened);
        Assert.Contains("order-1", opened);
        Assert.Contains("order-template-a", opened);
        Assert.Contains("Open", opened);

        var plated = simulation.Snapshot().Items.Single(item => item.Id == product);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.SubmitOrder,
            "submit", item: product, order: Order, expectedVersion: plated.Version)).Outcome);

        var settled = simulation.Snapshot().CanonicalText();
        Assert.NotEqual(opened, settled);
        Assert.Contains("settlements", settled);
        Assert.Contains("Completed", settled);
    }

    private static CookingRecipeSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Station.Value, Counter.Value }),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Raw] = new(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Product] = new(Product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BowlADefinition] = new(BowlADefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, new HashSet<DefinitionId> { Product })),
            [BowlBDefinition] = new(BowlBDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, new HashSet<DefinitionId> { Product })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Station] = new(Station, new HashSet<string>(StringComparer.Ordinal) { "heat", "blend" }),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [Recipe] = new(Recipe, new[] { Raw }, Product, new ProcessId("order-process"), "heat", 3),
            [OtherRecipe] = new(OtherRecipe, new[] { Raw }, Product, new ProcessId("order-other-process"), "blend", 3),
        };
        var orderTemplates = new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>
        {
            [TemplateA] = new(TemplateA, Recipe, BowlADefinition),
            [TemplateB] = new(TemplateB, Recipe, BowlBDefinition),
        };
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(scope, players, items, appliances, recipes,
            orderTemplates: orderTemplates));
        simulation.AddItem(BowlA, BowlADefinition, ItemLocation.Station(Counter));
        simulation.AddItem(BowlB, BowlBDefinition, ItemLocation.Station(Counter));
        return simulation;
    }

    private static ItemId CompleteAndPlate(CookingRecipeSimulation simulation, ItemId container)
    {
        var input = new ItemId($"input-{simulation.Snapshot().Version}-{simulation.SettlementHistory.Count}");
        simulation.AddItem(input, Raw, ItemLocation.Station(Station));
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.StartProcess,
            $"start-{input.Value}", recipe: Recipe, item: input, station: Station, expectedVersion: 1)).Outcome);
        var process = simulation.Snapshot().Processes.Single();
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks,
            $"complete-{input.Value}", process: process.Id, ticks: 3)).Outcome);
        var product = simulation.Snapshot().Items.Single(item => item.IsProduct).Id;
        var state = simulation.Snapshot().Items.Single(item => item.Id == product);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.Pickup,
            $"pickup-{product.Value}", item: product, expectedVersion: state.Version)).Outcome);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.PutIn,
            $"put-in-{product.Value}", item: product, container: container, expectedVersion: state.Version + 1)).Outcome);
        return product;
    }

    private static ItemId CompleteForeignProduct(CookingRecipeSimulation simulation, ItemId container)
    {
        var input = new ItemId("foreign-input");
        simulation.AddItem(input, Raw, ItemLocation.Station(Station));
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.StartProcess,
            "foreign-start", recipe: OtherRecipe, item: input, station: Station, expectedVersion: 1)).Outcome);
        var process = simulation.Snapshot().Processes.Single();
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks,
            "foreign-complete", process: process.Id, ticks: 3)).Outcome);
        var product = simulation.Snapshot().Items.Single(item => item.IsProduct).Id;
        var state = simulation.Snapshot().Items.Single(item => item.Id == product);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.Pickup,
            "foreign-pickup", item: product, expectedVersion: state.Version)).Outcome);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.PutIn,
            "foreign-put-in", item: product, container: container, expectedVersion: state.Version + 1)).Outcome);
        return product;
    }

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
        OrderId? order = null, int expectedVersion = 0, int ticks = 0) =>
        new(new CookingScope(Session, World, Match), 10, Player, new RecipeCommandId(commandId), operation, recipe,
            process, item, station, container, order, expectedVersion, ticks);

    private static void AssertRejected(CookingRecipeCommandResult result, CookingRecipeRejectionReason reason)
    {
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(reason, result.Reason);
        Assert.Empty(result.Events);
    }
}
