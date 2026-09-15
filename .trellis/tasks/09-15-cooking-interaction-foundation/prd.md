# P0 交互基础

## 当前状态

- Trellis task status：`in_progress`；迁移元数据：`planned`。遗留来源仍为只读追溯材料，不是实施完成证据。
- 本次实施范围仅为纯 .NET 的权威业务闭环：T01-T08 已由 `src/AbilityKit.Game.Cooking.Tests/` 实际运行并通过。
- T09-T10（Unity projection、scene/config fixture 与 EditMode）明确 `deferred`：未实施、未执行、未验证，不得视为通过或关闭。
- 纯 .NET 测试命令：`COOKING_EVIDENCE_DIRECTORY="$(pwd)/artifacts/cooking-interaction-foundation" dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj`。
- 本次命令实际生成 8 个 JSONL 文件、21 条验收记录，位于 `artifacts/cooking-interaction-foundation/`；该目录按仓库规则忽略。完整测试 10/10 通过，其中 T01-T08 为能力验收，另有 2 个领域不变量回归测试。JSONL 是可审阅证据，不能替代 `dotnet test` 退出码与测试结果。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-interaction-foundation/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。

## Why

做菜经营游戏的首个垂直切片需要一个可在纯 C# 中裁决的最小交互基础。目前路线图只定义了方向，尚无拾取/放下、唯一所有权和主机本地玩家与远端玩家共用验证路径的正式实现；先建立这一窄切片可在不选定网络传输、不引入真实 LAN 或 Unity 表现层的情况下验证核心争抢与失败语义。

## 首期实现范围

- 独立应用层项目 `src/AbilityKit.Game.Cooking/` 与 xUnit 测试项目 `src/AbilityKit.Game.Cooking.Tests/`；它们是纯 .NET `net10.0` 项目，不引用 Unity、Host transport、Orleans 或 NUnit。
- 最小 Session/World/Match/Player/Item/Definition/Command identity 与命令作用域。该 seam 只支持拒绝错误作用域、已移除和 stale 实例；不拥有完整 Room/Match 创建、销毁、重连或迁移生命周期。
- `WorldPosition | PlayerHand | StationSlot | ContainerSlot` 的显式位置模型；首期 fixture 只使用 world、hand、station，容量、资格和范围均来自 fixture，不构成产品人数或一人一物规则。
- pickup/drop 的唯一权威入口：封闭批次按 `(simulation batch, player identity, command identity)` 稳定排序，验证后原子提交，拒绝不产生状态或事件变更。
- host-local 与 remote-in-process adapter 只向同一 handler 提交相同 command envelope，不拥有规则，也不创建真实网络传输。
- canonical snapshot、领域事件和按 Session + Player + Command 作用域的幂等结果，以支持 mutation-free 拒绝、争抢和重排比较。
- T01-T08 的 xUnit 验收测试；每次被记录的命令产出 JSONL 证据，包含 test ID、fixture、scope、排序键、command、结果/拒绝原因、事件摘要、前后状态 SHA-256、断言摘要、runner 和时间。

## 明确延期范围

- Unity package、asmdef、`MonoBehaviour`、GameObject/Transform、scene fixture、projection 和 EditMode 测试。
- T09：带版本 Unity projection 的新旧/未知状态处理。
- T10：最小 Unity scene/config fixture 的加载与解析 smoke。
- 网络 transport、wire codec、监听端口、真实 LAN/WAN、完整 Room/Match 生命周期、重连、主机迁移、存档、配方、订单、移动、计时、预测与 rollback。

## Capabilities

### New Capabilities

- `cooking-interaction-foundation`：定义厨房物品实例、位置/所有权、pickup/drop 的权威验证、原子提交、幂等与纯 .NET adapter 边界；Unity projection 是同能力的后续阶段。

### Modified Capabilities

- 无。

## Impact

- 烹饪领域规则保留在新的应用层 .NET 项目，不加入 `Unity/Packages/com.abilitykit.ability`、`src/AbilityKit.Ability` 或 `Server/`。
- 首期直接以 `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj` 验证，不因未创建 Unity 宿主而跳过 T01-T08。
- [技术路线图](../../../Docs/design/CookingGame/technical-roadmap.md)、[ADR-0001](../../../ADR/decisions/0001-player-host.md) 与 [ADR-0002](../../../ADR/decisions/0002-authoritative-fixed-tick-state-sync.md) 仍是边界来源。本任务不锁定帧率、严格 lockstep、网络库、房主退出、断线恢复或存档归属。
