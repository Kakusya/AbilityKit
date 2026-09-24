using AbilityKit.Game.Cooking;

namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 代表一个由测试驱动的厨师角色门面（Actor DSL）。
/// 对高层测试用例屏蔽底层是单机本地直接调用还是网络命令封包。
/// </summary>
public interface ICookingActor
{
    PlayerId PlayerId { get; }

    /// <summary>
    /// 拾取物品。
    /// </summary>
    Task<CookingRecipeCommandResult> PickupAsync(ItemId item, CancellationToken ct = default);

    /// <summary>
    /// 放置物品到指定工位。
    /// </summary>
    Task<CookingRecipeCommandResult> DropAsync(ItemId item, StationSlotId station, CancellationToken ct = default);

    /// <summary>
    /// 将物品放入容器。
    /// </summary>
    Task<CookingRecipeCommandResult> PutInContainerAsync(ItemId item, ItemId container, CancellationToken ct = default);

    /// <summary>
    /// 启动加工（预处理切/打蛋/煮制等）。
    /// </summary>
    Task<CookingRecipeCommandResult> StartProcessAsync(RecipeId recipe, ItemId item, StationSlotId? station = null, CancellationToken ct = default);

    /// <summary>
    /// 装盘/倒出（例如从锅倒出到碗/盘）。
    /// </summary>
    Task<CookingRecipeCommandResult> PlateAsync(ItemId product, ItemId container, CancellationToken ct = default);

    /// <summary>
    /// 提交订单。
    /// </summary>
    Task<CookingRecipeCommandResult> SubmitOrderAsync(ItemId item, OrderId order, CancellationToken ct = default);

    /// <summary>
    /// 获取当前角色视角的最新快照（单机或 Host 为权威快照，Client 为投影快照）。
    /// </summary>
    CookingRecipeSnapshot GetCurrentSnapshot();
}
