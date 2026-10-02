namespace AbilityKit.Game.Cooking;

/// <summary>Owner-private publication prepared before a durable commit point.</summary>
internal sealed class CookingPreparedLevelGenerationCommit
{
    private readonly Action _publish;
    private bool _committed;
    internal CookingPreparedLevelGenerationCommit(CookingLevelSuccessorResult result, Action publish)
    {
        Result = result;
        _publish = publish;
    }
    internal CookingLevelSuccessorResult Result { get; }
    internal void Commit()
    {
        if (_committed) return;
        _publish();
        _committed = true;
    }
}

public sealed partial class CookingLevelLifecycle
{
    /// <summary>
    /// Validates and allocates publication before IO. The owner must exclude other lifecycle
    /// operations between preparation and Commit; this token never escapes the host transaction.
    /// </summary>
    internal bool PrepareSuccessorCandidateCommit(CookingLevelLifecycle candidate,
        out CookingPreparedLevelGenerationCommit? preparedCommit, out CookingLevelLifecycleReason reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        preparedCommit = null;
        reason = ValidateSuccessorCandidate(candidate.Scope.Level, candidate.Scope.LevelEpoch);
        if (reason != CookingLevelLifecycleReason.None) return false;
        if (candidate.State != CookingLevelState.Created || candidate.Scope.MatchScope != Scope.MatchScope ||
            candidate.Scope.RestaurantRuntime != Scope.RestaurantRuntime || candidate.Configuration.Identity != _configuration.Identity)
        {
            reason = CookingLevelLifecycleReason.InvalidState;
            return false;
        }
        long nextVersion, nextSequence;
        try { nextVersion = checked(Version + 1); nextSequence = checked(_eventSequence + 1); }
        catch (OverflowException)
        {
            reason = CookingLevelLifecycleReason.GameplayInitializationFailed;
            return false;
        }
        var entry = new CookingLevelLifecycleEvent(nextSequence, nextVersion, State, Outcome,
            "level-successor-created", CookingLevelLifecycleReason.None,
            "created the next successful level generation");
        var transition = new CookingLevelLifecycleResult(true, CookingLevelLifecycleReason.None,
            State, Outcome, nextVersion, CookingLevelLifecycleResult.ReadOnlyEvents(new[] { entry }));
        var result = new CookingLevelSuccessorResult(true, CookingLevelLifecycleReason.None, candidate,
            Scope, Outcome, candidate.Scope, Version, nextVersion, _gameplay is null || _gameplayClosed, transition);
        // Reserve storage before IO so normal publication performs no allocation or validation.
        _events.EnsureCapacity(checked(_events.Count + 1));
        preparedCommit = new(result, () =>
        {
            _events.Add(entry);
            Version = nextVersion;
            _eventSequence = nextSequence;
            _hasCreatedNextGeneration = true;
        });
        return true;
    }
}
