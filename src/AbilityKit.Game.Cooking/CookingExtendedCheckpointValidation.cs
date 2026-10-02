namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    private CookingCheckpointRestoreReason ValidateExtendedCheckpoint(CookingRecipeCheckpoint checkpoint)
    {
        if (checkpoint.SchemaVersion != 3 || checkpoint.Poses is null) return CookingCheckpointRestoreReason.UnsupportedSchema;
        if (checkpoint.Items is null || checkpoint.Processes is null) return CookingCheckpointRestoreReason.CounterInvalid;
        var items = new Dictionary<ItemId, CookingRecipeCheckpointItem>();
        foreach (var item in checkpoint.Items)
        {
            if (!items.TryAdd(item.Id, item)) return CookingCheckpointRestoreReason.DuplicateItemIdentity;
            if (item.Version <= 0 || item.RemainingPortions < 0) return CookingCheckpointRestoreReason.PortionStateInvalid;
            if (item.ContainerCompleted)
            {
                if (item.Removed || item.Recipe is not { } recipeId || !_fixture.Recipes.TryGetValue(recipeId, out var recipe) ||
                    recipe.Completion != CookingRecipeCompletionKind.RetainInputs || item.RemainingPortions <= 0 || item.RemainingPortions > recipe.YieldPortions ||
                    !_fixture.Items.TryGetValue(item.Definition, out var definition) || definition.Container is null)
                    return CookingCheckpointRestoreReason.PortionStateInvalid;
                var contents = checkpoint.Items.Where(i => !i.Removed && i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == item.Id.Value).ToArray();
                if (CookingRecipeMatcher.Match(contents.Select(i => i.Definition).ToArray(), null, new[]{recipe}).Outcome != CookingRecipeMatchOutcome.Matched)
                    return CookingCheckpointRestoreReason.PortionStateInvalid;
            }
            else if (item.RemainingPortions != 0) return CookingCheckpointRestoreReason.PortionStateInvalid;
        }
        var poses = new Dictionary<PlayerId, CookingPlayerPose>();
        foreach (var pose in checkpoint.Poses)
            if (!poses.TryAdd(pose.Player, pose)) return CookingCheckpointRestoreReason.SpatialStateInvalid;
        if (_fixture.Spatial is { } spatial)
        {
            if (poses.Count != _fixture.Players.Count || poses.Values.Any(p => !_fixture.Players.ContainsKey(p.Player) || !spatial.ValidPose(p) || p.LastMovementTick < -1 || p.LastMovementTick > checkpoint.LogicalTick) ||
                poses.Values.Any(p => poses.Values.Any(q => p.Player != q.Player && spatial.Overlap(p, q))))
                return CookingCheckpointRestoreReason.SpatialStateInvalid;
            foreach (var item in items.Values.Where(i => !i.Removed))
                if (item.Location.Kind is LocationKind.WorldPosition or LocationKind.StationSlot &&
                    !spatial.Anchors.Any(a => a.Kind == item.Location.Kind && a.Id == item.Location.SlotId))
                    return CookingCheckpointRestoreReason.SpatialStateInvalid;
            if (items.Values.Where(i => !i.Removed && !IsManagedPoolLocation(i.Location, i.Definition) && i.Location.Kind is LocationKind.StationSlot or LocationKind.WorldPosition).GroupBy(i => (i.Location.Kind, i.Location.SlotId)).Any(g => g.Count() > 1))
                return CookingCheckpointRestoreReason.SpatialStateInvalid;
        }
        else if (poses.Count != 0) return CookingCheckpointRestoreReason.SpatialStateInvalid;
        var workers = new HashSet<PlayerId>();
        foreach (var process in checkpoint.Processes)
        {
            if (!_fixture.Recipes.TryGetValue(process.Recipe, out var recipe)) continue; // ordinary validator diagnoses this
            if (process.ActiveWorker is not { } worker) continue;
            if (recipe.Execution != CookingRecipeExecutionKind.Manual || !workers.Add(worker) ||
                !_fixture.Players.TryGetValue(worker, out var config) || !config.IsAvailable ||
                !items.TryGetValue(process.Anchor, out var anchor) || anchor.Removed)
                return CookingCheckpointRestoreReason.WorkerStateInvalid;
            bool Reach(ItemLocation location)
            {
                var seen = new HashSet<ItemId>();
                while (location.Kind == LocationKind.ContainerSlot)
                {
                    var id = new ItemId(location.OwnerId ?? "");
                    if (!seen.Add(id) || !items.TryGetValue(id, out var parent) || parent.Removed) return false;
                    location = parent.Location;
                }
                if (location.Kind == LocationKind.PlayerHand) return location.OwnerId == worker.Value;
                if (_fixture.Spatial is null) return location.Kind == LocationKind.WorldPosition ||
                    (location.Kind == LocationKind.StationSlot && config.ReachableStations.Contains(location.SlotId ?? ""));
                var a = _fixture.Spatial.Anchors.FirstOrDefault(a => a.Kind == location.Kind && a.Id == location.SlotId);
                return a is not null && GeometryReach(poses[worker], a.X, a.Y);
            }
            if (!Reach(anchor.Location) || (process.Station is { } station && !Reach(ItemLocation.Station(station))))
                return CookingCheckpointRestoreReason.WorkerStateInvalid;
        }
        return CookingCheckpointRestoreReason.None;
    }
}
