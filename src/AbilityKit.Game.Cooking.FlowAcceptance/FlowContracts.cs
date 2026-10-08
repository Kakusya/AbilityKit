using AbilityKit.Game.Cooking;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

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
    SourceStamp Source,
    FlowInvocation ActualInvocation,
    FlowFailure FirstFailure,
    IReadOnlyList<FlowObservation> RelevantCuts,
    IReadOnlyList<LoggedEvent> EventWindow,
    string ReproduceCommand, CleanupResult Cleanup);

public interface IFlowOrchestrator
{
    Task<FlowResult> RunAsync(
        FlowRequest request,
        FlowRunOptions options,
        CancellationToken cancellationToken);
}

public sealed record FlowInvocation(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public enum FlowDiagnosticFault
{
    None,
    FailAfterStart
}

public sealed record FlowRunOptions(
    FlowInvocation ActualInvocation,
    FlowDiagnosticFault DiagnosticFault = FlowDiagnosticFault.None);

public interface IFlowReportWriter
{
    Task PublishAsync(
        string runDirectory, FlowResult result, FailurePack? failure,
        CancellationToken publishToken);
}

public enum RoleControlKind { Initialize, ArmAction, ReleaseBarrier, Observe, Stop }
public enum RoleReplyKind { Ready, Armed, Event, Fault, Stopped }

public sealed record RoleInit(
    string RoleId, string? ActorId, string? Endpoint,
    FixtureSpec? Fixture, FlowBudgets Budgets, FlowLogLimits Logs);

public sealed record RoleControl(
    int SchemaVersion, RunIdentity Run, string RoleId, long ControlSequence, string ControlId,
    RoleControlKind Kind, RoleInit? Init, FlowAction? Action, string? ReplayOfCallId,
    string? BarrierId, string? ObservationPoint);

public sealed record RoleReply(
    int SchemaVersion, RunIdentity Run, string RoleId, long HostSequence, string? ControlId,
    RoleReplyKind Kind, FlowEvent? Event, string? BoundEndpoint, string? ErrorCode);

// One test JSON boundary, unrelated to the product wire schema.
public static class FlowJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static T Read<T>(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 65536) throw new InvalidDataException("Test JSON exceeds 64 KiB.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        CheckKeys(document.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("Null test JSON.");
    }

    private static void CheckKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in element.EnumerateObject())
            {
                if (!names.Add(field.Name)) throw new InvalidDataException("Duplicate JSON field.");
                CheckKeys(field.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CheckKeys(child);
    }

    public static FlowRequest ReadRequest(string path)
    {
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Request exceeds 64 KiB.");
        return Read<FlowRequest>(File.ReadAllText(path));
    }

    public static void Validate(FlowRequest request)
    {
        if (request is null || request.SchemaVersion != 1 || request.FlowVersion != 1 || !Enum.IsDefined(request.Mode) ||
            request.FlowId is not ("compete-one-item" or "pickup-drop-one-item"))
            throw new InvalidDataException("Unsupported schema, mode or registered flow version.");
        RequireId(request.RequestId);
        var f = request.Fixture ?? throw new InvalidDataException("Missing fixture.");
        var registered = FlowFixture.Spec;
        if (f.Id != registered.Id || f.Version != registered.Version || f.ItemId != registered.ItemId ||
            f.InitialSlot != registered.InitialSlot || f.DropSlot != registered.DropSlot || f.Actors is null ||
            !f.Actors.SequenceEqual(registered.Actors)) throw new InvalidDataException("Unregistered fixed fixture.");
        var b = request.Budgets ?? throw new InvalidDataException("Missing budgets.");
        if (b.StartupMs != 10000 || b.StepMs != 10000 || b.ConvergenceMs is < 1000 or > 10000 ||
            b.OverallMs != 120000 || b.ResetMs != 10000 || b.PublishMs != 2000)
            throw new InvalidDataException("Budgets outside approved parameters.");
        var l = request.Logs ?? throw new InvalidDataException("Missing log limits.");
        if (l != new FlowLogLimits(256, 65536, 4096, 8388608, 262144)) throw new InvalidDataException("Log limits outside approved parameters.");
        var ruleCount = request.Mode == FlowMode.Offline ? 4 : 5;
        if (request.Rules is null || request.Rules.Count != ruleCount || request.Rules.Any(r => r is null) ||
            request.Rules.Select(r => r.Id).Distinct().Count() != ruleCount) throw new InvalidDataException("Missing or duplicate rule references.");
        if (string.IsNullOrWhiteSpace(request.OutputRoot) || request.OutputRoot.Length > 1024)
            throw new InvalidDataException("Missing or oversized output directory.");
        var path = Path.GetFullPath(request.OutputRoot);
        if (File.Exists(path) || Directory.Exists(path)) throw new InvalidDataException("Old output directory cannot be reused.");
    }

    internal static void RequireId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new InvalidDataException("Invalid stable test identifier.");
    }
}
