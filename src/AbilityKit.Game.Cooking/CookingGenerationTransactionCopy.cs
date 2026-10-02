namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    /// <summary>Unpublished transition staging. It cannot allocate products or notify external washing ports.</summary>
    internal CookingRecipeSimulation CreateGenerationTransactionCopy()
    {
        var copy = new CookingRecipeSimulation(_fixture, TransitionCopyAllocator.Instance);
        copy._installedSpatial = EffectiveSpatial?.Freeze();
        var restored = copy.RestoreExportedCheckpoint(ExportCheckpoint());
        if (restored != CookingCheckpointRestoreReason.None)
            throw new InvalidOperationException($"The transition source cannot be staged: {restored}.");
        copy._isClosing = _isClosing;
        copy._isCompleted = _isCompleted;
        copy._majorProgress = _majorProgress;
        return copy;
    }

    private sealed class TransitionCopyAllocator : ICookingProductIdAllocator
    {
        internal static TransitionCopyAllocator Instance { get; } = new();
        public ItemId GetProductId(long productSequence) =>
            throw new InvalidOperationException("Transition staging cannot allocate a new product.");
    }
}
