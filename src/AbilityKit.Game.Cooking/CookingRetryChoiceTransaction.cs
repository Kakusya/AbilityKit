namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    /// <summary>Stages retry choices privately; rejection retains the entire kitchen and progress.</summary>
    public CookingMajorProgressResult ApplyRetryChoices(CookingContent content, CookingMajorProgress progress) =>
        ApplyRetryChoicesAtomic(content, progress, null);

    // The Level owner selects placements from current trusted content/permission. Global choices persist.
    internal CookingMajorProgressResult ApplyRetryChoicesForLevel(CookingContent content, CookingMajorProgress progress,
        IReadOnlySet<DefinitionId> eligibleUnlocks) => ApplyRetryChoicesAtomic(content, progress, eligibleUnlocks);

    private CookingMajorProgressResult ApplyRetryChoicesAtomic(CookingContent content, CookingMajorProgress progress,
        IReadOnlySet<DefinitionId>? eligibleUnlocks)
    {
        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(progress);
        if (_lifecycleClosed || _mutationInProgress || !IsAuthorityMutationOpen ||
            (_lifecycleGate is not null && !_lifecycleGate.IsLayoutInstallationOpen))
            return new(false, CookingMajorProgressReason.InvalidState);
        try
        {
            var staged = CreateGenerationTransactionCopy();
            var result = staged.ApplyRetryChoicesCore(content, progress, eligibleUnlocks);
            if (!result.Accepted) return result;
            var checkpoint = staged.ExportCheckpoint();
            // Validate against the intended choices before publishing to the receiver's old grant.
            if (staged.RestoreExportedCheckpoint(checkpoint) != CookingCheckpointRestoreReason.None)
                return new(false, CookingMajorProgressReason.InvalidState);
            if (RestoreExportedCheckpoint(checkpoint) != CookingCheckpointRestoreReason.None)
                return new(false, CookingMajorProgressReason.InvalidState);
            _majorProgress = progress;
            return result;
        }
        catch (ArgumentException) { return new(false, CookingMajorProgressReason.UnknownChoice); }
        catch (InvalidOperationException) { return new(false, CookingMajorProgressReason.StationConflict); }
        catch (OverflowException) { return new(false, CookingMajorProgressReason.InvalidState); }
    }
}
