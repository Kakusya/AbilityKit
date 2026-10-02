using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using System.Text.Json.Nodes;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingSupplyEtTests
{
    private static readonly CookingScope Scope = new(new("s"), new("w"), new("m"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("l"), 1);
    private static readonly PlayerId Chef = new("p");
    private static readonly DefinitionId Raw = new("raw"), Box = new("box"), Meal = new("meal"), Plate = new("plate");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly RecipeId Recipe = new("cook");
    private static readonly OrderTemplateId Template = new("template");
    private sealed class Factory : ICookingLevelGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        public CookingConfigurationCandidate Candidate { get; }
        public CookingRecipeFixture Fixture { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public Factory()
        {
            var caps = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, caps), new(Meal, caps),
                new(Box, caps, new(3, new HashSet<DefinitionId> { Raw })), new(Plate, caps, new(1, new HashSet<DefinitionId> { Meal })) };
            var appliances = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Meal, new("heat"), "heat", 1) };
            var templates = new[] { new CookingOrderTemplateDefinition(Template, Recipe, Plate) };
            var spatial = new CookingSpatialConfiguration(-5000, -5000, 5000, 5000, 100, 2000,
                new[] { new CookingPlayerPose(Chef, 0, 0, 1, 0) }, new[] {
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 200, 0), new(LocationKind.WorldPosition, "receiving", 300, 0),
                    new(LocationKind.WorldPosition, "storage", 400, 0), new(LocationKind.WorldPosition, "plate", 500, 0),
                    new(LocationKind.StationSlot, "stove", 600, 0) }, Array.Empty<CookingSpatialObstacle>());
            var supply = new CookingSupplyConfiguration(new[] { new CookingSupplierDefinition("finite", "source", "receiving", Raw, Box, 3, 3, 9),
                new CookingSupplierDefinition("infinite", "source", "receiving", Raw, Box, 1, 0, Infinite: true) });
            Candidate = new(new[] { "heat" }, items, appliances, recipes, OrderTemplates: templates, Spatial: spatial) { Supply = supply };
            var registry = new CookingConfigurationRegistry(); Assert.True(registry.Submit(Candidate).Accepted); Config = registry.Current!;
            Fixture = new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, caps, new HashSet<string> { "stove" }) },
                items.ToDictionary(i => i.Id), appliances.ToDictionary(a => a.Station), recipes.ToDictionary(r => r.Id),
                orderTemplates: templates.ToDictionary(t => t.Id), spatial: spatial, supply: supply);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Simulation = new(Fixture); Simulation.AddItem(new("plate"), Plate, ItemLocation.World("plate"));
            Assert.True(Simulation.OpenOrder(new("order"), Template).Accepted); return Simulation;
        }
        public CookingLevelEtHost Start(bool closing = false)
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(Level, Config, this));
            if (closing) host.UseFrontOfHouse(new(new(1, 2, 1, 1, 2, 2, 100)), Template);
            Assert.True(host.Prepare(new(Level.Level, new("map"), new(new("layout"), new[] { Stove }, new[] { Plate, Box }), Config.Identity)).Accepted);
            Assert.True(host.Start().Accepted); return host;
        }
    }
    private static CookingRecipeCommand Command(CookingLevelEtHost host, CookingRecipeOperation op, string id,
        string? supplier = null, string? request = null, string? delivery = null) =>
        new(Scope, host.HostFrameSequence + 1, Chef, new(id), op, SupplierId: supplier, SupplyRequestId: request, DeliveryId: delivery);
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(host.Binding.LevelScope, command, "local", command.Command.Value)).Accepted);
        var frame = host.Tick(); Assert.True(frame.Accepted); return Assert.Single(frame.Dispositions).Result!;
    }
    private static CookingRecipeCommandResult Accept(CookingLevelEtHost host, CookingRecipeCommand command)
    {
        var result = Execute(host, command); Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome); return result;
    }
    private static CookingRecipeCommand ItemCommand(Factory f, CookingLevelEtHost host, CookingRecipeOperation op, ItemId item,
        ItemId? container = null, StationSlotId? station = null, string? world = null, OrderId? order = null) =>
        new(Scope, host.HostFrameSequence + 1, Chef, new($"{op}-{host.HostFrameSequence}"), op, Item: item,
            Container: container, Station: station, WorldAnchor: world, Order: order,
            ExpectedItemVersion: f.Simulation.Snapshot().Items.Single(i => i.Id == item).Version);

    [Fact]
    public void Physical_supply_runs_through_real_ET_pause_tick_move_takeout_recipe_and_submission()
    {
        var f = new Factory(); using var host = f.Start();
        var request = Command(host, CookingRecipeOperation.RequestSupply, "reserve", "finite", "r");
        var delivery = Accept(host, request).Supply!.DeliveryId;
        Assert.Equal(2, Assert.Single(f.Simulation.SupplySnapshot()!.Ledger.Deliveries).RemainingTicks);
        Assert.True(host.Pause().Accepted); var paused = f.Simulation.ExportCheckpoint().CanonicalText();
        Assert.False(host.Tick().Accepted); Assert.Equal(paused, f.Simulation.ExportCheckpoint().CanonicalText()); Assert.True(host.Resume().Accepted);
        host.Tick(); host.Tick();
        var receipt = Accept(host, Command(host, CookingRecipeOperation.ReceiveSupply, "receive", delivery: delivery)).Supply!;
        Assert.Equal(3, receipt.Units.Count); var package = receipt.Package!.Value;
        var replay = host.TryEnqueue(new(Level, request, "retry", "retry")); Assert.True(replay.TerminalDisposition!.Result!.IsDuplicate);
        Assert.Single(f.Simulation.SupplySnapshot()!.Ledger.Deliveries);
        var repeated = Accept(host, Command(host, CookingRecipeOperation.ReceiveSupply, "receive-again", delivery: delivery));
        Assert.True(repeated.IsDuplicate); Assert.Equal(package, repeated.Supply!.Package);
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.Pickup, package));
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.Drop, package, world: "storage"));
        var raw = receipt.Units[0]; Accept(host, ItemCommand(f, host, CookingRecipeOperation.TakeOut, raw, container: package));
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.Drop, raw, station: Stove));
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.StartProcess, raw, station: Stove));
        var meal = Assert.Single(f.Simulation.Snapshot().Items, i => i.IsProduct).Id;
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.Pickup, meal));
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.PutIn, meal, container: new("plate")));
        Accept(host, ItemCommand(f, host, CookingRecipeOperation.SubmitOrder, meal, order: new("order")));
        Assert.Single(f.Simulation.SettlementHistory); Assert.Equal(2, f.Simulation.ItemsInContainer(package).Count);
        Assert.Equal(6, f.Simulation.SupplySnapshot()!.Ledger.Balances.Single(b => b.SupplierId == "finite").AvailableUnits);
    }

    [Fact]
    public void Closing_refuses_new_procurement_but_arrives_and_receives_an_approved_delivery()
    {
        var f = new Factory(); using var host = f.Start(closing: true);
        var delivery = Accept(host, Command(host, CookingRecipeOperation.RequestSupply, "reserve", "finite", "r")).Supply!.DeliveryId;
        host.Tick(); Assert.True(host.FrontOfHouseSnapshot!.Closing); Assert.True(f.Simulation.SupplySnapshot()!.Ledger.Closing);
        var savedClosing = host.ExportCheckpoint().Checkpoint!;
        var forged = savedClosing with { Recipe = savedClosing.Recipe with { Supply = savedClosing.Recipe.Supply! with { Closing = false } } };
        var invalid = CookingLevelEtHost.Restore(forged, f.Config, f);
        Assert.Equal(CookingLevelCheckpointRestoreReason.GameplayRestoreRejected, invalid.Reason);
        Assert.Equal(CookingCheckpointRestoreReason.SupplyStateInvalid, invalid.RecipeRestoreReason);
        var refused = Execute(host, Command(host, CookingRecipeOperation.RequestSupply, "new", "finite", "new"));
        Assert.Equal(CookingRecipeRejectionReason.SupplyRejected, refused.Reason);
        Assert.Single(f.Simulation.SupplySnapshot()!.Ledger.Deliveries);
        Accept(host, Command(host, CookingRecipeOperation.ReceiveSupply, "receive", delivery: delivery));
        Assert.Equal(CookingDeliveryPhase.Received, f.Simulation.SupplySnapshot()!.Ledger.Deliveries.Single().Phase);
        Accept(host, Command(host, CookingRecipeOperation.TakeSupply, "take", "infinite", "finish-existing"));
        Assert.NotNull(f.Simulation.ItemInHand(Chef));
    }

    [Fact]
    public void Level7_Recipe5_supply_roundtrip_rebuilds_a_fresh_owner_with_exact_final_checkpoint()
    {
        var f = new Factory(); CookingLevelCheckpoint saved;
        using (var host = f.Start())
        {
            Accept(host, Command(host, CookingRecipeOperation.RequestSupply, "reserve", "finite", "r"));
            saved = host.ExportCheckpoint().Checkpoint!;
        }
        Assert.Equal(8, CookingLevelCheckpointCodec.CurrentFormatVersion); Assert.Equal(5, saved.Recipe.SchemaVersion);
        var json = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(saved));
        var old = JsonNode.Parse(json)!; old["formatVersion"] = 6;
        Assert.Equal(CookingCheckpointReadReason.UnknownFormatVersion, CookingLevelCheckpointCodec.Deserialize(old.ToJsonString()).Reason);
        var oldRecipe = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(saved with { Recipe = saved.Recipe with { SchemaVersion = 4 } }));
        Assert.Equal(CookingCheckpointReadReason.UnknownFormatVersion, CookingLevelCheckpointCodec.Deserialize(oldRecipe).Reason);
        var decoded = CookingLevelCheckpointCodec.Deserialize(json); Assert.True(decoded.Accepted);
        var fresh = new Factory(); var restored = CookingLevelEtHost.Restore(decoded.Checkpoint!, fresh.Config, fresh);
        Assert.True(restored.Accepted, restored.ToString()); using var active = restored.Host!;
        Assert.Equal(saved.CanonicalText(), active.ExportCheckpoint().Checkpoint!.CanonicalText());
        active.Tick(); active.Tick(); var delivery = Assert.Single(fresh.Simulation.SupplySnapshot()!.Ledger.Deliveries).DeliveryId;
        Accept(active, Command(active, CookingRecipeOperation.ReceiveSupply, "receive", delivery: delivery));
        var final = active.ExportCheckpoint().Checkpoint!;
        active.Dispose();
        var again = new Factory(); var twice = CookingLevelEtHost.Restore(final, again.Config, again); Assert.True(twice.Accepted, twice.ToString());
        using var second = twice.Host!; Assert.Equal(final.CanonicalText(), second.ExportCheckpoint().Checkpoint!.CanonicalText());
        Assert.False(CookingLevelEtHost.Restore(saved with { Recipe = saved.Recipe with { SchemaVersion = 4 } }, fresh.Config, fresh).Accepted);
        var missing = JsonNode.Parse(json)!; missing["checkpoint"]!["recipe"]!.AsObject().Remove("supplyOrigins");
        Assert.False(CookingLevelCheckpointCodec.Deserialize(missing.ToJsonString()).Accepted);
    }

    [Fact]
    public void Supply_extension_has_golden_length_encoding_and_malformed_payloads_cannot_alias()
    {
        var baseCommand = new CookingRecipeCommand(Scope, 1, Chef, new("c"), CookingRecipeOperation.RequestSupply);
        CookingLevelCommandEnvelope Envelope(CookingRecipeCommand c) => new(Level, c, "a", "b");
        var legacy = CookingCommandFingerprint.CanonicalBytes(Envelope(baseCommand));
        var extended = CookingCommandFingerprint.CanonicalBytes(Envelope(baseCommand with { SupplierId = "s", SupplyRequestId = "r" }));
        Assert.Equal(legacy, extended.Take(legacy.Length).ToArray());
        Assert.Equal("535550500000000101000000017300010000000172", Convert.ToHexString(extended.Skip(legacy.Length).ToArray()));
        var malformed = new[] { baseCommand, baseCommand with { SupplierId = "" }, baseCommand with { DeliveryId = "" },
            baseCommand with { SupplyRequestId = "" }, baseCommand with { SupplierId = "a|b", SupplyRequestId = "c" },
            baseCommand with { SupplierId = "a", SupplyRequestId = "b|c" }, baseCommand with { SupplierId = "s", DeliveryId = "d", SupplyRequestId = "r" } };
        Assert.Equal(malformed.Length, malformed.Select(c => CookingCommandFingerprint.Create(Envelope(c)).Value).Distinct().Count());
        var f = new Factory(); using var host = f.Start();
        foreach (var c in malformed.Where(c => !CookingRecipeCommandValidation.IsWellFormed(c))) Assert.Equal(CookingLevelAdmissionReason.MalformedCommand, host.TryEnqueue(Envelope(c)).Reason);
        Assert.Equal(CookingLevelAdmissionReason.MalformedCommand, host.TryEnqueue(Envelope(baseCommand with {
            Operation = CookingRecipeOperation.Move, SupplierId = "s", SupplyRequestId = "r" })).Reason);
        var valid = Command(host, CookingRecipeOperation.RequestSupply, "same", "finite", "one");
        Assert.True(host.TryEnqueue(Envelope(valid)).Accepted);
        var conflict = host.TryEnqueue(Envelope(valid with { SupplyRequestId = "two" }));
        Assert.Equal(CookingLevelAdmissionReason.CommandIdentityConflict, conflict.Reason);
        Assert.Equal(CookingLevelDispositionKind.Conflicted, conflict.TerminalDisposition!.Kind);
        Assert.Equal(2, host.DispositionHistory.Count(d => d.Kind == CookingLevelDispositionKind.Conflicted));
        host.Tick();
        Assert.Empty(f.Simulation.SupplySnapshot()!.Ledger.Deliveries);
    }

    [Fact]
    public void Created_successor_previews_choices_before_touching_kitchen_or_progress()
    {
        var f = new Factory(); using var host = f.Start();
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
        Assert.True(host.CreateSuccessor(new("next"), 2).Accepted);
        var kitchen = f.Simulation.ExportCheckpoint().CanonicalText(); var progress = new CookingMajorProgress();
        var failed = host.ChooseDecoration(progress, new[] { new CookingStationReplacement(Stove, new("unknown")) });
        Assert.False(failed.Accepted); Assert.Empty(progress.Decoration); Assert.Equal(kitchen, f.Simulation.ExportCheckpoint().CanonicalText());
        var content = new CookingContent(f.Candidate, f.Fixture.OrderTemplates, new[] { new CookingSupplyEntryDefinition(Raw, 1, "world:storage") }, f.Config.Identity);
        progress.Lock(); Assert.False(host.Unlock(progress, content, Raw).Accepted);
        Assert.Equal(kitchen, f.Simulation.ExportCheckpoint().CanonicalText()); Assert.Empty(progress.Unlocks);
        Assert.False(host.ChooseDecoration(progress, new[] { new CookingStationReplacement(Stove, Stove) }).Accepted);
        Assert.Equal(kitchen, f.Simulation.ExportCheckpoint().CanonicalText());
        var editable = new CookingMajorProgress(); Assert.True(host.Unlock(editable, content, Raw).Accepted);
        var once = f.Simulation.ExportCheckpoint().CanonicalText();
        Assert.Equal(CookingMajorProgressReason.Duplicate, host.Unlock(editable, content, Raw).Reason);
        Assert.Equal(once, f.Simulation.ExportCheckpoint().CanonicalText()); Assert.Single(editable.Unlocks);
        Assert.Throws<InvalidOperationException>(() => f.Simulation.AddItem(new("external"), Raw, ItemLocation.World("receiving")));
        Assert.Equal(once, f.Simulation.ExportCheckpoint().CanonicalText());
    }
}
