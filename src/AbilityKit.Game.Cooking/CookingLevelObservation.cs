using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public sealed record CookingObservedPlayer(PlayerId Id, CookingPlayerPose? Pose, ItemId? HeldItem);
public sealed record CookingObservedItem(ItemId Id, string DefinitionKey, string? RecipeKey,
    ItemLocation Location, bool IsProduct, bool ContainerCompleted, bool IsDirty, int RemainingPortions, OrderId? BoundOrder,
    IReadOnlyList<ItemId> Contents);
public sealed record CookingObservedInventory(string DefinitionKey, int LiveObjects, int ContainerObjects,
    int RemainingPreparedPortions, int ProductObjects, int SupplyLiveUnits,
    int SupplyHandUnits, int SupplyUnitsInTrackedPackages);

/// <summary>A frozen, scoped read of already committed owner snapshots. It owns no gameplay state.</summary>
public sealed record CookingLevelObservation(CookingLevelScope Scope, CookingLevelLifecycleSnapshot Lifecycle,
    long HostFrameSequence, CookingRecipeSnapshot? Recipe, CookingFrontOfHouseSnapshot? Front,
    CookingRestaurantLayout? InstalledLayout, string? PreparationPolicyIdentity, string? GeometryIdentity,
    IReadOnlyList<CookingObservedPlayer> Players, IReadOnlyList<CookingObservedItem> Items,
    IReadOnlyList<CookingObservedInventory> Inventory, int UnavailableOriginalSupplyUnits)
{
    public string CanonicalText() => JsonSerializer.Serialize(this, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}

public static class CookingLevelObservationProjector
{
    public static CookingLevelObservation Project(CookingLevelScope scope, CookingLevelLifecycleSnapshot lifecycle,
        long hostFrameSequence, CookingRecipeSnapshot? recipe, CookingFrontOfHouseSnapshot? front = null,
        CookingRestaurantLayout? installedLayout = null, string? preparationPolicyIdentity = null,
        string? geometryIdentity = null, IReadOnlyList<PlayerId>? configuredPlayers = null)
    {
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(lifecycle);
        if (scope != lifecycle.Scope || hostFrameSequence < 0 || lifecycle.Version < 0
            || (recipe is not null && (recipe.Scope != scope.MatchScope || recipe.Version < 0 || recipe.LogicalTick < 0))
            || (preparationPolicyIdentity is not null && string.IsNullOrWhiteSpace(preparationPolicyIdentity))
            || (geometryIdentity is not null && string.IsNullOrWhiteSpace(geometryIdentity)))
            throw new ArgumentException("Observation scope or watermark is inconsistent.");
        var frozenRecipe = recipe is null ? null : FreezeRecipe(recipe);
        var frozenFront = front is null ? null : FreezeFront(front);
        var frozenLayout = installedLayout is null ? null : installedLayout with {
            Floors = ReadOnly(installedLayout.Floors.OrderBy(x => x.Id, StringComparer.Ordinal)),
            Equipment = ReadOnly(installedLayout.Equipment.OrderBy(x => x.Station.Value, StringComparer.Ordinal)),
            Walls = ReadOnly(installedLayout.Walls.OrderBy(x => x.X).ThenBy(x => x.Y)),
            Targets = ReadOnly(installedLayout.Targets.OrderBy(x => x.Id, StringComparer.Ordinal)) };
        var sourceItems = frozenRecipe?.Items ?? Array.Empty<CookingRecipeSnapshotItem>();
        var itemMap = sourceItems.ToDictionary(i => i.Id);
        var containers = (frozenRecipe?.Containers ?? Array.Empty<CookingRecipeSnapshotContainer>()).ToDictionary(c => c.Id);
        if (containers.Values.Any(c => c.ItemIds.Any(id => !itemMap.ContainsKey(id))))
            throw new ArgumentException("Observation container refers to an unavailable live item.");
        var items = ReadOnly(sourceItems.Select(i => new CookingObservedItem(i.Id, i.Definition.Value, i.Recipe?.Value,
            i.Location, i.IsProduct, i.ContainerCompleted, i.IsDirty, i.RemainingPortions, i.BoundOrder,
            containers.TryGetValue(i.Id, out var container) ? container.ItemIds : Array.Empty<ItemId>())));
        var poses = (frozenRecipe?.Poses ?? Array.Empty<CookingPlayerPose>()).ToDictionary(p => p.Player);
        var players = (configuredPlayers ?? Array.Empty<PlayerId>()).Concat(poses.Keys)
            .Concat(sourceItems.Where(i => i.Location.Kind == LocationKind.PlayerHand).Select(i => new PlayerId(i.Location.OwnerId!)))
            .Concat((frozenRecipe?.Processes ?? Array.Empty<CookingRecipeSnapshotProcess>()).Where(p => p.ActiveWorker is not null).Select(p => p.ActiveWorker!.Value))
            .Concat((frozenFront?.Work ?? Array.Empty<CookingFrontWorkSnapshot>()).Where(w => w.Player is not null).Select(w => w.Player!.Value))
            .Distinct().OrderBy(p => p.Value, StringComparer.Ordinal).ToArray();
        if (players.Any(p => string.IsNullOrWhiteSpace(p.Value))) throw new ArgumentException("Player identity is invalid.");
        var observedPlayers = ReadOnly(players.Select(p => {
            var held = sourceItems.Where(i => i.Location.Kind == LocationKind.PlayerHand && i.Location.OwnerId == p.Value).ToArray();
            if (held.Length > 1) throw new ArgumentException("A player cannot hold multiple objects.");
            return new CookingObservedPlayer(p, poses.GetValueOrDefault(p), held.Length == 0 ? null : held[0].Id);
        }));
        var origins = frozenRecipe?.Supply?.Origins ?? Array.Empty<CookingSupplyOrigin>();
        var originalUnits = origins.SelectMany(o => o.Units).ToHashSet();
        var packages = origins.Where(o => o.Package is not null).Select(o => o.Package!.Value.Value).ToHashSet(StringComparer.Ordinal);
        var inventory = ReadOnly(sourceItems.GroupBy(i => i.Definition.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new CookingObservedInventory(g.Key, g.Count(), g.Count(i => containers.ContainsKey(i.Id)),
                g.Sum(i => i.RemainingPortions), g.Count(i => i.IsProduct),
                g.Count(i => originalUnits.Contains(i.Id)), g.Count(i => originalUnits.Contains(i.Id) && i.Location.Kind == LocationKind.PlayerHand),
                g.Count(i => originalUnits.Contains(i.Id) && i.Location.Kind == LocationKind.ContainerSlot && packages.Contains(i.Location.OwnerId!)))));
        return new(scope, lifecycle with { }, hostFrameSequence, frozenRecipe, frozenFront, frozenLayout, preparationPolicyIdentity,
            geometryIdentity, observedPlayers, items, inventory, originalUnits.Count(id => !itemMap.ContainsKey(id)));
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> source) => Array.AsReadOnly(source.ToArray());
    private static CookingRecipeSnapshot FreezeRecipe(CookingRecipeSnapshot source) => source with {
        Items = ReadOnly(source.Items.OrderBy(i => i.Id.Value, StringComparer.Ordinal)),
        Processes = ReadOnly(source.Processes.OrderBy(p => p.Id.Value, StringComparer.Ordinal)),
        Containers = ReadOnly(source.Containers.OrderBy(c => c.Id.Value, StringComparer.Ordinal).Select(c => c with { ItemIds = ReadOnly(c.ItemIds) })),
        AcceptedOrders = ReadOnly(source.AcceptedOrders), Orders = ReadOnly(source.Orders.OrderBy(o => o.Id.Value, StringComparer.Ordinal)),
        Settlements = ReadOnly(source.Settlements), Poses = ReadOnly((source.Poses ?? Array.Empty<CookingPlayerPose>()).OrderBy(p => p.Player.Value, StringComparer.Ordinal)),
        Supply = source.Supply is not { } supply ? null : supply with {
            Ledger = supply.Ledger with { Balances = ReadOnly(supply.Ledger.Balances.OrderBy(b => b.SupplierId, StringComparer.Ordinal)),
                Deliveries = ReadOnly(supply.Ledger.Deliveries.OrderBy(d => d.Sequence)), Requests = ReadOnly(supply.Ledger.Requests.OrderBy(r => r.RequestId, StringComparer.Ordinal)) },
            Origins = ReadOnly(supply.Origins.OrderBy(o => o.RequestId, StringComparer.Ordinal).Select(o => o with { Units = ReadOnly(o.Units) })) } };
    private static CookingFrontOfHouseSnapshot FreezeFront(CookingFrontOfHouseSnapshot source) => source with {
        OrderMenu = ReadOnly(source.OrderMenu), Customers = ReadOnly(source.Customers.OrderBy(c => c.ArrivalOrder)),
        WashQueue = ReadOnly(source.WashQueue), UnsatisfiedOrders = ReadOnly(source.UnsatisfiedOrders),
        Work = ReadOnly(source.Work.OrderBy(w => w.Id, StringComparer.Ordinal)), Tables = ReadOnly(source.Tables.OrderBy(t => t.Id, StringComparer.Ordinal)),
        Flow = source.Flow is not { } flow ? null : flow with {
            Walkable = ReadOnly(flow.Walkable), EntranceToQueue = ReadOnly(flow.EntranceToQueue), QueueToExit = ReadOnly(flow.QueueToExit),
            Tables = ReadOnly(flow.Tables.Select(t => t with { QueueToTable = ReadOnly(t.QueueToTable), TableToExit = ReadOnly(t.TableToExit) })),
            Spatial = flow.Spatial?.Freeze() } };
}
