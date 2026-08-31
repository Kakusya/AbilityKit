#nullable enable

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 客户端/服务器同步策略在单个 tick 中可发出的玩法无关同步健康事件分类。这是同步抽象审计（§6.5）
    /// 要求的统一诊断词汇：<see cref="SyncReconciliationReport"/> 保持聚焦预测/回滚校正，而更广泛的
    /// 生命周期信号（快照流、插值饥饿、恢复请求、输入接收、延迟补偿验证）会作为离散
    /// <see cref="SyncHealthEvent"/> 上报，让 DemoHarness 无需膨胀校正报告即可聚合它们。
    /// </summary>
    public enum SyncHealthEventKind
    {
        /// <summary>无事件；默认/空槽位。</summary>
        None = 0,

        // 快照流。
        SnapshotReceived = 1,
        SnapshotDropped = 2,
        SnapshotStale = 3,
        SnapshotGap = 4,

        // 远端插值播放。
        InterpolationStarved = 10,
        InterpolationRecovered = 11,

        // 本地预测 / 回滚。
        RollbackStarted = 20,
        ReplayCompleted = 21,

        // 恢复流程。
        FullSnapshotRequested = 30,
        FullSnapshotApplied = 31,
        KeyFrameRequested = 32,
        KeyFrameApplied = 33,
        AoiSliceRequested = 34,
        AoiSliceApplied = 35,

        // 输入接收。
        InputAccepted = 40,
        InputRemapped = 41,
        InputRejected = 42,

        // 服务器侧验证。
        LagCompensatedValidationAccepted = 50,
        LagCompensatedValidationRejected = 51,

        // Observer 发送队列与基线。
        ObserverSnapshotQueued = 60,
        ObserverSnapshotDropped = 61,
        ObserverSnapshotCoalesced = 62,
        ObserverBaselineInvalidated = 63,

        // 可靠事件流。
        ReliableEventGap = 70
    }

    /// <summary>
    /// <see cref="SyncHealthEvent"/> 的严重程度分层，让 harness 与 UI 无需在各处硬编码分类，
    /// 即可区分常规信号、降级和故障。
    /// </summary>
    public enum SyncHealthSeverity
    {
        /// <summary>常规、预期内信号（例如收到快照、输入被接收）。</summary>
        Info = 0,

        /// <summary>可恢复降级（例如插值饥饿、快照丢弃）。</summary>
        Warning = 1,

        /// <summary>需要校正/恢复的故障（例如快照 gap、输入被拒绝）。</summary>
        Error = 2
    }
}
