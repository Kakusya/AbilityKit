using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

// Test-only action planner over the catalog and the existing ET authority. No recipe
// or front state is simulated here; every material enters through finite procurement.
internal sealed class CookingNaturalOperatingFixture : ICookingMenuGameplayFactory, ICookingScopedFrontOfHouseGameplayFactory
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
    public CookingMenuCatalog Catalog { get; }
    public CookingContent Content { get; }
    public CookingRecipeFixture RecipeFixture { get; }
    public CookingRecipeSimulation Simulation { get; private set; } = null!;
    public CookingLevelScope Scope { get; } = new(new(new("natural-session"), new("world"), new("match")), new(1), new("service"), 1);
    public CookingLevelEtHost Host { get; private set; } = null!;
    public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; }
    public List<string> Trace { get; } = new();
    public List<CookingRecipeCommand?> Inputs { get; } = new();
    public Action? AfterFrame { get; set; }
    public bool HandedOff { get; private set; }
    public bool AutomaticLeftUnattended { get; private set; }
    public bool UnboundCupHandedOff { get; private set; }
    public IReadOnlyList<CookingRecipeSnapshotItem> Items => Simulation.Snapshot().Items;
    public string FinalCanonical => Host.Observe().CanonicalText() + "\n" + Simulation.ExportCheckpoint().CanonicalText();
    private CookingRecipeSnapshotItem State(ItemId id) => Items.Single(x => x.Id == id);

    public CookingNaturalOperatingFixture()
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
        FrontOfHouseConfiguration = new(new(3, 160, 60, 2, 2, 3, 150),
            new[] { Catalog.Document.Menus.Single(m => m.SourceId == "D31").OrderTemplate, Catalog.Document.Menus.Single(m => m.SourceId == "F01").OrderTemplate },
            ManualPolicyIdentity: "natural-front-v1", DeliveryPolicy: new(CookingFrontDeliveryMode.ServingAnchor, "service"));
    }

    public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
    {
        Assert.Equal(Scope, scope); Assert.Equal(Content.Identity, configuration.Identity);
        Simulation = new(RecipeFixture);
        foreach (var entry in initial) Simulation.AddItem(entry.Id, entry.Definition, ItemLocation.World(entry.Home));
        Assert.All(Simulation.Snapshot().Items, x => Assert.NotNull(Content.Items[x.Definition].Container));
        return Simulation;
    }
    public CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
        new(layout, bindings.Values.ToDictionary(d => d, d => new CookingEquipmentFootprint(d, 1, 1, 0, -1)), new(40, 800, 1000),
            bindings.Values.ToHashSet(), bindings.Values.ToHashSet(), bindings, 3, 2);
    public CookingMenuCatalog CreateMenuCatalog(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => Catalog;
    public CookingLevelMenuConfiguration CreateMenuConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
        new(Catalog.Sha256, new[] { "F01", "D31" }, Content.Items.Keys.ToHashSet(), new HashSet<DefinitionId>(), Content.Items.Keys.ToHashSet(), true);
    public CookingFrontOfHouseConfiguration CreateFrontOfHouseConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration) => FrontOfHouseConfiguration;
    public void Begin()
    {
        Host = new(new CookingLevelLifecycle(Scope, Content.Snapshot, this));
        Assert.True(Host.BeginPreparation(new(Scope.Level, new("natural-map"), new(layout.Id, Content.Appliances.Keys.ToArray(),
            Content.Items.Values.Where(i => i.Container is not null).Select(i => i.Id).ToArray()), Content.Identity)).Accepted);
    }
    private CookingRecipeCommandResult Dispatch(CookingRecipeCommand command)
    {
        Assert.True(Host.TryEnqueue(new(Scope, command, "natural-local", command.Command.Value)).Accepted);
        var frame = Host.Tick(); Assert.True(frame.Accepted); Inputs.Add(command);
        var result = Assert.Single(frame.Dispositions).Result!;
        Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, $"{command.Operation}/{command.Item}/{command.Recipe}: {result.Reason}");
        Capture(frame.Dispositions); return result;
    }
    public void Tick() { var frame = Host.Tick(); Assert.True(frame.Accepted); Inputs.Add(null); Capture(frame.Dispositions); }
    public void ReplayFrame(CookingRecipeCommand? command) { if (command is null) Tick(); else Dispatch(command); }
    private void Capture(IReadOnlyList<CookingLevelPendingDisposition> dispositions)
    {
        // Hash each complete frame projection to keep the long service trace bounded.
        var canonical = Host.ExportCheckpoint().Checkpoint!.CanonicalText() + "\n" + Host.Observe().CanonicalText() + "\n" + JsonSerializer.Serialize(dispositions);
        Trace.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))));
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
        Assert.Equal(0, Host.FrontOfHouseSnapshot!.ServiceTicks);
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
    private void Produce(CookingMenuStep step)
    {
        var vessel = working[step.SourceId + "|" + step.Carrier.Value];
        foreach (var input in step.Inputs.OrderBy(i => Catalog.Document.Materials.Single(m => m.Id == i.Definition).Kind == CookingMenuMaterialKind.Stage ? 0 : 1).ThenBy(i => i.Definition.Value, StringComparer.Ordinal))
            for (var portion = 0; portion < input.Portions; portion++) Transfer(Acquire(input.Definition), vessel);
        Pick(vessel); var station = Content.Appliances.Values.First(a => a.Capabilities.Contains(step.Capability)).Station;
        Go(LocationKind.StationSlot, station.Value); Act(CookingRecipeOperation.Drop, vessel, station: station);
        Act(CookingRecipeOperation.StartProcess, vessel, station: station, recipe: step.Id);
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
        if (step.YieldPortions > 1)
        {
            var target = storage[step.Id]; Pick(target); GoToItem(vessel);
            for (var portion = 0; portion < step.YieldPortions; portion++) Act(CookingRecipeOperation.ServePortion, vessel, container: target);
            Go(LocationKind.WorldPosition, homes[target]); Act(CookingRecipeOperation.Drop, target, world: homes[target]);
        }
        else if (storage.TryGetValue(step.Id, out var target)) Transfer(Simulation.ItemsInContainer(vessel).Single(), target);
    }
    public void Deliver(ItemId product, string menuId)
    {
        var menu = Catalog.Document.Menus.Single(m => m.SourceId == menuId);
        var budget = 400;
        while (!Host.FrontOfHouseSnapshot!.Customers.Any(c => c.OrderTemplate == menu.OrderTemplate && c.Order is not null))
        { Assert.True(budget-- > 0, $"No live {menuId} customer order; service tick {Host.FrontOfHouseSnapshot.ServiceTicks}."); Tick(); }
        var order = Host.FrontOfHouseSnapshot!.Customers.First(c => c.OrderTemplate == menu.OrderTemplate && c.Order is not null).Order!.Value;
        var vessel = new ItemId(State(product).Location.OwnerId!);
        if (menu.RequiresBinding)
        {
            Assert.Null(State(product).BoundOrder); Go(LocationKind.WorldPosition, "chef-parking"); player = Partner;
            Pick(vessel); Act(CookingRecipeOperation.BindOrder, product, order: order); UnboundCupHandedOff = true;
        }
        else Pick(vessel);
        Go(LocationKind.WorldPosition, "service"); Act(CookingRecipeOperation.SubmitOrder, product, order: order);
        Go(LocationKind.WorldPosition, player == Chef ? "chef-parking" : "partner-parking"); player = Chef;
    }
    public void FinishNaturally()
    {
        var budget = 600;
        while (!Host.TryFinishService().Accepted) { Assert.True(budget-- > 0); Tick(); }
        Assert.Single(Host.FrontOfHouseSnapshot!.UnsatisfiedOrders); Assert.Equal(0, Simulation.CalculateStars());
        Assert.True(Host.FrontOfHouseSnapshot.Closing); Assert.Empty(Host.FrontOfHouseSnapshot.Customers);
        Assert.Empty(Host.FrontOfHouseSnapshot.WashQueue);
        Assert.All(Host.FrontOfHouseSnapshot.Tables, table => Assert.Equal(CookingFrontTableState.Free, table.State));
        Assert.All(Host.FrontOfHouseSnapshot.Work, work => Assert.Null(work.Player));
        Assert.True(Host.CompleteEnd().Accepted); Assert.Equal(CookingLevelOutcome.Success, Host.Lifecycle.Outcome);
        Assert.Equal(CookingLevelState.Ended, Host.Lifecycle.State);
    }
    private void Transfer(ItemId item, ItemId vessel)
    {
        if (State(item).Location.OwnerId == vessel.Value) return;
        Pick(item); GoToItem(vessel); Act(CookingRecipeOperation.PutIn, item, container: vessel);
    }
    private void Pick(ItemId item)
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
    private void Go(LocationKind kind, string id)
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
        Assert.Equal(checkpoint.CanonicalText(), Host.ExportCheckpoint().Checkpoint!.CanonicalText());
    }
}
