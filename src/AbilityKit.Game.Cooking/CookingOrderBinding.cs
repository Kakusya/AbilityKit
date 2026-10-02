namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    private CookingRecipeCommandResult MutateBinding(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var reason)) return Reject(reason);
        if (command.Item is not { } id || !_items.TryGetValue(id, out var product) || product.Removed || !product.IsProduct)
            return Reject(CookingRecipeRejectionReason.ProductNotFound);
        if (product.Version != command.ExpectedItemVersion) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (IsLockedInput(id) || IsActiveProcessAnchor(id)) return Reject(CookingRecipeRejectionReason.ItemStale);
        if (!_fixture.Items[product.Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);
        var reachable = product.Location.Kind == LocationKind.ContainerSlot && product.Location.OwnerId is { } vesselOwner
            ? ContainerIsReachable(new ItemId(vesselOwner), player) : ItemIsReachable(id, command.Player);
        if (!reachable) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (product.Location.Kind == LocationKind.ContainerSlot && product.Location.OwnerId is { } servingOwner &&
            !_fixture.Items[_items[new ItemId(servingOwner)].Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);
        if (command.Operation == CookingRecipeOperation.UnbindOrder)
        {
            if (product.BoundOrder is null) return Reject(CookingRecipeRejectionReason.BindingNotFound);
            if (command.Order is { } expected && product.BoundOrder != expected) return Reject(CookingRecipeRejectionReason.BindingConflict);
        }
        else
        {
            if (command.Operation == CookingRecipeOperation.BindOrder && product.BoundOrder is not null)
                return Reject(CookingRecipeRejectionReason.BindingConflict);
            if (command.Operation == CookingRecipeOperation.RebindOrder && product.BoundOrder is null)
                return Reject(CookingRecipeRejectionReason.BindingNotFound);
            if (command.Order is not { } target || !_orders.TryGetValue(target, out var order))
                return Reject(CookingRecipeRejectionReason.OrderNotFound);
            if (order.Status != CookingOrderStatus.Open) return Reject(CookingRecipeRejectionReason.OrderAlreadyCompleted);
            if (product.Recipe != order.RequiredRecipe) return Reject(CookingRecipeRejectionReason.OrderRequirementMismatch);
            if (product.Location.Kind != LocationKind.ContainerSlot || product.Location.OwnerId is not { } owner)
                return Reject(CookingRecipeRejectionReason.ProductNotPlated);
            var vessel = new ItemId(owner);
            if (IsLockedInput(vessel) || IsActiveProcessAnchor(vessel)) return Reject(CookingRecipeRejectionReason.ItemStale);
            if (!TryGetContainerCapability(vessel, out var capability) || !capability.AcceptedDefinitions.Contains(product.Definition) || _items[vessel].Definition != order.RequiredContainerDefinition ||
                _items[vessel].ContainerCompleted || IsWorkingCarrier(_items[vessel].Definition) || ItemsInContainer(vessel).Count != 1)
                return Reject(CookingRecipeRejectionReason.OrderRequirementMismatch);
            if (!_fixture.Items[_items[vessel].Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
                return Reject(CookingRecipeRejectionReason.PlayerIneligible);
            if (_items[vessel].IsDirty) return Reject(CookingRecipeRejectionReason.OrderRequirementMismatch);
            if (_items.Any(p => p.Key != id && p.Value.BoundOrder == target))
                return Reject(CookingRecipeRejectionReason.BindingConflict);
        }
        var version = checked(product.Version + 1);
        _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        _items[id] = product with { BoundOrder = command.Operation == CookingRecipeOperation.UnbindOrder ? null : command.Order, Version = version };
        return Commit(command, product.Recipe, null, id, "order-binding-mutated");
    }

    private bool IsWorkingCarrier(DefinitionId definition) =>
        _fixture.Recipes.Values.Any(r => r.RequiredProcessingContainerDefinition == definition);

    private void ClearOrderBinding(OrderId order)
    {
        foreach (var pair in _items.Where(p => p.Value.BoundOrder == order).ToArray())
            _items[pair.Key] = pair.Value with { BoundOrder = null, Version = checked(pair.Value.Version + 1) };
    }

    private CookingCheckpointRestoreReason ValidateCheckpointBindings(CookingRecipeCheckpoint checkpoint)
    {
        if (checkpoint.Items is null || checkpoint.Orders is null || checkpoint.Containers is null || checkpoint.Processes is null)
            return CookingCheckpointRestoreReason.BindingStateInvalid;
        var claimed = new HashSet<OrderId>();
        foreach (var item in checkpoint.Items)
        {
            if (item.BoundOrder is not { } bound) continue;
            var order = checkpoint.Orders.FirstOrDefault(o => o.Id == bound);
            if (item.Removed || !item.IsProduct || !claimed.Add(bound) || order is null || order.Status != CookingOrderStatus.Open ||
                item.Recipe != order.RequiredRecipe || item.Location.Kind != LocationKind.ContainerSlot)
                return CookingCheckpointRestoreReason.BindingStateInvalid;
            var vessel = checkpoint.Items.FirstOrDefault(i => i.Id.Value == item.Location.OwnerId);
            if (vessel is null || vessel.Removed || vessel.IsDirty || vessel.ContainerCompleted || IsWorkingCarrier(vessel.Definition) || vessel.Definition != order.RequiredContainerDefinition ||
                checkpoint.Items.Count(i => !i.Removed && i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == vessel.Id.Value) != 1)
                return CookingCheckpointRestoreReason.BindingStateInvalid;
            if (!_fixture.Items.TryGetValue(vessel.Definition, out var definition) || definition.Container is not { } capability ||
                capability.Capacity < 1 || !capability.AcceptedDefinitions.Contains(item.Definition))
                return CookingCheckpointRestoreReason.BindingStateInvalid;
            var owners = checkpoint.Containers.Where(c => c.ItemIds is not null && c.ItemIds.Contains(item.Id)).ToArray();
            if (owners.Length != 1 || owners[0].Id != vessel.Id || owners[0].ItemIds.Count(i => i == item.Id) != 1 ||
                checkpoint.Containers.Count(c => c.Id == vessel.Id) != 1)
                return CookingCheckpointRestoreReason.BindingStateInvalid;
            if (checkpoint.Processes.Any(p => p.LockedInputs is null || p.Anchor == item.Id || p.Anchor == vessel.Id ||
                p.LockedInputs.Contains(item.Id) || p.LockedInputs.Contains(vessel.Id)))
                return CookingCheckpointRestoreReason.BindingStateInvalid;
        }
        return CookingCheckpointRestoreReason.None;
    }
}
