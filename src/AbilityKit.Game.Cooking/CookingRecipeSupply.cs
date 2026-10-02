using System.Text.Json.Serialization;

namespace AbilityKit.Game.Cooking;

public sealed record CookingSupplyItemProvenance([property: JsonRequired] string RequestId,
    [property: JsonRequired] string SupplierId, [property: JsonRequired] string? DeliveryId, [property: JsonRequired] int UnitIndex);
public sealed record CookingSupplyOrigin([property: JsonRequired] string RequestId, [property: JsonRequired] string SupplierId,
    [property: JsonRequired] string? DeliveryId, [property: JsonRequired] ItemId? Package, [property: JsonRequired] IReadOnlyList<ItemId> Units);
public sealed record CookingSupplyPhysicalResult([property: JsonRequired] string? DeliveryId,
    [property: JsonRequired] ItemId? Package, [property: JsonRequired] IReadOnlyList<ItemId> Units);
public sealed record CookingSupplySnapshot(CookingSupplyCheckpoint Ledger, IReadOnlyList<CookingSupplyOrigin> Origins);

internal static class CookingSupplyIntegration
{
    internal static CookingSupplyConfiguration? ValidateAndFreeze(CookingSupplyConfiguration? configuration,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items, CookingSpatialConfiguration? spatial)
    {
        if (configuration is null) return null;
        var frozen = new CookingSupplyState(configuration).Configuration;
        if (spatial is null) throw new ArgumentException("Physical supply requires actual spatial anchors.");
        foreach (var s in frozen.Suppliers)
            if (!items.TryGetValue(s.UnitDefinition, out var unit) || unit.Container is not null ||
                !items.TryGetValue(s.PackageDefinition, out var package) || package.Container is not { } container ||
                container.Capacity < s.UnitsPerPackage || !container.AcceptedDefinitions.Contains(s.UnitDefinition) ||
                !spatial.Anchors.Any(a => a.Kind == LocationKind.WorldPosition && a.Id == s.SourceAnchor) ||
                !spatial.Anchors.Any(a => a.Kind == LocationKind.WorldPosition && a.Id == s.ReceivingAnchor))
                throw new ArgumentException("Supply requires raw units, compatible packages and actual world anchors.");
        return frozen;
    }
}

public sealed partial class CookingRecipeSimulation
{
    private CookingSupplyState? _supply;
    private Dictionary<string, CookingSupplyOrigin> _supplyOrigins = new(StringComparer.Ordinal);

    public CookingSupplySnapshot? SupplySnapshot() => _supply is null ? null : new(_supply.ExportCheckpoint(),
        Array.AsReadOnly(_supplyOrigins.Values.OrderBy(o => o.RequestId, StringComparer.Ordinal)
            .Select(o => o with { Units = Array.AsReadOnly(o.Units.ToArray()) }).ToArray()));

    /// <summary>Level/front-house owner closes only new reservations; approved deliveries still advance.</summary>
    public void StopNewSupplyRequests()
    {
        EnsureGameplayMutationOpen();
        if (_supply is null || _supply.Closing) return;
        if (_stateVersion == long.MaxValue) throw new OverflowException();
        _supply.StopNewRequests();
        _stateVersion++;
    }

    private CookingSupplyState? CloneSupply()
    {
        if (_supply is null) return null;
        if (!CookingSupplyState.TryRestore(_supply.Configuration, _supply.ExportCheckpoint(), out var copy))
            throw new InvalidOperationException("Invalid authority supply state.");
        return copy;
    }

    private static CookingSupplyPhysicalResult Physical(CookingSupplyOrigin origin) =>
        new(origin.DeliveryId, origin.Package, Array.AsReadOnly(origin.Units.ToArray()));

    private CookingRecipeCommandResult ExecuteSupply(CookingRecipeCommand command)
    {
        var candidate = CloneSupply();
        if (candidate is null) return Reject(CookingRecipeRejectionReason.SupplyRejected);
        CookingSupplierDefinition? supplier;
        CookingSupplyReceivePlan? receive = null;
        CookingInfiniteSupplyPlan? take = null;
        string request;
        if (command.Operation == CookingRecipeOperation.ReceiveSupply)
        {
            var delivery = candidate.ExportCheckpoint().Deliveries.FirstOrDefault(d => d.DeliveryId == command.DeliveryId);
            if (delivery is null) return Reject(CookingRecipeRejectionReason.SupplyRejected);
            request = delivery.RequestId;
            supplier = candidate.Configuration.Suppliers.Single(s => s.SupplierId == delivery.SupplierId);
            if (delivery.Phase == CookingDeliveryPhase.Received)
                return new(CookingRecipeOutcome.Accepted, CookingRecipeRejectionReason.None, _stateVersion, true,
                    Array.Empty<CookingRecipeEvent>()) { Supply = Physical(_supplyOrigins[request]) };
            if (!candidate.PreviewReceive(delivery.DeliveryId, out receive).Accepted)
                return Reject(CookingRecipeRejectionReason.SupplyRejected);
        }
        else
        {
            request = command.SupplyRequestId!;
            supplier = candidate.Configuration.Suppliers.FirstOrDefault(s => s.SupplierId == command.SupplierId);
            if (supplier is null) return Reject(CookingRecipeRejectionReason.SupplyRejected);
        }
        var anchor = command.Operation == CookingRecipeOperation.ReceiveSupply ? supplier.ReceivingAnchor : supplier.SourceAnchor;
        if (!LocationIsReachable(ItemLocation.World(anchor), command.Player)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        var player = _fixture.Players[command.Player];
        var definition = command.Operation == CookingRecipeOperation.ReceiveSupply ? supplier.PackageDefinition : supplier.UnitDefinition;
        if (!_fixture.Items[definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities) ||
            !_fixture.Items[supplier.UnitDefinition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);
        if (_stateVersion == long.MaxValue || _eventSequence == long.MaxValue) return Reject(CookingRecipeRejectionReason.SupplyAllocationFailed);
        if (command.Operation == CookingRecipeOperation.RequestSupply)
        {
            var result = candidate.Request(request, supplier.SupplierId);
            if (!result.Accepted) return Reject(CookingRecipeRejectionReason.SupplyRejected);
            if (result.Duplicate) return new(CookingRecipeOutcome.Accepted, CookingRecipeRejectionReason.None, _stateVersion, true,
                Array.Empty<CookingRecipeEvent>()) { Supply = new(result.DeliveryId, null, Array.Empty<ItemId>()) };
            _supply = candidate;
            return Commit(command, null, null, null, "supply-requested") with { Supply = new(result.DeliveryId, null, Array.Empty<ItemId>()) };
        }
        if (command.Operation == CookingRecipeOperation.TakeSupply)
        {
            var result = candidate.PreviewInfiniteTake(request, supplier.SupplierId, out take);
            if (!result.Accepted) return Reject(CookingRecipeRejectionReason.SupplyRejected);
            if (result.Duplicate) return new(CookingRecipeOutcome.Accepted, CookingRecipeRejectionReason.None, _stateVersion, true,
                Array.Empty<CookingRecipeEvent>()) { Supply = Physical(_supplyOrigins[request]) };
            if (_hands[command.Player] is not null) return Reject(CookingRecipeRejectionReason.IngredientAlreadyCollected);
        }
        else if (_items.Values.Any(i => !i.Removed && i.Location == ItemLocation.World(anchor)))
            return Reject(CookingRecipeRejectionReason.ContainerFull);

        // Allocate and index a complete replacement before touching any live ledger or watermark.
        var items = new Dictionary<ItemId, ItemState>(_items);
        var containers = _containerItems.ToDictionary(p => p.Key, p => new List<ItemId>(p.Value));
        var next = _nextProductId;
        ItemId Allocate(DefinitionId def, ItemLocation location, int unitIndex)
        {
            var sequence = checked(next + 1);
            next = sequence;
            var id = _productIdAllocator.GetProductId(sequence);
            if (string.IsNullOrWhiteSpace(id.Value) || items.ContainsKey(id)) throw new InvalidOperationException("Supply allocator identity collision.");
            items.Add(id, new(def, 1, location, false, null, false, null,
                SupplyProvenance: new(request, supplier.SupplierId, receive?.DeliveryId, unitIndex)));
            return id;
        }
        ItemId? package = null;
        ItemId[] units;
        try
        {
            if (receive is not null)
            {
                package = Allocate(supplier.PackageDefinition, ItemLocation.World(anchor), -1);
                units = new ItemId[supplier.UnitsPerPackage];
                for (var n = 0; n < units.Length; n++) units[n] = Allocate(supplier.UnitDefinition, ItemLocation.Container(package.Value, $"slot-{n}"), n);
                containers.Add(package.Value, units.ToList());
                if (!candidate.CommitReceive(receive).Accepted) return Reject(CookingRecipeRejectionReason.SupplyRejected);
            }
            else
            {
                units = new[] { Allocate(supplier.UnitDefinition, ItemLocation.Hand(command.Player), 0) };
                if (!candidate.CommitInfiniteTake(take).Accepted) return Reject(CookingRecipeRejectionReason.SupplyRejected);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return Reject(CookingRecipeRejectionReason.SupplyAllocationFailed); }
        var origin = new CookingSupplyOrigin(request, supplier.SupplierId, receive?.DeliveryId, package, Array.AsReadOnly(units));
        var origins = new Dictionary<string, CookingSupplyOrigin>(_supplyOrigins, StringComparer.Ordinal) { [request] = origin };
        _items = items;
        _containerItems = containers;
        _supplyOrigins = origins;
        _supply = candidate;
        _nextProductId = next;
        if (take is not null) _hands[command.Player] = units[0];
        return Commit(command, null, null, package ?? units[0], "supply-materialized") with { Supply = Physical(origin) };
    }

    private CookingCheckpointRestoreReason ValidateSupplyCheckpoint(CookingRecipeCheckpoint checkpoint)
    {
        if (checkpoint.SupplyOrigins is null || checkpoint.Items is null || checkpoint.Items.Any(i => i is null) || checkpoint.Deduplication is null)
            return CookingCheckpointRestoreReason.SupplyStateInvalid;
        if (_supply is null) return checkpoint.Supply is null && checkpoint.SupplyOrigins.Count == 0 && checkpoint.Items.All(i => i.SupplyProvenance is null)
            ? CookingCheckpointRestoreReason.None : CookingCheckpointRestoreReason.SupplyStateInvalid;
        if (!CookingSupplyState.TryRestore(_supply.Configuration, checkpoint.Supply, out var supply))
            return CookingCheckpointRestoreReason.SupplyStateInvalid;
        var ledger = supply!.ExportCheckpoint();
        var identities = new HashSet<ItemId>();
        var requests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var origin in checkpoint.SupplyOrigins)
        {
            if (origin is null || string.IsNullOrWhiteSpace(origin.RequestId) || !requests.Add(origin.RequestId) || origin.Units is null)
                return CookingCheckpointRestoreReason.SupplyStateInvalid;
            var receipt = ledger.Requests.FirstOrDefault(r => r.RequestId == origin.RequestId);
            if (receipt is null || receipt.SupplierId != origin.SupplierId || receipt.DeliveryId != origin.DeliveryId)
                return CookingCheckpointRestoreReason.SupplyStateInvalid;
            var supplier = supply.Configuration.Suppliers.Single(s => s.SupplierId == origin.SupplierId);
            if (supplier.Infinite ? origin.Package is not null || origin.Units.Count != 1 :
                origin.Package is null || origin.Units.Count != supplier.UnitsPerPackage ||
                !ledger.Deliveries.Any(d => d.DeliveryId == origin.DeliveryId && d.Phase == CookingDeliveryPhase.Received))
                return CookingCheckpointRestoreReason.SupplyStateInvalid;
            bool Valid(ItemId id, DefinitionId def, int index) => identities.Add(id) && checkpoint.Items.Count(i => i.Id == id && i.Definition == def && !i.IsProduct &&
                i.SupplyProvenance == new CookingSupplyItemProvenance(origin.RequestId, origin.SupplierId, origin.DeliveryId, index)) == 1;
            if (origin.Package is { } package && !Valid(package, supplier.PackageDefinition, -1) || origin.Units.Where((id, index) => !Valid(id, supplier.UnitDefinition, index)).Any())
                return CookingCheckpointRestoreReason.SupplyStateInvalid;
        }
        if (ledger.Requests.Any(r => (r.InfiniteSequence > 0 || ledger.Deliveries.Any(d => d.DeliveryId == r.DeliveryId && d.Phase == CookingDeliveryPhase.Received)) != requests.Contains(r.RequestId)))
            return CookingCheckpointRestoreReason.SupplyStateInvalid;
        if (checkpoint.Items.Any(i => i.SupplyProvenance is not null && !identities.Contains(i.Id)))
            return CookingCheckpointRestoreReason.SupplyStateInvalid;
        foreach (var entry in checkpoint.Deduplication)
        {
            if (entry is null) return CookingCheckpointRestoreReason.SupplyStateInvalid;
            CookingRecipeCommand? command;
            try { command = System.Text.Json.JsonSerializer.Deserialize<CookingRecipeCommand>(entry.Fingerprint, CanonicalJsonOptions); }
            catch (System.Text.Json.JsonException) { return CookingCheckpointRestoreReason.DeduplicationEntryInvalid; }
            var supplyCommand = command?.Operation is CookingRecipeOperation.RequestSupply or CookingRecipeOperation.ReceiveSupply or CookingRecipeOperation.TakeSupply;
            if (entry.Supply is not { } result)
            {
                if (supplyCommand && entry.Outcome == CookingRecipeOutcome.Accepted) return CookingCheckpointRestoreReason.SupplyStateInvalid;
                continue;
            }
            if (!supplyCommand) return CookingCheckpointRestoreReason.SupplyStateInvalid;
            if (result.Units is null || entry.Outcome != CookingRecipeOutcome.Accepted) return CookingCheckpointRestoreReason.SupplyStateInvalid;
            if (command!.Operation == CookingRecipeOperation.RequestSupply)
            {
                if (result.Package is not null || result.Units.Count != 0 || !ledger.Deliveries.Any(d => d.DeliveryId == result.DeliveryId &&
                    d.RequestId == command.SupplyRequestId && d.SupplierId == command.SupplierId)) return CookingCheckpointRestoreReason.SupplyStateInvalid;
            }
            else if (!checkpoint.SupplyOrigins.Any(o => o.DeliveryId == result.DeliveryId && o.Package == result.Package && o.Units.SequenceEqual(result.Units) &&
                (command.Operation == CookingRecipeOperation.ReceiveSupply ? o.DeliveryId == command.DeliveryId && o.DeliveryId is not null :
                    o.DeliveryId is null && o.RequestId == command.SupplyRequestId && o.SupplierId == command.SupplierId)))
                return CookingCheckpointRestoreReason.SupplyStateInvalid;
        }
        return CookingCheckpointRestoreReason.None;
    }

    private void InstallSupplyCheckpoint(CookingRecipeCheckpoint checkpoint)
    {
        if (_supply is not null)
        {
            CookingSupplyState.TryRestore(_supply.Configuration, checkpoint.Supply, out var restored);
            _supply = restored;
        }
        _supplyOrigins = checkpoint.SupplyOrigins!.ToDictionary(o => o.RequestId,
            o => o with { Units = Array.AsReadOnly(o.Units.ToArray()) }, StringComparer.Ordinal);
    }
}
