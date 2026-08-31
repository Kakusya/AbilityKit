using AbilityKit.Orleans.Contracts.Battle;

namespace AbilityKit.Orleans.Grains.Battle;

internal sealed class ReliableBattleEventRetention
{
    private readonly int _capacity;
    private readonly LinkedList<ReliableBattleEventEnvelope> _events = new();
    private readonly Dictionary<string, long> _observerAcknowledgements = new(StringComparer.Ordinal);
    private long _nextSequence = 1;

    public ReliableBattleEventRetention(string battleId, string epoch, int capacity = 512)
    {
        BattleId = battleId ?? string.Empty;
        Epoch = epoch ?? string.Empty;
        _capacity = Math.Max(1, capacity);
    }

    public string BattleId { get; }

    public string Epoch { get; }

    public long Watermark => _nextSequence - 1;

    public long FirstAvailableSequence => _events.First?.Value.Sequence ?? _nextSequence;

    public int Count => _events.Count;

    public ReliableBattleEventEnvelope Append(int sourceFrame, int eventType, byte[]? payload)
    {
        var sequence = _nextSequence++;
        var envelope = new ReliableBattleEventEnvelope
        {
            EventId = CreateEventId(BattleId, Epoch, sequence),
            BattleId = BattleId,
            Epoch = Epoch,
            Sequence = sequence,
            SourceFrame = sourceFrame,
            EventType = eventType,
            Payload = payload
        };
        _events.AddLast(envelope);
        while (_events.Count > _capacity)
        {
            _events.RemoveFirst();
        }

        return envelope;
    }

    public ReliableBattleEventBatch CreateReplay(string? cursorEpoch, long lastAcknowledgedSequence)
    {
        var normalizedAck = Math.Max(0, lastAcknowledgedSequence);
        var hasKnownEpoch = !string.IsNullOrWhiteSpace(cursorEpoch);
        var epochMismatch = hasKnownEpoch
            && !string.Equals(cursorEpoch, Epoch, StringComparison.Ordinal);
        var retentionGap = epochMismatch
            || (hasKnownEpoch && normalizedAck + 1 < FirstAvailableSequence);
        var events = retentionGap
            ? new List<ReliableBattleEventEnvelope>()
            : _events.Where(item => item.Sequence > normalizedAck).ToList();

        return new ReliableBattleEventBatch
        {
            BattleId = BattleId,
            Epoch = Epoch,
            FirstAvailableSequence = FirstAvailableSequence,
            Watermark = Watermark,
            RetentionGap = retentionGap,
            Events = events
        };
    }

    public void RegisterObserver(string observerKey, string? epoch, long lastAcknowledgedSequence)
    {
        if (string.IsNullOrWhiteSpace(observerKey))
        {
            return;
        }

        var accepted = string.Equals(epoch, Epoch, StringComparison.Ordinal)
            ? Math.Clamp(lastAcknowledgedSequence, 0, Watermark)
            : 0;
        _observerAcknowledgements[observerKey] = accepted;
        TrimAcknowledgedEvents();
    }

    public void UnregisterObserver(string observerKey)
    {
        if (string.IsNullOrWhiteSpace(observerKey))
        {
            return;
        }

        _observerAcknowledgements.Remove(observerKey);
        TrimAcknowledgedEvents();
    }

    public long Acknowledge(string observerKey, string? epoch, long sequence)
    {
        if (string.IsNullOrWhiteSpace(observerKey)
            || !string.Equals(epoch, Epoch, StringComparison.Ordinal)
            || !_observerAcknowledgements.TryGetValue(observerKey, out var previous))
        {
            return 0;
        }

        var accepted = Math.Max(previous, Math.Clamp(sequence, 0, Watermark));
        _observerAcknowledgements[observerKey] = accepted;
        TrimAcknowledgedEvents();
        return accepted;
    }

    private void TrimAcknowledgedEvents()
    {
        if (_observerAcknowledgements.Count == 0)
        {
            return;
        }

        var minimumAcknowledged = _observerAcknowledgements.Values.Min();
        while (_events.First?.Value.Sequence <= minimumAcknowledged)
        {
            _events.RemoveFirst();
        }
    }

    internal static string CreateEventId(string battleId, string epoch, long sequence)
    {
        return $"{battleId}:{epoch}:{sequence}";
    }
}
