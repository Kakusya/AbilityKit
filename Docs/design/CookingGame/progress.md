# Cooking Game 当前工程进度

> 文档类型：跨阶段执行状态入口，不是行为规格、实施清单或完成证明。
>
> 证据归属：行为契约以 [`.trellis/spec/cooking/`](../../../.trellis/spec/cooking/index.md) 为准；当前任务范围以对应 Trellis task 的 PRD、design 与 implement 为准；实际执行结果以各 task 的 `check.jsonl` 为准。本文只汇总状态并提供指针，来源冲突时不得用本文覆盖原始证据。

## 1. 当前基线

- 最后更新：2026-09-15。
- 最近的 Cooking 实现提交：`472fa7ec feat: add cooking network measurement baseline`。
- 最近的会话规划记录提交：`6694c112 docs: record cooking measurement plan`。
- 当前阶段结论：P0–P6 均已完成一轮获批的纯 .NET 受限增量，并有实际 build/test 证据；七个 task 仍为 `in_progress`，完整产品出口均未完成或未归档。
- 当前产品形态：可运行、可测试的无界面纯 C# 业务纵向原型；不是 Unity 可玩版本，不是真实 LAN 联机版本，也不是生产级存档版本。
- 路线中没有 P7。后续工作是补齐 P0–P6 的完整出口，而不是自行创建一个未规划的新阶段。

本文使用以下状态词：

- `verified-limited-scope`：获批的受限增量已经实现并执行验证，但不等于阶段完整完成。
- `blocked`：完整出口依赖尚未批准的产品决策、缺失宿主或缺失真实环境证据。
- `not-run`：对应命令或环境验收没有实际执行，不能记为通过。

## 2. P0–P6 执行状态

| 阶段 | 当前状态 | 已实现的受限范围 | 实际验证 | 完整出口主要 blocker |
|---|---|---|---|---|
| **P0 交互基础** | `verified-limited-scope` | 纯 .NET 权威拾取/放下、唯一位置、稳定排序、原子校验提交、命令幂等与 canonical snapshot/hash | `10/10`；8 个 JSONL 文件、21 条记录；[check evidence](../../../.trellis/tasks/09-15-cooking-interaction-foundation/check.jsonl)；提交 `7c85bdf9` | Unity projection、最小场景 fixture、EditMode T09–T10：`not-run` |
| **P1 会话权威** | `verified-limited-scope` | transport-neutral connection binding、handshake、bounded ingress、baseline/delta、epoch/sequence、dedup、transport-loss diagnostics | build 0 warning/error，完整当时回归 `20/20`；6 个诊断文件、50 条记录；[check evidence](../../../.trellis/tasks/09-15-cooking-lan-session/check.jsonl)；提交 `7c85bdf9` | D1–D4 owner decisions、production transport/adapter、正式 wire codec、Unity、同机 socket 与真实两 PC LAN、L10–L12 |
| **P2 配方循环** | `verified-limited-scope` | 单输入/单工序/3 tick fixture、product、plate、注入式 order accept/reject、幂等与 in-process equivalence | 当时完整回归 `27/27`；5 个 JSONL 文件、19 条记录；[check evidence](../../../.trellis/tasks/09-15-cooking-recipe-loop/check.jsonl)；提交 `7c85bdf9` | 正式配方和订单内容、order owner、score/settlement、失败 UX、Unity/config integration、真实 LAN R07 |
| **P3 配置校验** | `verified-limited-scope` | definition batch 全错误诊断、原子替换、不可变 snapshot、canonical identity/hash、schema migration blocked result | focused `6/6`，当时完整回归 `34/34`；[check evidence](../../../.trellis/tasks/09-15-cooking-config-validation/check.jsonl)；提交 `abaeac49` | Process graph、Level/Map schema、正式内容、host/client binding C06、旧配置/快照 migration policy |
| **P4 Match 生命周期** | `verified-limited-scope` | fixture `Preparing → Ready → Started → Ended`、restart 新 Match/epoch、gameplay isolation/closure、snapshot 顺序与合法状态迁移 | focused `6/6`，当时完整回归 `40/40`；10 条生命周期证据；[check evidence](../../../.trellis/tasks/09-15-cooking-match-lifecycle/check.jsonl)；提交 `0e6a57ab` | 正式 Level/Map/Process schema、Unity authoring/projection、真实 host/client 与两 PC LAN、M08/M09、房主退出/恢复/迁移/保存决策 |
| **P5 持久化技术合同** | `verified-limited-scope` | 长期 progress、confirmed settlement、幂等 ledger、integrity envelope、`prepare → commit → read` in-memory fault store、validated restart | focused `12/12`，当时完整回归 `52/52`；7 条 JSONL 证据；[check evidence](../../../.trellis/tasks/09-15-cooking-persistence-management/check.jsonl)；提交 `3a388b69` | 真实文件/数据库/云 store、process-crash durability、存档 owner/时机、退出/断电、migration/backup/recovery、encryption、Unity/host integration |
| **P6 网络测量基线** | `verified-limited-scope` | 显式 `InProcess` workload/report、ingress-to-commit、queue/throughput/p50/p95/p99/allocation、逐 invocation state hash、logical fault trace、optimization prerequisite blockers | focused `5/5`，最新完整 Cooking 回归 `57/57`，build 0 warning/error；JSON/CSV/JSONL；[check evidence](../../../.trellis/tasks/09-15-cooking-network-measurement/check.jsonl)；提交 `472fa7ec` | production transport、socket/stream decoder、真实两 PC LAN、批准 workload/threshold/topology、interpolation/prediction/correction/rollback runtime、reconnect、Unity projection |

### 证据解释

- P1 的权威 `check.jsonl` 记录为 `20/20`。若其他旧 metadata 或摘要仍写 `19/19`，必须标为计数漂移并以实际 check evidence 为准，不能静默改写历史证据。
- 表中的“当时完整回归”表示该增量提交时，独立 Cooking 测试项目中的全部测试数量；后续新增测试会使总数增长，不应据此认为旧记录失效。
- P6 最新完整回归为 `57/57`。测试数量从此前记录发生变化，是因为 N10 的多个 theory case 合并为一个覆盖全部 prerequisite 的合同测试，不是能力回退。
- `artifacts/cooking-*/` 是实际运行生成且受忽略规则保护的证据目录。是否在当前工作区仍保留生成物，不改变 `check.jsonl` 对当次命令结果的记录。

## 3. 当前可运行纵向链路

纯 .NET fixture 已能演示以下无界面流程：

1. 创建 Session/World/Match scope、玩家、物品与 station fixture。
2. 建立进程内 connection 并完成协议、配置与 capability handshake。
3. 通过同一 authority ingress 执行拾取、放置、竞争仲裁和幂等命令。
4. 运行单输入、单工序、3 tick 的最小加工循环，生成产品并装盘。
5. 通过注入式 order port 接受或拒绝产品。
6. 推进 Match 生命周期，结束后关闭旧 gameplay admission，并以新 MatchId/epoch restart。
7. 将 confirmed settlement 幂等应用到长期 progress，并模拟 prepare/commit/read 故障与 restart read-back。
8. 生成明确标记为 `InProcess` 的 JSON/CSV/JSONL 测量与 logical fault diagnostics。

该链路证明领域合同和技术边界可以运行；它不证明 Unity 表现、真实网络 I/O、物理两机 LAN 或 durable storage 已经完成。

## 4. 跨阶段完整出口 blocker

### Unity 与内容

- 尚未建立 Cooking Unity 应用层、场景投影、authoring/export、UI、动画和 EditMode/scene smoke。
- Level、Map、Process graph、正式配方、订单、评分与失败体验尚未形成完整批准 schema 和产品规则。

### 网络与房间

- 尚未选择并实现 production transport、listen/connect adapter 与正式 wire codec。
- 没有同机真实 socket 集成和两台物理 PC 的网卡、地址、防火墙、发现/连接证据。
- 房间人数、host exit、disconnect recovery、reconnect 和 host migration 仍需 owner 决策。

### 持久化与经营

- 当前只有 in-memory fault-injection store，不是文件、数据库或云端 durable store。
- 存档 owner、保存时机、退出/断电/cancel、备份、隔离、恢复、migration 与 encryption/key policy 尚未批准或实现。

### 性能与同步优化

- workload 规模、采样窗口、拓扑覆盖及延迟、吞吐、队列、内存、分配等阈值仍为 `UNSET`。
- interpolation、local prediction、correction 与 rollback runtime 未获批准且未实现。
- 同机 `InProcess` 数据只能作为诊断基线，不能充当 LAN 性能通过证据。

## 5. 恢复工作时的读取顺序

1. 读取本文，确认当前执行基线、最后实现提交和完整出口 blocker。
2. 读取 [技术路线](technical-roadmap.md) 与 [交付计划](delivery-plan.md)，确认阶段顺序和产品边界；其中早期“尚未实现”描述若与本文冲突，应继续核对 task 实际 evidence，而不能直接选择任一摘要。
3. 读取准备继续的 `.trellis/tasks/09-15-cooking-*/`：
   - `task.json`
   - `prd.md`
   - `design.md`
   - `implement.md`
   - `research/`
   - `check.jsonl`
4. 读取对应的 [Cooking 规范索引](../../../.trellis/spec/cooking/index.md) 和行为草案。
5. 核对 Git 状态与本文记录的基线提交；不要覆盖用户未提交改动。
6. 开始新实现前重新审议完整出口、owner decisions 和实际环境，不从 `verified-limited-scope` 推导阶段已完成。
7. 每完成一个获批增量后：实际执行验证、更新 task `check.jsonl`、创建提交，再更新本文的基线、状态和 blocker 指针。

## 6. 维护规则

- 本文只维护跨阶段状态摘要与链接，不复制行为契约、测试矩阵或 task checklist。
- 行为变化写入 `.trellis/spec/cooking/`；当前变更的目标与设计写入对应 task；实际命令结果写入 `check.jsonl`。
- 计划测试、文档 checkbox、task `in_progress`、migration status、提交存在和测试通过彼此不等价。
- 未实际运行的 Unity、LAN、protocol 或 global gate 必须保留 `not-run` 或 `blocked`，不得推断为通过。
- 完整出口解除后，先更新原 task/spec/ADR 的权威事实，再更新本文的摘要；本文与来源冲突时修复本文，不建立第二套事实。
