using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using System.Text.Json.Nodes;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingOrderBindingEtTests
{
    private static readonly PlayerId Chef = new("chef"), Partner = new("partner");
    private static readonly StationSlotId Board = new("board"), Counter = new("counter");
    private static readonly DefinitionId Raw = new("raw"), Drink = new("drink"), CupDefinition = new("cup-definition");
    private static readonly ItemId Input = new("input"), Cup = new("cup");
    private static readonly RecipeId Recipe = new("drink-recipe");
    private static readonly OrderTemplateId Template = new("drink-order-template");
    private static readonly OrderId Order = new("order"), OtherOrder = new("other-order");
    private static readonly CookingScope Scope = new(new("binding-session"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);

    private sealed class Factory : ICookingLevelGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;

        public Factory()
        {
            var items = new[]
            {
                new CookingItemDefinition(Raw, new HashSet<string> { "cook" }),
                new CookingItemDefinition(Drink, new HashSet<string> { "cook" }),
                new CookingItemDefinition(CupDefinition, new HashSet<string> { "cook" },
                    new(1, new HashSet<DefinitionId> { Drink }, DisposableOnSubmission: true)),
            };
            var appliances = new[] { new CookingApplianceDefinition(Board, new HashSet<string> { "mix" }),
                new CookingApplianceDefinition(Counter, new HashSet<string>()) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Drink, new("mix"), "mix", 1) };
            var orders = new[] { new CookingOrderTemplateDefinition(Template, Recipe, CupDefinition, RequiresBinding: true) };
            var spatial = new CookingSpatialConfiguration(-3000, -3000, 3000, 3000, 50, 1000,
                new[] { new CookingPlayerPose(Chef, 0, 0, 1, 0), new CookingPlayerPose(Partner, 0, 300, 1, 0) },
                new[] { new CookingSpatialAnchor(LocationKind.StationSlot, "board", 500, 0),
                    new CookingSpatialAnchor(LocationKind.StationSlot, "counter", 500, 300) },
                Array.Empty<CookingSpatialObstacle>());
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "mix" }, items, appliances, recipes, OrderTemplates: orders, Spatial: spatial)).Accepted);
            Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig>
                { [Chef] = new(Chef, new HashSet<string> { "cook" }, new HashSet<string> { "board", "counter" }),
                  [Partner] = new(Partner, new HashSet<string> { "cook" }, new HashSet<string> { "board", "counter" }) },
                items.ToDictionary(x => x.Id), appliances.ToDictionary(x => x.Station), recipes.ToDictionary(x => x.Id),
                orderTemplates: orders.ToDictionary(x => x.Id), spatial: spatial);
        }

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Simulation = new(Fixture);
            Simulation.AddItem(Input, Raw, ItemLocation.Station(Board));
            Simulation.AddItem(Cup, CupDefinition, ItemLocation.Station(Counter));
            Assert.True(Simulation.OpenOrder(Order, Template).Accepted);
            Assert.True(Simulation.OpenOrder(OtherOrder, Template).Accepted);
            return Simulation;
        }

        public CookingLevelEtHost Start()
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(Level, Config, this));
            Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Board, Counter }, new[] { CupDefinition }), Config.Identity)).Accepted);
            Assert.True(host.Start().Accepted);
            return host;
        }
    }

    private static CookingRecipeCommand Command(Factory factory, CookingLevelEtHost host, CookingRecipeOperation operation,
        string id, ItemId item, OrderId? order = null, PlayerId? player = null, StationSlotId? station = null, ItemId? container = null) =>
        new(Scope, host.HostFrameSequence + 1, player ?? Chef, new(id), operation, Item: item, Order: order,
            Station: station, Container: container, ExpectedItemVersion: factory.Simulation.Snapshot().Items.Single(x => x.Id == item).Version);

    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(Level, command, "local", command.Command.Value)).Accepted);
        var frame = host.Tick();
        Assert.True(frame.Accepted);
        return Assert.Single(frame.Dispositions).Result!;
    }

    private static void Accepted(CookingRecipeCommandResult result) => Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);

    private static ItemId ProduceAndPlate(Factory factory, CookingLevelEtHost host)
    {
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.StartProcess, "mix", Input, station: Board)));
        var product = Assert.Single(factory.Simulation.Snapshot().Items, x => x.IsProduct).Id;
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.Pickup, "pickup-food", product)));
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.PutIn, "plate", product, container: Cup)));
        return product;
    }

    [Fact]
    public void Binding_via_ingress_codec_destroy_restore_delivers_once_and_consumes_cup()
    {
        var factory = new Factory();
        CookingLevelCheckpoint checkpoint;
        ItemId product;
        string expected;
        using (var host = factory.Start())
        {
            product = ProduceAndPlate(factory, host);
            Assert.Equal(CookingRecipeOutcome.Rejected, Execute(host, Command(factory, host, CookingRecipeOperation.SubmitOrder, "unbound", product, Order)).Outcome);
            Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.BindOrder, "bind", product, Order)));
            var encoded = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(host.ExportCheckpoint().Checkpoint!));
            var decoded = CookingLevelCheckpointCodec.Deserialize(encoded);
            Assert.True(decoded.Accepted);
            checkpoint = decoded.Checkpoint!;
            var missingAuthority = JsonNode.Parse(encoded)!;
            foreach (var entry in missingAuthority["checkpoint"]!["recipe"]!["items"]!.AsArray())
                entry!.AsObject().Remove("boundOrder");
            Assert.False(CookingLevelCheckpointCodec.Deserialize(missingAuthority.ToJsonString()).Accepted);
            var tampered = checkpoint with { Recipe = checkpoint.Recipe with
                { Items = checkpoint.Recipe.Items.Select(x => x.Id == product ? x with { BoundOrder = new("missing-order") } : x).ToArray() } };
            var badFactory = new Factory();
            Assert.False(CookingLevelEtHost.Restore(tampered, badFactory.Config, badFactory).Accepted);
            Assert.Equal(6, CookingLevelCheckpointCodec.CurrentFormatVersion);
            Assert.False(CookingLevelCheckpointCodec.Deserialize(encoded.Replace("\"formatVersion\":6", "\"formatVersion\":5")).Accepted);
            Finish(factory, host, product);
            expected = factory.Simulation.Snapshot().CanonicalText();
        }
        var restoredFactory = new Factory();
        var result = CookingLevelEtHost.Restore(checkpoint, restoredFactory.Config, restoredFactory);
        Assert.True(result.Accepted);
        using var restoredHost = result.Host!;
        Finish(restoredFactory, restoredHost, product);
        Assert.Equal(expected, restoredFactory.Simulation.Snapshot().CanonicalText());
        Assert.Single(restoredFactory.Simulation.Snapshot().Settlements);
    }

    private static void Finish(Factory factory, CookingLevelEtHost host, ItemId product)
    {
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.Pickup, "pickup-cup", Cup)));
        var submit = Command(factory, host, CookingRecipeOperation.SubmitOrder, "submit", product, Order);
        Accepted(Execute(host, submit));
        Assert.Null(factory.Simulation.ItemInHand(Chef));
        Assert.DoesNotContain(factory.Simulation.Snapshot().Items, x => x.Id == Cup || x.Id == product);
        var replay = host.TryEnqueue(new(Level, submit, "replay", "replay"));
        Assert.True(replay.Accepted);
        Assert.True(replay.TerminalDisposition!.Result!.IsDuplicate);
        Assert.Single(factory.Simulation.Snapshot().Settlements);
    }

    [Fact]
    public void Binding_fingerprint_golden_vector_preserves_existing_payload_encoding()
    {
        var command = new CookingRecipeCommand(Scope, 7, Partner, new("rebind"), CookingRecipeOperation.RebindOrder,
            Item: new("product-1"), Order: OtherOrder, ExpectedItemVersion: 5);
        var envelope = new CookingLevelCommandEnvelope(Level, command, "ignored-connection", "ignored-correlation");
        Assert.Equal("6F28E4F85048A27E3C6AB3E6058808B78C44479102629D83E9C6B57AA5A0F91D", CookingCommandFingerprint.Create(envelope).Value);
    }

    [Fact]
    public void Same_tick_binding_race_and_rebind_payload_are_authoritative()
    {
        var factory = new Factory();
        using var host = factory.Start();
        var product = ProduceAndPlate(factory, host);
        var bind = Command(factory, host, CookingRecipeOperation.BindOrder, "claim", product, Order);
        var second = bind with { Player = Partner };
        Assert.True(host.TryEnqueue(new(Level, second, "b", "b")).Accepted);
        Assert.True(host.TryEnqueue(new(Level, bind, "a", "a")).Accepted);
        Assert.Single(host.Tick().Dispositions, x => x.Result?.Outcome == CookingRecipeOutcome.Accepted);
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.UnbindOrder, "unbind", product, Order, Partner)));
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.BindOrder, "bind-again", product, Order, Partner)));
        var rebind = Command(factory, host, CookingRecipeOperation.RebindOrder, "rebind", product, OtherOrder, Partner);
        var envelope = new CookingLevelCommandEnvelope(Level, rebind, "b", "b");
        Assert.NotEqual(CookingCommandFingerprint.Create(envelope), CookingCommandFingerprint.Create(envelope with { Command = rebind with { Order = Order } }));
        Assert.NotEqual(CookingCommandFingerprint.Create(envelope), CookingCommandFingerprint.Create(envelope with { Command = rebind with { ExpectedItemVersion = rebind.ExpectedItemVersion + 1 } }));
        Accepted(Execute(host, rebind));
        Assert.Equal(CookingRecipeOutcome.Rejected, Execute(host, Command(factory, host, CookingRecipeOperation.SubmitOrder, "wrong-order", product, Order, Partner)).Outcome);
        Accepted(Execute(host, Command(factory, host, CookingRecipeOperation.SubmitOrder, "right-order", product, OtherOrder, Partner)));
        Assert.Single(factory.Simulation.Snapshot().Settlements);
    }
}
