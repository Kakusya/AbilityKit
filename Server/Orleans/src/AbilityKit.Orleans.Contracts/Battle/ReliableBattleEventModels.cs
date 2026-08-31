using Orleans;

namespace AbilityKit.Orleans.Contracts.Battle;

[GenerateSerializer]
public sealed class ReliableBattleEventEnvelope
{
    [Id(0)] public string EventId { get; set; } = string.Empty;
    [Id(1)] public string BattleId { get; set; } = string.Empty;
    [Id(2)] public string Epoch { get; set; } = string.Empty;
    [Id(3)] public long Sequence { get; set; }
    [Id(4)] public int SourceFrame { get; set; }
    [Id(5)] public int EventType { get; set; }
    [Id(6)] public byte[]? Payload { get; set; }
}

[GenerateSerializer]
public sealed class ReliableBattleEventBatch
{
    [Id(0)] public string BattleId { get; set; } = string.Empty;
    [Id(1)] public string Epoch { get; set; } = string.Empty;
    [Id(2)] public long FirstAvailableSequence { get; set; }
    [Id(3)] public long Watermark { get; set; }
    [Id(4)] public bool RetentionGap { get; set; }
    [Id(5)] public List<ReliableBattleEventEnvelope> Events { get; set; } = new();
}

[GenerateSerializer]
public sealed class ReliableBattleEventSubscribeCursor
{
    [Id(0)] public string Epoch { get; set; } = string.Empty;
    [Id(1)] public long LastAcknowledgedSequence { get; set; }
}

[GenerateSerializer]
public sealed class ReliableBattleEventAckResult
{
    [Id(0)] public bool Accepted { get; set; }
    [Id(1)] public string Epoch { get; set; } = string.Empty;
    [Id(2)] public long AcceptedSequence { get; set; }
    [Id(3)] public long Watermark { get; set; }
    [Id(4)] public bool RequiresResync { get; set; }
}
