# Cooking Game 当前工程进度

> 文档类型：跨阶段状态入口，不是行为规格或新的完成证明。行为契约以 [`.trellis/spec/cooking/`](../../../.trellis/spec/cooking/index.md) 为准；历史命令与结果以七个 task 的 `check.jsonl` 为准。

## 1. 2026-09-16 治理结论

Owner 已批准把过去混在同一阶段 task 中的状态拆为三类：

1. **Archived verified delivery**：P0–P6 各自已有 `check.jsonl` 支持的纯 .NET 受限增量，按 `completed-limited-scope` 语义归档。
2. **Non-Unity successor backlog**：P1–P6 仍可能有价值但未启动、未批准、无时间表的产品化工作，统一见 [successor backlog](successor-backlog.md)；P0 无 successor。
3. **Prohibited Unity future scope**：Cooking Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 scene smoke 长期禁止、不可领取、非 blocker，统一见 [future scope](future-scope.md)。

`archived`/`completed` 只修饰重划后的有限纯 .NET 交付，不表示完整 P0–P6 产品出口、Unity 可玩版本、production transport、真实两 PC LAN 或 durable storage 已完成。

## 2. Archived verified delivery

| 阶段 | 已验证并收口的受限交付 | 历史 evidence |
|---|---|---|
| **P0 交互基础** | 纯 .NET 权威拾取/放下、唯一位置、稳定排序、原子校验提交、命令幂等与 canonical snapshot/hash | `10/10`；8 个 JSONL、21 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-interaction-foundation/check.jsonl) |
| **P1 会话权威** | transport-neutral binding、handshake、bounded ingress、baseline/delta、epoch/sequence、dedup 与 transport-loss diagnostics | build 0 warning/error；权威摘要 `20/20`；6 个 JSONL、50 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-lan-session/check.jsonl) |
| **P2 配方循环** | 单输入/单工序/3 tick fixture、product、plate、注入式 order accept/reject、幂等与 in-process equivalence | 当时完整回归 `27/27`；5 个 JSONL、19 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-recipe-loop/check.jsonl) |
| **P3 配置校验** | definition batch 全错误诊断、原子替换、不可变 snapshot、canonical identity/hash、schema migration blocked result | focused `6/6`，当时完整回归 `34/34`；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-config-validation/check.jsonl) |
| **P4 Match 生命周期** | fixture `Preparing → Ready → Started → Ended`、restart 新 Match/epoch、隔离、snapshot watermark | focused `6/6`，当时完整回归 `40/40`；10 条 evidence；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-match-lifecycle/check.jsonl) |
| **P5 持久化技术合同** | 长期 progress、confirmed settlement、幂等 ledger、integrity envelope、in-memory `prepare → commit → read` 与 validated restart | focused `12/12`，当时完整回归 `52/52`；7 条 evidence；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-persistence-management/check.jsonl) |
| **P6 网络测量基线** | 显式 `InProcess` workload/report、ingress-to-commit、queue/throughput/p50/p95/p99/allocation、fault trace 与 optimization blocker | focused `5/5`，完整回归 `57/57`；JSON/CSV/JSONL；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-network-measurement/check.jsonl) |

P1 旧 metadata 曾写 `19/19`，属于摘要漂移；原始权威 check event 为 `20/20`，本次不改写该历史事件，只修正摘要。P0 的运行时 artifact 目录仍可在 check event/evidence 文字中引用，但不再作为 manifest `file` context。

## 3. 当前可运行纵向链路

纯 .NET fixture 已能无界面地创建 scope 与实例、完成 in-process handshake、执行权威交互、运行最小配方、推进 Match、应用 settlement/progress，并生成 InProcess 测量诊断。另有一个未归档的 pure .NET Cooking UDP task，已验证 LiteNetLib reliable-UDP loopback 和同机双进程 listen-host/client 最小链路；真实 artifact 仍将其标为 same-machine，且两台物理 PC LAN 未运行。以上证明有限领域合同、测试 transport 边界和同机真实 socket 可以运行；不证明 Unity 表现、物理两机 LAN 或 durable storage。

## 4. 未完成范围

### Non-Unity successor backlog

P1–P6 尚可另行审议的工作包括两台物理 PC LAN 验收、正式 recipe/order/content/schema、Room/Match 产品语义、durable store、process-crash 恢复、批准 workload/threshold 和非 Unity 优化验证。当前 UDP task 已提供 LiteNetLib minimal wire/adapter、loopback 与 same-machine harness，但未替代上述两 PC 证据，也未解决完整 production transport 的认证、安全、重连或产品生命周期语义；其两机执行入口见 [UDP two-PC LAN acceptance](udp-two-pc-lan-acceptance.md)。其余工作均未启动、未批准、没有时间表，详见 [successor backlog](successor-backlog.md)。

### Prohibited Unity scope

Cooking Unity 应用层、场景、authoring/export、projection、UI、动画、EditMode 与 scene smoke 长期禁止实施，不再作为旧 task、successor 或完整出口的当前 blocker。其历史来源、跨宿主 authority/identity/stale-input 不变量和重新授权条件见 [future scope](future-scope.md)。

## 5. 后续读取顺序

1. 读取本文确认三类状态。
2. 读取 [技术路线](technical-roadmap.md)、[交付计划](delivery-plan.md) 与 [Cooking spec index](../../../.trellis/spec/cooking/index.md)。
3. 需要历史证据时读取归档后的 task PRD/design/implement/check；不得因归档推导完整产品完成。
4. 只有 owner 明确批准新范围后才新建 Trellis task；不要恢复七个旧 task。
5. Unity 重新授权必须满足 [future scope](future-scope.md) 的独立条件；non-Unity 后续从 [successor backlog](successor-backlog.md) 选择并重新审议。

## 6. 维护规则

- 本文只汇总状态与链接，不复制行为契约、测试矩阵或未来 checklist。
- 未实际运行的 Unity、LAN、protocol、durability 或 global gate 继续是 not-run/未完成，不能因 task archive 记为通过。
- `.trellis/migration/legacy-cooking-changes/` 保持只读。
