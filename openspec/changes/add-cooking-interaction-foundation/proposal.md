## Why

做菜经营游戏的首个垂直切片需要一个可在纯 C# 中裁决、又能由 Unity 表现层消费的最小交互基础。目前路线图只定义了方向，尚无拾取/放下、唯一所有权和主机本地玩家与远端玩家共用验证路径的正式行为契约；先建立这一窄切片可在不选定网络传输、不引入真实 LAN 的情况下验证核心争抢与失败语义。

## What Changes

- 新增最小厨房物品交互能力：一个可移动物品、玩家手持槽、站点放置槽，以及拾取/放下命令。
- 以 World/Match/Session/Player/Item 的身份边界表达实例、位置和生命周期；配置的资格、范围与槽位容量参与验证。
- 所有命令经过稳定排序、幂等去重、验证和原子提交；拒绝不得改变状态。
- 为 host 本地玩家与 remote 逻辑玩家提供相同的 in-process adapter 入口；不决定网络传输，也不实现真实 LAN。
- 将纯 C# 状态投影给 Unity 表现层；投影不是权威，Unity GameObject 身份不作为模拟身份。
- 添加一个仅用于测试/演示的最小场景配置 fixture（两名逻辑玩家、一个物品、手和站点槽）；不做全局“一人一物”玩法决定，不加入配方、存档、公共网络或完整地图。
- 为 .NET 单元测试、两消费者 host/remote 集成测试和 Unity EditMode/投影/场景 smoke 预留明确的未来项目路径与门禁接入点；实现前不声称这些测试项目已经存在或已运行。

## Capabilities

### New Capabilities
- `cooking-interaction-foundation`: 定义厨房物品实例、位置/所有权、拾取放下命令的权威验证、原子提交、幂等与 Unity 投影边界。

### Modified Capabilities

- 无。

## Impact

- 未来实现将复用 `Unity/Packages/` 共享源码，并同时验证引用这些源码的 .NET SDK 项目与 Unity `.asmdef`；不得只验证一个宿主。
- 预期涉及纯 C# 领域/命令/适配器与 Unity Runtime/Editor 测试 fixture；具体项目路径以实施时仓库结构为准，本文档不创建业务代码。
- 依赖 [技术路线图](../../../Docs/design/CookingGame/technical-roadmap.md)、[ADR-0001](../../../ADR/decisions/0001-player-host.md) 与 [ADR-0002](../../../ADR/decisions/0002-authoritative-fixed-tick-state-sync.md)。本 change 不锁定帧率、严格 lockstep、网络库、房主退出、断线恢复或存档归属。
