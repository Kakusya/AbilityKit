using System.Collections.Frozen;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>Supplied by trusted level construction, not by ordinary commands or live items.</summary>
public sealed record CookingLevelMenuConfiguration(string CatalogIdentity, IReadOnlyList<string> SelectedMenuIds,
    IReadOnlySet<DefinitionId> BaseAuthorizedMaterialDefinitions, IReadOnlySet<DefinitionId> ConfirmedMaterialUnlocks,
    IReadOnlySet<DefinitionId> AllowedMaterialDefinitions, bool BindingCommandsEnabled)
{
    public CookingLevelMenuConfiguration Freeze()
    {
        if (string.IsNullOrWhiteSpace(CatalogIdentity) || SelectedMenuIds is null ||
            BaseAuthorizedMaterialDefinitions is null || ConfirmedMaterialUnlocks is null || AllowedMaterialDefinitions is null ||
            SelectedMenuIds.Any(string.IsNullOrWhiteSpace) || SelectedMenuIds.Distinct(StringComparer.Ordinal).Count() != SelectedMenuIds.Count ||
            BaseAuthorizedMaterialDefinitions.Concat(ConfirmedMaterialUnlocks).Concat(AllowedMaterialDefinitions).Any(d => string.IsNullOrWhiteSpace(d.Value)))
            throw new ArgumentException("Invalid trusted menu policy.");
        return this with { SelectedMenuIds = Array.AsReadOnly(SelectedMenuIds.Order(StringComparer.Ordinal).ToArray()),
            BaseAuthorizedMaterialDefinitions = BaseAuthorizedMaterialDefinitions.ToFrozenSet(),
            ConfirmedMaterialUnlocks = ConfirmedMaterialUnlocks.ToFrozenSet(), AllowedMaterialDefinitions = AllowedMaterialDefinitions.ToFrozenSet() };
    }
    public string CanonicalText() => JsonSerializer.Serialize(new { CatalogIdentity,
        SelectedMenuIds = SelectedMenuIds.Order(StringComparer.Ordinal).ToArray(),
        Base = BaseAuthorizedMaterialDefinitions.Select(d => d.Value).Order(StringComparer.Ordinal).ToArray(),
        Unlocks = ConfirmedMaterialUnlocks.Select(d => d.Value).Order(StringComparer.Ordinal).ToArray(),
        Allowed = AllowedMaterialDefinitions.Select(d => d.Value).Order(StringComparer.Ordinal).ToArray(), BindingCommandsEnabled });
    public string Identity() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));
}

public sealed record CookingMenuReadyDiagnostic(string MenuId, string NodeId, string Code, string Relation, bool Blocking = true);
public sealed record CookingMenuReadyResult(IReadOnlyList<CookingMenuReadyDiagnostic> Diagnostics)
{
    public bool IsReady => !Diagnostics.Any(d => d.Blocking);
}

/// <summary>Structural validation only. Counts warn, and never introduce a normal business failure.</summary>
public static class CookingMenuReadyValidation
{
    public static CookingMenuReadyResult Validate(CookingMenuCatalog catalog, CookingLevelMenuConfiguration trustedPolicy,
        CookingManufacturingAvailability actual)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(trustedPolicy); ArgumentNullException.ThrowIfNull(actual);
        var policy = trustedPolicy.Freeze(); var kitchen = actual.Freeze(); var doc = catalog.Document;
        var errors = new List<CookingMenuReadyDiagnostic>();
        void Error(string menu, string node, string code, string relation, bool blocking = true) => errors.Add(new(menu, node, code, relation, blocking));
        if (policy.CatalogIdentity != catalog.Sha256) Error("Policy", "Catalog", "CatalogMismatch", policy.CatalogIdentity);
        if (kitchen.Current.Scope != kitchen.Scope) Error("Policy", "Kitchen", "ScopeMismatch", kitchen.Scope.ToString());
        var authorized = policy.BaseAuthorizedMaterialDefinitions.Union(policy.ConfirmedMaterialUnlocks).Intersect(policy.AllowedMaterialDefinitions).ToHashSet();
        var materials = doc.Materials.ToDictionary(m => m.Id); var steps = doc.Steps.ToDictionary(s => s.Id);
        var sources = kitchen.RegisteredSeeds.Concat(kitchen.Current.Items.Select(i => new CookingManufacturingSeed(i.Id, i.Definition, i.Location))).ToArray();
        var sourceById = sources.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.Last());
        var reachCache = new Dictionary<(PlayerId, ItemLocation), CookingStaticReachability>();
        var reachLimits = new HashSet<(string Menu, string Node)>();
        bool Qualified(CookingPlayerConfig p, IEnumerable<DefinitionId> definitions) => p.IsAvailable && definitions.All(d =>
            kitchen.ItemDefinitions.TryGetValue(d, out var item) && item.AllowedPlayerCapabilities.Overlaps(p.Capabilities));
        CookingStaticReachability Reach(CookingPlayerConfig player, ItemLocation location)
        {
            var key = (player.Id, location);
            if (!reachCache.TryGetValue(key, out var result)) { result = ComputeReach(player, location); reachCache.Add(key, result); }
            return result;
        }
        CookingStaticReachability ComputeReach(CookingPlayerConfig player, ItemLocation location)
        {
            var seen = new HashSet<ItemId>();
            while (location.Kind == LocationKind.ContainerSlot)
            {
                var id = new ItemId(location.OwnerId ?? "");
                if (!seen.Add(id) || !sourceById.TryGetValue(id, out var parent)) return CookingStaticReachability.Unreachable;
                location = parent.Location;
            }
            if (kitchen.CurrentSpatial is not { } spatial) return location.Kind switch {
                LocationKind.StationSlot => player.ReachableStations.Contains(location.SlotId ?? "") ? CookingStaticReachability.Reachable : CookingStaticReachability.Unreachable,
                LocationKind.WorldPosition or LocationKind.PlayerHand => CookingStaticReachability.Reachable,
                _ => CookingStaticReachability.Unreachable };
            var pose = kitchen.Current.Poses?.SingleOrDefault(p => p.Player == player.Id);
            var anchor = location.Kind == LocationKind.PlayerHand
                ? kitchen.Current.Poses?.Where(p => p.Player.Value == location.OwnerId).Select(p => new CookingSpatialAnchor(LocationKind.PlayerHand, p.Player.Value, p.X, p.Y)).SingleOrDefault()
                : spatial.Anchors.SingleOrDefault(a => a.Kind == location.Kind && a.Id == location.SlotId);
            return pose is null || anchor is null ? CookingStaticReachability.Unreachable : CookingManufacturingPaths.Reach(spatial, pose, anchor);
        }
        bool ReachableByQualified(IEnumerable<DefinitionId> defs, ItemLocation location, string menu, string node)
        {
            var results = kitchen.Players.Values.Where(p => Qualified(p, defs)).Select(p => Reach(p, location)).ToArray();
            if (results.Contains(CookingStaticReachability.Reachable)) return true;
            if (results.Contains(CookingStaticReachability.ComplexityLimit)) reachLimits.Add((menu, node));
            return false;
        }
        void Definition(string menu, string node, DefinitionId definition)
        {
            if (!kitchen.ItemDefinitions.ContainsKey(definition)) Error(menu, node, "MissingDefinition", definition.Value);
            if (!authorized.Contains(definition)) Error(menu, node, "MaterialNotAuthorized", definition.Value);
        }
        void Source(string menu, string node, DefinitionId definition)
        {
            var found = sources.Where(s => s.Definition == definition).Any(s => ReachableByQualified(new[] { definition }, s.Location, menu, node));
            if (kitchen.CleanContainerSupply.GetValueOrDefault(definition) > 0)
                found |= ReachableByQualified(new[] { definition }, ItemLocation.World(kitchen.CleanPoolLocation), menu, node);
            foreach (var supplier in kitchen.SupplyConfiguration?.Suppliers.Where(s => s.UnitDefinition == definition || !s.Infinite && s.PackageDefinition == definition)
                ?? Enumerable.Empty<CookingSupplierDefinition>())
            {
                if (!authorized.Contains(supplier.UnitDefinition)) continue;
                if (!supplier.Infinite && (!authorized.Contains(supplier.PackageDefinition) ||
                    !kitchen.ItemDefinitions.TryGetValue(supplier.PackageDefinition, out var package) || package.Container is not { } container ||
                    container.Capacity < supplier.UnitsPerPackage || !container.AcceptedDefinitions.Contains(supplier.UnitDefinition))) continue;
                ItemLocation? Anchor(string id)
                {
                    if (kitchen.CurrentSpatial is null) return ItemLocation.World(id);
                    var anchors = kitchen.CurrentSpatial.Anchors.Where(a => a.Id == id).ToArray();
                    return anchors.Length != 1 ? null : anchors[0].Kind == LocationKind.StationSlot
                        ? ItemLocation.Station(new(id)) : ItemLocation.World(id);
                }
                var from = Anchor(supplier.SourceAnchor);
                if (from is null || !ReachableByQualified(new[] { supplier.UnitDefinition }, from, menu, node)) continue;
                if (!supplier.Infinite) {
                    var to = Anchor(supplier.ReceivingAnchor);
                    if (to is null || !ReachableByQualified(new[] { supplier.UnitDefinition, supplier.PackageDefinition }, to, menu, node)) continue;
                }
                found = true;
                var balance = kitchen.Current.Supply?.Ledger.Balances.FirstOrDefault(b => b.SupplierId == supplier.SupplierId)?.AvailableUnits ?? supplier.FiniteAvailable;
                if (!supplier.Infinite && balance < supplier.UnitsPerPackage) Error(menu, node, "SupplyExhausted", supplier.SupplierId, false);
            }
            if (!found) Error(menu, node, "MissingSource", definition.Value);
            else if (!kitchen.Current.Items.Any(i => i.Definition == definition)) Error(menu, node, "NoCurrentStock", definition.Value, false);
        }
        void Container(string menu, string node, DefinitionId id, IEnumerable<DefinitionId> inputs)
        {
            Definition(menu, node, id); Source(menu, node, id);
            var list = inputs.ToArray();
            if (!kitchen.ItemDefinitions.TryGetValue(id, out var item) || item.Container is not { } container)
                Error(menu, node, "MissingContainer", id.Value);
            else if (container.Capacity < list.Length || list.Any(d => !container.AcceptedDefinitions.Contains(d)))
                Error(menu, node, "ContainerIncompatible", id.Value);
        }
        foreach (var menuId in policy.SelectedMenuIds)
        {
            var menu = doc.Menus.SingleOrDefault(m => m.SourceId == menuId);
            if (menu is null) { Error(menuId, "Menu", "UnknownMenu", menuId); continue; }
            var required = catalog.Requirements(new[] { menuId });
            foreach (var supply in required.Supplies) { Definition(menuId, "Supply", supply); Source(menuId, "Supply", supply); }
            foreach (var recipeId in required.Recipes)
            {
                var step = steps[recipeId]; var node = recipeId.Value;
                var inputs = step.Inputs.SelectMany(i => Enumerable.Repeat(i.Definition, i.Portions)).ToArray();
                foreach (var definition in inputs.Append(step.Output).Distinct()) Definition(menuId, node, definition);
                Container(menuId, node, step.Carrier, inputs);
                if (step.OutputStorageContainer is { } storage) Container(menuId, node, storage, new[] { step.Output });
                if (!kitchen.Recipes.TryGetValue(recipeId, out var recipe)) { Error(menuId, node, "MissingRecipe", recipeId.Value); continue; }
                static bool Same(IEnumerable<DefinitionId> a, IEnumerable<DefinitionId> b) => a.Select(d => d.Value).Order(StringComparer.Ordinal).SequenceEqual(b.Select(d => d.Value).Order(StringComparer.Ordinal));
                if (recipe.Id != recipeId || !Same(recipe.Inputs, inputs) || recipe.DefaultInputs is { Count: > 0 } ||
                    recipe.ProductDefinition != step.Output || recipe.Process != step.Process || recipe.RequiredApplianceCapability != step.Capability ||
                    recipe.RequiredProcessingContainerDefinition != step.Carrier || recipe.YieldPortions != step.YieldPortions ||
                    recipe.Completion.ToString() != CookingMenuCatalog.Completion(step) || recipe.Execution.ToString() != step.ExecutionKind.ToString() ||
                    recipe.RequiredTicks != step.RequiredTicks || !recipe.RequiresStation)
                    Error(menuId, node, "RecipeMismatch", recipeId.Value);
                var qualifiedDefinitions = inputs.Append(step.Carrier).Distinct().ToArray();
                if (!kitchen.Players.Values.Any(p => Qualified(p, qualifiedDefinitions))) Error(menuId, node, "NoEligiblePlayer", recipeId.Value);
                var appliances = kitchen.Appliances.Values.Where(a => a.IsAvailable && a.Capabilities.Contains(step.Capability)).ToArray();
                if (appliances.Length == 0) Error(menuId, node, "MissingAppliance", step.Capability);
                else if (!appliances.Any(a => ReachableByQualified(qualifiedDefinitions, ItemLocation.Station(a.Station), menuId, node)))
                    Error(menuId, node, "UnreachableAppliance", step.Capability);
            }
            Container(menuId, "Delivery", menu.ServingContainer, new[] { menu.Product });
            if (!kitchen.Players.Values.Any(p => Qualified(p, new[] { menu.Product, menu.ServingContainer })))
                Error(menuId, "Delivery", "NoEligibleDeliveryPlayer", menu.Product.Value + "/" + menu.ServingContainer.Value);
            if (!kitchen.OrderTemplates.TryGetValue(menu.OrderTemplate, out var order) || order.RequiredRecipe != menu.FinalRecipe ||
                order.RequiredContainerDefinition != menu.ServingContainer || order.RequiresBinding != menu.RequiresBinding ||
                !kitchen.Recipes.TryGetValue(menu.FinalRecipe, out var final) || final.ProductDefinition != menu.Product)
                Error(menuId, "Delivery", "OrderMismatch", menu.OrderTemplate.Value);
            if (menu.RequiresBinding && !policy.BindingCommandsEnabled) Error(menuId, "Delivery", "BindingDisabled", menu.OrderTemplate.Value);
        }
        foreach (var limit in reachLimits)
            if (errors.Any(e => e.MenuId == limit.Menu && e.NodeId == limit.Node && e.Code is "MissingSource" or "UnreachableAppliance"))
                Error(limit.Menu, limit.Node, "ReachabilityLimit", "configuration-space graph exceeds 4096 candidates");
        return new(Array.AsReadOnly(errors.Distinct().OrderBy(e => e.MenuId, StringComparer.Ordinal).ThenBy(e => e.NodeId, StringComparer.Ordinal)
            .ThenBy(e => e.Code, StringComparer.Ordinal).ThenBy(e => e.Relation, StringComparer.Ordinal).ToArray()));
    }
}

internal enum CookingStaticReachability { Reachable, Unreachable, ComplexityLimit }

internal static class CookingManufacturingPaths
{
    // Compressed integer configuration-space visibility graph. Dynamic actors are not static obstacles.
    // Closed inflated rectangles use the same exact segment predicate as movement; ±1 stays outside tangency.
    internal static CookingStaticReachability Reach(CookingSpatialConfiguration spatial, CookingPlayerPose start, CookingSpatialAnchor target)
    {
        if (!spatial.ValidPose(start)) return CookingStaticReachability.Unreachable;
        var xs = new HashSet<int> { start.X, target.X, spatial.MinX + spatial.PlayerRadius, spatial.MaxX - spatial.PlayerRadius };
        var ys = new HashSet<int> { start.Y, target.Y, spatial.MinY + spatial.PlayerRadius, spatial.MaxY - spatial.PlayerRadius };
        void Add(HashSet<int> set, long value) { if (value >= int.MinValue && value <= int.MaxValue) set.Add((int)value); }
        Add(xs, (long)target.X - spatial.InteractionRadius); Add(xs, (long)target.X + spatial.InteractionRadius);
        Add(ys, (long)target.Y - spatial.InteractionRadius); Add(ys, (long)target.Y + spatial.InteractionRadius);
        foreach (var obstacle in spatial.Obstacles) {
            Add(xs, (long)obstacle.MinX - spatial.PlayerRadius - 1); Add(xs, (long)obstacle.MaxX + spatial.PlayerRadius + 1);
            Add(ys, (long)obstacle.MinY - spatial.PlayerRadius - 1); Add(ys, (long)obstacle.MaxY + spatial.PlayerRadius + 1);
        }
        var x = xs.Where(v => (long)v - spatial.PlayerRadius >= spatial.MinX && (long)v + spatial.PlayerRadius <= spatial.MaxX).Order().ToArray();
        var y = ys.Where(v => (long)v - spatial.PlayerRadius >= spatial.MinY && (long)v + spatial.PlayerRadius <= spatial.MaxY).Order().ToArray();
        if ((long)x.Length * y.Length > 4096) return CookingStaticReachability.ComplexityLimit;
        var nodes = x.SelectMany(a => y.Select(b => new CookingPlayerPose(start.Player, a, b, 1, 0))).Where(spatial.ValidPose).ToArray();
        bool Goal(CookingPlayerPose p) {
            var dx = (long)p.X - target.X; var dy = (long)p.Y - target.Y;
            return (BigInteger)dx * dx + (BigInteger)dy * dy <= (BigInteger)spatial.InteractionRadius * spatial.InteractionRadius &&
                !spatial.Obstacles.Any(o => CookingSpatialConfiguration.SegmentHits(p.X, p.Y, target.X, target.Y, o.MinX, o.MinY, o.MaxX, o.MaxY));
        }
        bool Edge(CookingPlayerPose a, CookingPlayerPose b) => !spatial.Obstacles.Any(o => CookingSpatialConfiguration.SegmentHits(a.X, a.Y, b.X, b.Y,
            (long)o.MinX - spatial.PlayerRadius, (long)o.MinY - spatial.PlayerRadius, (long)o.MaxX + spatial.PlayerRadius, (long)o.MaxY + spatial.PlayerRadius));
        var pending = new Queue<CookingPlayerPose>(); pending.Enqueue(start); var seen = new HashSet<(int, int)> { (start.X, start.Y) };
        while (pending.TryDequeue(out var current)) {
            if (Goal(current)) return CookingStaticReachability.Reachable;
            foreach (var next in nodes) if (!seen.Contains((next.X, next.Y)) && Edge(current, next)) { seen.Add((next.X, next.Y)); pending.Enqueue(next); }
        }
        return CookingStaticReachability.Unreachable;
    }
}
