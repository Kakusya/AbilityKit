# P6 响应性与网络测量

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

## 当前受限实施状态与验收矩阵协调

- 本 task 已于 2026-09-16 归档，Trellis status 为 `completed`；最终语义为 `completed-limited-scope`，只覆盖已验证的 InProcess pure .NET baseline。
- 本轮已实施且将验证的仅是纯 .NET、显式 `InProcess` 的 baseline measurement contract：immutable workload/hash、authority ingress-to-commit/queue/throughput/p50-p95-p99/allocation/state-hash 报告、逻辑 application-fault trace（delay/jitter/loss/duplicate/reorder/disconnect）、JSON/CSV/JSONL artifacts 与 optimization `Blocked` gate。
- 它消费现有 P1 `CookingSessionAuthority` 的 handshake、queue、baseline/delta、epoch/sequence 和 transport-loss 合同；不修改 P1，也不创建 socket、stream decoder、production adapter 或两 PC LAN runner。
- **验收矩阵协调：**N01–N08 是 task/spec/迁移元数据中的 canonical future matrix；design/implement 的 N09/N10 是 supplemental future gates。本轮仅运行 N01/N04/N05 的受限 in-process subset，以及 N10 `UNSET`/unapproved blocked result；未声称 N02/N03/N06–N09 已执行。
- P0–P5 的 pure .NET contract evidence 可作为本轮 API 前置，但它们及 P6 的 Unity、真实 LAN、内容/schema、owner/product exits 仍未毕业。
- 阻塞：production transport、真实两 PC LAN、workload/采样窗口/拓扑覆盖/性能阈值、插值/预测/纠正范围和回退规则、reconnect semantics 均未获 owner 决策；任何阈值保持 `UNSET`。

- 完整迁移任务仍为 Draft/Blocked；原有未决项、future 测试和 owner 决策门保持未完成。
- 依赖：P1-P5 的可运行纵向链路与真实测量 harness。
- 阻塞：性能目标、插值/预测与重连是否启用均待批准；不得用同机证据替代真实两 PC LAN。
- 规划验证：N01-N08（均为 future 规划，尚未执行）

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-network-measurement/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。


## 只读历史规划说明

- 下文仍保留的 `Draft`、`blocked`、future matrix 与未勾选 checklist 仅用于历史追溯，**不可执行、不是当前 task checklist，也不是本 limited delivery 的 blocker**。
- 未完成的 non-Unity 范围（production network measurement、真实两 PC LAN、批准阈值与非 Unity 优化）只以 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md) 为入口；如获批准必须新建 task。
- Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 Unity smoke 长期禁止；只见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)，不得按下文历史指令实施。

## 迁移的需求说明

## 只读历史 Draft / NOT ready to apply（不可执行、不是当前 blocker）

> 本阶段只创建规划文档，所有测试、基准和门禁均未执行。依赖阶段 1–6（P0–P5）的可运行垂直链路与实际实现/验收证据：`add-cooking-interaction-foundation`、`add-cooking-lan-session`、`add-cooking-recipe-loop`、`add-cooking-config-validation`、`add-cooking-match-lifecycle`、`add-cooking-persistence-management`；其中 P0 通过各阶段传递依赖，P1–P5 是本阶段直接需要的可运行链路。预计路径尚待对应 change 完成，不能将规划文件当作前置完成。性能阈值、插值/局部预测是否启用、纠正策略和重连语义均需基于真实测量及 owner 批准，未决时保持 Draft/Blocked。

## Why

阶段 7 需要用同一套可复现 workload 分开观察同机多实例与两台 PC LAN 的响应性、负载和同步故障，而不是凭主观流畅度选择 30Hz、插值或预测。只有先取得延迟、抖动、丢失/重复/乱序和负载证据，才能在不污染权威状态的前提下批准合适的表现优化及明确回退路径。

## What Changes

- 新增网络测量与报告契约：采集 ingress-to-commit、snapshot decode/apply、队列深度、吞吐、p50/p95/p99、丢失/重复/乱序错误率、内存/分配等指标，并保留 workload、环境和版本元数据。
- 将同机多实例流程与真实两台 PC LAN 测量分开验收；不把同机结果替代真实网卡、地址、防火墙和物理网络证据。
- 规划可注入延迟、抖动、丢失、重复、乱序和断线 fault 的测量 harness；区分 stream framing 证据与应用层 fault 证据。
- 只有 owner 根据真实报告批准后，才可启用客户端快照插值、局部预测和纠正；初始状态同步基线不依赖这些优化，不假定完整 rollback。
- 规定优化前后必须使用同一 workload/环境进行对照，并验证权威命令/结算/长期进度不受表现层污染；指标不达批准目标或数据不足时必须回退到确认快照基线并标记 unsynchronized/恢复需求。
- 不设置 30Hz、毫秒数、吞吐量或内存上限等默认网络阈值；不纳入 WAN/NAT/relay、账号平台、自动重连或主机迁移。

## Capabilities

### New Capabilities
- `cooking-network-measurement`: 定义做菜 LAN 的可复现测量、报告、证据分层，以及经批准的插值/局部预测/纠正优化与安全回退。

### Modified Capabilities
- 无。

## Impact

- 未来影响应用层 network/session measurement harness、snapshot playback/projection、可选 prediction/reconciliation seam、Unity 表现入口和 .NET 测试/benchmark 工程；所有 cooking 宿主路径标为拟建并在实施时确认。
- 依赖阶段 1–6（P0–P5）中 `add-cooking-lan-session` 的 session、baseline/delta、sequence 和 fault 边界，以及阶段 3–6（P2–P5）的 recipe/config/match/persistence 实际证据；P0 交互基础和 P1 LAN 通过前置链路传递，所有前置 change 尚待完成，本文不假称已实现。
- 需引用 `Docs/design/07-NetworkSynchronization/02-StateSync.md`、`03-RollbackPrediction.md`、`03.1-PredictionReconciliationDesign.md` 和 `08-NetworkOptimizationPlan.md` 的真实边界，但不把 Demo 专用实现升级为通用默认能力。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**InProcess 纯 .NET measurement/fault diagnostic baseline（focused 5/5；完整回归 57/57）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- production transport 测量、真实两 PC LAN、批准阈值和非 Unity 优化后续移至 successor backlog；Unity 移至 future scope。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P6、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始检查事件保持不变；closure 只记录 2026-09-16 的 owner scope 决定。
