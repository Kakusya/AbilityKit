using System.Collections.Concurrent;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
namespace AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance;

internal sealed class PassivePeer(PlayerId player, string credential, Func<ITransport> transport) : IDisposable
{
    private readonly object _gate = new();
    private ConnectionManager? _connection;
    private CookingNetworkJoined? _binding;
    private CookingNetworkBaseline? _baseline;
    private long _sequence;
    private readonly ConcurrentQueue<CookingNetworkWireEnvelope> _incoming = new();
    private int _incomingCount;
    private bool _joinSent;
    private CookingNetworkBaselineIdentity? _acknowledged;
    private readonly Dictionary<CookingNetworkBaselineIdentity, CookingNetworkBaseline> _validated = new();
    public Exception? Failure { get; private set; }
    public sealed record Terminal(string Correlation, CookingNetworkWireResult Result);
    public sealed record Issued(string Correlation, CookingNetworkWireCommand Wire);
    public ConcurrentQueue<Terminal> Results { get; } = new();
    public ConcurrentQueue<CookingNetworkBaselineIdentity> Grants { get; } = new();
    public ConcurrentQueue<Issued> Sent { get; } = new();
    public CookingNetworkJoined Binding { get { lock (_gate) return _binding ?? throw new InvalidOperationException("No binding."); } }
    public CookingNetworkBaseline Baseline { get { lock (_gate) return _baseline ?? throw new InvalidOperationException("No full baseline."); } }
    public bool HasBaseline { get { lock (_gate) return _baseline is not null; } }
    public bool CurrentGranted => HasBaseline && Grants.Contains(Baseline.Identity);
    public CookingNetworkBaseline? CurrentBusinessGrant => Grants.Reverse().Where(x => _binding is not null && x.ServerSessionInstance == _binding.ServerSessionInstance && x.ConnectionGeneration == _binding.ConnectionGeneration && x.Scope == ConcurrencyFixture.Scope).Select(x => _validated[x]).FirstOrDefault();
    public bool Connected => _connection?.IsConnected == true;
    public void Open(string address, int port)
    {
        _connection = new(transport, new ConnectionOptions { EnableReconnect = false, MaxFrameLength = 8 * 1024 * 1024 + 64 });
        _joinSent = false; _acknowledged = null;
        _connection.ServerPushReceived += (opcode, bytes) => {
            try {
                var owned = bytes.ToArray();
                if (opcode != CookingNetworkWireCodec.OpCode || !CookingNetworkWireCodec.TryDecode(owned, new(), out var envelope)) throw new InvalidOperationException("Malformed server frame.");
                if (Interlocked.Increment(ref _incomingCount) > 256) { Interlocked.Decrement(ref _incomingCount); throw new InvalidOperationException("Passive ingress bound exceeded."); }
                _incoming.Enqueue(envelope!);
            } catch (Exception e) { Failure = e; }
        };
        _connection.Error += e => Failure = e;
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
        while (_incoming.TryDequeue(out var envelope)) { Interlocked.Decrement(ref _incomingCount); Receive(envelope); }
        if (HasBaseline && _acknowledged != Baseline.Identity) {
            _acknowledged = Baseline.Identity;
            Send(CookingNetworkMessageKind.BaselineAck, "ack-" + _acknowledged.SnapshotSequence, _acknowledged);
        }
    }
    private void Receive(CookingNetworkWireEnvelope envelope)
    {
        lock (_gate) {
            switch (envelope!.Kind) {
                case CookingNetworkMessageKind.Joined:
                    var joined = CookingNetworkWireCodec.Read<CookingNetworkJoined>(envelope)!;
                    ConcurrencyFixture.Require(joined.Participant == player && joined.ConnectionGeneration > (_binding?.ConnectionGeneration ?? 0) && (_binding is null || joined.ServerSessionInstance == _binding.ServerSessionInstance), "Joined identity.");
                    _binding = joined; _baseline = null; break;
                case CookingNetworkMessageKind.Baseline:
                    var full = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope) ?? throw new InvalidOperationException("Typed baseline rejected.");
                    ConcurrencyFixture.Require(_binding is not null && full.Identity.ServerSessionInstance == _binding.ServerSessionInstance && full.Identity.ConnectionGeneration == _binding.ConnectionGeneration && full.Identity.Participant == player && full.Identity.Scope == full.State.Observation.Scope && full.Identity.StateHash == CookingNetworkWireCodec.BaselineHash(full.State, full.Session), "Complete baseline identity/hash.");
                    ConcurrencyFixture.Require(full.LevelFormatVersion == CookingLevelCheckpointCodec.CurrentFormatVersion && full.RecipeSchemaVersion == 5 && full.State.FullRecipe?.SchemaVersion == 5 && full.Identity.Epoch == full.Identity.Scope.LevelEpoch, "Current complete baseline format/epoch.");
                    ConcurrencyFixture.Require(full.Session.ServerSessionInstance == _binding!.ServerSessionInstance && full.Session.Participants.Count == 2 && full.Session.Participants.Single(x => x.Participant == player).ConnectionGeneration == _binding.ConnectionGeneration, "Complete current-generation Session metadata.");
                    ConcurrencyFixture.Require(_baseline is null || full.Identity.SnapshotSequence > _baseline.Identity.SnapshotSequence, "Baseline sequence.");
                    _baseline = full;
                    Proof.ValidateFull(full.State);
                    if (_validated.Count >= 4096) throw new InvalidOperationException("Validated baseline ledger bound exceeded.");
                    _validated.Add(full.Identity, full); break;
                case CookingNetworkMessageKind.Ready:
                    var ready = CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope)!;
                    ConcurrencyFixture.Require(_baseline?.Identity == ready, "Ready differs from actual issued baseline.");
                    ConcurrencyFixture.Require(_validated.ContainsKey(ready), "Ready must reference retained validated full image.");
                    ConcurrencyFixture.Require(Grants.Count < 4096, "Passive grant record bound.");
                    Grants.Enqueue(ready); break;
                case CookingNetworkMessageKind.CommandResult:
                case CookingNetworkMessageKind.Rejected:
                    ConcurrencyFixture.Require(Results.Count < 512, "Passive terminal record bound.");
                    Results.Enqueue(new(envelope.CorrelationId, CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope) ?? throw new InvalidOperationException("Malformed terminal result."))); break;
                default: throw new InvalidOperationException("Unexpected server message.");
            }
        }
    }
    private void Send<T>(CookingNetworkMessageKind kind, string correlation, T value) => _connection!.Send(CookingNetworkWireCodec.OpCode, new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, value)), (ushort)NetworkPacketFlags.ServerPush);
    public CookingNetworkWireCommand Command(CookingRecipeCommand command, string stable, string correlation, Func<CookingNetworkWireCommand, CookingNetworkWireCommand>? alter = null)
    {
        var binding = Binding;
        var wire = new CookingNetworkWireCommand(binding.ServerSessionInstance, binding.ConnectionGeneration, ++_sequence, stable, ConcurrencyFixture.Scope, command);
        wire = alter?.Invoke(wire) ?? wire; ConcurrencyFixture.Require(Sent.Count < 128, "Passive issued command record bound."); Sent.Enqueue(new(correlation, wire)); Send(CookingNetworkMessageKind.Command, correlation, wire); return wire;
    }
    public bool Has(string correlation) => Results.Any(x => x.Correlation == correlation);
    public CookingNetworkWireResult Result(string correlation) => Results.Single(x => x.Correlation == correlation).Result;
    public void Reopen(string address, int port) { _connection!.Dispose(); _sequence = 0; lock (_gate) _baseline = null; Open(address, port); }
    public void Dispose() => _connection?.Dispose();
}
