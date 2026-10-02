namespace AbilityKit.Game.Cooking.EtRuntime;

/// <summary>Owner-thread composition of the existing ET host; it has no independent simulation or clock.</summary>
public sealed class CookingNetworkAuthorityAdapter : ICookingNetworkAuthorityPort
{
    private readonly CookingLevelEtHost _host;
    private readonly Func<string, CookingLevelHostOperationResult>? _authorizedTransition;
    private readonly HashSet<PlayerId> _pendingCleanup = new();
    private readonly string _cleanupNamespace = "server-cleanup-" + Guid.NewGuid().ToString("N") + "-";
    private long _cleanupSequence;

    public CookingNetworkAuthorityAdapter(CookingLevelEtHost host,
        Func<string, CookingLevelHostOperationResult>? authorizedTransition = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _authorizedTransition = authorizedTransition;
        host.EnableNetworkAuthorityOrdering();
    }

    public CookingNetworkCaptureResult CaptureFullState() => _host.CaptureReadOnlyFullState();
    public CookingNetworkCancelResult CancelSources(IReadOnlyList<CookingNetworkCancellation> sources) => _host.CancelPendingSources(sources);

    public CookingNetworkOwnerFrameResult ConsumeFrame(IReadOnlyList<CookingNetworkMappedCommand> commands,
        IReadOnlyList<PlayerId> disconnectedParticipants)
    {
        ArgumentNullException.ThrowIfNull(commands); ArgumentNullException.ThrowIfNull(disconnectedParticipants);
        var capture = CaptureFullState();
        if (!capture.Accepted) return FrameFailure(Enum.Parse<CookingNetworkFrameReason>(capture.Reason.ToString()), capture);
        var state = capture.State!;
        if (state.Observation.Lifecycle.State == CookingLevelState.Created)
            return ConsumeCreatedFrame(commands, disconnectedParticipants, capture);
        var players = state.Observation.Players.Select(p => p.Id).ToHashSet();
        if (disconnectedParticipants.Any(p => !players.Contains(p)))
            return FrameFailure(CookingNetworkFrameReason.InvalidLifecycle, capture);
        if (state.Observation.Lifecycle.State == CookingLevelState.Paused)
        {
            foreach (var player in disconnectedParticipants) _pendingCleanup.Add(player);
            return FrameFailure(CookingNetworkFrameReason.InvalidLifecycle, capture);
        }

        // A frame may contain retransmissions, but all newly pending envelopes must share one owner batch.
        var batches = commands.Select(c => c.Envelope.Command.SimulationBatch).Distinct().ToArray();
        if (batches.Length > 1 || commands.Any(c => c.AuthorityOrdinal <= 0))
            return FrameFailure(CookingNetworkFrameReason.InvalidLifecycle, capture);
        long batch;
        try { batch = batches.Length == 1 ? batches[0] : checked(Math.Max(state.Observation.HostFrameSequence, state.LastCommittedSimulationBatch) + 1); }
        catch (OverflowException) { return FrameFailure(CookingNetworkFrameReason.ArithmeticOverflow, capture); }
        // Stage cleanup before changing the adapter's pending set or sequence.
        var candidates = _pendingCleanup.Concat(disconnectedParticipants).Distinct().ToArray();
        var sequence = _cleanupSequence;
        CookingNetworkMappedCommand[] cleanup;
        try { cleanup = CreateCleanup(state, batch, candidates, ref sequence); }
        catch (OverflowException) { return FrameFailure(CookingNetworkFrameReason.ArithmeticOverflow, capture); }
        foreach (var player in disconnectedParticipants) _pendingCleanup.Add(player);
        _cleanupSequence = sequence;
        var admissions = new List<CookingNetworkAdmission>();
        var dispositions = new List<CookingNetworkDisposition>();
        foreach (var command in cleanup.Concat(commands))
        {
            if (!cleanup.Contains(command) && _pendingCleanup.Contains(command.Envelope.Command.Player))
            {
                var blocked = command.Envelope;
                admissions.Add(new(blocked.LevelScope, blocked.Command.Player, blocked.Command.Command,
                    blocked.SourceConnectionId, blocked.CorrelationId, false, CookingNetworkAdmissionReason.Unauthorized));
                continue;
            }
            var admission = _host.TryEnqueueNetwork(command, serverCleanup: cleanup.Contains(command));
            var e = command.Envelope;
            admissions.Add(new(e.LevelScope, e.Command.Player, e.Command.Command, e.SourceConnectionId, e.CorrelationId,
                admission.Accepted, Enum.Parse<CookingNetworkAdmissionReason>(admission.Reason.ToString()),
                admission.TerminalDisposition is null ? null : Map(admission.TerminalDisposition)));
            if (admission.TerminalDisposition is { } immediate) dispositions.Add(Map(immediate));
            dispositions.AddRange(_host.DrainNewTerminalDispositions().Select(Map));
        }
        try
        {
            var frame = _host.Tick();
            dispositions.AddRange(_host.DrainNewTerminalDispositions().Select(Map));
            // Domain failures are real dispositions, not successful cleanup. Retry only on a later owner frame.
            foreach (var player in _pendingCleanup.ToArray())
                if (cleanup.Where(c => c.Envelope.Command.Player == player).All(c => dispositions.Any(d => d.CommandId == c.Envelope.Command.Command && d.Result?.Outcome == CookingRecipeOutcome.Accepted)))
                    _pendingCleanup.Remove(player);
            var cleanupRejected = _pendingCleanup.Count != 0;
            return new(frame.Accepted && !cleanupRejected, cleanupRejected ? CookingNetworkFrameReason.CleanupRejected
                : frame.Accepted ? CookingNetworkFrameReason.None : CookingNetworkFrameReason.InvalidLifecycle,
                Array.AsReadOnly(admissions.ToArray()), Unique(dispositions), Array.Empty<CookingNetworkCallerCancellation>(), CaptureFullState());
        }
        catch (Exception) when (_host.IsFaulted)
        {
            dispositions.AddRange(_host.DrainNewTerminalDispositions().Select(Map));
            return new(false, CookingNetworkFrameReason.AuthorityFaulted, Array.AsReadOnly(admissions.ToArray()),
                Unique(dispositions), Array.Empty<CookingNetworkCallerCancellation>(), CaptureFullState());
        }
    }

    private CookingNetworkOwnerFrameResult ConsumeCreatedFrame(IReadOnlyList<CookingNetworkMappedCommand> commands,
        IReadOnlyList<PlayerId> disconnectedParticipants, CookingNetworkCaptureResult capture)
    {
        var state = capture.State!;
        // No kitchen means no domain workers or claims exist; the caller is the trusted Session owner.
        // Initialized Created kitchens still validate the real roster and never silently release live work.
        var players = state.Observation.Players.Select(p => p.Id).ToHashSet();
        if (state.FullRecipe is not null && disconnectedParticipants.Any(p => !players.Contains(p)))
            return FrameFailure(CookingNetworkFrameReason.InvalidLifecycle, capture);
        var candidates = _pendingCleanup.Concat(disconnectedParticipants).Distinct().ToHashSet();
        if ((state.FullRecipe?.Processes.Any(p => p.ActiveWorker is { } worker && candidates.Contains(worker)) ?? false) ||
            (state.FullFront?.State.Work.Any(w => w.Status == CookingFrontWorkStatus.Working && w.Player is { } player && candidates.Contains(player)) ?? false))
        {
            foreach (var participant in disconnectedParticipants) _pendingCleanup.Add(participant);
            return FrameFailure(CookingNetworkFrameReason.CleanupRejected, capture);
        }
        foreach (var participant in candidates) _pendingCleanup.Remove(participant);
        var admissions = new List<CookingNetworkAdmission>();
        foreach (var command in commands)
        {
            // Preserve the Host's existing Created admission rejection, without calling Tick.
            var admission = _host.TryEnqueueNetwork(command);
            var e = command.Envelope;
            admissions.Add(new(e.LevelScope, e.Command.Player, e.Command.Command, e.SourceConnectionId, e.CorrelationId,
                admission.Accepted, Enum.Parse<CookingNetworkAdmissionReason>(admission.Reason.ToString())));
        }
        return new(commands.Count == 0, commands.Count == 0 ? CookingNetworkFrameReason.None : CookingNetworkFrameReason.InvalidLifecycle,
            Array.AsReadOnly(admissions.ToArray()), Array.Empty<CookingNetworkDisposition>(),
            Array.Empty<CookingNetworkCallerCancellation>(), capture);
    }

    private CookingNetworkMappedCommand[] CreateCleanup(CookingNetworkAuthorityCapture state, long batch,
        IReadOnlyList<PlayerId> players, ref long sequence)
    {
        var result = new List<CookingNetworkMappedCommand>();
        foreach (var player in players.OrderBy(p => p.Value, StringComparer.Ordinal))
        {
            foreach (var process in state.FullRecipe?.Processes.Where(p => p.ActiveWorker == player) ?? Enumerable.Empty<CookingRecipeCheckpointProcess>())
                result.Add(Cleanup(player, batch, state.Observation.Scope, CookingRecipeOperation.StopProcess, process.Id, null, checked(++sequence)));
            foreach (var work in state.FullFront?.State.Work.Where(w => w.Player == player && w.Status == CookingFrontWorkStatus.Working) ?? Enumerable.Empty<CookingFrontWorkSnapshot>())
                result.Add(Cleanup(player, batch, state.Observation.Scope, CookingRecipeOperation.StopFrontWork, null, work.Id, checked(++sequence)));
        }
        return result.ToArray();
    }

    private CookingNetworkMappedCommand Cleanup(PlayerId player, long batch, CookingLevelScope scope,
        CookingRecipeOperation operation, ProcessId? process, string? work, long sequence)
    {
        var command = new CookingRecipeCommand(scope.MatchScope, batch, player, new(_cleanupNamespace + sequence), operation,
            Process: process, WorldAnchor: work);
        return new(new(scope, command, "server-cleanup", "cleanup-" + sequence), 1, "trusted-cleanup");
    }

    public CookingNetworkControlResult ApplyControl(CookingNetworkOwnerControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        var capture = CaptureFullState();
        if (!capture.Accepted)
            return new(false, Enum.Parse<CookingNetworkControlReason>(capture.Reason.ToString()), _host.Binding.LevelScope,
                Array.Empty<CookingNetworkDisposition>(), capture);
        if (!Enum.IsDefined(control.Kind) || string.IsNullOrWhiteSpace(control.RequestId))
            return new(false, CookingNetworkControlReason.InvalidRequest, _host.Binding.LevelScope, Array.Empty<CookingNetworkDisposition>(), capture);
        CookingLevelHostOperationResult? result = control.Kind switch
        {
            CookingNetworkControlKind.Pause => _host.Pause(),
            CookingNetworkControlKind.Resume => _host.Resume(),
            CookingNetworkControlKind.TryFinishService => _host.TryFinishService(),
            CookingNetworkControlKind.ExecuteAuthorizedTransition => _authorizedTransition?.Invoke(control.RequestId),
            _ => null,
        };
        return new(result?.Accepted == true, result is null ? CookingNetworkControlReason.Unauthorized
            : result.Accepted ? CookingNetworkControlReason.None : CookingNetworkControlReason.InvalidLifecycle,
            _host.Binding.LevelScope, result is null ? Array.Empty<CookingNetworkDisposition>() : Array.AsReadOnly(result.Dispositions.Select(Map).ToArray()), CaptureFullState());
    }

    private static CookingNetworkOwnerFrameResult FrameFailure(CookingNetworkFrameReason reason, CookingNetworkCaptureResult capture) =>
        new(false, reason, Array.Empty<CookingNetworkAdmission>(), Array.Empty<CookingNetworkDisposition>(), Array.Empty<CookingNetworkCallerCancellation>(), capture);
    private static IReadOnlyList<CookingNetworkDisposition> Unique(IEnumerable<CookingNetworkDisposition> values) =>
        Array.AsReadOnly(values.DistinctBy(d => (d.SourceConnectionId, d.CorrelationId)).ToArray());
    private static CookingNetworkDisposition Map(CookingLevelPendingDisposition disposition)
    {
        var e = disposition.Envelope;
        return new(e.LevelScope, e.Command.Player, e.Command.Command, e.SourceConnectionId, e.CorrelationId,
            Enum.Parse<CookingNetworkDispositionKind>(disposition.Kind.ToString()), disposition.Reason, disposition.Result);
    }
}
