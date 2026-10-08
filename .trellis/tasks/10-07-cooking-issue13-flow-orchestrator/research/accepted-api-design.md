Current contract: apply [accepted-api-amendment](accepted-api-amendment.md) first for FailurePack/RunAsync/RoleControl and new invocation/options; original blocks below are retained as S0 design history.

# Accepted proposed test APIs (not implemented)

Source: AK-I13-S0-PLAN-20261007-01 / a8ea630ca1fd29fcf0327556f7847e13f589c218; full original [reply](dot-plan-reply-raw.txt). Signatures below are verbatim dot proposals, not main replacements.

## Block 1

```csharp
public enum FlowMode { Offline, Network }
public enum FlowVerb { Pickup, Drop }

public sealed record RuleRef(string Id, string Version);

public sealed record FixtureSpec(
    string Id, string Version,
    IReadOnlyList<string> Actors,
    string ItemId, string InitialSlot, string DropSlot);

public sealed record FlowBudgets(
    int StartupMs, int StepMs, int ConvergenceMs,
    int OverallMs, int ResetMs, int PublishMs);

public sealed record FlowLogLimits(
    int QueueCapacity, int MaxEventBytes,
    int MaxEvents, long MaxEventLogBytes, int DiagnosticBytesPerHost);

public sealed record FlowRequest(
    int SchemaVersion, string RequestId,
    string FlowId, int FlowVersion, FlowMode Mode,
    FixtureSpec Fixture, IReadOnlyList<RuleRef> Rules,
    int? Seed, FlowBudgets Budgets, FlowLogLimits Logs,
    string OutputRoot);

public sealed record RunIdentity(
    string RequestId, string RunId, string AttemptId,
    long RunGeneration);

public sealed record ApprovedRule(
    RuleRef Rule, string ApprovalRef,
    IReadOnlyDictionary<string, string> Parameters);

public interface IFlowRuleCatalog
{
    IReadOnlyList<ApprovedRule> ResolveApproved(
        IReadOnlyList<RuleRef> requested, FlowMode mode);
}
```

## Block 2

```csharp
public sealed record FlowAction(
    string CallId, string BusinessId, string ActorId,
    FlowVerb Verb, string ItemId, int ExpectedItemVersion,
    string? StationSlot);

public enum CallCompletion
{
    NotAdmitted, Terminal, ProtocolRejected, Unknown
}

public sealed record CommandObservation(
    string StepId, string CallId, string BusinessId, string ActorId,
    CookingRecipeCommand FrozenCommand,
    bool? AdmissionAccepted, string? AdmissionReason,
    string? NativeDisposition, string? DomainCommandId,
    string? WireCorrelationId, string? WireReason,
    CookingRecipeCommandResult? BusinessResult,
    CallCompletion Completion, string? ErrorCode);

public sealed record DispatchResult(
    bool Complete,
    IReadOnlyList<CommandObservation> Outcomes,
    string? ErrorCode);
```

## Block 3

```csharp
public enum ObservationOrigin { Authority, Client }
public enum HandEvidence { DomainHandIndex, ClientProjection }

public sealed record BindingFence(
    long RunGeneration, CookingLevelScope Scope,
    string? ServerSessionInstance, long? ConnectionGeneration);

public sealed record ItemProbe(
    string ItemId, int Version, bool Removed,
    string LocationKind, string? OwnerId, string? SlotId);

public sealed record HandProbe(
    string ActorId, string? ItemId, HandEvidence Evidence);

public sealed record FlowObservation(
    string ObserverId, ObservationOrigin Origin,
    bool Available, string? ErrorCode,
    BindingFence Fence,
    long? StateVersion, long? HostFrame, long? LogicalTick,
    long? BaselineSequence, bool? SynchronizedObserved,
    IReadOnlyList<ItemProbe> Items,
    IReadOnlyList<HandProbe> Hands,
    IReadOnlyList<CookingRecipeEvent> CommandEvents);

public sealed record ProjectionFence(
    BindingFence Authority,
    IReadOnlyList<string> RequiredClients,
    long MinimumStateVersion,
    IReadOnlyList<ItemProbe> ExpectedItems,
    IReadOnlyList<HandProbe> ExpectedHands);

public sealed record ConvergenceResult(
    bool Reached,
    IReadOnlyList<FlowObservation> LastObservations,
    string? ErrorCode);
```

## Block 4

```csharp
public interface IFlowSessionFactory
{
    IFlowSession Create(FlowMode mode);
}

public interface IFlowSession
{
    ValueTask<FlowObservation> StartAsync(
        FlowRequest request, RunIdentity run,
        IFlowEventSink events, CancellationToken cancellationToken);

    ValueTask<DispatchResult> DispatchGroupAsync(
        string stepId, IReadOnlyList<FlowAction> actions,
        int timeoutMs, CancellationToken cancellationToken);

    ValueTask<DispatchResult> ReplayAsync(
        string stepId, string originalCallId, string newCallId,
        int timeoutMs, CancellationToken cancellationToken);

    ValueTask<FlowObservation> CaptureAsync(
        string observerId, CancellationToken cancellationToken);

    ValueTask<ConvergenceResult> WaitForProjectionAsync(
        ProjectionFence expected, int timeoutMs,
        CancellationToken cancellationToken);

    ValueTask<CleanupResult> CloseAsync(
        int timeoutMs, CancellationToken cleanupToken);
}
```

## Block 5

```csharp
public enum FlowStatus { Passed, Failed, Blocked, Skipped, NotRun }
public enum FlowVerdict { Passed, Failed, Undetermined }
public enum FailureKind { ProductFailure, HarnessOrEnvironmentFailure, Finding }
public enum CleanupState { Complete, Incomplete, NotNeeded }
public enum FlowEventKind
{
    RoleReady, StepStarted, CommandObserved, StateObserved,
    RuleChecked, RoleFaulted, RoleStopped
}

public sealed record RuleCheck(
    RuleRef Rule, FlowVerdict Verdict,
    string StepId, string Expected, string Actual,
    IReadOnlyList<string> EvidenceRefs);

public sealed record FlowEvent(
    int SchemaVersion, string RunId, string HostId,
    long RunGeneration, long HostSequence,
    string StepId, string? CallId, FlowEventKind Kind,
    long? GameTick,
    CommandObservation? Command,
    FlowObservation? Observation,
    RuleCheck? Check,
    string? ErrorCode, string? Detail);

public sealed record LoggedEvent(
    long CollectorSequence, DateTimeOffset ObservedUtc, FlowEvent Event);

public interface IFlowEventSink
{
    bool TryPublish(FlowEvent value);
    bool EvidenceComplete { get; }
}

public sealed record FlowEvidence(
    IReadOnlyList<CommandObservation> Commands,
    IReadOnlyDictionary<string, FlowObservation> Cuts,
    bool RequiredEventsComplete);

public interface IFlowRule
{
    RuleRef Identity { get; }
    RuleCheck Evaluate(FlowEvidence evidence, ApprovedRule approval);
}

public sealed record ResourceOutcome(
    string RoleId, int? ProcessId, bool ExitConfirmed, string? ErrorCode);

public sealed record CleanupResult(
    CleanupState State,
    IReadOnlyList<ResourceOutcome> Resources,
    IReadOnlyList<string> Errors);

public sealed record FlowFailure(
    FailureKind Kind, string Code, string StepId, string? CallId,
    string Expected, string Actual,
    IReadOnlyList<string> EvidenceRefs);

public sealed record SourceStamp(
    string Commit, bool Dirty, string Configuration, string ToolVersion);

public sealed record FlowTiming(
    long? BuildMs, long StartupMs, long ExecuteMs,
    long ResetMs, long TotalMs);

public sealed record FlowResult(
    int SchemaVersion, RunIdentity Run, SourceStamp Source,
    string FlowId, int FlowVersion, FlowMode Mode,
    FlowStatus Status, bool ExecutionComplete, FlowVerdict Verdict,
    bool EvidenceComplete, CleanupResult Cleanup,
    IReadOnlyList<RuleCheck> Checks,
    IReadOnlyList<string> CompletedGoals,
    IReadOnlyList<string> UnverifiedGoals,
    IReadOnlyList<FlowFailure> Failures,
    FlowTiming Timing);

public sealed record FailurePack(
    RunIdentity Run, FlowRequest Request,
    FlowFailure FirstFailure,
    IReadOnlyList<FlowObservation> RelevantCuts,
    IReadOnlyList<LoggedEvent> EventWindow,
    string ReproduceCommand, CleanupResult Cleanup);

public interface IFlowOrchestrator
{
    Task<FlowResult> RunAsync(
        FlowRequest request, CancellationToken cancellationToken);
}

public interface IFlowReportWriter
{
    Task PublishAsync(
        string runDirectory, FlowResult result, FailurePack? failure,
        CancellationToken publishToken);
}
```

## Block 6

```csharp
var initial = await session.StartAsync(request, run, events, ct);
// 从 initial 选择已验证的目标版本；A/B 必须空手且合法可达。
var actions = new[]
{
    new FlowAction("call-a", "pickup-a", "A",
        FlowVerb.Pickup, itemId, initialItemVersion, null),
    new FlowAction("call-b", "pickup-b", "B",
        FlowVerb.Pickup, itemId, initialItemVersion, null)
};
var pair = await session.DispatchGroupAsync(
    "compete", actions, request.Budgets.StepMs, ct);
var authority = await session.CaptureAsync("authority", ct);
// 先检查 pair 的完整性与正式业务结果，再根据 Accepted 结果选胜者。
// network 在这里等待 pickup 目标投影收敛，不能拖到 Drop 之后补证明。
var replay = await session.ReplayAsync(
    "retry-winner", winningCallId, "call-retry",
    request.Budgets.StepMs, ct);
var afterRetry = await session.CaptureAsync("authority", ct);
// finally 使用独立 cleanupToken 调用 CloseAsync，不能复用已取消 ct。
```

## Block 7

```csharp
public enum RoleControlKind { Initialize, ArmAction, ReleaseBarrier, Observe, Stop }
public enum RoleReplyKind { Ready, Armed, Event, Fault, Stopped }

public sealed record RoleInit(
    string RoleId, string? ActorId, string? Endpoint,
    FixtureSpec? Fixture, FlowBudgets Budgets, FlowLogLimits Logs);

public sealed record RoleControl(
    int SchemaVersion, RunIdentity Run,
    string RoleId, long ControlSequence, string ControlId,
    RoleControlKind Kind,
    RoleInit? Init, FlowAction? Action,
    string? BarrierId, string? ObservationPoint);

public sealed record RoleReply(
    int SchemaVersion, RunIdentity Run,
    string RoleId, long HostSequence, string? ControlId,
    RoleReplyKind Kind, FlowEvent? Event,
    string? BoundEndpoint, string? ErrorCode);
```

## Explicit role protocol addition

；Replay 仍由同一 adapter 保存的冻结命令生成受支持的发送指令。实施时可以在 ArmAction 的已定义 payload 中区分新动作与原 call 的精确重放，但不能由主会话自行补一套协议：下述补充字段固定为 RoleControl 新增 string? ReplayOfCallId。Action 与 ReplayOfCallId 二选一，Replay 必须属于同角色、当前 run/scope 的已知调用；未知或两者同时提供则拒绝。

RoleControl includes `string? ReplayOfCallId` as explicitly required by dot; Action and replay are mutually exclusive.
