# Cooking Game 当前工程进度

> 文档类型：跨阶段状态入口，不是行为规格或新的完成证明。行为契约以 [`.trellis/spec/cooking/`](../../../.trellis/spec/cooking/index.md) 为准；历史命令与结果以对应归档 task 的 `check.jsonl` 为准。

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

纯 .NET fixture 已能无界面地创建 scope 与实例、完成 in-process handshake、执行权威交互、运行最小配方、推进 Match、应用 settlement/progress，并生成 InProcess 测量诊断。此外曾有 pure .NET Cooking UDP 链路（LiteNetLib reliable-UDP loopback 与同机双进程 listen-host/client），其 task 已于 2026-09-21 因 owner 决定放弃并归档；三个 Cooking UDP 项目退出当前构建范围，源码与 artifact 证据保留，不得据此宣称能力被删除或从未存在。真实 artifact 仍将其标为 same-machine，且两台物理 PC LAN 未运行。以上证明有限领域合同、测试 transport 边界和同机真实 socket 曾经可以运行；不证明 Unity 表现、物理两机 LAN 或 durable storage，也不证明当前范围包含网络内容。2026-09-21 起，单机纯 .NET 范围另有一条可运行的番茄蛋花汤厨房闭环，其契约层与仿真规则分两个 task 交付，见第 4 节。

## 4. 2026-09-21 单机厨房闭环增量

Owner 于 2026-09-21 把范围收敛为单机纯 C#/.NET，并按两个 Trellis task 交付厨房闭环的契约层与仿真规则；两个 task 均已归档，均不含传输、UDP/KCP 与 Unity 内容。

| Task | 已验证并收口的受限交付 | Evidence |
|---|---|---|
| `09-21-cooking-kitchen-loop-contracts` | schema 升 `cooking-definition-v2`（物品容器能力、多输入集合、默认供应、完成形态；v1 配置与快照以结构化 blocked 诊断拒绝，不写迁移）、纯函数配方匹配（集合相等，歧义时返回确定候选表）、命令形状放松与仿真/ET 两套指纹跨面对齐 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-contracts/check.jsonl)；`artifacts/cooking-kitchen-loop-contracts/` |
| `09-21-cooking-kitchen-loop-simulation` | 容器即物品（`CookingContainerDefinition` 退役，内容物按容器物品 ID 归属，快照与 canonical 可观察）、七项权威原子动作（拾取/放下/放入/取出/启动加工/倒出/提交，`Plate` 退役）、放入即拒绝、锁输入与 item→process 反查、端走继续的位置白名单（腐败检测器保持可达）、两种完成形态进入 fixed-tick 原子交换、命令与 fixed-tick 共享产物 ID allocator、按容器当前位置判定提交可达性、番茄蛋花汤闭环 fixture（免工位打蛋、工位按加工类型绑定、烤箱配方、订单要求与完成回报、碗脏/净与带上限的干净碗池加 NPC 清洗注入端口）、批次争抢按 Tick/玩家/命令序号稳定排序 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-simulation/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-simulation/research/verification-2026-09-21.md)；`artifacts/cooking-kitchen-loop-domain/`（21 个目录） |

门禁：新增 P1 `cooking-kitchen-loop`（域构建、ET runtime 构建、focused `Gate=CookingKitchenLoop` 42/42、Cooking 163/163、ET runtime 40/40），回归门禁 `cooking-et-level-runtime` 同时通过；[`tools/test-gates.json`](../../../tools/test-gates.json) 与 [测试门禁规范](../../AbilityKit测试门禁与批量回归规范.md) §3 已同步。

以上只证明单机纯 .NET 厨房闭环规则与闭环 fixture 可运行、可确定性重放、可按门禁验证，且 ET 命令指纹金样已随 `Plate` 退役处理（pickup 与 StartProcess 两个既有金样字节不变，新增 PutIn 金样）。不证明失败条件与小关成功条件（owner 明确推迟）、订单生成节奏与前厅、固定伙伴最终人数、生产传输（KCP，未启动未批准）、durable storage、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。计划中任务③的单机验收动作（指纹金样重锚、验收 evidence 落盘、回归门禁实际运行与记录）已由任务①②连带完成，本文与 [Todo.md](../../Todo.md) 的状态表述即其收口，未为此新建独立 task。

2026-09-21 另有一条单机增量把正式内容与 order owner 落地，见第 5 节。

## 5. 2026-09-21 单机正式内容与 order owner 增量

Owner 于 2026-09-21 批准 Trellis task `09-21-cooking-formal-content-and-orders`（successor backlog P2 首条），把“单输入/单工序/3 Tick fixture + 测试端口判定订单要求”的临时形态替换为正式内容与领域订单归属；task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 正式内容目录：数据文档 `cooking-definition-v2`（`src/AbilityKit.Game.Cooking/Content/cooking-content-v2.json`）经 `CookingContentCatalog` 加载并过 v2 校验；10 物品、4 工位、4 配方（切番茄/打蛋/番茄蛋花汤/烤面包）、1 订单模板、5 项标准初始供应；加工时长等数值只存在于内容文档；烤面包按 owner 采纳版本“面包片→烤面包”，占位 dough 退役 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/research/verification-2026-09-21.md)；`artifacts/cooking-formal-content/` |
| v2 校验扩展：候选/canonical 增加订单模板与标准初始供应两段（身份字符串保持 v2）；订单模板外键（要求 recipe 存在、要求容器定义存在、带容器能力且接受该 recipe 产物）与供应项校验（定义存在、数量为正、位置语法、station 引用）；两处既有规则显式放宽（工位可声明零能力、免工位配方豁免 `CapabilityUnavailable`） | 同上；spec [cooking-config-validation](../../../.trellis/spec/cooking/cooking-config-validation.md) P3 行 |
| order owner 移入领域：订单簿（模板、要求、Open/Completed）与快照/canonical 可观察；开单是前厅注入入口（`OpenOrder`，与 `CompleteWash` 同模式，不产生命令事件）；`ICookingOrderPort`/`CookingOrderSubmission`/`CookingOrderAcceptance` 退役 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/check.jsonl) |
| 提交/结算契约：五个结构化拒绝分支（未开单、订单已完成、recipe 不匹配、容器定义不匹配、产物已消费）全部 mutation-free；成功提交原子消耗产物、订单 Completed 一次、追加结算记录（sequence/order/template/recipe/product/player/container/logicalTick，无评分字段）；闭环 fixture 改从正式内容运行并保持确定性重放 | 同上 |

门禁：沿用 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 54/54、Cooking 175/175、ET runtime 40/40，含三个二进制指纹金样字节不变），回归 `cooking-et-level-runtime` 同时通过；未新增 gate。

以上只证明正式内容可数据驱动加载与校验、订单归属与提交/结算契约在领域内确定成立、闭环可从正式内容确定性重放。不证明评分/收益/评价与小关结算（owner 推迟，结算记录刻意不含这些字段）、失败条件与失败重试（owner 推迟）、订单生成节奏与前厅顾客/NPC 过程（订单只能由前厅经 `OpenOrder` 注入）、过度加工与烧焦、跨小关装修/道具/Buff、检查点、Level/Map schema 对内容的正式引用（P3）、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。

## 6. 未完成范围

### Non-Unity successor backlog

P1–P6 尚可另行审议的工作包括两台物理 PC LAN 验收、正式 recipe/order/content/schema、Room/Match 产品语义、durable store、process-crash 恢复、批准 workload/threshold 和非 Unity 优化验证。原 UDP task 曾提供 LiteNetLib minimal wire/adapter、loopback 与 same-machine harness，但未替代两 PC 证据，也未解决完整 production transport 的认证、安全、重连或产品生命周期语义；该方向已于 2026-09-21 放弃，传输计划改用 KCP，属未启动、未批准、无时间表的后续工作。其两机执行入口见 [UDP two-PC LAN acceptance](udp-two-pc-lan-acceptance.md)（已退役，仅作历史参考）。其余工作均未启动、未批准、没有时间表，详见 [successor backlog](successor-backlog.md)。2026-09-21 单机厨房闭环与正式内容增量落地后，与现状衔接最直接的是 P2 余下的评分、收益、评价与小关结算契约，以及失败条件与失败重试（均属 owner 明确推迟项，需重新审议后立项）；订单生成节奏与前厅属 P2/P4 交叉，依赖生产 session 的跨进程与真实 LAN recipe-loop 验收仍以 R07 形式挂起。上述均未批准，也未为此新建 task。

### Prohibited Unity scope

Cooking Unity 应用层、场景、authoring/export、projection、UI、动画、EditMode 与 scene smoke 长期禁止实施，不再作为旧 task、successor 或完整出口的当前 blocker。其历史来源、跨宿主 authority/identity/stale-input 不变量和重新授权条件见 [future scope](future-scope.md)。

## 7. 后续读取顺序

1. 读取本文确认三类状态。
2. 读取 [技术路线](technical-roadmap.md)、[交付计划](delivery-plan.md) 与 [Cooking spec index](../../../.trellis/spec/cooking/index.md)。
3. 需要历史证据时读取归档后的 task PRD/design/implement/check；不得因归档推导完整产品完成。
4. 只有 owner 明确批准新范围后才新建 Trellis task；不要恢复已归档 task。
5. Unity 重新授权必须满足 [future scope](future-scope.md) 的独立条件；non-Unity 后续从 [successor backlog](successor-backlog.md) 选择并重新审议。

## 8. 维护规则

- 本文只汇总状态与链接，不复制行为契约、测试矩阵或未来 checklist。
- 未实际运行的 Unity、LAN、protocol、durability 或 global gate 继续是 not-run/未完成，不能因 task archive 记为通过。
- `.trellis/migration/legacy-cooking-changes/` 保持只读。
