using System.Collections.Concurrent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Transport.LiteNet;

namespace AbilityKit.Game.Cooking.Session;

/// <summary>Framed passive client. It never restores or mutates an authority.</summary>
public sealed class CookingNetworkSessionClient : IDisposable
{
    private readonly object _projectionGate = new();
    private readonly PlayerId _participant;
    private readonly string _credential;
    private readonly Func<ITransport> _transportFactory;
    private readonly CookingNetworkSessionOptions _bounds;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CookingNetworkWireResult>> _requests = new();
    private ConnectionManager? _connection;
    private TaskCompletionSource<bool> _ready = NewCompletion();
    private CookingNetworkJoined? _binding;
    private CookingLevelScope? _acceptedScope;
    private string? _authorityUnavailable;
    private long _sequence, _correlation;
    public CookingNetworkBaseline? LatestBaseline { get; private set; }
    public bool IsSynchronized { get; private set; }
    public string? ServerSessionInstance => _binding?.ServerSessionInstance;
    public string? RebindToken => _binding?.RebindToken;
    public CookingNetworkSessionClient(PlayerId participant, string joinCredential, Func<ITransport>? transportFactory = null,
        CookingNetworkSessionOptions? options = null)
    {
        _participant = participant; _credential = joinCredential; _bounds = options ?? new();
        _transportFactory = transportFactory ?? (() => new LiteNetTransport("abilitykit-cooking-v3"));
    }
    private static TaskCompletionSource<bool> NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        _ready = NewCompletion(); IsSynchronized = false;
        _connection = new ConnectionManager(_transportFactory, new ConnectionOptions { EnableReconnect = false,
            MaxFrameLength = _bounds.FrameBytes + 64, FrameCodec = new CookingNetworkFrameCodec(_bounds.FrameBytes + 64) });
        _connection.PacketReceived += OnPacket;
        _connection.ServerPushReceived += (opCode, payload) => OnPacket(opCode, 0, payload);
        _connection.Connected += () => Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(_participant,
            _credential, _binding?.ServerSessionInstance, _binding?.RebindToken));
        _connection.Disconnected += OnDisconnected;
        _connection.Error += _ => OnDisconnected();
        _connection.Open(host, port);
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
    }
    public async Task ReconnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        Disconnect(); _sequence = 0;
        await ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
    }
    private void Send<T>(CookingNetworkMessageKind kind, string correlation, T payload)
    {
        var bytes = CookingNetworkWireCodec.Encode(kind, correlation, payload, _bounds);
        if (bytes.Length > _bounds.FrameBytes) throw new ArgumentException("Wire frame exceeds bound.");
        _connection!.Send(CookingNetworkWireCodec.OpCode, new ArraySegment<byte>(bytes), (ushort)NetworkPacketFlags.ServerPush);
    }
    private void OnPacket(uint opCode, uint sequence, ArraySegment<byte> payload)
    {
        if (opCode != CookingNetworkWireCodec.OpCode || !CookingNetworkWireCodec.TryDecode(payload.AsSpan(), _bounds, out var envelope) || envelope is null) return;
        lock (_projectionGate) {
            switch (envelope.Kind) {
                case CookingNetworkMessageKind.Joined:
                    var joined = CookingNetworkWireCodec.Read<CookingNetworkJoined>(envelope);
                    if (joined is null || joined.Participant != _participant || joined.ConnectionGeneration <= 0) return;
                    if (_binding is not null && _binding.ServerSessionInstance != joined.ServerSessionInstance) { _acceptedScope = null; _authorityUnavailable = null; }
                    _binding = joined; LatestBaseline = null; break;
                case CookingNetworkMessageKind.Baseline:
                    var baseline = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope);
                    if (baseline is null || !TryInstallBaseline(baseline)) return;
                    Send(CookingNetworkMessageKind.BaselineAck, "ack-" + baseline.Identity.SnapshotSequence, baseline.Identity); break;
                case CookingNetworkMessageKind.Ready:
                    var ack = CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope);
                    if (_authorityUnavailable is null && ack is not null && ack == LatestBaseline?.Identity) { IsSynchronized = true; _ready.TrySetResult(true); }
                    break;
                case CookingNetworkMessageKind.CommandResult:
                case CookingNetworkMessageKind.Rejected:
                    var result = CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope);
                    if (result?.Reason is "AuthorityFaulted" or "Disposed" or "Busy" or "FullStateExceedsWireBounds") {
                        _authorityUnavailable = result.Reason; IsSynchronized = false;
                        _ready.TrySetException(new InvalidOperationException(result.Reason));
                        foreach (var entry in _requests.ToArray()) if (_requests.TryRemove(entry.Key, out var waiting)) waiting.TrySetResult(result);
                    }
                    if (result is not null && _requests.TryRemove(envelope.CorrelationId, out var pending)) pending.TrySetResult(result);
                    if (envelope.CorrelationId == "join" && envelope.Kind == CookingNetworkMessageKind.Rejected)
                        _ready.TrySetException(new InvalidOperationException(result?.Reason ?? "Join rejected."));
                    break;
            }
        }
    }
    internal bool TryInstallBaseline(CookingNetworkBaseline? baseline)
    {
        if (_authorityUnavailable is "AuthorityFaulted" or "Disposed" or "FullStateExceedsWireBounds" || baseline is null || _binding is null || baseline.Identity.ServerSessionInstance != _binding.ServerSessionInstance ||
                        baseline.Identity.ConnectionGeneration != _binding.ConnectionGeneration || baseline.Identity.Participant != _participant ||
                        baseline.LevelFormatVersion != CookingLevelCheckpointCodec.CurrentFormatVersion || baseline.RecipeSchemaVersion != 5 ||
                        !ScopesAgree(baseline) ||
                        (_acceptedScope is { } accepted && (baseline.Identity.Scope.MatchScope != accepted.MatchScope ||
                            baseline.Identity.Scope.RestaurantRuntime != accepted.RestaurantRuntime ||
                            baseline.Identity.Scope.LevelEpoch < accepted.LevelEpoch ||
                            (baseline.Identity.Scope != accepted && baseline.Identity.Scope.LevelEpoch <= accepted.LevelEpoch))) ||
                        baseline.Identity.Scope != baseline.State.Observation.Scope || baseline.Identity.Epoch != baseline.Identity.Scope.LevelEpoch ||
                        baseline.Identity.StateHash != CookingNetworkWireCodec.BaselineHash(baseline.State, baseline.Session) ||
                        baseline.Session.ServerSessionInstance != _binding.ServerSessionInstance ||
                        baseline.Session.Participants.Count is < 1 or > 4 ||
                        baseline.Session.Participants.Select(p => p.Participant).Distinct().Count() != baseline.Session.Participants.Count ||
                        !baseline.Session.Participants.Select(p => p.Participant.Value).SequenceEqual(baseline.Session.Participants.Select(p => p.Participant.Value).OrderBy(p => p, StringComparer.Ordinal)) ||
                        baseline.Session.Participants.Any(p => p.ConnectionGeneration < 0 || p.LastValidatedClientSequence < 0 || p.LastTerminalClientSequence < 0 || (p.Ready && !p.ConnectedOwnerBinding)) ||
                        !baseline.Session.Participants.Any(p => p.Participant == _participant && p.ConnectionGeneration == _binding.ConnectionGeneration && p.ConnectedOwnerBinding) ||
                        (LatestBaseline is { } previous && (baseline.Identity.SnapshotSequence <= previous.Identity.SnapshotSequence ||
                            baseline.Identity.Scope.LevelEpoch < previous.Identity.Scope.LevelEpoch))) { IsSynchronized = false; return false; }
        if (LatestBaseline is { } oldBaseline && oldBaseline.Identity.Scope != baseline.Identity.Scope) IsSynchronized = false;
        LatestBaseline = CookingNetworkWireCodec.Freeze(baseline);
        _acceptedScope = baseline.Identity.Scope; _authorityUnavailable = null;
        return true;
    }
    private static bool ScopesAgree(CookingNetworkBaseline baseline)
    {
        var scope = baseline.Identity.Scope; var state = baseline.State;
        return state.Observation.Lifecycle.Scope == scope &&
            (state.Observation.Recipe is null || state.Observation.Recipe.Scope == scope.MatchScope) &&
            (state.FullRecipe is null || (state.FullRecipe.Scope == scope.MatchScope &&
                (state.FullRecipe.LevelScope is null || state.FullRecipe.LevelScope == scope))) &&
            (state.ResumableCheckpoint is null || (state.ResumableCheckpoint.Scope == scope &&
                state.ResumableCheckpoint.Recipe.Scope == scope.MatchScope &&
                (state.ResumableCheckpoint.Recipe.LevelScope is null || state.ResumableCheckpoint.Recipe.LevelScope == scope)));
    }
    public Task<CookingNetworkWireResult> SendCommandAsync(string stableWireId, CookingRecipeCommand command, CancellationToken cancellationToken = default)
    {
        lock (_projectionGate) {
            if (!IsSynchronized || _binding is null || LatestBaseline is null) throw new InvalidOperationException("Baseline not acknowledged.");
            var correlation = "cmd-" + Interlocked.Increment(ref _correlation);
            var completion = new TaskCompletionSource<CookingNetworkWireResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_requests.Count >= _bounds.PerConnectionCapacity || !_requests.TryAdd(correlation, completion)) throw new InvalidOperationException("Client request bound reached.");
            try {
                Send(CookingNetworkMessageKind.Command, correlation, new CookingNetworkWireCommand(_binding.ServerSessionInstance,
                    _binding.ConnectionGeneration, checked(++_sequence), stableWireId, LatestBaseline.Identity.Scope,
                    CookingNetworkWireCodec.Freeze(command)));
            } catch { _requests.TryRemove(correlation, out _); throw; }
            return AwaitResponse(completion.Task, correlation, cancellationToken);
        }
    }
    private async Task<CookingNetworkWireResult> AwaitResponse(Task<CookingNetworkWireResult> response, string correlation, CancellationToken cancellationToken)
    {
        try { return await response.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false); }
        finally { _requests.TryRemove(correlation, out _); }
    }
    private void OnDisconnected()
    {
        IsSynchronized = false;
        foreach (var request in _requests.Values) request.TrySetException(new IOException("Connection closed."));
        _requests.Clear(); _ready.TrySetException(new IOException("Connection closed before baseline acknowledgement."));
    }
    public void Disconnect() { OnDisconnected(); _connection?.Dispose(); _connection = null; }
    public void Dispose() => Disconnect();
}
