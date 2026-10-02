using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

// Test-only action planner over the catalog and the existing ET authority. No recipe
// or front state is simulated here; every material enters through finite procurement.
internal sealed class CookingScopedCarryFixture : ICookingMenuGameplayFactory
{
    public static readonly PlayerId Chef = new("natural-chef"), Partner = new("natural-partner");
    private readonly Dictionary<DefinitionId, CookingMenuStep> producers;
    private readonly Dictionary<string, ItemId> working = new(StringComparer.Ordinal);
    private readonly Dictionary<RecipeId, ItemId> storage = new();
    private readonly Dictionary<ItemId, string> homes = new();
    private readonly Dictionary<string, ItemId> servingVessels = new(StringComparer.Ordinal);
    private readonly List<(ItemId Id, DefinitionId Definition, string Home)> initial = new();
    private readonly HashSet<ItemId> reserved = new();
    private readonly Dictionary<string, CookingLayoutCell> cells = new(StringComparer.Ordinal);
    private readonly Dictionary<StationSlotId, DefinitionId> bindings = new();
    private readonly CookingRestaurantLayout layout;
    private readonly int floorHeight;
    private PlayerId player = Chef;
    private long sequence;
    public ItemId SpareMix { get; }
    public CookingMenuCatalog Catalog { get; }
    public CookingContent Content { get; }
    public CookingRecipeFixture RecipeFixture { get; }
    public CookingRecipeSimulation Simulation { get; private set; } = null!;
    public CookingLevelScope Scope { get; private set; } = new(new(new("natural-session"), new("world"), new("match")), new(1), new("service"), 1);
    public CookingLevelEtHost Host { get; private set; } = null!;
    public List<string> Trace { get; } = new();
    public List<CookingRecipeCommand?> Inputs { get; } = new();
    public Action? AfterFrame { get; set; }
    public bool HandedOff { get; private set; }
    public bool AutomaticLeftUnattended { get; private set; }
    public bool UnboundCupHandedOff { get; private set; }
    public IReadOnlyList<CookingRecipeSnapshotItem> Items => Simulation.Snapshot().Items;
    public string FinalCanonical => Host.Observe().CanonicalText() + "\n" + Simulation.ExportCheckpoint().CanonicalText();
    public CookingRecipeSnapshotItem State(ItemId id) => Items.Single(x => x.Id == id);

    public CookingScopedCarryFixture()
    {
        var bundled = CookingMenuCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingMenuCatalog.ContentFileName)));
        var document = bundled.Document;
        Catalog = CookingMenuCatalog.Load(document with { Steps = document.Steps.Select(s => s with { RequiredTicks = 6 }).ToArray() });
        // Trusted test timing for visible manual handoff and unattended equipment;
        // the catalog hash and projected provenance both derive from this copy.
        var requirements = Catalog.Requirements(new[] { "F01", "D31" });
        producers = Catalog.Document.Steps.Where(s => requirements.Recipes.Contains(s.Id)).ToDictionary(s => s.Output);
        var baseline = JsonSerializer.Deserialize<CookingContentDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            CookingContentCatalog.ContentFileName)), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var projected = Catalog.ToContentDocument(baseline, new[] { "F01", "D31" });
        var targets = new List<CookingLayoutTarget>();
        var equipment = new List<CookingEquipmentPlacement>();
        var anchors = new List<CookingSpatialAnchor>();
        var supplies = new List<CookingContentSupplyEntry>();
        var supplierDefinitions = new List<CookingSupplierDefinition>();
        int nextCell = 0;
        CookingLayoutCell AllocateCell() { var index = nextCell++; return new(2 + index % 8, index / 8 * 2); }
        string AddTarget(string id, CookingLayoutTargetKind kind = CookingLayoutTargetKind.Storage)
        {
            var cell = AllocateCell(); cells.Add(id, cell);
            targets.Add(new(id, kind, cell)); anchors.Add(new(LocationKind.WorldPosition, id, cell.X * 1000 + 500, cell.Y * 1000 + 500)); return id;
        }
        ItemId Empty(DefinitionId definition, string label)
        {
            // Instance/anchor IDs are compact stable fixture ordinals; catalog
            // definition/recipe IDs, all receipts and all item state remain intact.
            var id = new ItemId("v" + initial.Count); var home = AddTarget("h" + initial.Count);
            initial.Add((id, definition, home)); homes.Add(id, home); supplies.Add(new(definition.Value, 1, "world:" + home)); return id;
        }
        foreach (var step in producers.Values.OrderBy(s => s.Id.Value, StringComparer.Ordinal))
        {
            var key = step.SourceId + "|" + step.Carrier.Value;
            if (!working.ContainsKey(key)) working.Add(key, Empty(step.Carrier, key.Replace('|', '-')));
            if (step.OutputStorageContainer is { } store) storage.Add(step.Id, Empty(store, "storage-" + step.Id.Value));
        }
        foreach (var menu in Catalog.Document.Menus.Where(m => m.SourceId is "F01" or "D31"))
        {
            var container = Catalog.Document.Containers.Single(c => c.Id == menu.ServingContainer);
            if (container.Disposable) servingVessels.Add(menu.SourceId, Empty(menu.ServingContainer, "serving-" + menu.SourceId));
            else
            {
                supplies.Add(new(menu.ServingContainer.Value, 1, CookingContentCatalog.CleanPoolLocation));
                var pooled = new ItemId("pool-" + menu.ServingContainer.Value + "-1");
                servingVessels.Add(menu.SourceId, pooled); homes.Add(pooled, "clean-pool");
            }
        }
        SpareMix = Empty(new("menu-container-mix-bowl"), "spare-mix");
        AddTarget("clean-pool");
        var sharedSource = AddTarget("purchase-source");
        var sharedReceiving = AddTarget("delivery-receiving");
        foreach (var raw in requirements.Supplies)
        {
            var package = "natural-package-" + raw.Value;
            projected = projected with { Items = projected.Items.Append(new CookingContentItem(package, new[] { "cook" }, new(8, new[] { raw.Value }))).ToArray() };
            AddTarget("stock-" + raw.Value);
            supplierDefinitions.Add(new(raw.Value, sharedSource, sharedReceiving, raw, new(package), 8, 3, 16));
        }
        foreach (var appliance in projected.Appliances.OrderBy(a => a.Station, StringComparer.Ordinal))
        {
            var interaction = AllocateCell(); var cell = new CookingLayoutCell(interaction.X, interaction.Y + 1);
            var station = new StationSlotId(appliance.Station); var definition = new DefinitionId("natural-equipment-" + appliance.Station);
            bindings.Add(station, definition); equipment.Add(new(station, definition, cell, 1, 1, 0, 0, -1));
            cells.Add(appliance.Station, interaction); anchors.Add(new(LocationKind.StationSlot, appliance.Station, interaction.X * 1000 + 500, interaction.Y * 1000 + 500));
        }
        AddTarget("washing"); AddTarget("service");
        AddTarget("customer-entrance", CookingLayoutTargetKind.CustomerEntrance); AddTarget("queue", CookingLayoutTargetKind.Queue);
        AddTarget("exit", CookingLayoutTargetKind.Exit);
        for (var table = 1; table <= 3; table++) AddTarget("table-" + table, CookingLayoutTargetKind.Table);
        floorHeight = (nextCell / 8 + 2) * 2;
        cells.Add("chef-parking", new(0, floorHeight - 1)); targets.Add(new("chef-parking", CookingLayoutTargetKind.PlayerEntrance, cells["chef-parking"]));
        anchors.Add(new(LocationKind.WorldPosition, "chef-parking", 500, (floorHeight - 1) * 1000 + 500));
        cells.Add("partner-parking", new(1, floorHeight - 1)); targets.Add(new("partner-parking", CookingLayoutTargetKind.Storage, cells["partner-parking"]));
        anchors.Add(new(LocationKind.WorldPosition, "partner-parking", 1500, (floorHeight - 1) * 1000 + 500));
        layout = new(new("natural-layout"), new[] { new CookingFloorRegion("floor", 0, 0, 12, floorHeight) }, equipment, Array.Empty<CookingLayoutCell>(), targets) { ActorRadius = 40 };
        var spatial = new CookingSpatialConfiguration(0, 0, 12000, floorHeight * 1000, 40, 800,
            new[] { new CookingPlayerPose(Chef, 500, (floorHeight - 1) * 1000 + 500, 1, 0), new CookingPlayerPose(Partner, 1500, (floorHeight - 1) * 1000 + 500, 1, 0) }, anchors,
            Array.Empty<CookingSpatialObstacle>(), 1000);
        Content = CookingContentCatalog.Load(projected with { StandardInitialSupply = supplies, Spatial = spatial, Supply = new(supplierDefinitions) });
        var players = new[] { Chef, Partner }.ToDictionary(p => p, p => new CookingPlayerConfig(p, new HashSet<string> { "cook" }, Content.Appliances.Keys.Select(s => s.Value).ToHashSet()));
        RecipeFixture = CookingContentCatalog.BuildFixture(Content, Scope.MatchScope, players, "clean-pool") with
        { ScoreThresholds = new(1000, 2000, 3000) };
    }

    public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        Assert.Equal(Scope.MatchScope, scope.MatchScope); Assert.Equal(Content.Identity, configuration.Identity);
        var fixture = scope.LevelEpoch == 1 ? RecipeFixture : RecipeFixture with { Spatial = RecipeFixture.Spatial! with { InitialPoses = RecipeFixture.Spatial!.InitialPoses.Select(p => p with { X = p.X + 2000 }).ToArray() } };
        Simulation = new(fixture);
        foreach (var entry in initial) Simulation.AddItem(entry.Id, entry.Definition, ItemLocation.World(entry.Home));
        Assert.All(Simulation.Snapshot().Items, x => Assert.NotNull(Content.Items[x.Definition].Container));
        return Simulation;
    }
    public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
        new(layout, bindings.Values.ToDictionary(d => d, d => new CookingEquipmentFootprint(d, 1, 1, 0, -1)), new(40, 800, 1000),
            bindings.Values.ToHashSet(), bindings.Values.ToHashSet(), bindings, 3, 2);
    public CookingMenuCatalog CreateMenuCatalog(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => Catalog;
    public CookingLevelMenuConfiguration CreateMenuConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
        new(Catalog.Sha256, scope.LevelEpoch == 1 ? new[] { "F01", "D31" } : new[] { "F01" }, Content.Items.Keys.ToHashSet(), new HashSet<DefinitionId>(),
            scope.LevelEpoch == 1 ? Content.Items.Keys.ToHashSet() : NarrowMaterials(), true);
    public void Begin()
    {
        Host = new(new CookingLevelLifecycle(Scope, Content.Snapshot, this));
        Assert.True(Host.BeginPreparation(new(Scope.Level, new("natural-map"), new(layout.Id, Content.Appliances.Keys.ToArray(),
            Content.Items.Values.Where(i => i.Container is not null).Select(i => i.Id).ToArray()), Content.Identity)).Accepted);
    }
    private CookingRecipeCommandResult Dispatch(CookingRecipeCommand command, bool accepted = true)
    {
        Assert.True(Host.TryEnqueue(new(Scope, command, "natural-local", command.Command.Value)).Accepted);
        var frame = Host.Tick(); Assert.True(frame.Accepted); Inputs.Add(command);
        var result = Assert.Single(frame.Dispositions).Result!;
        Assert.True((result.Outcome == CookingRecipeOutcome.Accepted) == accepted, $"{command.Operation}/{command.Item}/{command.Recipe}: {result.Reason}");
        Capture(frame.Dispositions); return result;
    }
    public void Tick() { var frame = Host.Tick(); Assert.True(frame.Accepted); Inputs.Add(null); Capture(frame.Dispositions); }
    public void ReplayFrame(CookingRecipeCommand? command) { if (command is null) Tick(); else Dispatch(command); }
    private void Capture(IReadOnlyList<CookingLevelPendingDisposition> dispositions)
    {
        AfterFrame?.Invoke();
    }
    private CookingRecipeCommand Command(CookingRecipeOperation operation) => new(Scope.MatchScope, ++sequence, player, new("natural-" + sequence), operation);
    private void Act(CookingRecipeOperation operation, ItemId item, ItemId? container = null, StationSlotId? station = null,
        RecipeId? recipe = null, OrderId? order = null, string? world = null) => Dispatch(Command(operation) with
        { Item = item, Container = container, Station = station, Recipe = recipe, Order = order, WorldAnchor = world, ExpectedItemVersion = State(item).Version });
    public void Procure()
    {
        foreach (var supplier in Content.Snapshot.Supply!.Suppliers)
        {
            Go(LocationKind.WorldPosition, supplier.SourceAnchor);
            var delivery = Dispatch(Command(CookingRecipeOperation.RequestSupply) with { SupplierId = supplier.SupplierId, SupplyRequestId = "purchase-" + supplier.SupplierId }).Supply!.DeliveryId;
            Go(LocationKind.WorldPosition, supplier.ReceivingAnchor); Tick(); Tick();
            var package = Dispatch(Command(CookingRecipeOperation.ReceiveSupply) with { DeliveryId = delivery }).Supply!.Package!.Value;
            Pick(package); Go(LocationKind.WorldPosition, "stock-" + supplier.UnitDefinition.Value);
            Act(CookingRecipeOperation.Drop, package, world: "stock-" + supplier.UnitDefinition.Value);
        }
        var supply = Simulation.ExportCheckpoint().Supply!;
        Assert.Equal(Content.Snapshot.Supply!.Suppliers.Count, supply.Deliveries.Count);
        Assert.All(supply.Deliveries, delivery => Assert.Equal(CookingDeliveryPhase.Received, delivery.Phase));
        Assert.All(supply.Balances, balance => Assert.Equal(8, balance.AvailableUnits));
        Assert.Equal(Content.Snapshot.Supply.Suppliers.Count * 8, Host.Observe().Inventory.Sum(i => i.SupplyLiveUnits));
    }
    public ItemId ProduceAndPlate(string menuId)
    {
        var menu = Catalog.Document.Menus.Single(m => m.SourceId == menuId);
        var product = Acquire(menu.Product); var vessel = servingVessels[menuId];
        Transfer(product, vessel); return product;
    }
    public void PrepareComponents(string menuId)
    {
        var menu = Catalog.Document.Menus.Single(m => m.SourceId == menuId);
        foreach (var input in producers[menu.Product].Inputs)
            if (producers.TryGetValue(input.Definition, out var preparation) && !Items.Any(i => i.Definition == input.Definition)) Produce(preparation);
    }
    private ItemId Acquire(DefinitionId definition)
    {
        var found = Items.FirstOrDefault(i => i.Definition == definition && !reserved.Contains(i.Id));
        if (found is null) { Assert.True(producers.ContainsKey(definition), "Procured stock exhausted: " + definition); Produce(producers[definition]); found = Items.First(i => i.Definition == definition && !reserved.Contains(i.Id)); }
        reserved.Add(found.Id); return found.Id;
    }
    private void Produce(CookingMenuStep step, bool pause = false, bool leaveBatch = false)
    {
        var vessel = working[step.SourceId + "|" + step.Carrier.Value];
        foreach (var input in step.Inputs.OrderBy(i => Catalog.Document.Materials.Single(m => m.Id == i.Definition).Kind == CookingMenuMaterialKind.Stage ? 0 : 1).ThenBy(i => i.Definition.Value, StringComparer.Ordinal))
            for (var portion = 0; portion < input.Portions; portion++) Transfer(Acquire(input.Definition), vessel);
        Pick(vessel); var station = Content.Appliances.Values.First(a => a.Capabilities.Contains(step.Capability)).Station;
        Go(LocationKind.StationSlot, station.Value); Act(CookingRecipeOperation.Drop, vessel, station: station);
        Act(CookingRecipeOperation.StartProcess, vessel, station: station, recipe: step.Id);
        if (pause) { var process = Simulation.Snapshot().Processes.Single(p => p.Anchor == vessel); Dispatch(Command(CookingRecipeOperation.StopProcess) with { Process = process.Id }); return; }
        var original = player;
        if (!HandedOff && step.ExecutionKind == CookingMenuExecutionKind.Manual)
        {
            var process = Simulation.Snapshot().Processes.Single(p => p.Anchor == vessel);
            Dispatch(Command(CookingRecipeOperation.StopProcess) with { Process = process.Id });
            var elapsed = Simulation.Snapshot().Processes.Single(p => p.Id == process.Id).ElapsedTicks;
            Go(LocationKind.WorldPosition, "chef-parking"); player = Partner; Go(LocationKind.StationSlot, station.Value);
            Assert.Equal(elapsed, Simulation.Snapshot().Processes.Single(p => p.Id == process.Id).ElapsedTicks);
            Dispatch(Command(CookingRecipeOperation.ContinueProcess) with { Process = process.Id }); HandedOff = true;
        }
        if (step.ExecutionKind == CookingMenuExecutionKind.Automatic)
        {
            Assert.Contains(Simulation.Snapshot().Processes, p => p.Anchor == vessel && p.ActiveWorker is null);
            Go(LocationKind.WorldPosition, player == Chef ? "chef-parking" : "partner-parking"); AutomaticLeftUnattended = true;
        }
        var budget = step.RequiredTicks + 2;
        while (Simulation.Snapshot().Processes.Any(p => p.Anchor == vessel)) { Assert.True(budget-- > 0); Tick(); }
        Pick(vessel); Go(LocationKind.WorldPosition, homes[vessel]); Act(CookingRecipeOperation.Drop, vessel, world: homes[vessel]);
        if (player != original) { Go(LocationKind.WorldPosition, "partner-parking"); player = original; }
        if (leaveBatch) return;
        if (step.YieldPortions > 1)
        {
            var target = storage[step.Id]; Pick(target); GoToItem(vessel);
            for (var portion = 0; portion < step.YieldPortions; portion++) Act(CookingRecipeOperation.ServePortion, vessel, container: target);
            Go(LocationKind.WorldPosition, homes[target]); Act(CookingRecipeOperation.Drop, target, world: homes[target]);
        }
        else if (storage.TryGetValue(step.Id, out var target)) Transfer(Simulation.ItemsInContainer(vessel).Single(), target);
    }
    private void Transfer(ItemId item, ItemId vessel)
    {
        if (State(item).Location.OwnerId == vessel.Value) return;
        Pick(item); GoToItem(vessel); Act(CookingRecipeOperation.PutIn, item, container: vessel);
    }
    public void Pick(ItemId item)
    {
        var location = State(item).Location; if (location == ItemLocation.Hand(player)) return;
        GoToItem(item);
        if (location.Kind == LocationKind.ContainerSlot) Act(CookingRecipeOperation.TakeOut, item, container: new(location.OwnerId!));
        else Act(CookingRecipeOperation.Pickup, item);
    }
    private void GoToItem(ItemId item)
    {
        var location = State(item).Location;
        while (location.Kind == LocationKind.ContainerSlot) location = State(new(location.OwnerId!)).Location;
        if (location.Kind != LocationKind.PlayerHand) Go(location.Kind, location.SlotId!);
    }
    public void Go(LocationKind kind, string id)
    {
        var destination = cells[id]; var spatial = Host.Observe().Recipe!.Poses!;
        var pose = spatial.Single(p => p.Player == player);
        var start = new CookingLayoutCell((int)(pose.X / 1000), (int)(pose.Y / 1000));
        var other = spatial.Single(p => p.Player != player);
        var occupied = layout.Equipment.Select(e => e.Cell).Append(new((int)(other.X / 1000), (int)(other.Y / 1000))).ToHashSet();
        var queue = new Queue<CookingLayoutCell>(); queue.Enqueue(start);
        var parents = new Dictionary<CookingLayoutCell, CookingLayoutCell> { [start] = start };
        while (queue.Count > 0 && !parents.ContainsKey(destination))
        {
            var cell = queue.Dequeue();
            foreach (var next in new[] { new CookingLayoutCell(cell.X + 1, cell.Y), new(cell.X - 1, cell.Y), new(cell.X, cell.Y + 1), new(cell.X, cell.Y - 1) })
                if (next.X >= 0 && next.X < layout.Floors[0].Width && next.Y >= 0 && next.Y < floorHeight && !occupied.Contains(next) && !parents.ContainsKey(next))
                { parents.Add(next, cell); queue.Enqueue(next); }
        }
        Assert.True(parents.ContainsKey(destination), "No real movement route to " + id);
        var path = new Stack<CookingLayoutCell>(); var cursor = destination;
        while (cursor != start) { path.Push(cursor); cursor = parents[cursor]; }
        while (path.Count > 0)
        {
            var next = path.Pop(); pose = Simulation.Snapshot().Poses!.Single(p => p.Player == player);
            var dx = next.X * 1000 + 500 - pose.X; var dy = next.Y * 1000 + 500 - pose.Y;
            Dispatch(Command(CookingRecipeOperation.Move) with { MoveX = dx, MoveY = dy, FacingX = Math.Sign(dx), FacingY = Math.Sign(dy) });
        }
    }
    public void Restore()
    {
        var checkpoint = Host.ExportCheckpoint().Checkpoint!;
        var serialized = CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(checkpoint));
        var decoded = CookingLevelCheckpointCodec.Deserialize(serialized);
        Assert.True(decoded.Accepted, $"{decoded.Reason}; chars={serialized.Length}; frame={Host.HostFrameSequence}"); Host.Dispose();
        var restored = CookingLevelEtHost.Restore(decoded.Checkpoint!, Content.Snapshot, this); Assert.True(restored.Accepted, restored.ToString()); Host = restored.Host!;
        Assert.Same(Simulation, Host.Driver.Simulation);
        Assert.Equal(checkpoint.CanonicalText(), Host.ExportCheckpoint().Checkpoint!.CanonicalText());
    }
    private HashSet<DefinitionId> NarrowMaterials()
    {
        var requirements = Catalog.Requirements(new[] { "F01" });
        var recipes = requirements.Recipes.ToHashSet();
        return requirements.Supplies.Concat(requirements.Containers)
            .Concat(Catalog.Document.Steps.Where(s => recipes.Contains(s.Id)).SelectMany(s => s.Inputs.Select(i => i.Definition).Append(s.Output).Append(s.Carrier)))
            .Concat(Content.Snapshot.Supply!.Suppliers.Where(s => requirements.Supplies.Contains(s.UnitDefinition)).Select(s => s.PackageDefinition)).ToHashSet();
    }
    public CookingLevelPreparation Preparation(CookingLevelScope scope) => new(scope.Level, new("natural-map"), new(layout.Id, Content.Appliances.Keys.ToArray(),
        Content.Items.Values.Where(i => i.Container is not null).Select(i => i.Id).ToArray()), Content.Identity);
    public ItemId PreparePausedDrink()
    {
        var step = Catalog.Document.Steps.Single(s => s.Id.Value == "menu-recipe-finished-cheese-cap-tea-stage-2");
        Produce(step, pause: true); return working[step.SourceId + "|" + step.Carrier.Value];
    }
    public ItemId PrepareUnservedBatch()
    {
        var step = Catalog.Document.Steps.Single(s => s.Id.Value == "menu-recipe-preparation-cheese-cap");
        Produce(step, leaveBatch: true); return working[step.SourceId + "|" + step.Carrier.Value];
    }
    public void PrepareForbiddenStart()
    {
        Transfer(Acquire(new("menu-supply-cream")), SpareMix);
        Transfer(Acquire(new("menu-supply-soft-cheese")), SpareMix);
    }
    public ItemId BatchTarget => storage[new("menu-recipe-preparation-cheese-cap")];
    public void Near(ItemId id) => GoToItem(id);
    public string RequestTea()
    {
        var supplier = Content.Snapshot.Supply!.Suppliers.Single(s => s.UnitDefinition.Value == "menu-supply-green-tea-leaf");
        Go(LocationKind.WorldPosition, supplier.SourceAnchor);
        Dispatch(Command(CookingRecipeOperation.RequestSupply) with { SupplierId = supplier.SupplierId, SupplyRequestId = "carry-tea" });
        return Simulation.SupplySnapshot()!.Ledger.Deliveries.Single(d => d.RequestId == "carry-tea").DeliveryId;
    }
    public void RestoreFrom(CookingLevelCheckpoint checkpoint, long commandSequence)
    {
        Host.Dispose(); var decoded = CookingLevelCheckpointCodec.Deserialize(CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(checkpoint)));
        Assert.True(decoded.Accepted); var restored = CookingLevelEtHost.Restore(decoded.Checkpoint!, Content.Snapshot, this);
        Assert.True(restored.Accepted, restored.ToString()); Host = restored.Host!; sequence = commandSequence;
        Assert.Same(Simulation, Host.Driver.Simulation);
        Assert.Equal(checkpoint.CanonicalText(), Host.ExportCheckpoint().Checkpoint!.CanonicalText());
    }
    public long CommandSequence => sequence;
    public CookingRecipeCheckpoint HandoffBefore { get; private set; } = null!;
    public void Successor()
    {
        Assert.True(Host.CompletePreparation().Accepted); Assert.True(Host.Start().Accepted);
        var ending = Host.BeginEnd(CookingLevelOutcome.Success); Assert.True(ending.Accepted, ending.ToString());
        var ended = Host.CompleteEnd(); Assert.True(ended.Accepted, ended.ToString());
        HandoffBefore = Simulation.ExportCheckpoint();
        var next = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, new("narrow"), 2);
        var result = Host.CreateSuccessor(next.Level, next.LevelEpoch, Preparation(next)); Assert.True(result.Accepted, result.ToString()); Scope = next;
        Assert.True(Host.BeginPreparation(Preparation(next)).Accepted);
        Assert.True(Host.CompletePreparation().Accepted); Assert.True(Host.Start().Accepted);
    }
    public CookingRecipeCommandResult Send(CookingRecipeOperation operation, ItemId? item = null, ItemId? container = null,
        StationSlotId? station = null, RecipeId? recipe = null, ProcessId? process = null,
        string? supplier = null, string? request = null, string? delivery = null, string? world = null, bool accepted = true) =>
        Dispatch(Command(operation) with { Item = item, Container = container, Station = station, Recipe = recipe, Process = process,
            SupplierId = supplier, SupplyRequestId = request, DeliveryId = delivery, WorldAnchor = world,
            ExpectedItemVersion = item is { } id ? State(id).Version : 0 }, accepted);
    public void DropAtHome(ItemId item) { Pick(item); Go(LocationKind.WorldPosition, homes[item]); Act(CookingRecipeOperation.Drop, item, world: homes[item]); }
    public string BusinessCanonical() => JsonSerializer.Serialize(new { Items, Simulation.Snapshot().Processes, Supply = Simulation.SupplySnapshot(),
        Simulation.ExportCheckpoint().NextProductId, Simulation.ExportCheckpoint().NextProcessId });
}

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingScopedCarryEtTests
{
    [Fact]
    public void Narrowed_scope_preserves_approved_work_but_rejects_new_manufacturing_and_restores_exactly()
    {
        var f = new CookingScopedCarryFixture(); f.Begin();
        try
        {
            Assert.Same(f.Simulation, f.Host.Driver.Simulation);
            f.Procure(); var formed = f.ProduceAndPlate("D31");
            var manualAnchor = f.PreparePausedDrink(); var batch = f.PrepareUnservedBatch(); f.PrepareForbiddenStart();
            var deliveryId = f.RequestTea();
            Assert.Same(f.Simulation, f.Host.Driver.Simulation);
            var before = f.Simulation.ExportCheckpoint();
            var pending = before.Supply!.Deliveries.Single(d => d.DeliveryId == deliveryId);
            Assert.Equal(CookingDeliveryPhase.Pending, pending.Phase); Assert.True(pending.RemainingTicks > 0);
            var process = Assert.Single(before.Processes); Assert.Null(process.ActiveWorker); Assert.True(process.ElapsedTicks > 0);
            Assert.True(f.State(batch).RemainingPortions > 0); Assert.True(f.State(formed).IsProduct);
            var oldPose = f.Simulation.Snapshot().Poses!.Single(p => p.Player == CookingScopedCarryFixture.Chef);
            f.Successor(); Assert.Same(f.Simulation, f.Host.Driver.Simulation);
            var carried = f.Simulation.ExportCheckpoint();
            Assert.Equal(before.NextProductId, carried.NextProductId);
            var handoffPending = f.HandoffBefore.Supply!.Deliveries.Single(d => d.DeliveryId == deliveryId);
            Assert.True(handoffPending.RemainingTicks > 0);
            Assert.Equal(handoffPending.RemainingTicks, carried.Supply!.Deliveries.Single(d => d.DeliveryId == deliveryId).RemainingTicks);
            Assert.Equal(JsonSerializer.Serialize(before.Supply.Balances), JsonSerializer.Serialize(carried.Supply.Balances));
            Assert.Equal(process.ElapsedTicks, Assert.Single(carried.Processes).ElapsedTicks);
            Assert.Equal(before.Items.Single(i => i.Id == batch).RemainingPortions, f.State(batch).RemainingPortions);
            var seed = f.Simulation.Snapshot().Poses!.Single(p => p.Player == CookingScopedCarryFixture.Chef);
            Assert.Equal(2500, seed.X); Assert.NotEqual(oldPose.X, seed.X); Assert.Equal(-1, seed.LastMovementTick);
            var saved = f.Host.ExportCheckpoint().Checkpoint!; var sequence = f.CommandSequence;
            var narrowPolicy = f.CreateMenuConfiguration(f.Scope, f.Content.Snapshot);
            Assert.Equal(new[] { "F01" }, narrowPolicy.SelectedMenuIds);
            Assert.DoesNotContain(new DefinitionId("menu-supply-green-tea-leaf"), narrowPolicy.AllowedMaterialDefinitions);
            Assert.Equal(narrowPolicy.Identity(), saved.MenuConfigurationIdentity);
            Assert.Equal(before.Items.Single(i => i.Id == formed).Definition, f.State(formed).Definition);
            Assert.True(f.State(formed).IsProduct);
            Continue(f, formed, manualAnchor, batch, process.Id, deliveryId);
            var final = f.Host.ExportCheckpoint().Checkpoint!.CanonicalText(); var observation = f.Host.Observe().CanonicalText();
            f.RestoreFrom(saved, sequence);
            Continue(f, formed, manualAnchor, batch, process.Id, deliveryId);
            Assert.Equal(final, f.Host.ExportCheckpoint().Checkpoint!.CanonicalText());
            Assert.Equal(observation, f.Host.Observe().CanonicalText());
        }
        finally { f.Host.Dispose(); }
    }
    private static void Continue(CookingScopedCarryFixture f, ItemId formed, ItemId manualAnchor, ItemId batch, ProcessId process, string delivery)
    {
        Assert.Same(f.Simulation, f.Host.Driver.Simulation);
        var tea = f.Content.Snapshot.Supply!.Suppliers.Single(s => s.UnitDefinition.Value == "menu-supply-green-tea-leaf");
        f.Go(LocationKind.WorldPosition, tea.SourceAnchor);
        Reject(f, () => f.Send(CookingRecipeOperation.RequestSupply, supplier: tea.SupplierId, request: "forbidden-tea", accepted: false));
        f.Go(LocationKind.WorldPosition, tea.ReceivingAnchor);
        var watermark = f.Simulation.ExportCheckpoint().NextProductId;
        f.Send(CookingRecipeOperation.ReceiveSupply, delivery: delivery);
        Assert.True(f.Simulation.ExportCheckpoint().NextProductId > watermark);
        var receivedState = f.BusinessCanonical();
        var duplicate = f.Send(CookingRecipeOperation.ReceiveSupply, delivery: delivery);
        Assert.True(duplicate.IsDuplicate); Assert.Equal(receivedState, f.BusinessCanonical());
        var origin = f.Simulation.SupplySnapshot()!.Origins.Single(o => o.DeliveryId == delivery);
        Assert.Equal(8, origin.Units.Count);
        var raw = origin.Units[0]; f.Pick(raw);
        var teaTub = f.Items.First(i => i.Definition.Value == "menu-container-tea-tub" && !i.ContainerCompleted && f.Simulation.ItemsInContainer(i.Id).Count == 0).Id;
        f.Near(teaTub); Reject(f, () => f.Send(CookingRecipeOperation.PutIn, raw, container: teaTub, accepted: false));
        f.Send(CookingRecipeOperation.DiscardItem, raw);
        f.Pick(f.BatchTarget); f.Near(batch);
        Reject(f, () => f.Send(CookingRecipeOperation.ServePortion, batch, container: f.BatchTarget, accepted: false));
        f.DropAtHome(f.BatchTarget);
        f.Pick(f.SpareMix);
        var step = f.Catalog.Document.Steps.Single(s => s.Id.Value == "menu-recipe-preparation-cheese-cap");
        var station = f.Content.Appliances.Values.Single(a => a.Capabilities.Contains(step.Capability)).Station;
        f.Go(LocationKind.StationSlot, station.Value);
        f.Send(CookingRecipeOperation.Drop, f.SpareMix, station: station);
        Reject(f, () => f.Send(CookingRecipeOperation.StartProcess, f.SpareMix, station: station, recipe: step.Id, accepted: false));
        f.Send(CookingRecipeOperation.ClearContents, f.SpareMix); f.DropAtHome(f.SpareMix);
        f.Near(manualAnchor); f.Send(CookingRecipeOperation.ContinueProcess, process: process);
        while (f.Simulation.Snapshot().Processes.Any(p => p.Id == process)) f.Tick();
        Assert.Contains(f.Simulation.ItemsInContainer(manualAnchor), id => f.State(id).IsProduct);
        f.Pick(formed); f.Go(LocationKind.WorldPosition, "chef-parking");
        f.Send(CookingRecipeOperation.Drop, formed, world: "chef-parking");
        f.Pick(formed); f.Send(CookingRecipeOperation.DiscardItem, formed);
        f.Near(batch); f.Send(CookingRecipeOperation.ClearContents, batch);
        Assert.Equal(0, f.State(batch).RemainingPortions);
        Assert.Equal(CookingDeliveryPhase.Received, f.Simulation.SupplySnapshot()!.Ledger.Deliveries.Single(d => d.DeliveryId == delivery).Phase);
    }
    private static void Reject(CookingScopedCarryFixture f, Func<CookingRecipeCommandResult> action)
    {
        // ET advances its fixed clock; rejected gameplay must retain all item/process/supply/allocator state.
        var before = f.BusinessCanonical(); var result = action();
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, result.Reason);
        Assert.Equal(before, f.BusinessCanonical());
    }
}
