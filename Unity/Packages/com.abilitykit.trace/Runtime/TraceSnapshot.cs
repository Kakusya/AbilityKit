using System;

namespace AbilityKit.Trace
{
    /// <summary>
    /// 溯源节点快照（不可变结构体）
    /// 核心字段直接来自 TraceContextRecord，玩法数据通过 Metadata 字段访问
    /// </summary>
    [Serializable]
    public readonly struct TraceSnapshot<T> : IEquatable<TraceSnapshot<T>>
        where T : TraceMetadata
    {
        // 框架核心字段
        public long ContextId { get; }
        public long RootId { get; }
        public long ParentId { get; }
        public int Kind { get; }
        public int CreatedFrame { get; }
        public int EndedFrame { get; }
        public int EndReason { get; }
        public int ChildCount { get; }

        // 玩法元数据
        public T Metadata { get; }

        public bool IsEnded { get; }
        public bool IsRoot => ContextId == RootId;
        public bool IsLeaf => ChildCount == 0;
        public bool HasChildren => ChildCount > 0;
        public bool IsValid => ContextId != 0;

        public TraceSnapshot(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int endedFrame,
            int endReason,
            int childCount,
            T metadata)
            : this(
                contextId,
                rootId,
                parentId,
                kind,
                0,
                endedFrame,
                endReason,
                childCount,
                metadata,
                endedFrame != 0)
        {
        }

        public TraceSnapshot(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int createdFrame,
            int endedFrame,
            int endReason,
            int childCount,
            T metadata,
            bool isEnded)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            ChildCount = childCount;
            Metadata = metadata;
            IsEnded = isEnded;
        }

        public bool Equals(TraceSnapshot<T> other) => ContextId == other.ContextId;
        public override bool Equals(object obj) => obj is TraceSnapshot<T> other && Equals(other);
        public override int GetHashCode() => ContextId.GetHashCode();
        public static bool operator ==(TraceSnapshot<T> left, TraceSnapshot<T> right) => left.Equals(right);
        public static bool operator !=(TraceSnapshot<T> left, TraceSnapshot<T> right) => !left.Equals(right);

        public override string ToString()
        {
            return $"Trace[Id={ContextId}, Root={RootId}, Kind={Kind}, Ended={IsEnded}]";
        }
    }
}
