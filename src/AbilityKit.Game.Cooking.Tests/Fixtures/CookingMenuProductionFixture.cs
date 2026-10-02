using AbilityKit.Game.Cooking;
using System.Text.Json;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// Test action driver over the real simulation. Setup creates only finite raw units and empty
/// vessels from validated initial supply; every production/transfer is a public command.
/// This individual-slot fixture does not replace S07 procurement or ET recovery acceptance.
/// </summary>
internal sealed class CookingMenuProductionFixture
{
    private readonly CookingMenuDocument graph;
    private readonly string catalogHash;
    private readonly Dictionary<DefinitionId, CookingMenuStep> producers;
    private readonly Dictionary<string, ItemId> working = new(StringComparer.Ordinal);
    private readonly Dictionary<RecipeId, ItemId> storage = new();
    private readonly Dictionary<ItemId, string> homes = new();
    private readonly HashSet<ItemId> reserved = new();
    private PlayerId player = new("menu-chef");
    private readonly bool manualHandoff;
    private bool handedOff;
    private readonly CookingScope scope;
    private readonly CookingLevelScope level;
    private readonly IReadOnlyList<CookingSpatialAnchor> anchors;
    private readonly string servingHome;
    private readonly bool reverseBranches;
    private long commandSequence;
    private long frame;

    public CookingMenuProductionFixture(CookingMenuCatalog catalog, CookingContentDocument baseline, string sourceId,
        bool reverseBranches = false, int additionalWorkingCapacity = 0, RecipeId? counterfeitFor = null,
        bool manualHandoff = false)
    {
        this.reverseBranches = reverseBranches;
        this.manualHandoff = manualHandoff;
        catalogHash = catalog.Sha256;
        graph = catalog.Document;
        Menu = graph.Menus.Single(x => x.SourceId == sourceId);
        var closure = catalog.Requirements(new[] { sourceId });
        producers = graph.Steps.Where(x => closure.Recipes.Contains(x.Id)).ToDictionary(x => x.Output);
        scope = new(new SessionId("menu-production"), new WorldId("menu-world"), new MatchId(sourceId));
        level = new(scope, new RestaurantRuntimeId(1), new LevelId("menu-fixture"), 1);
        var projected = catalog.ToContentDocument(baseline, new[] { sourceId });
        if (manualHandoff)
            projected = projected with { Recipes = projected.Recipes.Select(x => x.Execution == "Manual"
                ? x with { RequiredTicks = 4 } : x).ToArray() }; // Explicit progress-test fixture, not balance.
        if (counterfeitFor is { } recipe)
        {
            var required = producers.Values.Single(x => x.Id == recipe).Carrier;
            var legitimate = projected.Items.Single(x => x.Id == required.Value);
            projected = projected with { Items = projected.Items.Append(legitimate with { Id = "fixture-counterfeit-vessel" }).ToArray() };
        }
        if (additionalWorkingCapacity > 0)
        {
            // An explicit negative-test fixture gives wrong early toppings room to be loaded,
            // isolating the recipe stage guard from the ordinary capacity guard. Not balance.
            var carrierIds = producers.Values.Select(x => x.Carrier.Value).ToHashSet(StringComparer.Ordinal);
            projected = projected with { Items = projected.Items.Select(x => x.Container is { } carrier && carrierIds.Contains(x.Id)
                ? x with { Container = carrier with { Capacity = carrier.Capacity + additionalWorkingCapacity } } : x).ToArray() };
        }
        var supply = new List<CookingContentSupplyEntry>();
        var initial = new List<(ItemId Id, DefinitionId Definition, string Anchor)>();
        var geometry = new List<CookingSpatialAnchor>();
        void Anchor(LocationKind kind, string id)
        {
            var index = geometry.Count;
            geometry.Add(new(kind, id, index % 12 * 2000, index / 12 * 2000));
        }
        ItemId EmptyOrRaw(DefinitionId definition, string label)
        {
            var id = new ItemId("fixture-" + label);
            var home = "initial-" + label;
            Anchor(LocationKind.WorldPosition, home);
            supply.Add(new(definition.Value, 1, "world:" + home));
            initial.Add((id, definition, home));
            homes.Add(id, home);
            return id;
        }
        // Explicit fixture number, not balance. Distinct world slots even for repeated definitions.
        foreach (var raw in closure.Supplies)
            for (var copy = 1; copy <= 8; copy++) EmptyOrRaw(raw, raw.Value + "-" + copy);
        foreach (var step in producers.Values.OrderBy(x => x.Id.Value, StringComparer.Ordinal))
        {
            var key = WorkingKey(step);
            if (!working.ContainsKey(key)) working.Add(key, EmptyOrRaw(step.Carrier, key.Replace('|', '-')));
            if (step.OutputStorageContainer is { } outputStorage)
                storage.Add(step.Id, EmptyOrRaw(outputStorage, "storage-" + step.Id.Value));
        }
        if (counterfeitFor is not null) CounterfeitWorkingVessel = EmptyOrRaw(new DefinitionId("fixture-counterfeit-vessel"), "counterfeit");
        Anchor(LocationKind.WorldPosition, "clean-pool");
        servingHome = "serving-" + sourceId;
        Anchor(LocationKind.WorldPosition, servingHome);
        var vessel = graph.Containers.Single(x => x.Id == Menu.ServingContainer);
        if (vessel.Disposable)
            for (var copy = 1; copy <= 2; copy++) EmptyOrRaw(vessel.Id, "cup-" + copy);
        else supply.Add(new(vessel.Id.Value, 2, CookingContentCatalog.CleanPoolLocation));
        foreach (var appliance in projected.Appliances.OrderBy(x => x.Station, StringComparer.Ordinal))
            Anchor(LocationKind.StationSlot, appliance.Station);
        if (manualHandoff) geometry.Add(new(LocationKind.WorldPosition, "partner-parking", 25000, 1000));
        anchors = geometry;
        var spatial = new CookingSpatialConfiguration(-1000, -1000, 26000, (geometry.Count / 12 + 2) * 2000,
            40, manualHandoff ? 1200 : 900, manualHandoff ? new[] { new CookingPlayerPose(player, 0, 0, 0, 1),
                new CookingPlayerPose(new("menu-partner"), 25000, 1000, 0, 1) } : new[] { new CookingPlayerPose(player, 0, 0, 0, 1) }, geometry,
            Array.Empty<CookingSpatialObstacle>());
        Content = CookingContentCatalog.Load(projected with { StandardInitialSupply = supply, Spatial = spatial });
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [player] = new(player, new HashSet<string> { "cook" }, Content.Appliances.Keys.Select(x => x.Value).ToHashSet()),
        };
        if (manualHandoff) players.Add(new("menu-partner"), new(new("menu-partner"),
            new HashSet<string> { "cook" }, Content.Appliances.Keys.Select(x => x.Value).ToHashSet()));
        RecipeFixture = CookingContentCatalog.BuildFixture(Content, scope, players, "clean-pool");
        Simulation = new(RecipeFixture);
        // The stock plan is the validated document above. Its repeated-definition unit entries
        // need distinct fixture IDs; the legacy standard spawner resets ordinals per entry.
        foreach (var entry in initial)
        {
            Assert.True(graph.Materials.Any(x => x.Id == entry.Definition && x.Kind == CookingMenuMaterialKind.Supply) ||
                Content.Items[entry.Definition].Container is not null);
            Simulation.AddItem(entry.Id, entry.Definition, ItemLocation.World(entry.Anchor));
        }
        Assert.Equal(supply.Sum(x => x.Count), Simulation.Snapshot().Items.Count);
    }

    public CookingMenuEntry Menu { get; }
    public CookingContent Content { get; }
    public CookingRecipeFixture RecipeFixture { get; }
    public CookingLevelScope Level => level;
    public CookingRecipeSimulation Simulation { get; private set; }
    public Func<CookingRecipeCommand, CookingRecipeCommandResult>? CommandDispatcher { get; set; }
    public Action? FrameAdvance { get; set; }
    public Action? AfterFrame { get; set; }
    public void UseRestoredSimulation(CookingRecipeSimulation simulation) => Simulation = simulation;
    public List<RecipeId> ExecutedRecipes { get; } = new();
    public IReadOnlyList<CookingRecipeSnapshotItem> Items => Simulation.Snapshot().Items;
    public ItemId? ServingVessel { get; private set; }
    public ItemId? CounterfeitWorkingVessel { get; }
    public bool HandedOff => handedOff;

    public ItemId ProduceAndPlate()
    {
        var product = Acquire(Menu.Product);
        var vessel = Items.First(x => x.Definition == Menu.ServingContainer && !x.IsDirty &&
            Simulation.ItemsInContainer(x.Id).Count == 0).Id;
        Pick(vessel);
        Go(LocationKind.WorldPosition, servingHome);
        Act(CookingRecipeOperation.Drop, vessel, world: servingHome);
        Transfer(product, vessel);
        ServingVessel = vessel;
        Assert.Equal(Menu.Product, State(product).Definition);
        Assert.Equal(Menu.FinalRecipe, State(product).Recipe);
        Assert.Equal(Menu.ServingContainer, State(vessel).Definition);
        Assert.Equal(vessel.Value, State(product).Location.OwnerId);
        return product;
    }

    public void SubmitMeal(ItemId product)
    {
        Assert.False(Menu.RequiresBinding); // S05 drinks remain an explicit pending delivery boundary.
        var order = new OrderId("order-" + Menu.SourceId);
        Assert.True(Simulation.OpenOrder(order, Menu.OrderTemplate).Accepted);
        GoToItem(product);
        Act(CookingRecipeOperation.SubmitOrder, product, order: order);
        var settlement = Assert.Single(Simulation.SettlementHistory);
        Assert.Equal(Menu.FinalRecipe, settlement.Recipe);
        Assert.Equal(Menu.OrderTemplate, settlement.Template);
        Assert.Equal(ServingVessel, settlement.Container);
        Assert.Equal(100, Simulation.Snapshot().TotalScore);
    }

    public void WriteEvidence(ItemId product)
    {
        var directory = Environment.GetEnvironmentVariable("COOKING_MENU_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var snapshot = Simulation.Snapshot();
        var evidence = new
        {
            schema = "cooking-menu-production-evidence-v1",
            menuSourceId = Menu.SourceId,
            status = Menu.RequiresBinding ? "produced-plated-binding-pending" : "produced-plated-submitted",
            scope = scope,
            contentIdentity = Content.Identity,
            catalogSha256 = catalogHash,
            sources = graph.Sources,
            fixtureValues = "raw-eight-per-definition-serving-two-not-balanced",
            initialSupply = Content.StandardInitialSupply,
            executedRecipes = ExecutedRecipes.Select(x => x.Value).ToArray(),
            productId = product.Value,
            servingVesselId = ServingVessel!.Value.Value,
            logicalTick = snapshot.LogicalTick,
            settlements = Simulation.SettlementHistory,
            remainingItems = snapshot.Items.OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToArray(),
            snapshotSha256 = snapshot.Sha256(),
            pending = "S05-drink-binding-disposal-and-all-87-ET-recovery-S07-procurement-S14-exit",
        };
        File.WriteAllText(Path.Combine(directory, Menu.SourceId + ".json"), JsonSerializer.Serialize(evidence,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
    }

    public void RejectPrematureAdditionThenRecover(DefinitionId addition)
    {
        Assert.Contains(addition, Menu.FinalAdditions);
        var first = producers.Values.Single(x => x.SourceId == Menu.SourceId &&
            graph.Materials.Single(m => m.Id == x.Output).Kind == CookingMenuMaterialKind.Stage &&
            !x.Inputs.Any(i => graph.Materials.Single(m => m.Id == i.Definition).Kind == CookingMenuMaterialKind.Stage));
        var vessel = working[WorkingKey(first)];
        var topping = Acquire(addition);
        Transfer(topping, vessel);
        var baseUnits = new List<ItemId>();
        foreach (var input in first.Inputs)
            for (var portion = 0; portion < input.Portions; portion++)
            {
                var unit = Acquire(input.Definition);
                baseUnits.Add(unit);
                Transfer(unit, vessel);
            }
        Pick(vessel);
        var station = Content.Appliances.Values.First(x => x.Capabilities.Contains(first.Capability)).Station;
        Go(LocationKind.StationSlot, station.Value);
        var before = Simulation.Snapshot().CanonicalText();
        var result = Simulation.Submit(new(scope, ++commandSequence, player, new RecipeCommandId("premature-" + commandSequence),
            CookingRecipeOperation.StartProcess, Recipe: first.Id, Item: vessel, Station: station,
            ExpectedItemVersion: State(vessel).Version));
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, result.Reason);
        Assert.Equal(before, Simulation.Snapshot().CanonicalText());
        Go(LocationKind.WorldPosition, homes[vessel]);
        Act(CookingRecipeOperation.Drop, vessel, world: homes[vessel]);
        var toppingProducer = producers[addition];
        var safeStorage = storage.TryGetValue(toppingProducer.Id, out var stored) ? stored : working[WorkingKey(toppingProducer)];
        Transfer(topping, safeStorage); // Actual take-out/put-in recovery, never a fixture reset.
        reserved.Remove(topping);
        foreach (var unit in baseUnits) reserved.Remove(unit);
    }

    public void RejectInsufficientCountThenRecover()
    {
        var step = producers.Values.First(x => x.SourceId == Menu.SourceId && x.Inputs.Any(i => i.Portions > 1));
        var shortDefinition = step.Inputs.First(x => x.Portions > 1).Definition;
        var vessel = working[WorkingKey(step)];
        var units = new List<ItemId>();
        foreach (var input in step.Inputs.OrderBy(x => graph.Materials.Single(m => m.Id == x.Definition).Kind == CookingMenuMaterialKind.Stage ? 0 : 1))
            for (var portion = 0; portion < input.Portions - (input.Definition == shortDefinition ? 1 : 0); portion++)
            {
                var unit = Acquire(input.Definition);
                units.Add(unit);
                Transfer(unit, vessel);
            }
        Pick(vessel);
        var station = Content.Appliances.Values.First(x => x.Capabilities.Contains(step.Capability)).Station;
        Go(LocationKind.StationSlot, station.Value);
        var before = Simulation.Snapshot().CanonicalText();
        var result = Simulation.Submit(new(scope, ++commandSequence, player, new RecipeCommandId("short-count-" + commandSequence),
            CookingRecipeOperation.StartProcess, Recipe: step.Id, Item: vessel, Station: station,
            ExpectedItemVersion: State(vessel).Version));
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, result.Reason);
        Assert.Equal(before, Simulation.Snapshot().CanonicalText());
        Go(LocationKind.WorldPosition, homes[vessel]);
        Act(CookingRecipeOperation.Drop, vessel, world: homes[vessel]);
        foreach (var unit in units) reserved.Remove(unit); // Release test intentions; actual units remain in the real vessel.
    }

    public void RejectWrongCarrierThenRecover(RecipeId recipe)
    {
        var step = producers.Values.Single(x => x.Id == recipe);
        var wrong = CounterfeitWorkingVessel!.Value;
        var units = new List<ItemId>();
        foreach (var input in step.Inputs)
            for (var portion = 0; portion < input.Portions; portion++)
            {
                var unit = Acquire(input.Definition);
                units.Add(unit);
                Transfer(unit, wrong);
            }
        Pick(wrong);
        var station = Content.Appliances.Values.First(x => x.Capabilities.Contains(step.Capability)).Station;
        Go(LocationKind.StationSlot, station.Value);
        var before = Simulation.Snapshot().CanonicalText();
        Assert.DoesNotContain(Simulation.PreviewInteraction(player, commandSequence + 1, new RecipeCommandId("wrong-preview")),
            x => x.Command.Operation == CookingRecipeOperation.StartProcess);
        Assert.Equal(before, Simulation.Snapshot().CanonicalText());
        var result = Simulation.Submit(new(scope, ++commandSequence, player, new RecipeCommandId("wrong-carrier-" + commandSequence),
            CookingRecipeOperation.StartProcess, Recipe: recipe, Item: wrong, Station: station,
            ExpectedItemVersion: State(wrong).Version));
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, result.Reason);
        Assert.Equal(before, Simulation.Snapshot().CanonicalText());
        Go(LocationKind.WorldPosition, homes[wrong]);
        Act(CookingRecipeOperation.Drop, wrong, world: homes[wrong]);
        foreach (var unit in units)
        {
            Transfer(unit, working[WorkingKey(step)]);
            reserved.Remove(unit);
        }
    }

    private ItemId Acquire(DefinitionId definition)
    {
        var found = Items.FirstOrDefault(x => x.Definition == definition && !reserved.Contains(x.Id));
        if (found is null)
        {
            Assert.True(producers.ContainsKey(definition), $"Finite supply exhausted: {Menu.SourceId}/{definition}");
            Produce(producers[definition]);
            found = Items.First(x => x.Definition == definition && !reserved.Contains(x.Id));
        }
        reserved.Add(found.Id);
        return found.Id;
    }

    private void Produce(CookingMenuStep step)
    {
        var vessel = working[WorkingKey(step)];
        // Finish an explicit previous stage before adding its final toppings. Transfer each
        // acquired unit immediately so repeated single-yield preparation can reuse its vessel.
        var ordered = step.Inputs.OrderBy(x => graph.Materials.Single(m => m.Id == x.Definition).Kind == CookingMenuMaterialKind.Stage ? 0 : 1);
        var inputPlan = reverseBranches ? ordered.ThenByDescending(x => x.Definition.Value, StringComparer.Ordinal)
            : ordered.ThenBy(x => x.Definition.Value, StringComparer.Ordinal);
        foreach (var input in inputPlan)
            for (var portion = 0; portion < input.Portions; portion++) Transfer(Acquire(input.Definition), vessel);
        Assert.Equal(CookingMenuCatalog.ExpandInputs(step).Order(),
            Simulation.ItemsInContainer(vessel).Select(x => State(x).Definition.Value).Order());
        Pick(vessel);
        var station = Content.Appliances.Values.First(x => x.Capabilities.Contains(step.Capability)).Station;
        Go(LocationKind.StationSlot, station.Value);
        var willHandoff = manualHandoff && !handedOff && step.ExecutionKind == CookingMenuExecutionKind.Manual;
        if (willHandoff) Act(CookingRecipeOperation.Drop, vessel, station: station);
        Act(CookingRecipeOperation.StartProcess, vessel, station: station, recipe: step.Id);
        var originalPlayer = player;
        if (willHandoff)
        {
            var process = Simulation.Snapshot().Processes.Single(x => x.Anchor == vessel);
            Assert.Equal(CookingRecipeOutcome.Accepted, Dispatch(new(scope, ++commandSequence, player,
                new("pause-" + commandSequence), CookingRecipeOperation.StopProcess, Process: process.Id)).Outcome);
            var elapsed = Simulation.Snapshot().Processes.Single(x => x.Id == process.Id).ElapsedTicks;
            Tick();
            Assert.Equal(elapsed, Simulation.Snapshot().Processes.Single(x => x.Id == process.Id).ElapsedTicks);
            player = new("menu-partner");
            Go(LocationKind.StationSlot, station.Value);
            Assert.Equal(elapsed, Simulation.Snapshot().Processes.Single(x => x.Id == process.Id).ElapsedTicks);
            Assert.Equal(CookingRecipeOutcome.Accepted, Dispatch(new(scope, ++commandSequence, player,
                new("resume-" + commandSequence), CookingRecipeOperation.ContinueProcess, Process: process.Id)).Outcome);
            handedOff = true;
        }
        var budget = Content.Recipes[step.Id].RequiredTicks + 1;
        while (Simulation.Snapshot().Processes.Any(x => x.Anchor == vessel))
        {
            Assert.True(budget-- > 0, $"Process did not progress: {step.Id}");
            Tick();
        }
        if (player != originalPlayer) Go(LocationKind.WorldPosition, "partner-parking");
        player = originalPlayer;
        if (willHandoff) Pick(vessel);
        Go(LocationKind.WorldPosition, homes[vessel]);
        Act(CookingRecipeOperation.Drop, vessel, world: homes[vessel]);
        ExecutedRecipes.Add(step.Id);
        if (step.YieldPortions > 1)
        {
            var target = storage[step.Id];
            Pick(target);
            GoToItem(vessel);
            for (var portion = 0; portion < step.YieldPortions; portion++)
            {
                var before = State(vessel).RemainingPortions;
                Act(CookingRecipeOperation.ServePortion, vessel, container: target);
                Assert.Equal(before - 1, State(vessel).RemainingPortions);
            }
            Assert.Empty(Simulation.ItemsInContainer(vessel));
            Assert.False(State(vessel).ContainerCompleted);
            Go(LocationKind.WorldPosition, homes[target]);
            Act(CookingRecipeOperation.Drop, target, world: homes[target]);
        }
        else if (storage.TryGetValue(step.Id, out var singleStorage))
        {
            var output = Simulation.ItemsInContainer(vessel).Single(x => State(x).Definition == step.Output);
            Transfer(output, singleStorage);
            Assert.Empty(Simulation.ItemsInContainer(vessel));
            Assert.Equal(singleStorage.Value, State(output).Location.OwnerId);
        }
    }

    private void Transfer(ItemId item, ItemId vessel)
    {
        if (State(item).Location is { Kind: LocationKind.ContainerSlot, OwnerId: { } owner } && owner == vessel.Value) return;
        Pick(item);
        GoToItem(vessel);
        Act(CookingRecipeOperation.PutIn, item, container: vessel);
    }

    private void Pick(ItemId item)
    {
        var location = State(item).Location;
        if (location == ItemLocation.Hand(player)) return;
        GoToItem(item);
        if (location is { Kind: LocationKind.ContainerSlot, OwnerId: { } owner })
            Act(CookingRecipeOperation.TakeOut, item, container: new ItemId(owner));
        else Act(CookingRecipeOperation.Pickup, item);
    }

    private void GoToItem(ItemId item)
    {
        var location = State(item).Location;
        while (location is { Kind: LocationKind.ContainerSlot, OwnerId: { } owner }) location = State(new ItemId(owner)).Location;
        if (location.Kind == LocationKind.PlayerHand) return;
        Go(location.Kind, location.SlotId!);
    }

    private void Go(LocationKind kind, string id)
    {
        var anchor = anchors.Single(x => x.Kind == kind && x.Id == id);
        var budget = 200;
        while (true)
        {
            Assert.True(budget-- > 0, $"Navigation did not converge: {Menu.SourceId}/{id}");
            var pose = Simulation.Snapshot().Poses!.Single(x => x.Player == player);
            if (player.Value == "menu-partner" && kind == LocationKind.StationSlot &&
                Math.Abs(anchor.X - pose.X) + Math.Abs(anchor.Y - pose.Y) <= 1000) break;
            var dx = Math.Clamp(anchor.X - pose.X, -1000, 1000);
            var dy = dx == 0 ? Math.Clamp(anchor.Y - pose.Y, -1000, 1000) : 0;
            if (dx == 0 && dy == 0) break;
            var result = Dispatch(new(scope, ++commandSequence, player, new RecipeCommandId("move-" + commandSequence),
                CookingRecipeOperation.Move, MoveX: dx, MoveY: dy, FacingX: Math.Sign(dx), FacingY: Math.Sign(dy)));
            Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.Reason.ToString());
        }
    }

    private void Act(CookingRecipeOperation operation, ItemId item, ItemId? container = null,
        StationSlotId? station = null, RecipeId? recipe = null, OrderId? order = null, string? world = null)
    {
        var result = Dispatch(new(scope, ++commandSequence, player, new RecipeCommandId("action-" + commandSequence),
            operation, Recipe: recipe, Item: item, Station: station, Container: container, Order: order,
            ExpectedItemVersion: State(item).Version, WorldAnchor: world));
        Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, $"{Menu.SourceId}/{recipe}/{operation}: {result.Reason}");
    }

    private CookingRecipeCommandResult Dispatch(CookingRecipeCommand command)
    {
        if (CommandDispatcher is { } dispatcher)
        {
            var result = dispatcher(command);
            AfterFrame?.Invoke();
            return result;
        }
        var directResult = Simulation.Submit(command);
        Tick();
        return directResult;
    }
    private void Tick()
    {
        if (FrameAdvance is { } advance) advance();
        else Simulation.AdvanceFixedTick(level, ++frame);
        AfterFrame?.Invoke();
    }
    private CookingRecipeSnapshotItem State(ItemId id) => Items.Single(x => x.Id == id);
    private static string WorkingKey(CookingMenuStep step) => step.SourceId + "|" + step.Carrier.Value;
}
