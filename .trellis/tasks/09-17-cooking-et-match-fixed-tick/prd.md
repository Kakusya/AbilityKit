# Cooking Match ET 宿主与单一固定加工时钟

> 状态：`planning`，等待 owner 审阅。本文与 `design.md`、`implement.md` 只描述拟实施范围，不代表能力已实现或已验证。

## Goal

在不复制 Cooking 权威状态的前提下，建立一个纯 C# 的 Cooking Match ET 应用宿主，使 `CookingMatchLifecycle`、ET Entity 所有权和 `CookingRecipeSimulation` 在同一 owner thread 上协作；同时把加工推进从调用者提交的任意 `AdvanceTicks` 命令收敛为宿主每个权威帧只推进一次的固定时钟，为后续 checkpoint、跨小关延续和 UDP ingress 提供稳定基础。

## Background and confirmed facts

- 独立 ET runtime 与最小 Cooking 命令调度接点已完成；当前 `CookingRecipeTickHost.Enqueue` 只入队，ET `UpdateSystem` 在显式 Tick 中调用 `CookingRecipeSimulation.Submit`。
- `CookingRecipeSimulation` 是物品、手持、Process、容器、订单、版本和命令去重账本的唯一权威 owner，不得在 ET Entity 中复制一套可独立修改的业务状态。
- 现有 `AdvanceTicks` 是 wire-facing 玩家命令：每次提交都会把全局 `LogicalTick` 增加 `TickCount`，并且只推进一个指定 Process。若多个 Process 在同一宿主帧分别提交该命令，全局时间会被增加多次。
- `CookingMatchLifecycle` 已实现 `Preparing -> Ready -> Started -> Ended`，Start 创建唯一 gameplay simulation，End 关闭 gameplay admission，Restart 要求新 MatchId 与更高 epoch。
- `EtRuntimeHost` 是进程级单实例、owner-thread、显式 Tick 的运行时；本任务不能为每个 Match 创建并行 ET host。
- ADR-0002 要求网络线程只入队、模拟线程按稳定顺序提交命令，逻辑 Tick 独立于渲染帧和墙钟；30Hz 不是本任务要锁定的产品参数。

## Requirements

### R1 — 正式 Match ET 应用宿主

新增应用层 `CookingMatchEtHost`（最终命名可在实现中按项目惯例微调），由它持有一个 `CookingMatchLifecycle`、一个 ET runtime/restaurant scene，以及当前活动 Match 的 ET Entity 投影。

- 宿主必须暴露 Prepare、Start、End 与 Restart 的应用入口，并复用既有 `CookingMatchLifecycle` 校验和状态迁移，不复制生命周期状态机。
- Restaurant/宿主 Entity 在宿主生命周期内存活；活动 Match Entity 在 Start/Restart 边界创建或替换，并在 End/Dispose 时显式释放。
- ET Entity 只保存宿主/生命周期引用和调度关系，不成为物品、加工、订单、网络或存档的第二权威 owner。
- 本任务只支持一个 `CookingMatchEtHost` 对应一个活动 Match；同进程多 Match scene 调度后置。

### R2 — 生命周期准入、命令接收与帧批次

- gameplay command 只允许在 lifecycle 为 Started 且 `TryGetGameplay` 成功时进入待执行队列。
- 宿主提供结构化 `TryEnqueue` admission result；生命周期、scope、队列容量和保留时钟操作属于普通拒绝，不以异常表示。disposed、foreign-thread、reentry 和 faulted 属于编程/宿主状态错误，继续抛异常。
- 命令必须在 Tick 开始时形成不可变批次；该批次之外的新输入只能进入下一 Tick。
- 一个宿主 Tick 最多执行一个 `SimulationBatch`：选择当前队列中的最小 batch，执行该 batch 后推进一次固定时钟；更大的 batch 留待后续 Tick。队列为空时仍推进一个空帧。
- 已经执行过的或小于当前已提交 batch 水位的命令视为 stale 并在 admission 阶段拒绝；batch 不要求连续，跳号不阻止后续最小 batch 执行。
- 同一 closed batch 按 `(PlayerId, CommandId)` ordinal 顺序稳定执行，不依赖入队线程调度、字典枚举或 ET Entity 顺序。
- 同一 `(Session, Player, Command)` 身份在同一 closed batch 内出现多个完全相同 fingerprint 时只执行一次，其余返回 duplicate disposition；若 fingerprint 不同，则该冲突身份组全部拒绝且不写入 simulation ledger，避免到达顺序决定哪一个 payload 获胜。
- 当前任务保持 owner-thread ingress；线程安全 UDP ingress 后置，但 API 不得暗示可从网络回调直接调用。

### R3 — Simulation-owned 单一固定加工时钟

在 `CookingRecipeSimulation` 增加一条不伪装成玩家命令的固定步进契约。

- Started 状态下，每次宿主 Tick 恰好把全局 `LogicalTick` 增加 1；无活动 Process 时也推进全局时钟。
- 一次固定 Tick 批量推进 Tick 开始命令批次执行后存在的全部活动 Process，每个 Process 最多推进 1。
- 当帧命令先执行，固定时钟后执行；因此当帧成功创建的 Process 会在同一权威帧获得第一个加工 Tick。
- 多 Process 按 `ProcessId` ordinal 顺序推进和完成，以稳定产品 ID 分配与结果顺序。
- 整个固定步进作为一次权威帧提交：全局 Tick 只增加一次，并输出一个 first-class tick result/event，不使用虚构玩家命令进入命令去重账本。
- lifecycle 已关闭时固定步进必须拒绝且状态不变。

### R4 — 禁止双时钟

- 新 Match host 必须拒绝外部 `CookingRecipeOperation.AdvanceTicks`，避免玩家命令时钟与自动固定时钟同时推进。
- 现有 `CookingRecipeSimulation.Submit(AdvanceTicks)` 暂时保留，以兼容既有领域测试和旧直接调用；本任务不删除或重解释其历史行为。
- 新宿主自身不得根据墙钟、Unity 帧率或 elapsed time 自动补跑；调用一次 `Tick()` 就代表一个明确的权威逻辑帧。

### R5 — 原子性、事件水位与确定性

- 固定 Tick 应返回 Scope、Epoch、HostFrameSequence、Before/After LogicalTick、Before/After StateVersion，以及按稳定顺序排列的 progressed/completed Process 结果；完成结果必须能关联 Process、Recipe、Input、Station 和生成 Product。
- 每次固定步进必须先完整预验证并 stage 全部 Process 更新、输入消耗、产品身份、事件和结果；只有整帧可提交时才一次替换权威状态。任何中途异常或碰撞必须使 LogicalTick、Version、Process、Item、产品计数器和事件历史全部保持不变。
- 多 Process 同帧完成时，所有输入各消耗一次、各自产物只生成一次，结果和 canonical snapshot hash 在相同初态/输入下稳定。
- 固定 Tick 增加 snapshot `Version` 一次，并写入独立的 `CookingRecipeTickEvent`；Tick event 与既有 command event 共用 simulation 的单调 `_eventSequence`，发生顺序为当帧 command events 在前、tick event 在后。
- Tick event/result 必须携带 Scope、Epoch 与 HostFrameSequence；现有 `CookingRecipeSnapshot` 和 UDP wire schema 本任务不新增字段，避免把 frame event 变更扩散为同步协议迁移。snapshot event watermark/checkpoint 后置。
- ET System 捕获的 authority 异常必须由宿主 Tick 重新暴露并将宿主置为 faulted；不得返回成功或继续处理后续帧。

### R6 — 生命周期结束、重开与释放

- End 后旧 simulation 不再接受命令或固定 Tick。`End` 返回的 host operation result 必须包含全部 pending command 的 ordered disposition（CommandId、Rejected/Cancelled reason），每条 pending command 恰有一个结果且不进入 simulation ledger。
- Restart 只接受既有 lifecycle 允许的新 MatchId/更高 epoch；旧 Match Entity 必须释放，新 lifecycle 回到 Preparing，旧命令不得污染新 Match。HostFrameSequence 在宿主生命周期内保持单调、不因 Restart 归零；Scope/Epoch 区分 Match 代际。
- Dispose 必须要求 idle owner thread，递归释放 ET runtime/entity，关闭尚未结束的 gameplay admission；Dispose 前清理 pending disposition 到只读 `FinalDispositionHistory`，后续业务操作稳定失败。
- 本任务不定义失败重开标准供应、成功跨小关状态延续或 settlement；Restart 只验证身份/epoch/生命周期隔离。

### R7 — 兼容性与分层

- 不修改通用 `AbilityKit.ET.Runtime` 的 Tick 签名或加入 Cooking/固定频率知识。
- 不修改 Cooking Unity 范围，不新增场景、GameObject、MonoBehaviour、Editor 或 asmdef gameplay 接入。
- 既有 `CookingRecipeTickHost` 可保留为受限兼容接点；若实现中复用其内部机制，必须确保原 9 项 ET runtime 回归继续通过。
- 不改变正式 recipe、订单、评分、人数、断线、房主退出或持久化产品语义。

## Acceptance Criteria

- [ ] AC1：合法 Prepare 后预建一个 inert Match Entity，Start 通过正式 ET host 创建且只创建一个 gameplay simulation 并绑定 driver；ET entity 安装失败时 lifecycle 不得进入 Started、gameplay factory 不得被调用。
- [ ] AC2：Started 后连续调用三次宿主 Tick，`LogicalTick` 精确从 0 变为 3；无活动 Process 时仍每帧只增加 1。
- [ ] AC3：队列包含两个不同 `SimulationBatch` 时，单次 Tick 只执行最小 batch，较大 batch 留到下一 Tick；同 batch 按 Player/Command 稳定执行，Tick 过程中批次不扩张。
- [ ] AC4：同一命令身份的相同 fingerprint 重复只执行一次；冲突 fingerprint 在不同到达排列下均整组拒绝，simulation、ledger、版本和事件保持一致。
- [ ] AC5：当帧成功 StartProcess 后，该 Process 在同帧固定步进到 elapsed=1；RequiredTicks=3 时第三个宿主 Tick 恰好完成并只生成一个产品。
- [ ] AC6：两个或以上 Process 同帧推进/完成时，全局 `LogicalTick` 和 Version 都只增加 1；结果、产品 ID、command/tick event sequence 和 snapshot hash 在重复运行中一致。
- [ ] AC7：注入中帧产品身份/提交失败时，整帧原子回滚：LogicalTick、Version、Process、Item、计数器和事件历史全部与帧前一致，宿主报告 fault。
- [ ] AC8：通过新宿主提交外部 `AdvanceTicks` 在 admission 阶段得到稳定 rejection、不占队列容量，且 LogicalTick、Process、版本和事件水位均不变；旧 simulation 直接调用行为仍由现有回归覆盖。
- [ ] AC9：End 后 admission 和固定步进均关闭，End result 对每条 pending command 给出一次 ordered disposition；Restart 使用新 MatchId/更高 epoch、旧 Match Entity 释放、HostFrameSequence 继续单调，旧命令不改变新 Match。
- [ ] AC10：宿主 Dispose、owner-thread、reentry、queue capacity 和 faulted 状态均有聚焦测试；Dispose 后 `FinalDispositionHistory` 可只读检查，异常不会被 ET System 静默吞掉。
- [ ] AC11：新增 Match/fixed-tick 聚焦验收与完整 Cooking、完整 ET runtime 测试通过；`cooking-udp` 仅作为旧外部 AdvanceTicks/UDP 兼容回归单独报告，不作为新 Match host 已接网的证据。任何失败、受阻或跳过按真实结果记录。

## Out of scope

- checkpoint 导出/恢复和进程重建继续运行。
- 成功跨小关保留食材、半成品与加工进度。
- 失败重开标准初始供应和成功 settlement/persistence。
- 工位升级时的 Process 迁移。
- LiteNetLib/UDP 线程安全 ingress、连接身份绑定和两台物理 PC LAN。
- 多个并行 ET Match scene、Host 迁移、断线恢复和房主退出。
- 30Hz 等真实时间调度器、catch-up、帧跳过或性能阈值。
- Cooking Unity 实现与 `world.entitas`/`world.ecs` 清退。

## Key decisions for review

1. 每次 Started 宿主 Tick 都推进全局逻辑时钟，包括没有活动 Process 的帧。
2. 每个宿主 Tick 只执行当前最小 `SimulationBatch`，然后批量推进一次加工；未来 batch 留到后续帧，当帧创建的 Process 获得第一个 Tick。
3. 相同命令身份的 conflicting fingerprints 在进入 simulation 前整组拒绝，不让到达顺序选择获胜 payload。
4. 新宿主在 admission 阶段拒绝外部 `AdvanceTicks`，但暂不删除旧领域 API。
5. 固定时钟是 simulation-owned、先 stage 后原子 commit 的 first-class 操作，不伪造成玩家命令。
6. lifecycle Prepare 成功后先建立 inert Match Entity，再调用 Start 创建 gameplay，避免 Started 后 driver 安装失败的半初始化状态。
7. Tick event 与 command event 共用 simulation 单调 event sequence；HostFrameSequence 在 Restart 后不归零，Scope/Epoch 区分 Match。
8. 本任务只做单宿主、单活动 Match；多 Match scene 与网络线程 ingress 后置。

## Blocking open questions

无。以上关键决定均作为本次审阅内容；只有 owner 明确批准本 task 后才能从 `planning` 进入实现。
