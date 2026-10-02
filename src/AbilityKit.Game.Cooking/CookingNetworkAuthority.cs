namespace AbilityKit.Game.Cooking;

// Application contracts only. Transport callbacks must not invoke this owner-thread port.
public enum CookingNetworkCaptureReason { None, AuthorityFaulted, Disposed, Busy }
public enum CookingNetworkControlKind { Pause, Resume, TryFinishService, ExecuteAuthorizedTransition }
public enum CookingNetworkControlReason { None, InvalidRequest, Unauthorized, InvalidLifecycle, AuthorityFaulted, Disposed, Busy }
public enum CookingNetworkCancelReason { None, AuthorityFaulted, Disposed, Busy, InvalidSource }
public enum CookingNetworkFrameReason { None, AuthorityFaulted, Disposed, Busy, InvalidLifecycle, ArithmeticOverflow, CleanupRejected }
public enum CookingNetworkAdmissionReason
{
    None, LevelNotRunning, LevelPaused, ScopeMismatch, MalformedCommand, ReservedClockOperation,
    BatchStale, QueueFull, CommandIdentityConflict, CommandTerminal, ProtocolMismatch,
    Unauthorized, BaselineRequired, SequenceStale, ReceiptCapacityExceeded,
}
public enum CookingNetworkDispositionKind { Executed, Duplicate, Conflicted, Cancelled, Stale }
public enum CookingNetworkCallerCancellationReason { ConnectionClosed, ConnectionSuperseded, Paused, ScopeRetired }
public enum CookingNetworkCheckpointUnavailableReason
{
    None, NotInitialized, LevelNotRunning, LevelPaused, PendingCommands, PreparationMissing,
}

public sealed record CookingLevelCommandEnvelopeData(CookingLevelScope LevelScope,
    CookingRecipeCommand Command, string SourceConnectionId, string CorrelationId);
public sealed record CookingNetworkMappedCommand(CookingLevelCommandEnvelopeData Envelope,
    long AuthorityOrdinal, string WireFingerprint);
public sealed record CookingNetworkCancellation(string SourceConnectionId,
    long ConnectionGeneration, CookingNetworkCallerCancellationReason Reason);
public sealed record CookingNetworkCallerCancellation(string SourceConnectionId, string CorrelationId,
    CookingLevelScope Scope, PlayerId Participant, RecipeCommandId CommandId,
    CookingNetworkCallerCancellationReason Reason);
public sealed record CookingNetworkDisposition(CookingLevelScope Scope, PlayerId Participant,
    RecipeCommandId CommandId, string SourceConnectionId, string CorrelationId,
    CookingNetworkDispositionKind Kind, string Reason, CookingRecipeCommandResult? Result);
public sealed record CookingNetworkAdmission(CookingLevelScope Scope, PlayerId Participant,
    RecipeCommandId CommandId, string SourceConnectionId, string CorrelationId,
    bool Accepted, CookingNetworkAdmissionReason Reason, CookingNetworkDisposition? Disposition = null);

/// <summary>Full committed state, distinct from a resumable Level checkpoint.</summary>
public sealed record CookingNetworkAuthorityCapture(CookingLevelObservation Observation,
    CookingRecipeCheckpoint? FullRecipe, CookingFrontOfHouseCheckpoint? FullFront,
    CookingInstalledLayoutCheckpoint? InstalledLayout,
    CookingConfigurationIdentity ConfigurationIdentity,
    CookingLevelPreparation? Preparation, long ServiceStartLogicalTick,
    long LastCommittedSimulationBatch, string? FrontConfigurationIdentity,
    string? PreparationConfigurationIdentity, string? MenuConfigurationIdentity,
    CookingLevelCheckpoint? ResumableCheckpoint,
    CookingNetworkCheckpointUnavailableReason CheckpointUnavailableReason);
public sealed record CookingNetworkCaptureResult(bool Accepted, CookingNetworkCaptureReason Reason,
    CookingNetworkAuthorityCapture? State);
public sealed record CookingNetworkCancelResult(bool Accepted, CookingNetworkCancelReason Reason,
    IReadOnlyList<CookingNetworkCallerCancellation> CancelledCallers);
public sealed record CookingNetworkOwnerFrameResult(bool Accepted, CookingNetworkFrameReason Reason,
    IReadOnlyList<CookingNetworkAdmission> Admissions,
    IReadOnlyList<CookingNetworkDisposition> Dispositions,
    IReadOnlyList<CookingNetworkCallerCancellation> CancelledCallers,
    CookingNetworkCaptureResult Capture);
public sealed record CookingNetworkOwnerControl(CookingNetworkControlKind Kind, string RequestId);
public sealed record CookingNetworkControlResult(bool Accepted, CookingNetworkControlReason Reason,
    CookingLevelScope Scope, IReadOnlyList<CookingNetworkDisposition> Dispositions,
    CookingNetworkCaptureResult Capture);

/// <summary>Called exclusively by the application's fixed owner scheduler, never by a socket callback.</summary>
public interface ICookingNetworkAuthorityPort
{
    CookingNetworkCaptureResult CaptureFullState();
    CookingNetworkCancelResult CancelSources(IReadOnlyList<CookingNetworkCancellation> sources);
    CookingNetworkOwnerFrameResult ConsumeFrame(IReadOnlyList<CookingNetworkMappedCommand> commands,
        IReadOnlyList<PlayerId> disconnectedParticipants);
    CookingNetworkControlResult ApplyControl(CookingNetworkOwnerControl control);
}
