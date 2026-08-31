#nullable enable

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 客户端同步策略判定本地状态已偏离权威服务器状态并需要校正的玩法无关原因。
    /// 它统一了各个示例过去通过自定义枚举表达的含义（例如 Shooter 的 ShooterClientResyncReason），
    /// 让演示框架可以跨所有 <see cref="NetworkSyncModel"/> 统一报告校正。
    /// </summary>
    public enum SyncReconciliationReason
    {
        /// <summary>不需要校正；本地状态与服务器保持同步。</summary>
        None = 0,

        /// <summary>导入/应用权威快照失败。</summary>
        ImportFailed = 1,

        /// <summary>权威状态哈希与本地预测哈希不一致。</summary>
        AuthoritativeHashMismatch = 2,

        /// <summary>服务器拒绝了客户端上报的状态哈希。</summary>
        ClientHashRejected = 3,

        /// <summary>本地模拟落后权威帧过多，无法增量追赶。</summary>
        FrameTooFarBehind = 4,

        /// <summary>本地模拟超前权威帧过多。</summary>
        FrameTooFarAhead = 5,

        /// <summary>预期的权威快照未在允许窗口内到达。</summary>
        SnapshotTimeout = 6,

        /// <summary>权威更新指向了与本地不同的世界/会话。</summary>
        WorldMismatch = 7,

        /// <summary>本地角色 pose 已按权威状态和未确认输入进行局部校正。</summary>
        LocalAuthorityCorrection = 8
    }
}
