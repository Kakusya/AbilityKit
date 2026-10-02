using System.Collections.Frozen;

namespace AbilityKit.Game.Cooking;

/// <summary>Historical real seed registration is a source witness, never a grant or refill promise.</summary>
public sealed record CookingManufacturingSeed(ItemId Id, DefinitionId Definition, ItemLocation Location);

/// <summary>A detached view of the actual kitchen configuration and registered physical sources.</summary>
public sealed record CookingManufacturingAvailability(
    CookingScope Scope, IReadOnlyDictionary<DefinitionId, CookingItemDefinition> ItemDefinitions,
    IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes,
    IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances,
    IReadOnlyDictionary<PlayerId, CookingPlayerConfig> Players,
    IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition> OrderTemplates,
    CookingSpatialConfiguration? CurrentSpatial, CookingSupplyConfiguration? SupplyConfiguration,
    CookingRecipeSnapshot Current, IReadOnlyList<CookingManufacturingSeed> RegisteredSeeds,
    IReadOnlyDictionary<DefinitionId, int> CleanContainerSupply,
    IReadOnlyDictionary<DefinitionId, int> CurrentCleanPoolCounts, string CleanPoolLocation)
{
    public CookingManufacturingAvailability Freeze() => this with {
        ItemDefinitions = ItemDefinitions.ToFrozenDictionary(p => p.Key, p => p.Value with {
            AllowedPlayerCapabilities = p.Value.AllowedPlayerCapabilities.ToFrozenSet(StringComparer.Ordinal),
            Container = p.Value.Container is not { } c ? null : c with { AcceptedDefinitions = c.AcceptedDefinitions.ToFrozenSet() } }),
        Recipes = Recipes.ToFrozenDictionary(p => p.Key, p => p.Value with {
            Inputs = Array.AsReadOnly(p.Value.Inputs.ToArray()),
            DefaultInputs = p.Value.DefaultInputs is null ? null : Array.AsReadOnly(p.Value.DefaultInputs.ToArray()) }),
        Appliances = Appliances.ToFrozenDictionary(p => p.Key, p => p.Value with { Capabilities = p.Value.Capabilities.ToFrozenSet(StringComparer.Ordinal) }),
        Players = Players.ToFrozenDictionary(p => p.Key, p => p.Value with {
            Capabilities = p.Value.Capabilities.ToFrozenSet(StringComparer.Ordinal), ReachableStations = p.Value.ReachableStations.ToFrozenSet(StringComparer.Ordinal) }),
        OrderTemplates = OrderTemplates.ToFrozenDictionary(), CurrentSpatial = CurrentSpatial?.Freeze(),
        SupplyConfiguration = SupplyConfiguration is null ? null : new(Array.AsReadOnly(SupplyConfiguration.Suppliers.ToArray())),
        Current = FreezeSnapshot(Current),
        RegisteredSeeds = Array.AsReadOnly(RegisteredSeeds.ToArray()), CleanContainerSupply = CleanContainerSupply.ToFrozenDictionary(),
        CurrentCleanPoolCounts = CurrentCleanPoolCounts.ToFrozenDictionary() };

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    private static CookingRecipeSnapshot FreezeSnapshot(CookingRecipeSnapshot snapshot) => snapshot with {
        Items = Copy(snapshot.Items), Processes = Copy(snapshot.Processes),
        Containers = Copy(snapshot.Containers.Select(c => c with { ItemIds = Copy(c.ItemIds) })),
        AcceptedOrders = Copy(snapshot.AcceptedOrders), Orders = Copy(snapshot.Orders), Settlements = Copy(snapshot.Settlements),
        Poses = Copy(snapshot.Poses ?? Array.Empty<CookingPlayerPose>()),
        Supply = snapshot.Supply is not { } supply ? null : supply with {
            Origins = Copy(supply.Origins.Select(o => o with { Units = Copy(o.Units) })),
            Ledger = supply.Ledger with { Balances = Copy(supply.Ledger.Balances), Requests = Copy(supply.Ledger.Requests), Deliveries = Copy(supply.Ledger.Deliveries) } } };
}

public sealed partial class CookingRecipeSimulation
{
    /// <summary>Reads this owner only; no mutable fixture, allocator or supply state escapes.</summary>
    public CookingManufacturingAvailability DescribeManufacturingAvailability() => new CookingManufacturingAvailability(
        _fixture.Scope, _fixture.Items, _fixture.Recipes, _fixture.Appliances, _fixture.Players, _fixture.OrderTemplates,
        EffectiveSpatial, _supply?.Configuration, Snapshot(),
        _items.Where(p => p.Value.AllocationSequence == 0 && !p.Value.IsProduct)
            .OrderBy(p => p.Key.Value, StringComparer.Ordinal)
            .Select(p => new CookingManufacturingSeed(p.Key, p.Value.Definition, ManufacturingSeedLocation(p.Value.Location))).ToArray(),
        _fixture.CleanContainerSupply, _cleanContainerCount, _fixture.CleanPoolLocation).Freeze();

    private ItemLocation ManufacturingSeedLocation(ItemLocation location)
    {
        var seen = new HashSet<ItemId>();
        while (location.Kind == LocationKind.ContainerSlot) {
            var parent = new ItemId(location.OwnerId ?? "");
            if (!seen.Add(parent) || !_items.TryGetValue(parent, out var item)) break;
            location = item.Location; // Tombstones retain the actual historical source location.
        }
        return location;
    }
}
