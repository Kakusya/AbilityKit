## Why

阶段 5/P4 需要把已验证的配置与阶段 1/2 的身份、位置和会话边界组合成可重入的房间/对局生命周期。仅有 snapshot 不能证明地图加载、准备、开始、结束和重新开局语义；本 change 建立最小地图到 Match 的生命周期契约，不扩展长期经营，也不猜房主退出、重连或迁移策略。

## What Changes

- 新增最小 Map/Level/Room/Match 生命周期：加载地图逻辑布局、准备、开始、结束、清理并允许重新开局。
- 明确 Unity authoring 到逻辑 layout 的边界；Unity GameObject 身份不得作为网络或模拟身份。
- 将 Match 实例与 session/config identity 绑定，隔离多实例、快照版本和阶段 3 业务状态；非法迁移和跨 Match 污染必须阻断。
- 覆盖可操作的准备→开始→结束→重新开局验收，不把 lifecycle 退化为 snapshot 接入。
- 保持 scope：不实现长期存档/经营，不决定 host exit、断线恢复、主机迁移、人数上限或首发平台；跨阶段未决事项仅链接 owner 入口。
- 依赖关系明确：阶段 5/P4 依赖阶段 1/P0 identity/location/lifecycle、阶段 2/P1 session（含 LAN 需要的阶段 2 证据）和阶段 4/P3 config/layout validation；后续阶段 6/P5 persistence 与阶段 7/P6 network measurement 仅作为后续依赖，不假定文件已存在。

## Capabilities

### New Capabilities
- `cooking-match-lifecycle`: 定义地图/关卡逻辑布局加载与 Room/Match 准备、开始、结束、重新开局及隔离边界。

### Modified Capabilities
- 无；阶段 1/2 仍为规划，阶段 3/4 变更不在此复制或修改。

## Impact

- 未来影响应用层纯 C# Room/Match 状态与 Unity layout authoring/projection seam，以及阶段 2 session/snapshot 接入；不修改通用 AbilityKit 或 Server/Orleans 规则。
- 实施时同时核对 Unity package、对应 .NET `.csproj` Compile Include 与 `.asmdef`；未知测试宿主和场景路径标为 future。
- 阶段出口要求完整 lifecycle 与 snapshot/version/multi-instance 证据；P1 LAN 集成和 D1-D4 决策门不被 P4 单机工作静默删除。
