#nullable enable

using System;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 单次同步 tick 中发出的玩法无关同步健康信号。Carrier 会将它与 <see cref="SyncReconciliationReport"/> 一起暴露，
    /// 让 DemoHarness 可以聚合统一的健康视图（快照流、插值、恢复、输入、验证），而不是继续扩展校正报告。
    /// 该事件刻意保持轻量：包含类别、严重级别、关联帧，以及一个含义由类别决定的数值负载
    /// （例如重放 tick 数、丢弃快照数、重映射帧）。
    /// </summary>
    public readonly struct SyncCorrelationContext : IEquatable<SyncCorrelationContext>
    {
        public SyncCorrelationContext(
            string? correlationId,
            string? runId = null,
            string? sessionId = null,
            string? accountId = null,
            string? playerId = null,
            string? roomId = null,
            string? battleId = null,
            string? worldId = null,
            string? observerId = null,
            string? syncMode = null,
            long tick = 0L,
            ulong commandSequence = 0UL,
            long snapshotSequence = 0L,
            long snapshotBaseline = 0L,
            long reliableEventSequence = 0L,
            string? reliableEventEpoch = null)
        {
            CorrelationId = correlationId ?? string.Empty;
            RunId = runId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            ObserverId = observerId ?? string.Empty;
            SyncMode = syncMode ?? string.Empty;
            Tick = tick;
            CommandSequence = commandSequence;
            SnapshotSequence = snapshotSequence;
            SnapshotBaseline = snapshotBaseline;
            ReliableEventSequence = reliableEventSequence;
            ReliableEventEpoch = reliableEventEpoch ?? string.Empty;
        }

        public string CorrelationId { get; }
        public string RunId { get; }
        public string SessionId { get; }
        public string AccountId { get; }
        public string PlayerId { get; }
        public string RoomId { get; }
        public string BattleId { get; }
        public string WorldId { get; }
        public string ObserverId { get; }
        public string SyncMode { get; }
        public long Tick { get; }
        public ulong CommandSequence { get; }
        public long SnapshotSequence { get; }
        public long SnapshotBaseline { get; }
        public long ReliableEventSequence { get; }
        public string ReliableEventEpoch { get; }
        public bool HasCorrelation => !string.IsNullOrEmpty(CorrelationId);

        public bool Equals(SyncCorrelationContext other)
        {
            return CorrelationId == other.CorrelationId && RunId == other.RunId &&
                   SessionId == other.SessionId && AccountId == other.AccountId &&
                   PlayerId == other.PlayerId && RoomId == other.RoomId && BattleId == other.BattleId &&
                   WorldId == other.WorldId && ObserverId == other.ObserverId && SyncMode == other.SyncMode &&
                   Tick == other.Tick && CommandSequence == other.CommandSequence &&
                   SnapshotSequence == other.SnapshotSequence && SnapshotBaseline == other.SnapshotBaseline &&
                   ReliableEventSequence == other.ReliableEventSequence && ReliableEventEpoch == other.ReliableEventEpoch;
        }

        public override bool Equals(object? obj) => obj is SyncCorrelationContext other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CorrelationId, RunId, SessionId, AccountId, PlayerId, RoomId, BattleId, WorldId);
    }

    public readonly struct SyncHealthEvent : IEquatable<SyncHealthEvent>
    {
        public SyncHealthEvent(SyncHealthEventKind kind, SyncHealthSeverity severity, int frame = 0, long value = 0L)
            : this(kind, severity, frame, value, default)
        {
        }

        public SyncHealthEvent(
            SyncHealthEventKind kind,
            SyncHealthSeverity severity,
            int frame,
            long value,
            SyncCorrelationContext context)
        {
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));

            Kind = kind;
            Severity = severity;
            Frame = frame;
            Value = value;
            Context = context;
        }

        public SyncHealthEventKind Kind { get; }

        public SyncHealthSeverity Severity { get; }

        public int Frame { get; }

        public long Value { get; }

        public SyncCorrelationContext Context { get; }

        public string CorrelationId => Context.CorrelationId ?? string.Empty;

        public bool HasEvent => Kind != SyncHealthEventKind.None;

        public static SyncHealthEvent None { get; } = default;

        /// <summary>创建指定类别的 <see cref="SyncHealthSeverity.Info"/> 事件。</summary>
        public static SyncHealthEvent Info(SyncHealthEventKind kind, int frame = 0, long value = 0L)
        {
            return new SyncHealthEvent(kind, SyncHealthSeverity.Info, frame, value);
        }

        /// <summary>创建指定类别的 <see cref="SyncHealthSeverity.Warning"/> 事件。</summary>
        public static SyncHealthEvent Warning(SyncHealthEventKind kind, int frame = 0, long value = 0L)
        {
            return new SyncHealthEvent(kind, SyncHealthSeverity.Warning, frame, value);
        }

        /// <summary>创建指定类别的 <see cref="SyncHealthSeverity.Error"/> 事件。</summary>
        public static SyncHealthEvent Error(SyncHealthEventKind kind, int frame = 0, long value = 0L)
        {
            return new SyncHealthEvent(kind, SyncHealthSeverity.Error, frame, value);
        }

        public SyncHealthEvent WithContext(in SyncCorrelationContext context)
        {
            return new SyncHealthEvent(Kind, Severity, Frame, Value, context);
        }

        public bool Equals(SyncHealthEvent other)
        {
            return Kind == other.Kind &&
                   Severity == other.Severity &&
                   Frame == other.Frame &&
                   Value == other.Value &&
                   Context.Equals(other.Context);
        }

        public override bool Equals(object? obj)
        {
            return obj is SyncHealthEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Severity, Frame, Value, Context);
        }

        public static bool operator ==(SyncHealthEvent left, SyncHealthEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SyncHealthEvent left, SyncHealthEvent right)
        {
            return !left.Equals(right);
        }
    }
}
