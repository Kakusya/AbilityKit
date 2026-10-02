using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingLevelObservationTests
{
    private static readonly CookingScope Match = new(new("session"), new("world"), new("match"));
    private static readonly CookingLevelScope Scope = new(Match, new(1), new("level"), 3);
    private static readonly PlayerId Chef = new("chef");
    private static CookingLevelLifecycleSnapshot Life(CookingLevelState state = CookingLevelState.Running) => new(Scope, "config", null, null, state, null, 4, false);
    private static CookingRecipeSnapshot Recipe(IReadOnlyList<CookingRecipeSnapshotItem>? items = null,
        IReadOnlyList<CookingRecipeSnapshotContainer>? containers = null) => new(Match, 9, 6,
        items ?? Array.Empty<CookingRecipeSnapshotItem>(), Array.Empty<CookingRecipeSnapshotProcess>(),
        containers ?? Array.Empty<CookingRecipeSnapshotContainer>(), Array.Empty<OrderId>(), Array.Empty<CookingOrderSnapshotOrder>(), Array.Empty<CookingOrderSettlement>(),
        Poses: new[] { new CookingPlayerPose(Chef, 500, 500, 1, 0) });
    private static CookingLevelObservation Observe(CookingRecipeSnapshot? recipe, CookingFrontOfHouseSnapshot? front = null,
        CookingRestaurantLayout? layout = null) => CookingLevelObservationProjector.Project(Scope, Life(), 12, recipe, front, layout, "policy", "geometry");

    [Fact]
    public void Scope_epoch_and_frame_are_observed_and_created_kitchen_may_be_absent()
    {
        var created = CookingLevelObservationProjector.Project(Scope, Life(CookingLevelState.Created), 0, null, configuredPlayers: new[] { Chef });
        Assert.Null(created.Recipe); Assert.Equal(Scope, created.Scope); Assert.Equal(3, created.Scope.LevelEpoch);
        Assert.Null(created.Players.Single().HeldItem); Assert.Equal(CookingLevelState.Created, created.Lifecycle.State);
        var running = Observe(Recipe()); Assert.Equal(12, running.HostFrameSequence); Assert.Equal(6, running.Recipe!.LogicalTick);
        Assert.Throws<ArgumentException>(() => CookingLevelObservationProjector.Project(new CookingLevelScope(Match, new(1), new("level"), 4), Life(), 12, Recipe()));
        Assert.Throws<ArgumentException>(() => CookingLevelObservationProjector.Project(Scope, Life(), -1, Recipe()));
        Assert.Throws<ArgumentException>(() => Observe(Recipe() with { Scope = Match with { Match = new("foreign") } }));
    }
    [Fact]
    public void Held_container_half_prepared_and_order_data_are_read_from_actual_owner_fields()
    {
        var bowl = new ItemId("pot"); var raw = new ItemId("chopped"); var order = new OrderId("order-1");
        var recipe = Recipe(new[] {
            new CookingRecipeSnapshotItem(bowl, new("pot-definition"), 2, ItemLocation.Hand(Chef), new RecipeId("cooked-batch"), false, null, true, RemainingPortions: 5, BoundOrder: order),
            new CookingRecipeSnapshotItem(raw, new("chopped-tomato"), 1, ItemLocation.Container(bowl, "slot-0"), null, false, null) },
            new[] { new CookingRecipeSnapshotContainer(bowl, 3, new[] { raw }) }) with {
            Processes = new[] { new CookingRecipeSnapshotProcess(new("process"), new("cut"), Chef, raw, null, 2, 4, Chef) },
            Orders = new[] { new CookingOrderSnapshotOrder(order, new("template"), new("food"), new("plate"), "Pending", null) } };
        var observation = Observe(recipe);
        Assert.Equal(bowl, observation.Players.Single().HeldItem);
        Assert.Equal(raw, observation.Items.Single(i => i.Id == bowl).Contents.Single());
        Assert.Equal("chopped-tomato", observation.Items.Single(i => i.Id == raw).DefinitionKey);
        Assert.Equal(order, observation.Items.Single(i => i.Id == bowl).BoundOrder);
        var stock = observation.Inventory.Single(i => i.DefinitionKey == "pot-definition");
        Assert.Equal(1, stock.LiveObjects); Assert.Equal(1, stock.ContainerObjects); Assert.Equal(5, stock.RemainingPreparedPortions);
        Assert.Equal(Chef, observation.Recipe!.Processes.Single().ActiveWorker); Assert.Equal(2, observation.Recipe.Processes.Single().ElapsedTicks);
        Assert.Equal("Pending", observation.Recipe.Orders.Single().Status);
    }
    [Fact]
    public void External_balance_and_pending_deliveries_never_become_physical_inventory()
    {
        var box = new ItemId("box"); var one = new ItemId("one"); var two = new ItemId("two"); var gone = new ItemId("gone");
        var recipe = Recipe(new[] {
            new CookingRecipeSnapshotItem(box, new("box-def"), 1, ItemLocation.World("receiving"), null, false, null),
            new CookingRecipeSnapshotItem(one, new("raw"), 1, ItemLocation.Container(box, "slot-0"), null, false, null),
            new CookingRecipeSnapshotItem(two, new("raw"), 2, ItemLocation.Hand(Chef), null, false, null) },
            new[] { new CookingRecipeSnapshotContainer(box, 3, new[] { one }) }) with { Supply = new(
                new("supply-config", 3, 1, false, new[] { new CookingSupplierBalance("supplier", 99) },
                    new[] { new CookingSupplyDelivery("delivery:1", 1, "r", "supplier", 1, CookingDeliveryPhase.Received, 0),
                        new CookingSupplyDelivery("delivery:2", 2, "pending", "supplier", 1, CookingDeliveryPhase.Pending, 4) },
                    new[] { new CookingSupplyRequestReceipt("r", "supplier", 1, "delivery:1"), new CookingSupplyRequestReceipt("pending", "supplier", 1, "delivery:2") }),
                new[] { new CookingSupplyOrigin("r", "supplier", "delivery:1", box, new[] { one, two, gone }) }) };
        var observation = Observe(recipe); var stock = observation.Inventory.Single(s => s.DefinitionKey == "raw");
        Assert.Equal(2, stock.LiveObjects); Assert.Equal(2, stock.SupplyLiveUnits); Assert.Equal(1, stock.SupplyHandUnits);
        Assert.Equal(1, stock.SupplyUnitsInTrackedPackages); Assert.Equal(1, observation.UnavailableOriginalSupplyUnits);
        Assert.Equal(99, observation.Recipe!.Supply!.Ledger.Balances.Single().AvailableUnits);
        Assert.Equal(4, observation.Recipe.Supply.Ledger.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).RemainingTicks);
    }
    [Fact]
    public void Published_observation_deep_freezes_all_nested_input_lists()
    {
        var raw = new ItemId("raw"); var box = new ItemId("box");
        var items = new List<CookingRecipeSnapshotItem> { new(box, new("box-def"), 1, ItemLocation.Hand(Chef), null, false, null),
            new(raw, new("raw-def"), 1, ItemLocation.Container(box, "slot-0"), null, false, null) };
        var content = new List<ItemId> { raw }; var units = new List<ItemId> { raw };
        var balance = new List<CookingSupplierBalance> { new("supplier", 1) };
        var recipe = Recipe(items, new[] { new CookingRecipeSnapshotContainer(box, 2, content) }) with { Supply = new(
            new("config", 1, 1, false, balance, new List<CookingSupplyDelivery>(), new List<CookingSupplyRequestReceipt>()),
            new List<CookingSupplyOrigin> { new("r", "supplier", null, box, units) }) };
        var points = new List<CookingFrontPoint> { new(0, 0), new(1, 0) };
        var tables = new List<CookingFrontTableRoute> { new("table-1", points, points) };
        var front = new CookingFrontOfHouse(new(1, 20, 4, 2, 2, 3, 10)).Snapshot() with {
            Flow = new("geometry", points, points, points, tables, 2, 3),
            Work = new List<CookingFrontWorkSnapshot> { new("inquiry", CookingCompanionWorkKind.Inquiring, "table-1", null, null, 1, 2, CookingFrontWorkStatus.Working, Chef) } };
        var floors = new List<CookingFloorRegion> { new("floor", 0, 0, 3, 3) };
        var layout = new CookingRestaurantLayout(new("layout"), floors, new List<CookingEquipmentPlacement>(), new List<CookingLayoutCell>(), new List<CookingLayoutTarget>());
        var observation = Observe(recipe, front, layout); var before = observation.CanonicalText();
        items.Clear(); content.Clear(); units.Clear(); balance.Clear(); points.Clear(); tables.Clear(); floors.Clear();
        Assert.Equal(before, observation.CanonicalText());
        Assert.Throws<NotSupportedException>(() => ((IList<ItemId>)observation.Items.Single(i => i.Id == box).Contents).Clear());
        Assert.Equal(Chef, observation.Front!.Work.Single().Player);
    }
    [Fact]
    public void Read_projection_does_not_change_any_simulation_state()
    {
        var scope = Scope.MatchScope; var chef = new CookingPlayerConfig(Chef, new HashSet<string>(), new HashSet<string>());
        var sim = new CookingRecipeSimulation(new CookingRecipeFixture(scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = chef },
            new Dictionary<DefinitionId, CookingItemDefinition>(), new Dictionary<StationSlotId, CookingApplianceDefinition>(), new Dictionary<RecipeId, CookingRecipeDefinition>()));
        var before = sim.ExportCheckpoint().CanonicalText();
        Observe(sim.Snapshot()); Observe(sim.Snapshot());
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText());
    }
}
