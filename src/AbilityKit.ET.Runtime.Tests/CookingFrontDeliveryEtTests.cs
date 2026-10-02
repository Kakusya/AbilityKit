using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingFrontDeliveryEtTests
{
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Dish = new("dish"), Plate = new("plate");
    private static readonly StationSlotId Board = new("board"), Counter = new("counter");
    private static readonly ItemId Input = new("input"), Vessel = new("vessel");
    private static readonly RecipeId Recipe = new("recipe");
    private static readonly OrderTemplateId Template = new("template");
    private static readonly CookingScope Scope = new(new("delivery"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);

    private sealed class Factory : ICookingFrontOfHouseGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; }
        public Factory(CookingFrontDeliveryPolicy? policy, int wait = 200, bool servingAnchor = true, bool ambiguousAnchor = false)
        {
            var items = new[] { new CookingItemDefinition(Raw, new HashSet<string> { "cook" }),
                new CookingItemDefinition(Dish, new HashSet<string> { "cook" }),
                new CookingItemDefinition(Plate, new HashSet<string> { "cook" }, new(1, new HashSet<DefinitionId> { Dish })) };
            var appliances = new[] { new CookingApplianceDefinition(Board, new HashSet<string> { "mix" }),
                new CookingApplianceDefinition(Counter, new HashSet<string>()) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Dish, new("mix"), "mix", 1) };
            var templates = new[] { new CookingOrderTemplateDefinition(Template, Recipe, Plate) };
            var anchors = new List<CookingSpatialAnchor> { new(LocationKind.StationSlot, "board", 1000, 1000),
                new(LocationKind.StationSlot, "counter", 1000, 1000),
                new(LocationKind.WorldPosition, "table-1", 5000, 1000), new(LocationKind.WorldPosition, "table-2", 8000, 1000) };
            if (servingAnchor) anchors.Add(new(LocationKind.WorldPosition, "serving", 6000, 1000));
            if (ambiguousAnchor) anchors.Add(new(LocationKind.StationSlot, "serving", 6000, 1000));
            var spatial = new CookingSpatialConfiguration(0, 0, 11000, 3000, 100, 1000,
                new[] { new CookingPlayerPose(Chef, 500, 1000, 1, 0) }, anchors, Array.Empty<CookingSpatialObstacle>());
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "mix" }, items, appliances, recipes, OrderTemplates: templates, Spatial: spatial)).Accepted);
            Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> {
                [Chef] = new(Chef, new HashSet<string> { "cook" }, new HashSet<string> { "board", "counter" }) },
                items.ToDictionary(x => x.Id), appliances.ToDictionary(x => x.Station), recipes.ToDictionary(x => x.Id),
                orderTemplates: templates.ToDictionary(x => x.Id), spatial: spatial);
            FrontOfHouseConfiguration = new(new(2, 2, 1, 1, 2, 2, wait), new[] { Template }, DeliveryPolicy: policy);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Simulation = new(Fixture);
            Simulation.AddItem(Input, Raw, ItemLocation.Station(Board));
            Simulation.AddItem(Vessel, Plate, ItemLocation.Station(Counter));
            return Simulation;
        }
        public CookingLevelEtHost Start()
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(Level, Config, this));
            Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board, Counter }, new[] { Plate }), Config.Identity)).Accepted);
            var started = host.Start();
            if (!started.Accepted) { host.Dispose(); throw new InvalidOperationException(started.ToString()); }
            return host;
        }
    }
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(Level, command, "local", command.Command.Value)).Accepted);
        var frame = host.Tick(); Assert.True(frame.Accepted);
        return Assert.Single(frame.Dispositions).Result!;
    }
    private static CookingRecipeCommand ItemCommand(Factory f, CookingLevelEtHost host, CookingRecipeOperation operation,
        string id, ItemId item, OrderId? order = null, StationSlotId? station = null, ItemId? container = null) =>
        new(Scope, host.HostFrameSequence + 1, Chef, new(id), operation, Item: item, Order: order, Station: station,
            Container: container, ExpectedItemVersion: f.Simulation.Snapshot().Items.Single(x => x.Id == item).Version);
    private static void Accept(CookingRecipeCommandResult result) => Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
    private static ItemId MakeDish(Factory f, CookingLevelEtHost host)
    {
        Accept(Execute(host, ItemCommand(f, host, CookingRecipeOperation.StartProcess, "cook", Input, station: Board)));
        var product = Assert.Single(f.Simulation.Snapshot().Items, x => x.IsProduct).Id;
        Accept(Execute(host, ItemCommand(f, host, CookingRecipeOperation.Pickup, "pickup-food", product)));
        Accept(Execute(host, ItemCommand(f, host, CookingRecipeOperation.PutIn, "plate-food", product, container: Vessel)));
        Accept(Execute(host, ItemCommand(f, host, CookingRecipeOperation.Pickup, "carry", Vessel)));
        Assert.Equal(2, host.FrontOfHouseSnapshot!.Customers.Count(x => x.Phase == CookingTablePhase.Ordered));
        return product;
    }
    private static OrderId Order(CookingLevelEtHost host, string table) =>
        host.FrontOfHouseSnapshot!.Customers.Single(x => x.TableId == table).Order!.Value;
    private static void Move(CookingLevelEtHost host, int count)
    {
        for (var i = 0; i < count; i++)
            Accept(Execute(host, new(Scope, host.HostFrameSequence + 1, Chef, new($"move-{host.HostFrameSequence}"),
                CookingRecipeOperation.Move, MoveX: 1)));
    }
    private static void RejectedWithoutFoodMutation(Factory f, CookingLevelEtHost host, CookingRecipeCommand command,
        CookingRecipeRejectionReason reason)
    {
        var items = f.Simulation.Snapshot().Items.ToArray();
        var settlements = f.Simulation.SettlementHistory.ToArray();
        var hands = f.Simulation.ItemInHand(Chef);
        var orders = f.Simulation.Orders.ToArray();
        var result = Execute(host, command);
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome); Assert.Equal(reason, result.Reason);
        Assert.Equal(items, f.Simulation.Snapshot().Items);
        Assert.Equal(settlements, f.Simulation.SettlementHistory);
        Assert.Equal(hands, f.Simulation.ItemInHand(Chef));
        Assert.Equal(orders, f.Simulation.Orders);
    }

    [Fact]
    public void Table_delivery_rejects_remote_wrong_table_and_wrong_facing_then_accepts_the_actual_orders_table()
    {
        var f = new Factory(new(CookingFrontDeliveryMode.CustomerTable)); using var host = f.Start();
        var food = MakeDish(f, host); var one = Order(host, "table-1"); var two = Order(host, "table-2");
        var remote = ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, "remote", food, one);
        RejectedWithoutFoodMutation(f, host, remote, CookingRecipeRejectionReason.TargetOutOfRange);
        Move(host, 4);
        var beforeReplay = f.Simulation.Snapshot().CanonicalText();
        var replay = host.TryEnqueue(new(Level, remote, "again", "again"));
        Assert.True(replay.Accepted); Assert.True(replay.TerminalDisposition!.Result!.IsDuplicate);
        Assert.Equal(CookingRecipeOutcome.Rejected, replay.TerminalDisposition.Result.Outcome);
        Assert.Equal(beforeReplay, f.Simulation.Snapshot().CanonicalText());
        RejectedWithoutFoodMutation(f, host, ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, "wrong-table", food, two), CookingRecipeRejectionReason.TargetOutOfRange);
        Accept(Execute(host, new(Scope, host.HostFrameSequence + 1, Chef, new("turn-away"), CookingRecipeOperation.Move, FacingX: -1)));
        RejectedWithoutFoodMutation(f, host, ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, "wrong-facing", food, one), CookingRecipeRejectionReason.TargetOutOfRange);
        Accept(Execute(host, new(Scope, host.HostFrameSequence + 1, Chef, new("face-table"), CookingRecipeOperation.Move, FacingX: 1)));
        Accept(Execute(host, ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, "serve", food, one)));
        Assert.Equal(one, Assert.Single(f.Simulation.SettlementHistory).Order);
    }

    [Theory]
    [InlineData(CookingFrontDeliveryMode.ServingAnchor, 5)]
    [InlineData(CookingFrontDeliveryMode.CustomerTable, 4)]
    public void Restored_owner_rebinds_trusted_delivery_policy_and_rejects_changed_factory_policy(CookingFrontDeliveryMode mode, int steps)
    {
        var policy = new CookingFrontDeliveryPolicy(mode, mode == CookingFrontDeliveryMode.ServingAnchor ? "serving" : null);
        var original = new Factory(policy); CookingLevelCheckpoint saved; ItemId food; OrderId order;
        using (var host = original.Start())
        {
            food = MakeDish(original, host); order = Order(host, "table-1");
            RejectedWithoutFoodMutation(original, host, ItemCommand(original, host, CookingRecipeOperation.SubmitOrder, "remote", food, order), CookingRecipeRejectionReason.TargetOutOfRange);
            saved = host.ExportCheckpoint().Checkpoint!;
        }
        var changed = new Factory(mode == CookingFrontDeliveryMode.CustomerTable ? new(CookingFrontDeliveryMode.ServingAnchor, "serving") : new(CookingFrontDeliveryMode.CustomerTable));
        Assert.NotEqual(original.FrontOfHouseConfiguration.Identity(), changed.FrontOfHouseConfiguration.Identity());
        Assert.False(CookingLevelEtHost.Restore(saved, changed.Config, changed).Accepted);
        var fresh = new Factory(policy); var restored = CookingLevelEtHost.Restore(saved, fresh.Config, fresh);
        Assert.True(restored.Accepted, restored.ToString()); using var active = restored.Host!;
        RejectedWithoutFoodMutation(fresh, active, ItemCommand(fresh, active, CookingRecipeOperation.SubmitOrder, "still-remote", food, order), CookingRecipeRejectionReason.TargetOutOfRange);
        Move(active, steps);
        Accept(Execute(active, ItemCommand(fresh, active, CookingRecipeOperation.SubmitOrder, "serve", food, order)));
        Assert.Single(fresh.Simulation.SettlementHistory);
        Assert.Equal(CookingOrderStatus.Completed.ToString(), fresh.Simulation.Orders.Single(x => x.Id == order).Status);
        var final = active.ExportCheckpoint().Checkpoint!; active.Dispose();
        var finalFactory = new Factory(policy); var finalRestore = CookingLevelEtHost.Restore(final, finalFactory.Config, finalFactory);
        Assert.True(finalRestore.Accepted, finalRestore.ToString()); using var finalHost = finalRestore.Host!;
        Assert.Equal(final.CanonicalText(), finalHost.ExportCheckpoint().Checkpoint!.CanonicalText());
    }

    [Fact]
    public void Legacy_null_policy_keeps_existing_generic_delivery_behavior()
    {
        var f = new Factory(null); using var host = f.Start(); var food = MakeDish(f, host);
        Accept(Execute(host, ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, "serve", food, Order(host, "table-1"))));
        Assert.Single(f.Simulation.SettlementHistory);
    }

    [Theory]
    [InlineData(CookingFrontDeliveryMode.ServingAnchor)]
    [InlineData(CookingFrontDeliveryMode.CustomerTable)]
    public void An_order_without_a_current_ordered_customer_cannot_be_served_at_either_trusted_destination(CookingFrontDeliveryMode mode)
    {
        var f = new Factory(new(mode, mode == CookingFrontDeliveryMode.ServingAnchor ? "serving" : null), wait: 12);
        using var host = f.Start(); var food = MakeDish(f, host); var oldOrder = Order(host, "table-1");
        Move(host, mode == CookingFrontDeliveryMode.ServingAnchor ? 5 : 4);
        for (var i = 0; i < 30 && host.FrontOfHouseSnapshot!.Customers.Count != 0; i++) host.Tick();
        Assert.Empty(host.FrontOfHouseSnapshot!.Customers);
        RejectedWithoutFoodMutation(f, host, ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, "departed", food, oldOrder), CookingRecipeRejectionReason.OrderAlreadyCompleted);
    }

    [Fact]
    public void Malformed_delivery_mode_and_missing_or_ambiguous_anchor_are_not_trusted_configuration()
    {
        var f = new Factory(null);
        Assert.Throws<ArgumentException>(() => (f.FrontOfHouseConfiguration with { DeliveryPolicy = new((CookingFrontDeliveryMode)42) }).Freeze());
        Assert.Throws<ArgumentException>(() => (f.FrontOfHouseConfiguration with { DeliveryPolicy = new(CookingFrontDeliveryMode.ServingAnchor) }).Freeze());
        Assert.Throws<ArgumentException>(() => (f.FrontOfHouseConfiguration with { DeliveryPolicy = new(CookingFrontDeliveryMode.CustomerTable, "serving") }).Freeze());
        foreach (var invalid in new[] { new Factory(new(CookingFrontDeliveryMode.ServingAnchor, "missing")),
                     new Factory(new(CookingFrontDeliveryMode.ServingAnchor, "serving"), ambiguousAnchor: true) })
        {
            using var host = new CookingLevelEtHost(new CookingLevelLifecycle(Level, invalid.Config, invalid));
            Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board, Counter }, new[] { Plate }), invalid.Config.Identity)).Accepted);
            var error = Assert.Throws<InvalidOperationException>(() => host.Start());
            Assert.IsType<ArgumentException>(error.InnerException);
        }
    }
}
