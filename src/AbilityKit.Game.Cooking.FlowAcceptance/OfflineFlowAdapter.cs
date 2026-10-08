using System.Diagnostics;
using AbilityKit.Game.Cooking.EtRuntime;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

public sealed class OfflineFlowSessionFactory : IFlowSessionFactory
{
    public IFlowSession Create(FlowMode mode) => mode == FlowMode.Offline
        ? new OfflineFlowAdapter() : throw new NotSupportedException("Network is NotRun in S1.");
}

public sealed class OfflineFlowAdapter : IFlowSession
{
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly Func<FlowRequest, RunIdentity, CookingLevelEtHost>? hostFactory;
    private CookingLevelEtHost? host;
    private RunIdentity? run;
    private FlowRequest? request;
    private IFlowEventSink? events;
    private BindingFence? fence;
    private readonly Dictionary<string, CookingRecipeCommand> calls = new(StringComparer.Ordinal);
    private long sequence;
    private bool started, closing;
    private CleanupResult? cleanup;

    // An assembly/test seam for exercising existing host faults, never a second authority.
    public OfflineFlowAdapter(Func<FlowRequest, RunIdentity, CookingLevelEtHost>? hostFactory = null) => this.hostFactory = hostFactory;

    public ValueTask<FlowObservation> StartAsync(FlowRequest request, RunIdentity run, IFlowEventSink events, CancellationToken cancellationToken)
    {
        CheckOwner();
        cancellationToken.ThrowIfCancellationRequested();
        if (started || closing) throw new InvalidOperationException("Start is allowed once.");
        started = true;
        this.request = request; this.run = run; this.events = events;
        host = hostFactory is null ? new FlowFixture(run).CreateHost() : hostFactory(request, run);
        if (host.Binding.LevelScope.MatchScope.Session.Value != run.RunId)
            throw new InvalidOperationException("Host belongs to another run.");
        fence = new(run.RunGeneration, host.Binding.LevelScope, null, null);
        var initial = Capture("authority");
        Emit(FlowEventKind.RoleReady, "initial", observation: initial);
        return ValueTask.FromResult(initial);
    }

    public ValueTask<DispatchResult> DispatchGroupAsync(string stepId, IReadOnlyList<FlowAction> actions, int timeoutMs, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken);
        if (actions is null || actions.Count is < 1 or > 2 || timeoutMs <= 0) throw new ArgumentException("Invalid bounded action group.");
        FlowJson.RequireId(stepId);
        var callIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            FlowJson.RequireId(action.CallId); FlowJson.RequireId(action.BusinessId);
            if (!callIds.Add(action.CallId) || calls.ContainsKey(action.CallId) || !request!.Fixture.Actors.Contains(action.ActorId) ||
                !Enum.IsDefined(action.Verb) || action.ItemId != request.Fixture.ItemId || action.ExpectedItemVersion <= 0 ||
                (action.Verb == FlowVerb.Pickup ? action.StationSlot is not null : action.StationSlot != request.Fixture.DropSlot))
                throw new ArgumentException("Invalid action, actor, call or slot.");
        }
        var batch = checked(host!.LastCommittedSimulationBatch + 1);
        var commands = actions.Select(a => (a.CallId, Command: new CookingRecipeCommand(fence!.Scope.MatchScope, batch,
            new(a.ActorId), new(a.BusinessId), a.Verb == FlowVerb.Pickup ? CookingRecipeOperation.Pickup : CookingRecipeOperation.Drop,
            Item: new ItemId(a.ItemId), Station: a.StationSlot is null ? null : new StationSlotId(a.StationSlot), ExpectedItemVersion: a.ExpectedItemVersion))).ToArray();
        foreach (var value in commands) calls.Add(value.CallId, value.Command);
        return ValueTask.FromResult(Dispatch(stepId, commands, timeoutMs, cancellationToken));
    }

    public ValueTask<DispatchResult> ReplayAsync(string stepId, string originalCallId, string newCallId, int timeoutMs, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken);
        FlowJson.RequireId(stepId); FlowJson.RequireId(newCallId);
        if (timeoutMs <= 0 || calls.ContainsKey(newCallId) || !calls.TryGetValue(originalCallId, out var command))
            throw new ArgumentException("Replay needs a known current-run call and a new call ID.");
        // Exact immutable payload, including the original batch and item version.
        calls.Add(newCallId, command);
        return ValueTask.FromResult(Dispatch(stepId, new[] { (newCallId, command) }, timeoutMs, cancellationToken));
    }

    private DispatchResult Dispatch(string stepId, IReadOnlyList<(string CallId, CookingRecipeCommand Command)> commands,
        int timeoutMs, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        Emit(FlowEventKind.StepStarted, stepId);
        var admissions = new Dictionary<string, CookingLevelAdmissionResult>();
        var terminals = new Dictionary<string, CookingLevelPendingDisposition>();
        string? error = null;
        // Same owner, all admission attempts before any Tick or await. No disk wait here.
        foreach (var value in commands)
        {
            var admission = host!.TryEnqueue(new(fence!.Scope, value.Command, "offline-authority", value.CallId));
            admissions.Add(value.CallId, admission);
            if (admission.TerminalDisposition is { } terminal) Merge(new[] { terminal });
        }
        try
        {
            while (commands.Any(v => admissions[v.CallId].Accepted && !terminals.ContainsKey(v.CallId)))
            {
                if (token.IsCancellationRequested || clock.ElapsedMilliseconds >= timeoutMs)
                { error = token.IsCancellationRequested ? "CancelledAfterAdmission" : "CommandTimeout"; break; }
                Merge(host!.Tick().Dispositions);
                Merge(host.DispositionHistory);
                if (host.IsFaulted) { error = "FrameFault"; break; }
            }
        }
        catch (Exception e) { error = "FrameFault:" + e.GetType().Name; }
        // Histories remain readable when the host is faulted; notification queues are not the source.
        Merge(host!.DispositionHistory);
        Merge(host.FinalDispositionHistory);
        var outcomes = commands.Select(value =>
        {
            var admission = admissions[value.CallId];
            terminals.TryGetValue(value.CallId, out var terminal);
            return new CommandObservation(stepId, value.CallId, value.Command.Command.Value, value.Command.Player.Value,
                value.Command, admission.Accepted, admission.Reason.ToString(), terminal?.Kind.ToString(),
                terminal?.Envelope.Command.Command.Value, null, null, terminal?.Result is { } result
                    ? result with { Events = Array.AsReadOnly(result.Events.ToArray()) } : null,
                terminal is not null ? CallCompletion.Terminal : !admission.Accepted ? CallCompletion.NotAdmitted : CallCompletion.Unknown,
                terminal is null && admission.Accepted ? error ?? "MissingTerminal" : null);
        }).ToArray();
        foreach (var observation in outcomes) Emit(FlowEventKind.CommandObserved, stepId, observation.CallId, command: observation);
        if (error is not null) Emit(FlowEventKind.RoleFaulted, stepId, error: error);
        return new(error is null && outcomes.All(o => o.Completion != CallCompletion.Unknown), Array.AsReadOnly(outcomes), error);

        void Merge(IEnumerable<CookingLevelPendingDisposition> values)
        {
            foreach (var value in values)
                if (admissions.ContainsKey(value.Envelope.CorrelationId) && value.Envelope.SourceConnectionId == "offline-authority" &&
                    value.Envelope.LevelScope == fence!.Scope)
                    terminals.TryAdd(value.Envelope.CorrelationId, value);
        }
    }

    public ValueTask<FlowObservation> CaptureAsync(string observerId, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken, requireEvidence: false);
        if (observerId != "authority") throw new ArgumentException("Only authority observations exist in S1.");
        var observation = Capture(observerId);
        Emit(FlowEventKind.StateObserved, "capture", observation: observation);
        return ValueTask.FromResult(observation);
    }

    private FlowObservation Capture(string observerId)
    {
        var capture = host!.CaptureReadOnlyFullState();
        if (!capture.Accepted || capture.State is null)
            return new(observerId, ObservationOrigin.Authority, false, capture.Reason.ToString(), fence!, null, null, null,
                null, null, Array.Empty<ItemProbe>(), Array.Empty<HandProbe>(), Array.Empty<CookingRecipeEvent>());
        var observation = capture.State.Observation;
        if (observation.Scope != fence!.Scope || !host.TryPeekBoundKitchen(out var borrowed) || borrowed is null)
            throw new InvalidOperationException("Scope or kitchen unavailable at idle owner boundary.");
        var hands = request!.Fixture.Actors.Select(a => new HandProbe(a, borrowed.ItemInHand(new(a))?.Value,
            HandEvidence.DomainHandIndex)).ToArray();
        var commandEvents = borrowed.EventHistory.ToArray();
        borrowed = null; // Discard the borrowed mutable kitchen; only copied values escape.
        var items = observation.Recipe!.Items.Select(i => new ItemProbe(i.Id.Value, i.Version, false,
            i.Location.Kind.ToString(), i.Location.OwnerId, i.Location.SlotId)).ToArray();
        return new(observerId, ObservationOrigin.Authority, true, null, fence, observation.Recipe.Version,
            observation.HostFrameSequence, observation.Recipe.LogicalTick, null, null,
            Array.AsReadOnly(items), Array.AsReadOnly(hands), Array.AsReadOnly(commandEvents));
    }

    public ValueTask<ConvergenceResult> WaitForProjectionAsync(ProjectionFence expected, int timeoutMs, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken);
        return ValueTask.FromResult(new ConvergenceResult(false, Array.Empty<FlowObservation>(), "NotApplicableOffline"));
    }

    public ValueTask<CleanupResult> CloseAsync(int timeoutMs, CancellationToken cleanupToken)
    {
        CheckOwner();
        if (cleanup is not null) return ValueTask.FromResult(cleanup);
        closing = true; // Retire the run before releasing resources or publishing stop.
        var clock = Stopwatch.StartNew();
        try
        {
            cleanupToken.ThrowIfCancellationRequested();
            host?.Dispose();
            var disposed = host is null || host.Scene.IsDisposed;
            if (!disposed || timeoutMs <= 0 || clock.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("Owner close incomplete.");
            if (run is not null) Emit(FlowEventKind.RoleStopped, "cleanup");
            cleanup = new(CleanupState.Complete, new[] { new ResourceOutcome("authority", null, true, null) }, Array.Empty<string>());
        }
        catch (Exception e)
        {
            cleanup = new(CleanupState.Incomplete, new[] { new ResourceOutcome("authority", null, false, e.GetType().Name) },
                new[] { e.GetType().Name });
        }
        return ValueTask.FromResult(cleanup);
    }

    private void Emit(FlowEventKind kind, string step, string? call = null, CommandObservation? command = null,
        FlowObservation? observation = null, string? error = null) => events!.TryPublish(new(1, run!.RunId, "authority",
        run.RunGeneration, ++sequence, step, call, kind, observation?.LogicalTick, command, observation, null, error, null));

    private void CheckOwner()
    {
        if (Environment.CurrentManagedThreadId != ownerThread) throw new InvalidOperationException("Offline owner thread changed.");
    }

    private void CheckActive(CancellationToken token, bool requireEvidence = true)
    {
        CheckOwner(); token.ThrowIfCancellationRequested();
        if (!started || closing || host is null || run is null || fence is null || fence.RunGeneration != run.RunGeneration ||
            host.Binding.LevelScope != fence.Scope) throw new InvalidOperationException("Run is not live at its original scope.");
        if (requireEvidence && !events!.EvidenceComplete) throw new InvalidOperationException("Evidence incomplete; no new actions allowed.");
    }
}
