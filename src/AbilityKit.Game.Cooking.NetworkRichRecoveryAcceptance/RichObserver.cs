using System.Collections.Concurrent;
using System.Diagnostics;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

// Observation follows forwarding. It proves delivery to Session, never admission.
internal sealed class RichObserver(IChannelListener inner, RichCommandPathDiagnostics? diagnostics = null) : IChannelListener
{
    private readonly RichCommandPathDiagnostics? _diagnostics = diagnostics;
    private long _ordinal;
    public ConcurrentQueue<RichWireGrant> Grants { get; } = new();
    public ConcurrentQueue<RichIssued> Images { get; } = new();
    public ConcurrentQueue<RichWireReply> Replies { get; } = new();
    public ConcurrentQueue<RichWireInput> Received { get; } = new();
    public ConcurrentQueue<RichClose> ClosedChannels { get; } = new();
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
        private readonly RichObserver _owner;
        private readonly NetworkFrameReader _outbound = new() { MaxFrameLength = 8 * 1024 * 1024 + 64 };
        private readonly NetworkFrameReader _reader = new() { MaxFrameLength = 8 * 1024 * 1024 + 64 };
        private readonly RichCommandPathDiagnostics.CallbackSegments? _inSegments;
        private readonly RichCommandPathDiagnostics.CallbackSegments? _outSegments;
        public ObservedChannel(IServerChannel inner, RichObserver owner) { _inner = inner; _owner = owner;
            if (owner._diagnostics is not null) { _inSegments = new(); _outSegments = new(); }
            inner.BytesReceived += OnBytes; inner.Closed += OnClosed; inner.Error += OnError; }
        public string Id => _inner.Id;
        public string RemoteEndpoint => _inner.RemoteEndpoint;
        public bool IsConnected => _inner.IsConnected;
        public event Action<ArraySegment<byte>>? BytesReceived;
        public event Action<IServerChannel>? Closed;
        public event Action<Exception>? Error;
        private void OnBytes(ArraySegment<byte> bytes)
        {
            var diagnostic = _owner._diagnostics;
            var callback = diagnostic?.CallbackId() ?? 0;
            var entered = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
            var owned = bytes.ToArray();
            var copied = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
            var forwarding = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
            try { BytesReceived?.Invoke(bytes); } // original bytes exactly once, production framing first
            catch (Exception e) {
                diagnostic?.Callback(new(0, callback, "observer.original-forward-error", "in", Id, null, null, null, bytes.Count,
                    entered, entered, copied, forwarding, RichCommandPathDiagnostics.Now, null, null, null, null, null,
                    null, "No parsed correlation; native timing UNKNOWN.", RichCommandPathDiagnostics.Text(e.ToString())));
                throw;
            }
            var forwarded = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
            try {
                lock (_reader) {
                    _inSegments?.Append(callback, owned.Length);
                    _reader.Append(new ArraySegment<byte>(owned));
                    while (_reader.TryRead(out var header, out var payload)) {
                        var mapping = _inSegments?.Consume(4 + NetworkPacketHeader.Size + payload.Count);
                        var decodeStart = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
                        CookingNetworkWireEnvelope? envelope = null;
                        var decoded = header.OpCode == CookingNetworkWireCodec.OpCode && CookingNetworkWireCodec.TryDecode(payload.AsSpan(), new(), out envelope);
                        if (!decoded) {
                            diagnostic?.Callback(new(0, callback, "observer.raw-envelope-rejected", "in", Id, null, null, null,
                                bytes.Count, diagnostic is null ? 0 : decodeStart, null, null, null, null, decodeStart,
                                diagnostic is null ? null : RichCommandPathDiagnostics.Now, null, null, null, null, mapping!,
                                RichCommandPathDiagnostics.Text(header.OpCode != CookingNetworkWireCodec.OpCode ? "Unexpected opcode; original observation skipped." : "TryDecode rejected; original observation skipped."), payload.Count));
                            continue; // Preserve original observer disposition; no new production exception.
                        }
                        var decodeEnd = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
                        if (_owner.Received.Count >= 65536) throw new InvalidOperationException("Observer record bound exceeded.");
                        var typedStart = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
                        Exception? typedFailure = null;
                        try {
                        var command = envelope!.Kind == CookingNetworkMessageKind.Command ? CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(envelope) : null;
                        _owner.Received.Enqueue(new(Interlocked.Increment(ref _owner._ordinal), Stopwatch.GetTimestamp(), Id, RemoteEndpoint, envelope!.CorrelationId, envelope.Kind,
                            command,
                            envelope.Kind == CookingNetworkMessageKind.BaselineAck ? CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope) : null));
                        if (command is not null) diagnostic?.ObserveCommand(command, envelope.CorrelationId);
                        } catch (Exception error) { typedFailure = error; throw; }
                        finally { TypedTrace(diagnostic, callback, "in", envelope!, bytes.Count, payload.Count, typedStart, mapping!, typedFailure); }
                        diagnostic?.Callback(new(0, callback, "observer.after-original-forward", "in", Id, null, envelope.CorrelationId,
                            envelope.Kind.ToString(), bytes.Count, entered, entered, copied, forwarding, forwarded, decodeStart,
                            decodeEnd, null, null, null, null, mapping!, null, payload.Count));
                    }
                }
            } catch (Exception e) {
                diagnostic?.Callback(new(0, callback, "observer.decode-error", "in", Id, null, null, null, bytes.Count,
                    entered, entered, copied, forwarding, forwarded, null, null, null, null, null, null,
                    "Framework callback bytes; no decoded correlation/native timing.", RichCommandPathDiagnostics.Text(e.ToString())));
                _owner.OnError(e);
            }
        }
        private void OnClosed(IServerChannel _) { Closed?.Invoke(this); _owner.ClosedChannels.Enqueue(new(Id, Stopwatch.GetTimestamp())); }
        private void OnError(Exception e) { _owner.OnError(e); Error?.Invoke(e); }
        public void Send(ArraySegment<byte> bytes)
        {
            var diagnostic = _owner._diagnostics;
            var callback = diagnostic?.CallbackId() ?? 0;
            var before = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
            try { _inner.Send(bytes); }
            catch (Exception e) {
                diagnostic?.Callback(new(0, callback, "observer.send-error", "out", Id, null, null, null, bytes.Count,
                    before, null, null, before, RichCommandPathDiagnostics.Now, null, null, null, null, null,
                    null, "Send enqueue failed; no native send guarantee.", RichCommandPathDiagnostics.Text(e.ToString())));
                throw;
            }
            var after = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
            lock (_outbound) {
                _outSegments?.Append(callback, bytes.Count);
                _outbound.Append(bytes);
                while (_outbound.TryRead(out var header, out var payload)) {
                    var mapping = _outSegments?.Consume(4 + NetworkPacketHeader.Size + payload.Count);
                    var decodeStart = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
                    CookingNetworkWireEnvelope? envelope = null;
                        var decoded = header.OpCode == CookingNetworkWireCodec.OpCode && CookingNetworkWireCodec.TryDecode(payload.AsSpan(), new(), out envelope);
                        if (!decoded) {
                            diagnostic?.Callback(new(0, callback, "observer.raw-envelope-rejected", "out", Id, null, null, null,
                                bytes.Count, diagnostic is null ? 0 : decodeStart, null, null, null, null, decodeStart,
                                diagnostic is null ? null : RichCommandPathDiagnostics.Now, null, null, null, null, mapping!,
                                RichCommandPathDiagnostics.Text(header.OpCode != CookingNetworkWireCodec.OpCode ? "Unexpected opcode; original observation skipped." : "TryDecode rejected; original observation skipped."), payload.Count));
                            continue; // Preserve original observer disposition; no new production exception.
                        }
                    var decodeEnd = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
                    var typedStart = diagnostic is null ? 0 : RichCommandPathDiagnostics.Now;
                    Exception? typedFailure = null;
                    try {
                    if (envelope!.Kind is CookingNetworkMessageKind.CommandResult or CookingNetworkMessageKind.Rejected) {
                        if (_owner.Replies.Count >= 8192) throw new InvalidOperationException("Readonly reply record bound exceeded.");
                        _owner.Replies.Enqueue(new(Id, envelope.CorrelationId, CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope)!, RichProof.Sha(payload.ToArray())));
                        diagnostic?.Terminal(envelope.CorrelationId);
                    } else if (envelope.Kind == CookingNetworkMessageKind.Ready) {
                        if (_owner.Grants.Count >= 65536) throw new InvalidOperationException("Readonly grant record bound exceeded.");
                        _owner.Grants.Enqueue(new(Id, CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope)!));
                    } else if (envelope.Kind == CookingNetworkMessageKind.Baseline) {
                        var baseline = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope)!;
                        if (_owner.Images.Count >= 65536) throw new InvalidOperationException("Readonly image record bound exceeded.");
                        _owner.Images.Enqueue(new(Id, baseline.Identity, CookingNetworkWireCodec.Hash(baseline.State)));
                    }
                    } catch (Exception error) { typedFailure = error; throw; }
                    finally { TypedTrace(diagnostic, callback, "out", envelope!, bytes.Count, payload.Count, typedStart, mapping!, typedFailure); }
                    diagnostic?.Callback(new(0, callback, "observer.after-send-enqueue", "out", Id, null, envelope.CorrelationId,
                        envelope.Kind.ToString(), bytes.Count, before, null, null, before, after, decodeStart, decodeEnd,
                        null, null, null, null, mapping!, null, payload.Count));
                }
            }
        }
        private void TypedTrace(RichCommandPathDiagnostics? diagnostic, long callback, string direction,
            CookingNetworkWireEnvelope envelope, int callbackBytes, int payloadBytes, long started, string mapping, Exception? error)
        {
            diagnostic?.Callback(new(0, callback, "observer.typed-read-validation", direction, Id, null,
                envelope.CorrelationId, envelope.Kind.ToString(), callbackBytes, started, null, null, null, null,
                started, RichCommandPathDiagnostics.Now, null, null, null, null, mapping,
                error is null ? null : RichCommandPathDiagnostics.Text(error.ToString()), payloadBytes));
        }
        public void Close() => _inner.Close();
        public void Dispose() { _inner.BytesReceived -= OnBytes; _inner.Closed -= OnClosed; _inner.Error -= OnError; _inner.Dispose(); }
    }
}
