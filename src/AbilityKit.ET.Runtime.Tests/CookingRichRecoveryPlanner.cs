using AbilityKit.Game.Cooking;

namespace AbilityKit.ET.Runtime.Tests;

// This planner reads passive captures and sends commands. It never calls a simulation.
internal sealed class CookingRichRecoveryPlanner(
    CookingRichRecoveryFixture fixture,
    PlayerId player,
    Func<CookingNetworkAuthorityCapture> capture,
    Func<CookingRecipeCommand, Task<CookingRecipeCommandResult>> send,
    Func<Task> wait,
    Func<IReadOnlyList<CookingRecipeCommand>, Task<IReadOnlyList<CookingRecipeCommandResult>>> sendMoves)
{
    private readonly HashSet<ItemId> reserved = new();
    private long sequence;
    private IReadOnlyList<CookingRecipeSnapshotItem> Items => capture().Observation.Recipe!.Items;
    private CookingRecipeSnapshotItem State(ItemId id) => Items.Single(i => i.Id == id);
    private CookingRecipeCommand Command(CookingRecipeOperation operation) =>
        new(capture().Observation.Scope.MatchScope, 0, player, new("process-action-" + ++sequence), operation);

    private async Task<CookingRecipeCommandResult> Dispatch(CookingRecipeCommand command)
    {
        var result = await send(command);
        Require(result.Outcome == CookingRecipeOutcome.Accepted,
            $"{command.Operation}/{command.Item}/{command.Recipe}: {result.Reason}");
        await WaitUntil(() => capture().Observation.Recipe!.Version >= result.StateVersion, "committed projection");
        return result;
    }

    private Task<CookingRecipeCommandResult> Act(CookingRecipeOperation op, ItemId item,
        ItemId? container = null, StationSlotId? station = null, RecipeId? recipe = null,
        OrderId? order = null, string? world = null) => Dispatch(Command(op) with
        { Item = item, Container = container, Station = station, Recipe = recipe, Order = order,
            WorldAnchor = world, ExpectedItemVersion = State(item).Version });

    public async Task Procure()
    {
        foreach (var supplier in fixture.Content.Snapshot.Supply!.Suppliers)
        {
            await Go(supplier.SourceAnchor);
            var requested = await Dispatch(Command(CookingRecipeOperation.RequestSupply) with
            { SupplierId = supplier.SupplierId, SupplyRequestId = "process-purchase-" + supplier.SupplierId });
            var delivery = requested.Supply!.DeliveryId;
            await Go(supplier.ReceivingAnchor);
            await WaitUntil(() => capture().FullRecipe!.Supply!.Deliveries.Any(d =>
                d.DeliveryId == delivery && d.Phase == CookingDeliveryPhase.Arrived), "delivery arrival");
            var received = await Dispatch(Command(CookingRecipeOperation.ReceiveSupply) with { DeliveryId = delivery });
            var package = received.Supply!.Package!.Value;
            await Pick(package);
            var stock = "stock-" + supplier.UnitDefinition.Value;
            await Go(stock); await Act(CookingRecipeOperation.Drop, package, world: stock);
        }
        Require(capture().FullFront!.State.ServiceTicks == 0, "preparation must not advance service time");
    }

    public async Task PrepareManualHandoff()
    {
        var requirements = fixture.Catalog.Requirements(new[] { "F01" });
        var step = fixture.Catalog.Document.Steps.First(s => requirements.Recipes.Contains(s.Id) &&
            s.ExecutionKind == CookingMenuExecutionKind.Manual &&
            s.Output != fixture.Catalog.Document.Menus.Single(m => m.SourceId == "F01").Product);
        var vessel = fixture.WorkingVessels[step.SourceId + "|" + step.Carrier.Value];
        foreach (var input in step.Inputs)
            for (var count = 0; count < input.Portions; count++) await Transfer(await Acquire(input.Definition), vessel);
        await Pick(vessel);
        var station = fixture.Content.Appliances.Values.First(a => a.Capabilities.Contains(step.Capability)).Station;
        await Go(station.Value); await Act(CookingRecipeOperation.Drop, vessel, station: station);
        await Act(CookingRecipeOperation.StartProcess, vessel, station: station, recipe: step.Id);
        var process = capture().Observation.Recipe!.Processes.Single(p => p.Anchor == vessel);
        // Recovery hook may have legitimately released this active worker through normal cleanup.
        if (process.ActiveWorker == player)
            await Dispatch(Command(CookingRecipeOperation.StopProcess) with { Process = process.Id });
        else Require(process.ActiveWorker is null, "Disconnected manual worker must be released before handoff");
        await Go("chef-parking");
        Require(capture().Observation.Recipe!.Processes.Any(p => p.Id == process.Id && p.ActiveWorker is null),
            "manual work remains available for remote handoff");
    }

    public async Task<ItemId> ProduceAndPlate(string menuId)
    {
        var menu = fixture.Catalog.Document.Menus.Single(m => m.SourceId == menuId);
        var product = await Acquire(menu.Product);
        await Transfer(product, fixture.ServingVessels[menuId]);
        return product;
    }

    public async Task CompleteAvailableManualHandoff()
    {
        var process = capture().Observation.Recipe!.Processes.Single(p => p.ActiveWorker is null &&
            fixture.Catalog.Document.Steps.Any(s => s.Id == p.Recipe && s.ExecutionKind == CookingMenuExecutionKind.Manual));
        await Produce(fixture.Catalog.Document.Steps.Single(s => s.Id == process.Recipe));
        Require(!capture().Observation.Recipe!.Processes.Any(p => p.Id == process.Id),
            "remote completed the original shared manual process");
    }

    public async Task PrepareComponents(string menuId)
    {
        var menu = fixture.Catalog.Document.Menus.Single(m => m.SourceId == menuId);
        var final = fixture.Catalog.Document.Steps.Single(s => s.Output == menu.Product);
        foreach (var input in final.Inputs) {
            var step = fixture.Catalog.Document.Steps.SingleOrDefault(s => s.Output == input.Definition);
            if (step is not null && !Items.Any(i => i.Definition == input.Definition)) await Produce(step);
        }
    }

    private async Task<ItemId> Acquire(DefinitionId definition)
    {
        var found = Items.FirstOrDefault(i => i.Definition == definition && !reserved.Contains(i.Id));
        if (found is null)
        {
            var step = fixture.Catalog.Document.Steps.SingleOrDefault(s => s.Output == definition);
            Require(step is not null, "No procured stock or producer: " + definition);
            await Produce(step!);
            found = Items.First(i => i.Definition == definition && !reserved.Contains(i.Id));
        }
        reserved.Add(found.Id);
        return found.Id;
    }

    private async Task Produce(CookingMenuStep step)
    {
        var vessel = fixture.WorkingVessels[step.SourceId + "|" + step.Carrier.Value];
        var process = capture().Observation.Recipe!.Processes.SingleOrDefault(p => p.Anchor == vessel);
        var station = fixture.Content.Appliances.Values.First(a => a.Capabilities.Contains(step.Capability)).Station;
        if (process is null)
        {
            foreach (var input in step.Inputs.OrderBy(i =>
                fixture.Catalog.Document.Materials.Single(m => m.Id == i.Definition).Kind == CookingMenuMaterialKind.Stage ? 0 : 1)
                .ThenBy(i => i.Definition.Value, StringComparer.Ordinal))
                for (var count = 0; count < input.Portions; count++) await Transfer(await Acquire(input.Definition), vessel);
            await Pick(vessel); await Go(station.Value);
            await Act(CookingRecipeOperation.Drop, vessel, station: station);
            await Act(CookingRecipeOperation.StartProcess, vessel, station: station, recipe: step.Id);
        }
        else
        {
            Require(process.ActiveWorker is null, "Remote handoff must release the original worker");
            await Go(station.Value);
            await Dispatch(Command(CookingRecipeOperation.ContinueProcess) with { Process = process.Id });
        }
        if (step.ExecutionKind == CookingMenuExecutionKind.Automatic)
            await Go(player == CookingRichRecoveryFixture.Chef ? "chef-parking" : "partner-parking");
        await WaitUntil(() => !capture().Observation.Recipe!.Processes.Any(p => p.Anchor == vessel), "process completion");
        await Pick(vessel); await Go(fixture.Homes[vessel]);
        await Act(CookingRecipeOperation.Drop, vessel, world: fixture.Homes[vessel]);
        if (step.YieldPortions > 1)
        {
            var target = fixture.StorageVessels[step.Id]; await Pick(target); await GoToItem(vessel);
            for (var count = 0; count < step.YieldPortions; count++)
                await Act(CookingRecipeOperation.ServePortion, vessel, container: target);
            await Go(fixture.Homes[target]); await Act(CookingRecipeOperation.Drop, target, world: fixture.Homes[target]);
        }
        else if (fixture.StorageVessels.TryGetValue(step.Id, out var target))
            await Transfer(Items.Single(i => i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == vessel.Value).Id, target);
    }

    public async Task Deliver(ItemId product, string menuId)
    {
        var menu = fixture.Catalog.Document.Menus.Single(m => m.SourceId == menuId);
        await WaitUntil(() => capture().FullFront!.State.Customers.Any(c => c.OrderTemplate == menu.OrderTemplate && c.Order is not null),
            "customer order " + menuId);
        var order = capture().FullFront!.State.Customers.First(c => c.OrderTemplate == menu.OrderTemplate && c.Order is not null).Order!.Value;
        var vessel = new ItemId(State(product).Location.OwnerId!);
        await Pick(vessel);
        if (menu.RequiresBinding)
        {
            Require(State(product).BoundOrder is null, "unbound drink remains independently transferable");
            await Act(CookingRecipeOperation.BindOrder, product, order: order);
        }
        await Go("service"); await Act(CookingRecipeOperation.SubmitOrder, product, order: order);
        await Go(player == CookingRichRecoveryFixture.Chef ? "chef-parking" : "partner-parking");
        // A legal facing-only command marks that the final route projection was observed.
        // Cardinal path movement never produces this diagonal facing. No further old-scope
        // projection barrier follows: the owner may now publish the successor baseline.
        var returned = await send(Command(CookingRecipeOperation.Move) with { FacingX = 1, FacingY = 1 });
        Require(returned.Outcome == CookingRecipeOutcome.Accepted, "Return-route completion marker accepted.");
    }

    private async Task Transfer(ItemId item, ItemId vessel)
    {
        if (State(item).Location.OwnerId == vessel.Value) return;
        await Pick(item); await GoToItem(vessel); await Act(CookingRecipeOperation.PutIn, item, container: vessel);
    }
    private async Task Pick(ItemId item)
    {
        var location = State(item).Location;
        if (location == ItemLocation.Hand(player)) return;
        await GoToItem(item);
        if (location.Kind == LocationKind.ContainerSlot)
            await Act(CookingRecipeOperation.TakeOut, item, container: new(location.OwnerId!));
        else await Act(CookingRecipeOperation.Pickup, item);
    }
    private Task GoToItem(ItemId item)
    {
        var location = State(item).Location;
        while (location.Kind == LocationKind.ContainerSlot) location = State(new(location.OwnerId!)).Location;
        return location.Kind == LocationKind.PlayerHand ? Task.CompletedTask : Go(location.SlotId!);
    }

    public async Task Go(string anchor)
    {
        var destination = fixture.Cells[anchor];
        var poses = capture().Observation.Recipe!.Poses!;
        var pose = poses.Single(p => p.Player == player);
        var start = new CookingLayoutCell((int)(pose.X / 1000), (int)(pose.Y / 1000));
        var other = poses.Single(p => p.Player != player);
        var blocked = fixture.Layout.Equipment.Select(e => e.Cell)
            .Append(new((int)(other.X / 1000), (int)(other.Y / 1000))).ToHashSet();
        var queue = new Queue<CookingLayoutCell>(); queue.Enqueue(start);
        var parents = new Dictionary<CookingLayoutCell, CookingLayoutCell> { [start] = start };
        while (queue.Count > 0 && !parents.ContainsKey(destination))
        {
            var cell = queue.Dequeue();
            foreach (var next in new[] { new CookingLayoutCell(cell.X + 1, cell.Y), new(cell.X - 1, cell.Y),
                new(cell.X, cell.Y + 1), new(cell.X, cell.Y - 1) })
                if (next.X >= 0 && next.X < fixture.Layout.Floors[0].Width && next.Y >= 0 &&
                    next.Y < fixture.Layout.Floors[0].Height && !blocked.Contains(next) && !parents.ContainsKey(next))
                { parents.Add(next, cell); queue.Enqueue(next); }
        }
        Require(parents.ContainsKey(destination), "No movement route to " + anchor);
        var path = new Stack<CookingLayoutCell>(); var cursor = destination;
        while (cursor != start) { path.Push(cursor); cursor = parents[cursor]; }
        while (path.Count > 0)
        {
            // Every movement waits for its committed response (one translation per owner tick).
            // The complete position projection is checked once per short route segment.
            var commands = new List<CookingRecipeCommand>();
            pose = capture().Observation.Recipe!.Poses!.Single(p => p.Player == player);
            var x = pose.X; var y = pose.Y;
            while (path.Count > 0 && commands.Count < 8)
            {
                var next = path.Pop(); var nextX = next.X * 1000 + 500; var nextY = next.Y * 1000 + 500;
                var dx = nextX - x; var dy = nextY - y;
                commands.Add(Command(CookingRecipeOperation.Move) with
                { MoveX = dx, MoveY = dy, FacingX = Math.Sign(dx), FacingY = Math.Sign(dy) });
                x = nextX; y = nextY;
            }
            var results = await sendMoves(commands);
            Require(results.Count == commands.Count && results.All(r => r.Outcome == CookingRecipeOutcome.Accepted),
                "Batched movement must accept every physical step: " + System.Text.Json.JsonSerializer.Serialize(results.Select(r => new {r.Outcome,r.Reason,r.StateVersion})));
            var version = results.Max(r => r.StateVersion);
            await WaitUntil(() => capture().Observation.Recipe!.Version >= version, "movement batch projection");
            pose = capture().Observation.Recipe!.Poses!.Single(p => p.Player == player);
            Require(pose.X == x && pose.Y == y, "Authoritative position must match the accepted route batch.");
        }
    }

    public async Task WaitUntil(Func<bool> condition, string operation)
    {
        var deadline = Environment.TickCount64 + 30000;
        while (!condition() && Environment.TickCount64 < deadline) await wait();
        Require(condition(), "Deadline expired: " + operation);
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
