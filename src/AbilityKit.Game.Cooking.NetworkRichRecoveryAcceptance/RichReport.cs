using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.RichEvidence;

public sealed record RichContainerRule(DefinitionId Definition, int Capacity, IReadOnlyList<DefinitionId> Accepted, bool Disposable);
public sealed record ArtifactHash(string RelativePath, string Sha256);
public sealed record LocalProcessReceipt(int Pid, DateTimeOffset StartUtc, int? ExitCode, string ExecutableSha256);
public sealed record RichProvenance(string SourceHead, string DirtyState, IReadOnlyList<ArtifactHash> Files, Guid EtMvid, Guid SessionMvid, Guid WireCodecMvid, string OriginalMenuHash, string FixtureCatalogHash, CookingConfigurationIdentity ContentIdentity, string RolePolicyHash, string LinkedFixtureHash, string LinkedPlannerHash, string? FrontConfigurationIdentity, string? PreparationConfigurationIdentity, string? InstalledLayoutHash, string Runtime, string OS);
public sealed record RichRolePolicy(PlayerId OriginalWorker, PlayerId TakeoverWorker, PlayerId DrinkProducer, PlayerId DrinkDelivery, PlayerId SaladProducer, PlayerId SaladDelivery);
public sealed record RichBudget(int WholeMs, int InitialMs, int OperationMs, int CutMs, int OwnerDelayMs, long ElapsedMs, long Commands, long Frames);
public sealed record RichCapture(string Id, long Timestamp, string BusinessHash, string RecipeCanonicalHash, string? FrontCanonicalHash, CookingNetworkAuthorityCapture Capture);
public sealed record RichCallerCheckpoint(string Id, PlayerId Participant, CookingNetworkBaseline Baseline, CookingNetworkBaseline GrantedBaseline, CookingNetworkBaselineIdentity ExactAckSent, CookingNetworkBaselineIdentity ActualReady, long BaselineReceiveOrdinal, long AckSendOrdinal, long ReadyReceiveOrdinal, string? PendingStable);
public sealed record RichWireInput(long Ordinal, long Timestamp, string Channel, string Endpoint, string Correlation, CookingNetworkMessageKind Kind, CookingNetworkWireCommand? Command, CookingNetworkBaselineIdentity? Ack);
public sealed record RichWireReply(string Channel, string Correlation, CookingNetworkWireResult Result, string BytesHash);
public sealed record RichIssued(string Channel, CookingNetworkBaselineIdentity Identity, string BusinessHash);
public sealed record RichWireGrant(string Channel, CookingNetworkBaselineIdentity Identity);
public sealed record RichClose(string Channel, long Timestamp);
public sealed record RichCallerCommand(string Correlation, CookingNetworkWireCommand Wire, long SentOrdinal);
public sealed record RichCallerOutcome(string Correlation, CookingNetworkWireResult Result, string BytesHash, long ReceivedOrdinal);
public sealed record RichDroppedReply(string Correlation, CookingNetworkWireResult Diagnostic, string BytesHash, int Count, bool OriginalWaiterCompleted);
public sealed record RichPhase(int Ordinal, string Name, long Timestamp, string? MarkerStable, string? MarkerDomain, RichCapture? Capture, RichCallerCheckpoint? Caller);
public sealed record RichConsumption(string BeforeId, string AfterId, CookingNetworkOwnerFrameResult Frame);
public sealed record RichCut(
    string CaseId, bool InjectedExactlyOnce, CookingLevelState LifecycleAtCut, PlayerId OriginalParticipant, PlayerId TakeoverParticipant,
    long OriginalGeneration, long RecoveredGeneration, string ServerInstance, ProcessId? Process, ItemId? Item, ItemId? Container,
    IReadOnlyList<RichCapture> Snapshots, IReadOnlyList<RichCallerCheckpoint> CallerSnapshots,
    IReadOnlyList<RichConsumption> Frames, IReadOnlyList<CookingNetworkControlResult> Controls,
    RichClose? Close, CookingNetworkWireCommand? OriginalWire, RichWireReply? OriginalReply,
    RichCallerCommand? RetryWire, RichCallerOutcome? RetryOutcome, RichDroppedReply? Dropped,
    long? ManualElapsed, int OwnerHoldFrames, string? CompletionMarker, RichIssued? PreCutIssued, RichWireInput? PreCutAck, RichIssued? CommittedIssued, CookingNetworkSessionProjection? PausedPendingProjection, CookingNetworkBaseline? CommittedCallerImage);
public sealed record RichEnded(RichCapture State, CookingNetworkControlResult EndControl, int Deliveries, int Unmet, int Stars, bool Cleared);
public sealed record RichSuccessor(CookingLevelScope Source, CookingLevelScope Target, CookingNetworkControlResult Transition,
    IReadOnlyList<ArtifactHash> StoreFiles, bool ActualReadAccepted, JsonElement SavedPayload, RichCapture State, string CarryPolicy);
public sealed record RichFinal(string BusinessHash, string ServerInstance, CookingLevelScope Scope, CookingNetworkSessionProjection PreCloseProjection,
    CookingNetworkBaseline LocalBaseline, CookingNetworkBaselineIdentity RemoteIssued, CookingNetworkBaselineIdentity RemoteAck,
    CookingNetworkBaselineIdentity RemoteReady, bool ExactCurrentReady, RichClose? CurrentClose, int LiveHoldMs, long FrozenTimestamp, bool CurrentChannelLive);
public sealed record RichEndpointReport(int SchemaVersion, string Suite, string Role, string RunId, string CaseId, bool Passed, string Stage, string? Failure,
    LocalProcessReceipt Process, string Topology, string Endpoint, int Protocol, int LevelFormat, int RecipeSchema,
    RichProvenance Provenance, RichRolePolicy Roles, RichBudget Budget, IReadOnlyList<RichPhase> Phases, RichCut? Cut, RichEnded? Ended,
    RichSuccessor? Successor, RichFinal? Final, IReadOnlyList<RichWireInput> ObservedIngress, IReadOnlyList<RichWireReply> ObservedReplies,
    IReadOnlyList<RichCallerCommand> CallerCommands, IReadOnlyList<RichCallerOutcome> CallerOutcomes, RichDroppedReply? DroppedReply, IReadOnlyList<RichIssued> ObservedIssued, IReadOnlyList<RichWireGrant> ObservedReady, IReadOnlyList<RichClose> ObservedClose, IReadOnlyList<CookingNetworkBaselineIdentity> CallerReady, IReadOnlyList<RichContainerRule> ContainerRules, IReadOnlyList<ArtifactHash> CanonicalFiles);
