using System.Collections.Concurrent;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

internal sealed class RichPeer(PlayerId player, string credential, Func<ITransport> transport) : IDisposable
{
    private readonly object _gate = new();
    private ConnectionManager? _connection;
    private CookingNetworkJoined? _binding;
    private CookingNetworkBaseline? _baseline;
    private long _sequence;
    private readonly ConcurrentQueue<(CookingNetworkWireEnvelope Envelope, string Hash, long Incarnation)> _incoming = new();
    private int _incomingCount;
    private long _incarnation;
    private bool _joinSent;
    private CookingNetworkBaselineIdentity? _acknowledged;
    private readonly Dictionary<CookingNetworkBaselineIdentity, CookingNetworkBaseline> _validated = new();
    public Exception? Failure { get; private set; }
    private CookingNetworkWireCommand? _watched; private CookingLevelState _watchedLifecycle;
    public CookingNetworkBaseline? WatchedCommittedImage { get; private set; }
    public void Watch(CookingNetworkWireCommand wire, CookingLevelState lifecycle) { _watched=wire; _watchedLifecycle=lifecycle; WatchedCommittedImage=null; }
    private long _ordinal, _baselineOrdinal, _ackOrdinal, _readyOrdinal;
    public ConcurrentQueue<RichCallerOutcome> Results { get; } = new();
    public ConcurrentQueue<CookingNetworkBaselineIdentity> Grants { get; } = new();
    public ConcurrentQueue<RichCallerCommand> Sent { get; } = new();
    public string? DropCorrelation { get; set; }
    public RichDroppedReply? Dropped { get; private set; }
    public CookingNetworkJoined Binding { get { lock (_gate) return _binding ?? throw new InvalidOperationException("No binding."); } }
    public CookingNetworkBaseline Baseline { get { lock (_gate) return _baseline ?? throw new InvalidOperationException("No full baseline."); } }
    public bool HasBaseline { get { lock (_gate) return _baseline is not null; } }
    public bool CurrentGranted => HasBaseline && Grants.Contains(Baseline.Identity);
    public CookingNetworkBaseline? CurrentBusinessGrant => Grants.Reverse().Where(x => _validated.ContainsKey(x) && _binding is not null && x.ServerSessionInstance == _binding.ServerSessionInstance && x.Participant == player && x.ConnectionGeneration == _binding.ConnectionGeneration && x.Scope == _baseline?.Identity.Scope).Select(x => _validated[x]).FirstOrDefault();
    public bool Connected => _connection?.IsConnected == true;
    public void Open(string address, int port)
    {
        var incarnation = Interlocked.Increment(ref _incarnation);
        _connection = new(transport, new ConnectionOptions { EnableReconnect = false,
            MaxFrameLength = RichFrameCodec.MaximumBodyBytes, FrameCodec = new RichFrameCodec() });
        _joinSent = false; _acknowledged = null;
        _connection.ServerPushReceived += (opcode, bytes) => {
            try {
                if (incarnation != Volatile.Read(ref _incarnation)) return;
                var owned = bytes.ToArray();
                if (opcode != CookingNetworkWireCodec.OpCode || !CookingNetworkWireCodec.TryDecode(owned, new(), out var envelope)) throw new InvalidOperationException("Malformed server frame.");
                if (Interlocked.Increment(ref _incomingCount) > 256) { Interlocked.Decrement(ref _incomingCount); throw new InvalidOperationException("Passive ingress bound exceeded."); }
                _incoming.Enqueue((envelope!, RichProof.Sha(owned), incarnation));
            } catch (Exception e) { if (incarnation == Volatile.Read(ref _incarnation)) Failure = e; }
        };
        _connection.Error += e => { if (incarnation == Volatile.Read(ref _incarnation)) Failure = e; };
        _connection.Open(address, port);
    }
    // Called only by the scenario owner. ACK only the last validated issued image after draining.
    public void Poll()
    {
        if (Failure is not null) throw new InvalidOperationException("Passive callback failure", Failure);
        if (!_joinSent && Connected) {
            _joinSent = true;
            Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(player, credential, _binding?.ServerSessionInstance, _binding?.RebindToken));
        }
        while (_incoming.TryDequeue(out var envelope)) { Interlocked.Decrement(ref _incomingCount); if (envelope.Incarnation == _incarnation) Receive(envelope.Envelope, envelope.Hash); }
        if (HasBaseline && _acknowledged != Baseline.Identity) {
            _acknowledged = Baseline.Identity; _ackOrdinal = ++_ordinal;
            Send(CookingNetworkMessageKind.BaselineAck, "ack-" + _acknowledged.SnapshotSequence, _acknowledged);
        }
    }
    private void Receive(CookingNetworkWireEnvelope envelope, string bytesHash)
    {
        lock (_gate) {
            switch (envelope!.Kind) {
                case CookingNetworkMessageKind.Joined:
                    var joined = CookingNetworkWireCodec.Read<CookingNetworkJoined>(envelope)!;
                    RichProof.Require(joined.Participant == player && joined.ConnectionGeneration > (_binding?.ConnectionGeneration ?? 0) && (_binding is null || joined.ServerSessionInstance == _binding.ServerSessionInstance), "Joined identity.");
                    _binding = joined; _baseline = null; break;
                case CookingNetworkMessageKind.Baseline:
                    var full = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope) ?? throw new InvalidOperationException("Typed baseline rejected.");
                    RichProof.Require(_binding is not null && full.Identity.ServerSessionInstance == _binding.ServerSessionInstance && full.Identity.ConnectionGeneration == _binding.ConnectionGeneration && full.Identity.Participant == player && full.Identity.Scope == full.State.Observation.Scope && full.Identity.StateHash == CookingNetworkWireCodec.BaselineHash(full.State, full.Session), "Complete baseline identity/hash.");
                    RichProof.Require(full.LevelFormatVersion == CookingLevelCheckpointCodec.CurrentFormatVersion && full.RecipeSchemaVersion == 5 && full.State.FullRecipe?.SchemaVersion == 5 && full.Identity.Epoch == full.Identity.Scope.LevelEpoch, "Current complete baseline format/epoch.");
                    RichProof.Require(full.Session.ServerSessionInstance == _binding!.ServerSessionInstance && full.Session.Participants.Count == 2 && full.Session.Participants.Single(x => x.Participant == player).ConnectionGeneration == _binding.ConnectionGeneration, "Complete current-generation Session metadata.");
                    RichProof.Require(_baseline is null || full.Identity.SnapshotSequence > _baseline.Identity.SnapshotSequence, "Baseline sequence.");
                    _baseline = full; _baselineOrdinal = ++_ordinal;
                    RichProof.Validate(full.State);
                    if (_validated.Count >= 32) {
                        var retained = Grants.LastOrDefault();
                        var evict = _validated.Keys.First(x => x != retained && x != full.Identity);
                        _validated.Remove(evict);
                    }
                    _validated.Add(full.Identity, full);
                    // Immutable side evidence outside the bounded live Ready ledger. This never grants permission.
                    if (_watched is { } watched && WatchedCommittedImage is null && full.Identity.ConnectionGeneration == watched.ConnectionGeneration && full.Identity.Scope == watched.Scope && full.State.Observation.Lifecycle.State == _watchedLifecycle) {
                        var domain = CookingNetworkWireCodec.DomainId(watched.ServerSessionInstance, watched.Scope, player, watched.StableCommandId);
                        if (full.State.FullRecipe!.Deduplication.Any(x => x.Player == player && x.Command == domain && x.Outcome == CookingRecipeOutcome.Accepted) && (watched.Command.Operation != CookingRecipeOperation.StartProcess || full.State.FullRecipe.Processes.Any(x => x.Anchor == watched.Command.Item && x.Recipe == watched.Command.Recipe))) WatchedCommittedImage=full;
                    }
                    break;
                case CookingNetworkMessageKind.Ready:
                    var ready = CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope)!;
                    RichProof.Require(_binding is not null && ready.ServerSessionInstance == _binding.ServerSessionInstance && ready.Participant == player && ready.ConnectionGeneration == _binding.ConnectionGeneration && ready.Scope == _baseline?.Identity.Scope && _acknowledged == ready && _baseline?.Identity == ready, "Ready differs from actual issued baseline.");
                    RichProof.Require(_validated.ContainsKey(ready), "Ready must reference retained validated full image.");
                    RichProof.Require(Grants.Count < 65536, "Passive grant record bound.");
                    _readyOrdinal = ++_ordinal; Grants.Enqueue(ready); break;
                case CookingNetworkMessageKind.CommandResult:
                case CookingNetworkMessageKind.Rejected:
                    RichProof.Require(Results.Count < 8192, "Passive terminal record bound.");
                    var result = CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope) ?? throw new InvalidOperationException("Malformed terminal result.");
                    if (envelope.Kind == CookingNetworkMessageKind.CommandResult && DropCorrelation == envelope.CorrelationId && Dropped is null) {
                        Dropped = new(envelope.CorrelationId, result, bytesHash, 1, false); break;
                    }
                    Results.Enqueue(new(envelope.CorrelationId, result, bytesHash, ++_ordinal)); break;
                default: throw new InvalidOperationException("Unexpected server message.");
            }
        }
    }
    private void Send<T>(CookingNetworkMessageKind kind, string correlation, T value) => _connection!.Send(CookingNetworkWireCodec.OpCode, new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, value)), (ushort)NetworkPacketFlags.ServerPush);
    public CookingNetworkWireCommand Command(CookingRecipeCommand command, string stable, string correlation, Func<CookingNetworkWireCommand, CookingNetworkWireCommand>? alter = null)
    {
        var binding = Binding;
        var wire = new CookingNetworkWireCommand(binding.ServerSessionInstance, binding.ConnectionGeneration, ++_sequence, stable, _baseline?.Identity.Scope, command);
        wire = alter?.Invoke(wire) ?? wire; RichProof.Require(Sent.Count < 8192, "Passive issued command record bound."); Sent.Enqueue(new(correlation, wire, ++_ordinal)); Send(CookingNetworkMessageKind.Command, correlation, wire); return wire;
    }
    public bool Has(string correlation) => Results.Any(x => x.Correlation == correlation);
    public CookingNetworkWireResult Result(string correlation) => Results.Single(x => x.Correlation == correlation).Result;
    public RichCallerCheckpoint Checkpoint(string id, string? pending = null)
    {
        var grant = CurrentBusinessGrant ?? throw new InvalidOperationException("No current generation/scope validated Ready grant.");
        RichProof.Require(_acknowledged == Baseline.Identity, "Latest actual issued ACK has not been sent.");
        return new(id, player, Baseline, grant, _acknowledged!, grant.Identity, _baselineOrdinal, _ackOrdinal, _readyOrdinal, pending);
    }
    public void Reopen(string address, int port) { _connection!.Dispose(); _sequence = 0; lock (_gate) _baseline = null; Open(address, port); }
    public void Dispose() => _connection?.Dispose();
}
