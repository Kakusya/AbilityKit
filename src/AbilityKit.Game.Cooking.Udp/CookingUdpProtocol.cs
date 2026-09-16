using System.Text.Json;

namespace AbilityKit.Game.Cooking.Udp;

public enum CookingUdpMessageKind
{
    HandshakeRequest,
    HandshakeAccepted,
    HandshakeRejected,
    Baseline,
    Command,
    CommandResult,
    Delta,
    SynchronizationRejected,
    TransportClosed,
}

public sealed record CookingUdpScope(string SessionId, string WorldId, string MatchId)
{
    public static CookingUdpScope FromDomain(CookingScope scope) => new(scope.Session.Value, scope.World.Value, scope.Match.Value);
    public CookingScope ToDomain() => new(new SessionId(SessionId), new WorldId(WorldId), new MatchId(MatchId));
}

public sealed record CookingUdpEnvelope(
    string Schema,
    string ProtocolName,
    int ProtocolVersion,
    CookingUdpMessageKind Kind,
    string CorrelationId,
    CookingUdpScope Scope,
    long Epoch,
    string ConfigIdentity,
    JsonElement Payload);

public sealed record CookingUdpHandshakeRequest(IReadOnlyList<string> Capabilities);
public sealed record CookingUdpHandshakeAccepted(string PlayerId);
public sealed record CookingUdpHandshakeRejected(CookingSessionReason Reason);
public sealed record CookingUdpSnapshotMessage(long Sequence, long BaselineReference, string SnapshotHash, CookingSnapshot Snapshot);
public sealed record CookingUdpCommandMessage(CookingCommand Command);
public sealed record CookingUdpCommandResultMessage(CookingSessionCommandDisposition Disposition, CookingSessionReason Reason,
    int QueueDepth, bool IsDuplicate, CommandResult? AuthorityResult);
public sealed record CookingUdpSynchronizationRejected(CookingSessionReason Reason, long? ExpectedSequence, long? BaselineReference);
public sealed record CookingUdpTransportClosed(string Reason);

public static class CookingUdpCodec
{
    public const string Schema = "abilitykit.cooking.udp.v1";
    public const int MaximumDatagramBytes = 1200;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static byte[] Encode<T>(CookingUdpMessageKind kind, CookingSessionDescriptor descriptor, string correlationId, T payload)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation ID must not be blank.", nameof(correlationId));
        var envelope = new CookingUdpEnvelope(Schema, descriptor.Protocol.Name, descriptor.Protocol.MaximumVersion, kind,
            correlationId, CookingUdpScope.FromDomain(descriptor.Scope), descriptor.Epoch, descriptor.ConfigIdentity,
            JsonSerializer.SerializeToElement(payload, Options));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
        if (bytes.Length > MaximumDatagramBytes)
            throw new InvalidOperationException($"Cooking UDP datagram exceeds {MaximumDatagramBytes} bytes.");
        return bytes;
    }

    public static bool TryDecode(ReadOnlySpan<byte> datagram, out CookingUdpEnvelope? envelope, out string reason)
    {
        envelope = null;
        if (datagram.Length is <= 0 or > MaximumDatagramBytes)
        {
            reason = "datagram-size";
            return false;
        }

        try
        {
            envelope = JsonSerializer.Deserialize<CookingUdpEnvelope>(datagram, Options);
            if (envelope is null || !StringComparer.Ordinal.Equals(envelope.Schema, Schema) ||
                string.IsNullOrWhiteSpace(envelope.ProtocolName) || envelope.ProtocolVersion <= 0 ||
                string.IsNullOrWhiteSpace(envelope.CorrelationId) || envelope.Scope is null ||
                string.IsNullOrWhiteSpace(envelope.Scope.SessionId) || string.IsNullOrWhiteSpace(envelope.Scope.WorldId) ||
                string.IsNullOrWhiteSpace(envelope.Scope.MatchId) || envelope.Epoch <= 0 ||
                string.IsNullOrWhiteSpace(envelope.ConfigIdentity) || !Enum.IsDefined(envelope.Kind))
            {
                envelope = null;
                reason = "envelope-invalid";
                return false;
            }

            reason = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            reason = "json-invalid";
            return false;
        }
    }

    public static bool TryReadPayload<T>(CookingUdpEnvelope envelope, out T? payload, out string reason)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        try
        {
            payload = envelope.Payload.Deserialize<T>(Options);
            if (payload is null)
            {
                reason = "payload-null";
                return false;
            }
            reason = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            payload = default;
            reason = "payload-invalid";
            return false;
        }
    }
}

public sealed class CookingUdpClientProjection
{
    private readonly CookingScope _scope;
    private readonly long _epoch;
    private readonly string _configIdentity;
    private long _sequence;
    private long _baselineReference;

    public CookingUdpClientProjection(CookingSessionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _scope = descriptor.Scope;
        _epoch = descriptor.Epoch;
        _configIdentity = descriptor.ConfigIdentity;
    }

    public CookingSnapshot? Snapshot { get; private set; }
    public CookingSynchronizationState State { get; private set; } = CookingSynchronizationState.WaitingForBaseline;
    public long Sequence => _sequence;

    public CookingSynchronizationResult InstallBaseline(CookingUdpSnapshotMessage message)
    {
        if (!ValidateSnapshot(message, requireBaseline: false, out var reason))
            return Reject(reason);
        if (message.BaselineReference != message.Sequence)
            return Reject(CookingSessionReason.BaselineReferenceMismatch);
        if (message.Sequence <= 0 || message.Sequence < _sequence)
            return Reject(CookingSessionReason.SnapshotSequenceStale);

        Snapshot = message.Snapshot;
        _sequence = message.Sequence;
        _baselineReference = message.Sequence;
        State = CookingSynchronizationState.Synchronized;
        return new(true, CookingSessionReason.None, State, _sequence, _baselineReference);
    }

    public CookingSynchronizationResult ApplyDelta(CookingUdpSnapshotMessage message)
    {
        if (!ValidateSnapshot(message, requireBaseline: true, out var reason))
            return Reject(reason);
        if (State != CookingSynchronizationState.Synchronized)
            return Reject(CookingSessionReason.BaselineRequired);
        if (message.BaselineReference != _baselineReference)
            return Reject(CookingSessionReason.BaselineReferenceMismatch);
        if (message.Sequence == _sequence)
            return Reject(CookingSessionReason.SnapshotSequenceDuplicate);
        if (message.Sequence < _sequence)
            return Reject(CookingSessionReason.SnapshotSequenceStale);
        if (message.Sequence > _sequence + 1)
            return Reject(CookingSessionReason.SnapshotSequenceGap);

        Snapshot = message.Snapshot;
        _sequence = message.Sequence;
        return new(true, CookingSessionReason.None, State, _sequence, _baselineReference);
    }

    public void ReportTransportLoss()
    {
        State = CookingSynchronizationState.Unsynchronized;
    }

    private bool ValidateSnapshot(CookingUdpSnapshotMessage message, bool requireBaseline, out CookingSessionReason reason)
    {
        if (message is null || message.Snapshot is null || message.Sequence <= 0 || string.IsNullOrWhiteSpace(message.SnapshotHash))
        {
            reason = CookingSessionReason.InvalidBaseline;
            return false;
        }
        if (!Equals(message.Snapshot.Scope, _scope))
        {
            reason = CookingSessionReason.ScopeMismatch;
            return false;
        }
        if (!StringComparer.Ordinal.Equals(message.Snapshot.Sha256(), message.SnapshotHash))
        {
            reason = CookingSessionReason.AuthoritySnapshotMismatch;
            return false;
        }
        if (_epoch <= 0 || string.IsNullOrWhiteSpace(_configIdentity))
        {
            reason = CookingSessionReason.InvalidBaseline;
            return false;
        }
        if (requireBaseline && message.BaselineReference <= 0)
        {
            reason = CookingSessionReason.InvalidBaseline;
            return false;
        }
        reason = CookingSessionReason.None;
        return true;
    }

    private CookingSynchronizationResult Reject(CookingSessionReason reason)
    {
        State = CookingSynchronizationState.Unsynchronized;
        return new(false, reason, State, _sequence == 0 ? null : _sequence,
            _baselineReference == 0 ? null : _baselineReference);
    }
}
