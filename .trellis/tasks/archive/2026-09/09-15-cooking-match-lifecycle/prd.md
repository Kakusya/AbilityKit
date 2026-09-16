# P4 关卡/地图/Match 生命周期

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

## 当前实施状态

- 本 task 已于 2026-09-16 归档，Trellis status 为 `completed`；最终语义为 `completed-limited-scope`，只覆盖已验证的 fixture-only pure .NET subset。
- 已实现并验证受限的纯 .NET fixture-only logical Level/Map/Layout preparation、`Preparing → Ready → Started → Ended` 状态机、新 MatchId/epoch 重开、P2 gameplay instance 隔离与 lifecycle snapshot watermark contract。
- 已实际通过 M01、M02、M03、M04、M05、M06 及 M07 的纯 .NET snapshot 顺序部分；这不代表正式 Level/Map schema、Unity authoring/projection、P1 LAN 或完整 P4 已完成。
- 当前依赖为已提交的 P3 definition configuration identity；Process graph/Level 表、正式内容、真实 P1 session/LAN 和 Unity layout export 未落地。
- 阻塞：房间人数、房主退出、断线恢复、主机迁移、保存与结算 owner 未确认；本轮只返回 blocked，不推导策略。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-match-lifecycle/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。


## 只读历史规划说明

- 下文仍保留的 `Draft`、`blocked`、future matrix 与未勾选 checklist 仅用于历史追溯，**不可执行、不是当前 task checklist，也不是本 limited delivery 的 blocker**。
- 未完成的 non-Unity 范围（正式 Room/Level/Map、产品生命周期语义与真实 LAN）只以 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md) 为入口；如获批准必须新建 task。
- Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 Unity smoke 长期禁止；只见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)，不得按下文历史指令实施。

## 迁移的需求说明

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
- **只读历史、不可执行：**该 Unity package/asmdef/scene/EditMode/host 指令已由 prohibited future scope 取代；本 task 不实施，也不把它作为 blocker。
- 阶段出口要求完整 lifecycle 与 snapshot/version/multi-instance 证据；P1 LAN 集成和 D1-D4 决策门不被 P4 单机工作静默删除。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**fixture-only 纯 .NET lifecycle/snapshot 增量（focused 6/6；当时完整回归 40/40）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- 正式 Room/Level/Map 生命周期、产品退出语义与真实 LAN 移至 successor backlog；Unity 移至 future scope。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P4、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始检查事件保持不变；closure 只记录 2026-09-16 的 owner scope 决定。
