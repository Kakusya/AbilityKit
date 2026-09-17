# Cooking 同机 UDP 最小经营闭环

## Goal

在一台电脑上，以现有独立进程的 LiteNetLib reliable-UDP listen-host/client 为唯一网络开发与回归基线，交付一个纯 .NET、可重复验证的最小做菜经营协作闭环。它的用户价值是让后续玩法不再只验证“UDP 能连接、单条交互命令能同步”，而能验证一条实际的订单制作流程始终经由主机权威规则处理，并在 host/client 两端收敛到相同状态。

本 task 只验证一个明确标注为 fixture 的最小场景，不把菜谱、订单、收益、评分或时长宣称为正式产品内容或经营平衡。

## Confirmed facts

- Owner 已确认当前只在**一台电脑**开发和测试；同机独立 host/client 进程的真实 UDP 是近期基线。两台物理 PC LAN 不属于本 task 的近期验收，也不得由同机结果替代。
- 已归档的 UDP task 已提供 Cooking-owned LiteNetLib 2.1.4 `ReliableOrdered` adapter、版本化 envelope、authority-bound identity、baseline/delta、同机 harness 和 `cooking-udp` P1 gate。它包装的是 `CookingSessionAuthority` 与 `CookingSimulation` 的 `CookingCommand` 路径。
- 现有同机 runner 已为 host/client 分开 stdout/stderr，但二者仍共享一个 artifact root，且 `failure.json` 等运行结果名可在双进程失败时竞争覆盖；本 task 必须消除该风险。
- `CookingSessionDiagnosticWriter` 和 recipe evidence writer 已采用 UTF-8 JSONL 追加、`File.ReadLines` 逐行解析的结构化日志模式；这可作为无需整文件读入内存的验收基础。
- 已归档的 P2 recipe loop 已提供纯 .NET fixture：单输入、单工序、3 logical ticks、产物、装盘、注入式 order accept/reject port、幂等与 canonical recipe snapshot/hash。其 `CookingRecipeSimulation` 当前独立于 UDP adapter 与 `CookingSessionAuthority`。
- 当前 P2 fixture 不等于正式 recipe/order/score/settlement/failure UX；正式内容与产品经济语义仍未批准。
- Cooking Unity、Orleans、通用 AbilityKit 网络包改动长期不在范围。权威固定 tick、主机同时参与、网络线程只入队的 ADR-0001/0002 仍适用。

## In scope

1. 在纯 .NET Cooking 应用层定义一个明确命名的最小协作经营 fixture：订单创建/可接收状态、食材输入、加工、装盘、提交订单和最小结算可观察结果。
2. 让 fixture 的 gameplay 命令通过与现有 host-local/remote UDP 相同的单一权威 dispatcher、身份绑定、命令结果与 snapshot/delta 路径，而不是只在另一个 in-process recipe simulation 中执行。
3. 在同机独立 host/client 进程中执行完整 fixture，产出 topology 标注为 same-machine UDP 的**每角色独占** JSON artifacts 与 JSONL event logs、authority/transport diagnostics、命令结果、订单/结算可观察结果与最终 canonical state hash；runner 另写只读的 run-level manifest/summary。
4. 基于结构化 JSONL 做自动验收：按固定 `runId`、角色、correlation/command/order identity 流式读取所需事件，验证主链路与拒绝路径；无需扫描或载入无关运行的日志。
5. 测试合法完整流程、host 与 remote 的共享权威路径、重复命令、无资格/过期/不合法步骤的 mutation-safe 拒绝，以及两端 snapshot/hash 收敛。
6. 在需要时扩展 `CookingUdp` 的版本化 message payload，使其能够承载经审议的 gameplay command/result/snapshot；保持严格 size/identity/scope/epoch/config validation。
7. 每次 harness 运行采用可配置 artifact retention：失败运行完整保留；成功运行默认仅保留最近 10 次完整 run；`--keep-artifacts` 可明确保留本次成功证据。清理只允许发生在当前 run 已关闭、验收摘要已原子发布之后，且不得删除失败、当前或显式保留的 run。

## Out of scope

- 两台物理 PC LAN 执行、自动发现、改防火墙或网卡配置。
- Unity package、scene、authoring、projection、UI、EditMode 或 PlayMode。
- Orleans、generic AbilityKit networking 的改造，或把当前 UDP spike 宣称为 production transport。
- 账号、认证、加密、反作弊、NAT/WAN/relay、endpoint rebinding、自动重连、host migration、host-exit/save 语义。
- 预测、插值、纠正、rollback、性能阈值或预先构建 snapshot fragmentation/compression；如 fixture 确实超过现有 1200-byte datagram 限制，记录为阻塞证据而不是绕过限制。
- 正式菜谱表、正式订单生命周期、评分/奖励平衡、长期餐厅 progress、durable persistence 或产品级失败 UX。
- 恢复或改写任一已归档的 P0-P6 task；本 task 是新的 non-Unity successor。

## Requirements

### G01 — One authority-owned gameplay loop

最小经营 fixture 的每个 gameplay mutation MUST 由 listen host 的单一权威路径串行处理。host-local 与 remote UDP player MUST 使用相同的命令验证、稳定顺序、原子提交、deduplication 和 snapshot publication 边界；UDP callback MUST NOT 直接改变规则状态。

### G02 — Explicit fixture order and settlement observability

fixture MUST 暴露稳定的 order identity、可接受/已完成状态，以及一个最小、可在 snapshot/hash 和 command result 中观察到的结算结果。拒绝的订单提交 MUST 保留已装盘物品与既有状态；接受的订单提交 MUST 至多完成及结算一次。

### G03 — Fixed-tick cooking and atomic steps

加工进度 MUST 由明确 logical tick 推进，不依赖墙钟、网络抵达顺序或表现帧。取料/输入、开始加工、完成加工、装盘、提交订单的每一步 MUST 在验证失败时保持完整权威状态不变。

### G04 — Same-machine UDP evidence

同机 host/client harness MUST 运行完整协作 fixture，并记录：workload/fixture identity、protocol/config identity、每个参与者执行的命令与结果、订单/结算结果、authority/transport diagnostics、snapshot sequence/baseline reference 和双方最终 state hash。它 MUST 明确标记为 same-machine UDP，不能报告为 two-PC LAN。

### G05 — Role-isolated structured logs and fast acceptance reads

每一次 harness run MUST 生成不可复用的 `runId` 和独占 run root；host 与 client MUST 分别写入 `host/`、`client/` 子目录，且任一角色只写自己的 stdout、stderr、JSONL event log、result/failure document。host/client 不得共同写入任何同名 result、failure 或 append log 文件。

事件日志 MUST 是 UTF-8 JSONL：每行一个完整、带 schema/version、`runId`、role、timestamp、event sequence、scope/epoch、correlation/command/order identity 和 outcome/reason 的事件。角色进程仅追加自己的 JSONL；只有 runner 在两角色都退出后才原子写入 run-level `manifest.json` 和 `acceptance-summary.json`。验收读取 MUST 使用流式逐行 JSON parsing，按 run/role/identity 过滤所需事件，读取结束文件的固定小型 JSON summary；不得为了单次验收将整个日志目录或大日志完整读入内存。

### G06 — Contract and regression coverage

测试 MUST 覆盖至少一条成功闭环、命令重放至多一次 mutation/settlement、错误身份或不合法步骤的无 mutation 拒绝、baseline/delta 后两端 hash 收敛，以及 authority 不在 LiteNet callback context 中执行。另须覆盖 role-specific output 路径不重叠、每角色日志可独立解析、缺失/截断/不兼容日志使验收失败而非误报成功。现有 UDP codec 的 schema/scope/epoch/config/size 检查不能因 gameplay 扩展而弱化。

## Acceptance criteria

1. 在一个 fresh fixture session 中，host-local 与 remote UDP client 都完成握手和 baseline；他们按已批准的协作角色完成同一条订单的制作、装盘和提交。
2. 主机权威 snapshot、远端 client projection snapshot 和 harness artifacts 的最终 canonical hash 完全一致；所有被接受步骤均有对应 command result 与连续 delta evidence。
3. 订单接受导致 exactly-once 的 fixture completion/settlement 记录；同 command identity 重放不重复消耗产物、完成订单或结算。
4. 无效身份、错误 scope/version、过期物品、未完成加工、容器满、重复/已消费提交或 order reject 均有稳定拒绝，并经前后 canonical hash 证明没有部分 mutation（order reject 保留已成功装盘状态）。
5. 同机独立 host/client runner 在有界时间内通过，并输出完整、topology-labelled artifacts。host/ 与 client/ 的实际输出路径必须不相等且不嵌套；任一角色的 failure/result/log 不得覆盖另一角色文件。runner 仅在所有角色退出、所需日志和结果均可解析且验收判定通过后，原子地写入 `acceptance-summary.json` 为 passed；失败时保留各角色原始 diagnostics，并写 failed summary，不回填为成功。
6. 自动验收器从 `host/events.jsonl` 与 `client/events.jsonl` 流式读取，验证同一 `runId` 下每个必需阶段、身份、命令关联、订单结算、snapshot 连续性和最终 hash；日志缺失、JSON 无效、事件版本不兼容、角色/运行 ID 混淆或 required event 缺失必须失败。
7. focused tests、`cooking-udp` P1 gate、适用 Cooking regression tests 和 same-machine harness 的实际命令与结果记录到 task `check.jsonl`。两物理 PC LAN 保持 `not-run`，且不作为本 task blocker。
8. 成功运行的默认 retention 不超过最近 10 个完整 run；失败与明确标记为保留的 run 不受该成功清理规则影响。

## Technical decisions and constraints

- 首先审计并选择最小的整合方式，使现有 `CookingRecipeSimulation` 的 command/snapshot seam 能进入 UDP host 的 authority dispatcher；不得维护两套彼此独立、无法共同收敛的 gameplay authority。
- 优先复用既有 recipe fixture 的定义、原子性、命令 ledger、canonical hashing 和 order-port seam；只在网络 integration 真正需要时提炼共享接口或 adapter。
- fixture identity 与 UDP workload identity 必须固定、可记录，并和 protocol/config identity 一同出现在 artifact 中。
- 每次 harness 运行创建唯一 `runId`；runner 在启动任一子进程前创建 `host/` 与 `client/` 目录并将独占 artifact directory 明确传入。host/client 永不推导或共享对方输出路径；run-level 文件仅由 runner 在进程退出后以临时文件 + replace/move 的方式发布。
- 日志优先服务机器验收：JSONL 的稳定字段/事件名称是 versioned contract；全文 stdout/stderr 仅作人工排障证据，不是成功判定的唯一依据。验收器用 `FileStream` 顺序扫描/逐行 `Utf8JsonReader` 或等价低分配方式读取 JSONL，并在发现首个结构、关联或业务断言失败时停止，避免全量反序列化。
- 维持 LiteNetLib connection key 的既有限定：它仅是受控开发测试的连通性门，不是身份认证或加密。
- 不新建本地分支；保留用户已有及 host 生成的未跟踪文件，不清理 `Unity/Assets/Practice/` 或 `src/AbilityKit.Demo.MyPractice/`。

## Open product decision

- **协作分工：**最小闭环是否必须要求 host-local player 与 remote UDP player 对同一个订单分别完成至少一个必需 gameplay 步骤？推荐为“必须”：可令主机完成取料/启动加工和固定 tick 推进，远端完成装盘与提交；这样能证明它是协作闭环，而非单人流程在旁边有一个已连接客户端。若选择“不必须”，验收只需证明两人都能通过同一权威路径操作，但不强制同一订单跨玩家交接，范围更小但协作价值较弱。

## Planning status

- Task created in `planning`.
- This PRD records the known constraints and one owner decision required before architecture/design and implementation checklist can converge.
- No product code, test code, gate configuration or implementation behavior has changed in this planning step.
