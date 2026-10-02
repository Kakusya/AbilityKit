using System.Numerics;

﻿namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    public CookingSpatialConfiguration? SpatialConfiguration => EffectiveSpatial;

    /// <summary>Shared read-only reach query for application-owned World/Station anchors.</summary>
    public bool ValidateSpatialReach(PlayerId player, LocationKind kind, string anchorId) =>
        _fixture.Players.ContainsKey(player) && kind is LocationKind.WorldPosition or LocationKind.StationSlot &&
        LocationIsReachable(new ItemLocation(kind, SlotId: anchorId), player);

    /// <summary>Read-only candidates contain executable payloads; admission still revalidates at execution.</summary>
    public IReadOnlyList<CookingInteractionPreview> PreviewInteraction(PlayerId player, long batch, RecipeCommandId commandId)
    {
        if (!_fixture.Players.TryGetValue(player, out var config) || !config.IsAvailable || !IsGameplayMutationOpen) return Array.Empty<CookingInteractionPreview>();
        var commands = new List<(string Target, CookingRecipeCommand Command)>();
        CookingRecipeCommand C(CookingRecipeOperation op, ItemId? item = null, ItemId? container = null,
            StationSlotId? station = null, OrderId? order = null, ProcessId? process = null) =>
            new(_fixture.Scope, batch, player, commandId, op, Item: item, Container: container, Station: station,
                Order: order, Process: process, ExpectedItemVersion: item is { } i ? _items[i].Version : 0);
        var held = ItemInHand(player);
        foreach (var (id, state) in _items.Where(p => !p.Value.Removed).OrderBy(p => p.Key.Value, StringComparer.Ordinal))
        {
            if (held is null && state.Location.Kind is LocationKind.WorldPosition or LocationKind.StationSlot)
                commands.Add((id.Value, C(CookingRecipeOperation.Pickup, id)));
            if (state.Location.Kind == LocationKind.ContainerSlot && state.Location.OwnerId is { } owner && held is null)
                commands.Add((id.Value, C(CookingRecipeOperation.TakeOut, id, new ItemId(owner))));
            if (TryGetContainerCapability(id, out _))
            {
                if (held is { } h && h != id)
                {
                    commands.Add((id.Value, C(CookingRecipeOperation.PutIn, h, id)));
                    commands.Add((id.Value, C(CookingRecipeOperation.Pour, h, id)));
                    commands.Add((id.Value, C(CookingRecipeOperation.ServePortion, h, id)));
                }
                commands.Add((id.Value, C(CookingRecipeOperation.ClearContents, id)));
            }
            commands.Add((id.Value, C(CookingRecipeOperation.StartProcess, id)));
            foreach (var station in _fixture.Appliances.Keys.OrderBy(s => s.Value, StringComparer.Ordinal))
                commands.Add((station.Value, C(CookingRecipeOperation.StartProcess, id, station: station)));
            foreach (var order in _orders.Keys.OrderBy(o => o.Value, StringComparer.Ordinal))
                commands.Add((id.Value, C(CookingRecipeOperation.SubmitOrder, id, order: order)));
        }
        if (held is { } hand)
        {
            if (EffectiveSpatial is { } map)
                foreach (var anchor in map.Anchors.Where(a => a.Kind == LocationKind.WorldPosition))
                    commands.Add((anchor.Id, C(CookingRecipeOperation.Drop, hand) with { WorldAnchor = anchor.Id }));
            foreach (var station in _fixture.Appliances.Keys.OrderBy(s => s.Value, StringComparer.Ordinal)) commands.Add((station.Value, C(CookingRecipeOperation.Drop, hand, station: station)));
        }
        foreach (var process in AllProcesses().OrderBy(p => p.Id.Value, StringComparer.Ordinal))
        {
            commands.Add((process.Anchor.Value, C(CookingRecipeOperation.ContinueProcess, process: process.Id)));
            commands.Add((process.Anchor.Value, C(CookingRecipeOperation.StopProcess, process: process.Id)));
        }
        var checkpoint = ExportCheckpoint();
        var result = new List<CookingInteractionPreview>();
        foreach (var (target, command) in commands)
        {
            if (!CookingRecipeCommandValidation.IsWellFormed(command)) continue;
            // Sandbox uses the same authority implementation, a private allocator, and no washing callback.
            var sandbox = new CookingRecipeSimulation(_fixture);
            sandbox._menuPolicy = _menuPolicy; sandbox._menuPolicyRecipeIds = _menuPolicyRecipeIds;
            sandbox._menuPolicyMaterialDefinitions = _menuPolicyMaterialDefinitions;
            if (!sandbox.RestoreCheckpoint(checkpoint).Accepted) continue;
            try { if (sandbox.ExecuteValidatedCommand(command).Outcome != CookingRecipeOutcome.Accepted) continue; }
            catch (Exception error) when (error is OverflowException or InvalidOperationException) { continue; }
            long distance = 0, dot = 0;
            if (EffectiveSpatial is not null && _poses.TryGetValue(player, out var pose))
            {
                ItemLocation? location = command.Process is { } pr && TryGetProcess(pr, out var ps) ? _items[ps.Anchor].Location :
                    command.WorldAnchor is { } world ? ItemLocation.World(world) : command.Station is { } st ? ItemLocation.Station(st) :
                    command.Container is { } ct ? _items[ct].Location : command.Item is { } it ? _items[it].Location : null;
                if (location is not null && ResolvePosition(location, out var x, out var y, out _))
                {
                    var dx = (long)x - pose.X; var dy = (long)y - pose.Y;
                    // Accepted candidate lies within int interaction radius, hence squared distance fits long.
                    distance = checked(dx * dx + dy * dy); dot = dx * pose.FacingX + dy * pose.FacingY;
                }
            }
            result.Add(new CookingInteractionPreview(target, command, distance, dot));
        }
        static int Priority(CookingRecipeOperation op) => op switch {
            CookingRecipeOperation.Pickup or CookingRecipeOperation.TakeOut => 0,
            CookingRecipeOperation.Drop or CookingRecipeOperation.PutIn => 1,
            CookingRecipeOperation.ServePortion => 2, CookingRecipeOperation.Pour => 3,
            CookingRecipeOperation.StopProcess or CookingRecipeOperation.ContinueProcess => 4,
            CookingRecipeOperation.SubmitOrder => 5, CookingRecipeOperation.StartProcess => 6, _ => 7 };
        var main = result.GroupBy(p => p.TargetId, StringComparer.Ordinal)
            .Select(g => g.OrderBy(p => Priority(p.Command.Operation)).ThenBy(p => p.Command.Operation).First()).ToList();
        main.Sort((a, b) => {
            // Accepted candidates have nonnegative forward dot; compare cosine squared exactly.
            var aa = a.DistanceSquared == 0 ? BigInteger.One : (BigInteger)a.FacingDot * a.FacingDot;
            var bb = b.DistanceSquared == 0 ? BigInteger.One : (BigInteger)b.FacingDot * b.FacingDot;
            var ad = a.DistanceSquared == 0 ? BigInteger.One : new BigInteger(a.DistanceSquared);
            var bd = b.DistanceSquared == 0 ? BigInteger.One : new BigInteger(b.DistanceSquared);
            var angle = (bb * ad).CompareTo(aa * bd);
            if (angle != 0) return angle;
            var distance = a.DistanceSquared.CompareTo(b.DistanceSquared);
            return distance != 0 ? distance : StringComparer.Ordinal.Compare(a.TargetId, b.TargetId);
        });
        return main;
    }

    private CookingRecipeCommandResult ServePortion(CookingRecipeCommand command)
    {
        if (command.Item is not { } sourceId || !TryGetContainerCapability(sourceId, out _)) return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        var source = _items[sourceId];
        if (source.Version != command.ExpectedItemVersion) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (!source.ContainerCompleted || source.RemainingPortions <= 0) return Reject(CookingRecipeRejectionReason.ProcessNotComplete);
        if (command.Container is not { } target || target == sourceId || !TryGetContainerCapability(target, out var capability))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        if (!ItemIsReachable(sourceId, command.Player) || !ItemIsReachable(target, command.Player)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        return TransferPortions(command, sourceId, source, target, capability, 1);
    }

    private CookingRecipeCommandResult TransferPortions(CookingRecipeCommand command, ItemId sourceId, ItemState source,
        ItemId target, CookingItemContainerCapability capability, int count)
    {
        if (source.Recipe is not { } recipeId || !_fixture.Recipes.TryGetValue(recipeId, out var recipe) ||
            count <= 0 || count > source.RemainingPortions) return Reject(CookingRecipeRejectionReason.ProductNotFound);
        if (ItemsInContainer(target).Any(id => _items[id].BoundOrder is not null)) return Reject(CookingRecipeRejectionReason.BindingConflict);
        if (_items[target].ContainerCompleted) return Reject(CookingRecipeRejectionReason.BatchCompleted);
        if (IsLockedInput(sourceId) || IsLockedInput(target)) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (WouldCreateContainmentCycle(target, sourceId)) return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        if (capability.Capacity - ItemsInContainer(target).Count < count) return Reject(CookingRecipeRejectionReason.ContainerFull);
        if (!capability.AcceptedDefinitions.Contains(recipe.ProductDefinition)) return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        if (!MenuAllowsRecipe(recipe) || !MenuAllowsObjects(sourceId, target)) return Reject(CookingRecipeRejectionReason.MenuNotAuthorized);
        var stagedItems = new Dictionary<ItemId, ItemState>(_items);
        var stagedContents = _containerItems.ToDictionary(p => p.Key, p => new List<ItemId>(p.Value));
        var sequence = _nextProductId;
        var occupied = ItemsInContainer(target).Select(i => _items[i].Location.SlotId!).ToHashSet(StringComparer.Ordinal);
        var products = new List<ItemId>();
        _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        for (var i = 0; i < count; i++)
        {
            sequence = checked(sequence + 1);
            var product = _productIdAllocator.GetProductId(sequence);
            if (string.IsNullOrWhiteSpace(product.Value) || stagedItems.ContainsKey(product))
                throw new InvalidOperationException("Product allocator returned an invalid or duplicate identity.");
            var slotIndex = 0;
            while (occupied.Contains($"slot-{slotIndex}")) slotIndex = checked(slotIndex + 1);
            var slot = $"slot-{slotIndex}"; occupied.Add(slot);
            stagedItems.Add(product, new ItemState(recipe.ProductDefinition, 1, ItemLocation.Container(target, slot), false, recipe.Id, true, null, AllocationSequence: sequence));
            StageContainerContents(stagedContents, target, product); products.Add(product);
        }
        var remaining = source.RemainingPortions - count;
        if (remaining == 0)
        {
            foreach (var content in ItemsInContainer(sourceId))
            {
                var state = stagedItems[content];
                stagedItems[content] = state with { Removed = true, BoundOrder = null, Version = checked(state.Version + 1) };
            }
            stagedContents[sourceId].Clear();
        }
        stagedItems[sourceId] = source with { Version = checked(source.Version + 1), RemainingPortions = remaining,
            ContainerCompleted = remaining > 0, Recipe = remaining > 0 ? source.Recipe : null };
        _items = stagedItems; _containerItems = stagedContents; _nextProductId = sequence;
        return Commit(command, recipe.Id, null, products[0], count == 1 ? "portion-served" : "batch-poured");
    }

    private CookingRecipeCommandResult ClearContents(CookingRecipeCommand command)
    {
        if (command.Item is not { } id || !TryGetContainerCapability(id, out _)) return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        var source = _items[id];
        if (source.Version != command.ExpectedItemVersion) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (!ItemIsReachable(id, command.Player)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        var contents = ItemsInContainer(id);
        if (IsLockedInput(id) || contents.Any(IsLockedInput)) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (contents.Any(i => TryGetContainerCapability(i, out _))) return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        var staged = new Dictionary<ItemId, ItemState>(_items);
        _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        foreach (var item in contents) staged[item] = staged[item] with { Removed = true, BoundOrder = null, Version = checked(staged[item].Version + 1) };
        staged[id] = source with { Version = checked(source.Version + 1), Recipe = null, ContainerCompleted = false, RemainingPortions = 0 };
        _items = staged; EnsureContainerList(id).Clear();
        return Commit(command, null, null, id, "contents-cleared");
    }

    private CookingRecipeCommandResult DiscardItem(CookingRecipeCommand command)
    {
        if (command.Item is not { } id || !_items.TryGetValue(id, out var item) || item.Removed) return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (item.Version != command.ExpectedItemVersion) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (TryGetContainerCapability(id, out _)) return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        if (IsLockedInput(id)) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (!ItemIsReachable(id, command.Player)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        var version = checked(item.Version + 1); _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        _items[id] = item with { Removed = true, BoundOrder = null, Version = version };
        foreach (var p in _hands.Keys.ToArray()) if (_hands[p] == id) _hands[p] = null;
        foreach (var list in _containerItems.Values) list.Remove(id);
        return Commit(command, null, null, id, "item-discarded");
    }
}
