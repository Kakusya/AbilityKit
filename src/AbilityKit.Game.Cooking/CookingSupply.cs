using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>External stock is measured in ingredient units, not packages or kitchen inventory.</summary>
public sealed record CookingSupplierDefinition(string SupplierId, string SourceAnchor, string ReceivingAnchor,
    DefinitionId UnitDefinition, DefinitionId PackageDefinition, int UnitsPerPackage, int DeliveryTicks,
    long FiniteAvailable = 0, bool Infinite = false);

public sealed record CookingSupplyConfiguration(IReadOnlyList<CookingSupplierDefinition> Suppliers);
public enum CookingDeliveryPhase { Pending, Arrived, Received }
public enum CookingSupplyRejection
{
    None, InvalidRequest, SupplierMissing, InsufficientSupply, Closing, PayloadConflict,
    DeliveryMissing, NotArrived, AlreadyReceived, InvalidPlan, NotInfinite, InvalidCheckpoint
}

public sealed record CookingSupplyResult(bool Accepted, CookingSupplyRejection Reason,
    string? DeliveryId = null, bool Duplicate = false);
public sealed record CookingSupplyDelivery(string DeliveryId, long Sequence, string RequestId,
    string SupplierId, int PackageCount, CookingDeliveryPhase Phase, int RemainingTicks);
public sealed record CookingSupplyRequestReceipt(string RequestId, string SupplierId, int PackageCount,
    string? DeliveryId, long InfiniteSequence = 0);
public sealed record CookingSupplierBalance(string SupplierId, long AvailableUnits);
public sealed record CookingSupplyCheckpoint(
    [property: System.Text.Json.Serialization.JsonRequired] string ConfigurationIdentity,
    [property: System.Text.Json.Serialization.JsonRequired] long NextDeliverySequence,
    [property: System.Text.Json.Serialization.JsonRequired] long NextInfiniteSequence,
    [property: System.Text.Json.Serialization.JsonRequired] bool Closing,
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<CookingSupplierBalance> Balances,
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<CookingSupplyDelivery> Deliveries,
    [property: System.Text.Json.Serialization.JsonRequired] IReadOnlyList<CookingSupplyRequestReceipt> Requests);

/// <summary>Read-only creation terms. Only the originating state can commit this plan.</summary>
public sealed class CookingSupplyReceivePlan
{
    internal CookingSupplyReceivePlan(object owner, CookingSupplyDelivery delivery, CookingSupplierDefinition supplier)
    { Owner = owner; Delivery = delivery; Supplier = supplier; }
    internal object Owner { get; }
    internal CookingSupplyDelivery Delivery { get; }
    public string DeliveryId => Delivery.DeliveryId;
    public int PackageCount => Delivery.PackageCount;
    public CookingSupplierDefinition Supplier { get; }
    public long UnitCount => (long)PackageCount * Supplier.UnitsPerPackage;
}

/// <summary>No ingredient exists until the kitchen owner atomically creates it and commits this plan.</summary>
public sealed class CookingInfiniteSupplyPlan
{
    internal CookingInfiniteSupplyPlan(object owner, string requestId, CookingSupplierDefinition supplier)
    { Owner = owner; RequestId = requestId; Supplier = supplier; }
    internal object Owner { get; }
    public string RequestId { get; }
    public CookingSupplierDefinition Supplier { get; }
    public int UnitCount => 1;
}

/// <summary>
/// A supply ledger owned by the existing kitchen simulation. It has no timer, item allocator,
/// or consumable kitchen balance. The authority explicitly advances it once per fixed tick.
/// </summary>
public sealed class CookingSupplyState
{
    private readonly Dictionary<string, CookingSupplierDefinition> _suppliers;
    private readonly Dictionary<string, long> _available;
    private readonly Dictionary<string, CookingSupplyDelivery> _deliveries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CookingSupplyRequestReceipt> _requests = new(StringComparer.Ordinal);
    private long _nextDelivery = 1;
    private long _nextInfinite = 1;
    private bool _closing;

    public CookingSupplyState(CookingSupplyConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Suppliers is null) throw new ArgumentException("Supply definitions are required.", nameof(configuration));
        _suppliers = new(StringComparer.Ordinal);
        foreach (var supplier in configuration.Suppliers)
        {
            if (supplier is null || !ValidId(supplier.SupplierId) || !ValidId(supplier.SourceAnchor)
                || !ValidId(supplier.ReceivingAnchor) || !ValidId(supplier.UnitDefinition.Value)
                || !ValidId(supplier.PackageDefinition.Value) || supplier.UnitDefinition == supplier.PackageDefinition
                || supplier.UnitsPerPackage <= 0 || supplier.DeliveryTicks < 0 || supplier.FiniteAvailable < 0
                || (supplier.Infinite && supplier.FiniteAvailable != 0) || !_suppliers.TryAdd(supplier.SupplierId, supplier))
                throw new ArgumentException("Invalid or duplicate supplier definition.", nameof(configuration));
        }
        _available = _suppliers.Values.ToDictionary(x => x.SupplierId, x => x.FiniteAvailable, StringComparer.Ordinal);
        Configuration = new(Array.AsReadOnly(_suppliers.Values.OrderBy(x => x.SupplierId, StringComparer.Ordinal).ToArray()));
        ConfigurationIdentity = Hash(JsonSerializer.Serialize(Configuration));
    }

    public CookingSupplyConfiguration Configuration { get; }
    public string ConfigurationIdentity { get; }
    public bool Closing => _closing;
    public void StopNewRequests() => _closing = true;

    /// <summary>The owner supplies authoritative definitions, compatible package capacities, and actual anchors.</summary>
    public bool ValidateDefinitions(IReadOnlySet<DefinitionId> unitDefinitions,
        IReadOnlyDictionary<DefinitionId, int> compatiblePackageCapacities, IReadOnlySet<string> anchors)
    {
        ArgumentNullException.ThrowIfNull(unitDefinitions);
        ArgumentNullException.ThrowIfNull(compatiblePackageCapacities);
        ArgumentNullException.ThrowIfNull(anchors);
        return _suppliers.Values.All(x => unitDefinitions.Contains(x.UnitDefinition)
            && compatiblePackageCapacities.TryGetValue(x.PackageDefinition, out var capacity) && capacity >= x.UnitsPerPackage
            && anchors.Contains(x.SourceAnchor) && anchors.Contains(x.ReceivingAnchor));
    }

    /// <summary>One request produces exactly one package. Other counts are rejected without allocating a delivery.</summary>
    public CookingSupplyResult Request(string requestId, string supplierId, int packageCount = 1)
    {
        if (!ValidId(requestId) || !ValidId(supplierId) || packageCount != 1)
            return Reject(CookingSupplyRejection.InvalidRequest);
        if (_requests.TryGetValue(requestId, out var previous))
            return previous.SupplierId == supplierId && previous.PackageCount == packageCount && previous.InfiniteSequence == 0
                ? new(true, CookingSupplyRejection.None, previous.DeliveryId, true) : Reject(CookingSupplyRejection.PayloadConflict);
        if (_closing) return Reject(CookingSupplyRejection.Closing);
        if (!_suppliers.TryGetValue(supplierId, out var supplier)) return Reject(CookingSupplyRejection.SupplierMissing);
        if (supplier.Infinite) return Reject(CookingSupplyRejection.InvalidRequest);
        var units = (long)packageCount * supplier.UnitsPerPackage;
        if (_available[supplierId] < units) return Reject(CookingSupplyRejection.InsufficientSupply);
        if (_nextDelivery == long.MaxValue) return Reject(CookingSupplyRejection.InvalidRequest);
        var id = DeliveryId(_nextDelivery);
        var delivery = new CookingSupplyDelivery(id, _nextDelivery, requestId, supplierId, packageCount,
            supplier.DeliveryTicks == 0 ? CookingDeliveryPhase.Arrived : CookingDeliveryPhase.Pending, supplier.DeliveryTicks);
        _available[supplierId] -= units;
        _deliveries.Add(id, delivery);
        _requests.Add(requestId, new(requestId, supplierId, packageCount, id));
        _nextDelivery++;
        return new(true, CookingSupplyRejection.None, id);
    }

    /// <summary>Called by the existing authority, never from transport callbacks or a separate timer.</summary>
    public void AdvanceFixedTick()
    {
        foreach (var delivery in _deliveries.Values.Where(x => x.Phase == CookingDeliveryPhase.Pending).ToArray())
        {
            var remaining = delivery.RemainingTicks - 1;
            _deliveries[delivery.DeliveryId] = delivery with
            { RemainingTicks = remaining, Phase = remaining == 0 ? CookingDeliveryPhase.Arrived : CookingDeliveryPhase.Pending };
        }
    }

    /// <summary>Preview allocates no kitchen items and changes no ledger state; a full slot leaves the delivery arrived.</summary>
    public CookingSupplyResult PreviewReceive(string deliveryId, out CookingSupplyReceivePlan? plan)
    {
        plan = null;
        if (!ValidId(deliveryId) || !_deliveries.TryGetValue(deliveryId, out var delivery))
            return Reject(CookingSupplyRejection.DeliveryMissing);
        if (delivery.Phase == CookingDeliveryPhase.Received) return new(false, CookingSupplyRejection.AlreadyReceived, deliveryId, true);
        if (delivery.Phase != CookingDeliveryPhase.Arrived) return Reject(CookingSupplyRejection.NotArrived);
        plan = new(this, delivery, _suppliers[delivery.SupplierId]);
        return new(true, CookingSupplyRejection.None, deliveryId);
    }

    /// <summary>
    /// The owner must precheck the slot and all instance allocations on its atomic candidate before committing.
    /// Commit only changes external ledger status; it cannot create, clone, or consume kitchen objects.
    /// </summary>
    public CookingSupplyResult CommitReceive(CookingSupplyReceivePlan? plan)
    {
        if (plan is null || !ReferenceEquals(plan.Owner, this)) return Reject(CookingSupplyRejection.InvalidPlan);
        if (!_deliveries.TryGetValue(plan.DeliveryId, out var delivery)) return Reject(CookingSupplyRejection.InvalidPlan);
        if (delivery.Phase == CookingDeliveryPhase.Received) return new(false, CookingSupplyRejection.AlreadyReceived, delivery.DeliveryId, true);
        if (delivery != plan.Delivery || delivery.Phase != CookingDeliveryPhase.Arrived) return Reject(CookingSupplyRejection.InvalidPlan);
        _deliveries[delivery.DeliveryId] = delivery with { Phase = CookingDeliveryPhase.Received };
        return new(true, CookingSupplyRejection.None, delivery.DeliveryId);
    }

    public CookingSupplyResult PreviewInfiniteTake(string requestId, string supplierId, out CookingInfiniteSupplyPlan? plan)
    {
        plan = null;
        if (!ValidId(requestId) || !ValidId(supplierId)) return Reject(CookingSupplyRejection.InvalidRequest);
        if (_requests.TryGetValue(requestId, out var receipt))
            return receipt.SupplierId == supplierId && receipt.InfiniteSequence > 0
                ? new(true, CookingSupplyRejection.None, Duplicate: true) : Reject(CookingSupplyRejection.PayloadConflict);
        if (!_suppliers.TryGetValue(supplierId, out var supplier)) return Reject(CookingSupplyRejection.SupplierMissing);
        if (!supplier.Infinite) return Reject(CookingSupplyRejection.NotInfinite);
        if (_nextInfinite == long.MaxValue) return Reject(CookingSupplyRejection.InvalidRequest);
        plan = new(this, requestId, supplier);
        return new(true, CookingSupplyRejection.None);
    }

    public CookingSupplyResult CommitInfiniteTake(CookingInfiniteSupplyPlan? plan)
    {
        if (plan is null || !ReferenceEquals(plan.Owner, this)) return Reject(CookingSupplyRejection.InvalidPlan);
        var result = PreviewInfiniteTake(plan.RequestId, plan.Supplier.SupplierId, out _);
        if (!result.Accepted || result.Duplicate) return result;
        _requests.Add(plan.RequestId, new(plan.RequestId, plan.Supplier.SupplierId, 0, null, _nextInfinite++));
        return result;
    }

    public CookingSupplyCheckpoint ExportCheckpoint() => new(ConfigurationIdentity, _nextDelivery, _nextInfinite, _closing,
        Array.AsReadOnly(_available.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new CookingSupplierBalance(x.Key, x.Value)).ToArray()),
        Array.AsReadOnly(_deliveries.Values.OrderBy(x => x.Sequence).ToArray()),
        Array.AsReadOnly(_requests.Values.OrderBy(x => x.RequestId, StringComparer.Ordinal).ToArray()));

    public string CanonicalText() => JsonSerializer.Serialize(ExportCheckpoint());
    public string Sha256() => Hash(CanonicalText());

    /// <summary>Builds a new validated state. Invalid input never replaces or mutates a current authority.</summary>
    public static bool TryRestore(CookingSupplyConfiguration configuration, CookingSupplyCheckpoint? checkpoint,
        out CookingSupplyState? restored)
    {
        restored = null;
        CookingSupplyState candidate;
        try { candidate = new(configuration); }
        catch (ArgumentException) { return false; }
        if (checkpoint is null || checkpoint.ConfigurationIdentity != candidate.ConfigurationIdentity
            || checkpoint.NextDeliverySequence <= 0 || checkpoint.NextInfiniteSequence <= 0
            || checkpoint.Balances is null || checkpoint.Deliveries is null || checkpoint.Requests is null) return false;
        if (checkpoint.Balances.Count != candidate._suppliers.Count) return false;
        var balances = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var balance in checkpoint.Balances)
            if (balance is null || !ValidId(balance.SupplierId) || balance.AvailableUnits < 0
                || !candidate._suppliers.ContainsKey(balance.SupplierId) || !balances.TryAdd(balance.SupplierId, balance.AvailableUnits)) return false;
        foreach (var request in checkpoint.Requests)
            if (request is null || !ValidId(request.RequestId) || !ValidId(request.SupplierId)
                || !candidate._suppliers.TryGetValue(request.SupplierId, out var supplier)
                || !candidate._requests.TryAdd(request.RequestId, request)
                || (supplier.Infinite ? request.PackageCount != 0 || request.DeliveryId is not null || request.InfiniteSequence <= 0
                    : request.PackageCount != 1 || !ValidId(request.DeliveryId) || request.InfiniteSequence != 0)) return false;
        var reserved = candidate._suppliers.Keys.ToDictionary(x => x, _ => 0L, StringComparer.Ordinal);
        foreach (var delivery in checkpoint.Deliveries)
        {
            if (delivery is null || delivery.Sequence <= 0 || delivery.Sequence >= checkpoint.NextDeliverySequence
                || delivery.DeliveryId != DeliveryId(delivery.Sequence) || !ValidId(delivery.SupplierId)
                || !candidate._suppliers.TryGetValue(delivery.SupplierId, out var supplier) || supplier.Infinite
                || delivery.PackageCount != 1 || !Enum.IsDefined(delivery.Phase)
                || delivery.RemainingTicks < 0 || delivery.RemainingTicks > supplier.DeliveryTicks
                || (delivery.Phase == CookingDeliveryPhase.Pending ? delivery.RemainingTicks == 0 : delivery.RemainingTicks != 0)
                || !ValidId(delivery.RequestId) || !candidate._requests.TryGetValue(delivery.RequestId, out var request)
                || request.DeliveryId != delivery.DeliveryId || request.SupplierId != delivery.SupplierId
                || request.PackageCount != delivery.PackageCount || request.InfiniteSequence != 0
                || !candidate._deliveries.TryAdd(delivery.DeliveryId, delivery)) return false;
            var units = (long)delivery.PackageCount * supplier.UnitsPerPackage;
            if (reserved[delivery.SupplierId] > long.MaxValue - units) return false;
            reserved[delivery.SupplierId] += units;
        }
        if (candidate._deliveries.Count != checkpoint.NextDeliverySequence - 1) return false;
        var infiniteSequences = new HashSet<long>();
        foreach (var request in candidate._requests.Values)
        {
            if (request.InfiniteSequence > 0)
            {
                if (request.InfiniteSequence >= checkpoint.NextInfiniteSequence || !infiniteSequences.Add(request.InfiniteSequence)) return false;
            }
            else if (!candidate._deliveries.TryGetValue(request.DeliveryId!, out var delivery)
                || delivery.RequestId != request.RequestId) return false;
        }
        if (infiniteSequences.Count != checkpoint.NextInfiniteSequence - 1) return false;
        foreach (var supplier in candidate._suppliers.Values)
        {
            var available = balances[supplier.SupplierId];
            if (supplier.Infinite ? available != 0 || reserved[supplier.SupplierId] != 0
                : available > supplier.FiniteAvailable || reserved[supplier.SupplierId] != supplier.FiniteAvailable - available) return false;
            candidate._available[supplier.SupplierId] = available;
        }
        candidate._nextDelivery = checkpoint.NextDeliverySequence;
        candidate._nextInfinite = checkpoint.NextInfiniteSequence;
        candidate._closing = checkpoint.Closing;
        restored = candidate;
        return true;
    }

    private static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value);
    private static string DeliveryId(long sequence) => "delivery:" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static CookingSupplyResult Reject(CookingSupplyRejection reason) => new(false, reason);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
