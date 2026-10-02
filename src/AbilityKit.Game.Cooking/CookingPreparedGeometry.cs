namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    internal IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> ConfiguredAppliances => _fixture.Appliances;
    internal IReadOnlyList<PlayerId> ConfiguredPlayers =>
        Array.AsReadOnly(_fixture.Players.Keys.OrderBy(p => p.Value, StringComparer.Ordinal).ToArray());
    // The Level owner builds this projection from trusted content and validates front routes
    // before calling the installer. No gameplay command accepts a caller-supplied projection.
    internal bool CanInstallPreparedGeometry(CookingLayoutGeometryResult projection,
        CookingRecipeCheckpoint? restoreReferences = null)
    {
        if (!projection.Accepted || projection.Geometry is not { } geometry ||
            _lifecycleClosed || _mutationInProgress ||
            (_lifecycleGate is not null && !_lifecycleGate.IsLayoutInstallationOpen))
            return false;
        if (projection.ProjectedPoses.Count != _fixture.Players.Count ||
            projection.ProjectedPoses.Any(p => !_fixture.Players.ContainsKey(p.Player) || !geometry.ValidPose(p)) ||
            projection.ProjectedPoses.Select(p => p.Player).Distinct().Count() != _fixture.Players.Count ||
            geometry.Anchors.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != geometry.Anchors.Count)
            return false;
        var poses = projection.ProjectedPoses;
        for (var i = 0; i < poses.Count; i++)
        {
            if (restoreReferences is null &&
                (!_poses.TryGetValue(poses[i].Player, out var old) || poses[i].LastMovementTick != old.LastMovementTick))
                return false;
            for (var j = i + 1; j < poses.Count; j++)
                if (geometry.Overlap(poses[i], poses[j])) return false;
        }
        bool HasAnchor(LocationKind kind, string? id) =>
            geometry.Anchors.Any(a => a.Kind == kind && a.Id == id);
        if (_fixture.Appliances.Keys.Any(s => !HasAnchor(LocationKind.StationSlot, s.Value)) ||
            geometry.Anchors.Any(a => a.Kind == LocationKind.StationSlot &&
                !_fixture.Appliances.ContainsKey(new StationSlotId(a.Id)))) return false;
        if (restoreReferences is null)
        {
            foreach (var item in _items.Values.Where(i => !i.Removed))
                if (item.Location.Kind is LocationKind.WorldPosition or LocationKind.StationSlot &&
                    !HasAnchor(item.Location.Kind, item.Location.SlotId)) return false;
            foreach (var process in AllProcesses())
                if (process.Station is { } station && !HasAnchor(LocationKind.StationSlot, station.Value)) return false;
        }
        else
        {
            if (!Equals(restoreReferences.Scope, _fixture.Scope) || restoreReferences.Items is null ||
                restoreReferences.Processes is null || restoreReferences.Poses is not { } restoredPoses ||
                restoredPoses.Count != _fixture.Players.Count ||
                restoredPoses.Any(p => p is null || !_fixture.Players.ContainsKey(p.Player) || !geometry.ValidPose(p)) ||
                restoredPoses.Select(p => p.Player).Distinct().Count() != restoredPoses.Count)
                return false;
            for (var i = 0; i < restoredPoses.Count; i++)
                for (var j = i + 1; j < restoredPoses.Count; j++)
                    if (geometry.Overlap(restoredPoses[i], restoredPoses[j])) return false;
            foreach (var item in restoreReferences.Items)
                if (item is null || (!item.Removed &&
                    item.Location.Kind is LocationKind.WorldPosition or LocationKind.StationSlot &&
                    !HasAnchor(item.Location.Kind, item.Location.SlotId))) return false;
            foreach (var process in restoreReferences.Processes)
                if (process is null || (process.Station is { } station &&
                    !HasAnchor(LocationKind.StationSlot, station.Value))) return false;
        }
        if (_fixture.Supply is { } supply)
            foreach (var supplier in supply.Suppliers)
                if (!geometry.Anchors.Any(a => a.Id == supplier.SourceAnchor) ||
                    !geometry.Anchors.Any(a => a.Id == supplier.ReceivingAnchor)) return false;
        return true;
    }

    internal bool InstallPreparedGeometry(CookingLayoutGeometryResult projection,
        CookingRecipeCheckpoint? restoreReferences = null)
    {
        if (!IsAuthorityMutationOpen || !CanInstallPreparedGeometry(projection, restoreReferences)) return false;
        var geometry = projection.Geometry!.Freeze();
        var poses = projection.ProjectedPoses.ToDictionary(p => p.Player);
        var stationProcesses = _processesByStation.ToDictionary(p => p.Key, p => p.Value with { ActiveWorker = null });
        var anchoredProcesses = _processesByAnchorItem.ToDictionary(p => p.Key, p => p.Value with { ActiveWorker = null });
        var version = checked(_stateVersion + 1);
        _installedSpatial = geometry;
        _poses = poses;
        _processesByStation = stationProcesses;
        _processesByAnchorItem = anchoredProcesses;
        _stateVersion = version;
        return true;
    }
}
