using System.Collections.Concurrent;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
namespace AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance;

// Observation follows forwarding. It proves delivery to Session, never admission.
internal sealed class ObservedListener(IChannelListener inner) : IChannelListener
{
    public sealed record Seen(string Channel, string Endpoint, string Correlation, CookingNetworkMessageKind Kind, CookingNetworkWireCommand? Command, CookingNetworkBaselineIdentity? Ack);
    public sealed record Reply(string Channel, string Correlation, CookingNetworkWireResult Result);
    public sealed record Image(string Channel, CookingNetworkBaselineIdentity Identity, int Phase, string BusinessHash);
    public sealed record Grant(string Channel, CookingNetworkBaselineIdentity Identity);
    public ConcurrentQueue<Grant> Grants { get; } = new();
    public ConcurrentQueue<Image> Images { get; } = new();
    public ConcurrentQueue<Reply> Replies { get; } = new();
    public ConcurrentQueue<Seen> Received { get; } = new();
    public ConcurrentQueue<string> ClosedChannels { get; } = new();
    public Exception? Failure { get; private set; }
    public bool IsListening => inner.IsListening;
    public string Endpoint => inner.Endpoint;
    public event Action<IServerChannel>? ChannelAccepted;
    public event Action<Exception>? Error;
    public void Start() { inner.ChannelAccepted += Accepted; inner.Error += OnError; inner.Start(); }
    private void OnError(Exception e) { Failure = e; Error?.Invoke(e); }
    private void Accepted(IServerChannel channel) => ChannelAccepted?.Invoke(new ObservedChannel(channel, this));
    public bool Has(string correlation) => Received.Any(x => x.Kind == CookingNetworkMessageKind.Command && x.Correlation == correlation);
    public void Stop() => inner.Stop();
    public void Dispose() { inner.ChannelAccepted -= Accepted; inner.Error -= OnError; inner.Dispose(); }
    private sealed class ObservedChannel : IServerChannel
    {
        private readonly IServerChannel _inner;
        private readonly ObservedListener _owner;
        private readonly NetworkFrameReader _outbound = new() { MaxFrameLength = 8 * 1024 * 1024 + 64 };
        private readonly NetworkFrameReader _reader = new() { MaxFrameLength = 8 * 1024 * 1024 + 64 };
        public ObservedChannel(IServerChannel inner, ObservedListener owner) { _inner = inner; _owner = owner; inner.BytesReceived += OnBytes; inner.Closed += OnClosed; inner.Error += OnError; }
        public string Id => _inner.Id;
        public string RemoteEndpoint => _inner.RemoteEndpoint;
        public bool IsConnected => _inner.IsConnected;
        public event Action<ArraySegment<byte>>? BytesReceived;
        public event Action<IServerChannel>? Closed;
        public event Action<Exception>? Error;
        private void OnBytes(ArraySegment<byte> bytes)
        {
            var owned = bytes.ToArray();
            BytesReceived?.Invoke(bytes); // original bytes exactly once, production framing first
            try {
                lock (_reader) {
                    _reader.Append(new ArraySegment<byte>(owned));
                    while (_reader.TryRead(out var header, out var payload)) {
                        if (header.OpCode != CookingNetworkWireCodec.OpCode || !CookingNetworkWireCodec.TryDecode(payload.AsSpan(), new(), out var envelope)) continue;
                        if (_owner.Received.Count >= 4096) throw new InvalidOperationException("Observer record bound exceeded.");
                        _owner.Received.Enqueue(new(Id, RemoteEndpoint, envelope!.CorrelationId, envelope.Kind,
                            envelope.Kind == CookingNetworkMessageKind.Command ? CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(envelope) : null,
                            envelope.Kind == CookingNetworkMessageKind.BaselineAck ? CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope) : null));
                    }
                }
            } catch (Exception e) { _owner.OnError(e); }
        }
        private void OnClosed(IServerChannel _) { Closed?.Invoke(this); _owner.ClosedChannels.Enqueue(Id); }
        private void OnError(Exception e) { _owner.OnError(e); Error?.Invoke(e); }
        public void Send(ArraySegment<byte> bytes)
        {
            _inner.Send(bytes);
            lock (_outbound) {
                _outbound.Append(bytes);
                while (_outbound.TryRead(out var header, out var payload)) {
                    if (header.OpCode != CookingNetworkWireCodec.OpCode || !CookingNetworkWireCodec.TryDecode(payload.AsSpan(), new(), out var envelope)) continue;
                    if (envelope!.Kind is CookingNetworkMessageKind.CommandResult or CookingNetworkMessageKind.Rejected) {
                        if (_owner.Replies.Count >= 512) throw new InvalidOperationException("Readonly reply record bound exceeded.");
                        _owner.Replies.Enqueue(new(Id, envelope.CorrelationId, CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope)!));
                    } else if (envelope.Kind == CookingNetworkMessageKind.Ready) {
                        if (_owner.Grants.Count >= 4096) throw new InvalidOperationException("Readonly grant record bound exceeded.");
                        _owner.Grants.Enqueue(new(Id, CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope)!));
                    } else if (envelope.Kind == CookingNetworkMessageKind.Baseline) {
                        var baseline = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope)!;
                        if (_owner.Images.Count >= 4096) throw new InvalidOperationException("Readonly image record bound exceeded.");
                        _owner.Images.Enqueue(new(Id, baseline.Identity, ConcurrencyFixture.Phase(baseline.State), CookingNetworkWireCodec.Hash(baseline.State)));
                    }
                }
            }
        }
        public void Close() => _inner.Close();
        public void Dispose() { _inner.BytesReceived -= OnBytes; _inner.Closed -= OnClosed; _inner.Error -= OnError; _inner.Dispose(); }
    }
}
