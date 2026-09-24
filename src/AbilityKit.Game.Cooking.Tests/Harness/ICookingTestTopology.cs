using AbilityKit.Game.Cooking;

namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 代表统一抽象的测试执行拓扑环境。
/// 支持单机内存直接模拟、Host/Client 进程内双向传输管道、以及未来真实网络传输。
/// </summary>
public interface ICookingTestTopology : IAsyncDisposable
{
    string Name { get; }
    CookingScope Scope { get; }
    CookingLevelScope LevelScope { get; }

    /// <summary>
    /// 获取指定角色的测试操作门面。
    /// </summary>
    ICookingActor GetActor(PlayerId playerId);

    /// <summary>
    /// 推进固定逻辑时钟 Tick。
    /// </summary>
    Task AdvanceTicksAsync(int tickCount, CancellationToken ct = default);

    /// <summary>
    /// 等待权威端与客户端状态同步排空。
    /// </summary>
    Task SyncAndDrainAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// 校验参与的所有节点状态哈希是否严格达成共识一致。
    /// </summary>
    void AssertStateHashConsensus();

    /// <summary>
    /// 获取当前权威端最新快照。
    /// </summary>
    CookingRecipeSnapshot GetAuthoritySnapshot();
}
