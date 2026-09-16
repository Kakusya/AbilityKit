# P0 交互基础

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

## 当前状态

- 本 task 已于 2026-09-16 归档，Trellis status 为 `completed`；最终语义为 `completed-limited-scope`。遗留来源仍为只读追溯材料，不是实施完成证据。
- 本次实施范围仅为纯 .NET 的权威业务闭环：T01-T08 已由 `src/AbilityKit.Game.Cooking.Tests/` 实际运行并通过。
- T09-T10（Unity projection、scene/config fixture 与 EditMode）未实施、未执行、未验证，并已移入长期禁止的统一 future scope；它们不属于本 task 归档条件。
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
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**T01-T08 纯 .NET authority/interaction contract（10/10；8 个 JSONL、21 条记录）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- P0 无 non-Unity successor；T09-T10 及全部 Unity 执行移至 `Docs/design/CookingGame/future-scope.md`。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P0、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始 check events 保持不变；仅删除无效的 artifact `file` context，其余只追加 closure 记录 2026-09-16 的 owner scope 决定。
