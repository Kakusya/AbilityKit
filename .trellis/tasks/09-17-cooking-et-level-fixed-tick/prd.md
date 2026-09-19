# Cooking Level ET 生命周期与单一固定加工时钟

> 状态：`in_progress`。Owner 已批准并完成本轮 Phase A 实现；task-specific build/tests/gate 证据见 `research/validation.md`。实现已纳入本次提交，task 尚未执行 finish/archive。

## Goal

在不复制 Cooking 权威状态的前提下，建立 canonical `CookingLevelLifecycle` 和纯 C# `CookingLevelEtHost`，把现有仓库中实际属于 Level 的旧 `CookingMatchLifecycle*` 语义迁移到正确命名；同时让每个 Running Level 的一次 ET Tick 恰好执行一个稳定命令批次并推进一次 simulation-owned 固定加工时钟。

本任务是目标 ET 树的 **Phase A**：ET 负责 Application/Match/Runtime/Kitchen/Level 的最小生命周期 Component seam 与调度，`CookingRecipeSimulation` 继续作为 Item、Station、Process、Container、Order、command ledger、event 和 snapshot 的唯一可写玩法权威。

## Background and confirmed facts

- 独立 ET runtime 与最小 `CookingRecipeTickHost` 接点已经存在；当前 ET `UpdateSystem` 只负责调用 `CookingRecipeSimulation.Submit`。
- 现有 `CookingMatchLifecycle` 保存 Level/Map/Layout、创建一个 gameplay simulation，并控制 `Preparing -> Ready -> Started -> Ended`；它实际是 Level-like lifecycle，不是产品定义的长期联机 Match。
- 产品 `Match` 已确认是玩家建立协作关系后持续到明确解散的长期容器；Connection、LoadedSave、RestaurantRuntime 和 Level 均不是 Match 本身。
- `CookingRecipeSimulation.AdvanceTicks` 是外部玩家命令：一次调用增加全局 `LogicalTick` 并只推进一个 Process。每 Process 提交一次会错误地让同一帧全局时间增加多次。
- `EtRuntimeHost` 是进程级单实例、owner-thread、显式 Tick；本任务不引入并行 ET runtime、多 Match scene 或真实时间调度器。
- 目标 ET 结构、Component/Entity 基数、typed Registry、ID、authority 迁移、存档与平台边界记录在 `Docs/design/CookingGame/reference/`。这些参考决定不代表全部进入本任务实现。

## Requirements

### R1 — Canonical Level identity and lifecycle

新增 canonical Level 合同：

```text
CookingLevelScope
├── MatchScope / MatchId
├── RestaurantRuntimeId
├── LevelId
└── LevelEpoch
```

- parent Match、RestaurantRuntime 和 Level 运行身份必须显式区分；不得继续使用旧 `CookingScope.Match` 冒充 Level identity。
- 主状态固定为：

```text
Created -> Preparing -> Ready -> Running <-> Paused -> Ending -> Ended
```

- `Success`、`Failed`、`Aborted` 是 Ending/Ended Outcome，不扩展成另一组互斥主状态。Outcome 在 `BeginEnd` 成功时一次性设置，此后不可改变；`CompleteEnd` 仅在 Ending 且已有 Outcome 时合法。
- lifecycle 转换表固定为：

| Operation | Accepted source | Target / effect |
| --- | --- | --- |
| `BeginPreparation` | Created | Preparing，保存不可变 preparation candidate |
| `CompletePreparation` | Preparing | 完整校验 Level/Map/Layout/config 后进入 Ready |
| `Start` | Ready | 创建唯一 gameplay，进入 Running |
| `Pause` | Running | 进入 Paused，关闭新 admission，保留旧 queue/watermarks |
| `Resume` | Paused | 回到 Running，恢复旧 queue 执行 |
| `BeginEnd(Success)` | Running | 锁定 Success，进入 Ending |
| `BeginEnd(Failed)` | Running | 锁定 Failed，进入 Ending |
| `BeginEnd(Aborted)` | Created/Preparing/Ready/Running/Paused | 锁定 Aborted，进入 Ending |
| `CompleteEnd` | Ending with immutable Outcome | 完成清理并进入 Ended |

- Paused 不允许直接 Success/Failed；必须先 Resume 回 Running，或以 Aborted 结束。
- 所有未列出的转换都以 `InvalidState` 拒绝，且不增加 lifecycle version/event sequence、不创建/关闭 gameplay、不改变 Outcome。
- Level preparation 继续验证 Level/Map/Layout/config identity，不创建第二套配置规则。
- gameplay factory 只在合法 Start 时创建一个 `CookingRecipeSimulation`；初始化失败不得打开 command admission。
- 失败重试复用 `LevelId`、要求更高 `LevelEpoch`；成功进入下一关要求新的 `LevelId` 和更高 Epoch。
- retry/successor 保持 parent Match 和 RestaurantRuntime identity 不变；旧 Level epoch 不再接受 Tick、命令或 snapshot。
- `CreateRetry(newEpoch)` 仅在 `Ended + Failed` 合法，要求 `newEpoch > current LevelEpoch`；复用原 LevelId，返回新的 Created lifecycle generation。否则 mutation-free rejection。
- `CreateSuccessor(newLevelId,newEpoch)` 仅在 `Ended + Success` 合法，要求新 LevelId 非空且不同于 source LevelId、`newEpoch > current LevelEpoch`；返回新的 Created lifecycle generation。否则 mutation-free rejection。
- retry/successor result 必须包含 source LevelScope/outcome、new LevelScope、新旧 lifecycle version，以及旧 gameplay 已关闭的可观察结果；source lifecycle 保持 terminal，不转移 mutable state 到新 lifecycle。

### R2 — Old Match lifecycle compatibility facade

- 新 `CookingLevelLifecycle` family 是唯一 canonical 状态机。
- 现有 `CookingMatchLifecycle*` 保留为 `[Obsolete]` 兼容 facade，并委托给同一个 Level lifecycle 实现；不得复制状态、事件序列或 gameplay owner。
- 旧 `Preparing/Ready/Started/Ended` API、旧 focused tests 和 historical evidence 在兼容期继续工作。
- facade 将 canonical `Ready/Running/Ended` 投影为旧 `Ready/Started/Ended`；旧 Restart 映射到创建 successor Level generation 的兼容操作。
- 本任务不全局重命名或重解释 `CookingScope`、`MatchId`、LAN/UDP DTO、persistence schema 或历史证据文件。
- 删除 facade 属于后续迁移 task，必须证明旧调用方和测试引用清零。

### R3 — Minimal ET Component seam

本任务只实现以下最小 Phase A 树：

```text
ET Scene
└── CookingApplicationComponent
    └── CookingMatchRegistryComponent
        └── CookingMatchEntity
            ├── CookingRestaurantRuntimeComponent
            │   ├── CookingKitchenComponent
            │   └── CookingLevelComponent
            │       └── CookingLevelDriverComponent (IUpdate)
            └── compatibility identity/admission components as needed
```

关系规则：

- `[ComponentOf]` 类型使用 `Component` 后缀；`[ChildOf]` 类型使用 `Entity` 后缀。
- Application、RestaurantRuntime、Kitchen、Level 是其 Owner 下唯一从属对象，因此是 Component。
- Match 是 Registry 下多实例对象，因此是 Entity；当前 task 只创建一个 Match seam。
- `CookingLevelDriverComponent` 是 `CookingLevelComponent` 的唯一调度能力。
- 当前 task 不创建可写 Item/Station/Process/Order ET Entity；否则会与 `CookingRecipeSimulation` 形成双权威。
- 当前 `CookingRecipeDriver : Entity` 保留为 legacy compatibility 或迁移到新 driver 后由等价测试保护；不得继续作为 canonical shape。

### R4 — Admission, Pause and command queue

- gameplay command 只允许在 Level `Running` 且 gameplay simulation 可用时进入队列。
- `Created/Preparing/Ready/Paused/Ending/Ended` 均返回结构化 admission rejection，不以普通生命周期拒绝抛异常。
- wrong thread、reentry、disposed 和 faulted 仍是 host misuse，继续抛异常。
- Paused 状态拒绝所有新 gameplay command，拒绝项不占队列容量。
- Pause 保留同一 `CookingLevelComponent`、Kitchen、LogicalTick、HostFrameSequence、batch/snapshot/event watermark，以及暂停前已准入但尚未执行的 live queue/frozen batch。
- Pause 不推进 ET Tick/fixed Tick；Resume 后旧命令保持原 `SimulationBatch`，继续稳定仲裁并接受正常 scope、item version 和 stale validation。
- 失败进入 Ending 时，旧 LevelEpoch 的全部 pending/frozen commands 必须获得 ordered cancelled disposition；新 Epoch 不执行旧命令。

### R5 — Stable one-batch frame

一次 `CookingLevelEtHost.Tick()` 在 Running 状态代表一个权威逻辑帧：

1. 检查 owner thread、idle 和 fault state；
2. 冻结当前 pending queue；
3. 选择最小 `SimulationBatch` 作为本帧 closed batch，保留未来 batch；
4. 按 `(LevelScope, Player, Command)` 分组；
5. fingerprint 使用显式 canonical bytes（固定字段顺序、长度前缀 ordinal string、null marker、invariant numeric/enum）覆盖 LevelScope、batch 和完整 command semantic payload；不得把 Connection/correlation/arrival/ET identity 纳入 fingerprint；
6. 相同 fingerprint 以 ordinal `(SourceConnectionId, CorrelationId)` 选 representative，只执行一次，其余返回 duplicate disposition；
7. conflicting fingerprints 整组拒绝，不进入 simulation ledger，并在当前 Epoch 形成 terminal host conflict disposition；
8. accepted representatives 按 `PlayerId.Value`、`CommandId.Value` ordinal 排序；
9. 提交命令；
10. 调用一次 simulation-owned `AdvanceFixedTick`；
11. 按 Player/Command/Fingerprint/Connection/Correlation 稳定排序并返回每个 envelope 的 immutable disposition。

补充约束：

- 队列为空时仍推进一个 fixed Tick。
- `LastCommittedSimulationBatch` 属于 LevelEpoch；提交 B 后，`SimulationBatch <= B` 均为 stale，不能重新打开已执行 batch。
- batch 可以跳号；当前最小 future batch 成为下一执行 batch。
- queue capacity 按 unique live pending command identity 计，不按 raw envelope 计；identical duplicate/conflicting payload 不额外占容量，所有 admission rejection 不占容量。
- cancelled/stale/conflicted identity 在当前 LevelEpoch 是 terminal host disposition，不保留 null-result ledger；retry/successor 的新 Epoch 使用新 ledger。
- Tick 期间 owner-thread reentry 继续拒绝；线程安全 UDP ingress 后置。
- `CookingRecipeOperation.AdvanceTicks` 在新 host admission 阶段以 `ReservedClockOperation` 拒绝且不占队列容量；旧 simulation direct API 暂时保持兼容。

### R6 — Simulation-owned atomic fixed tick

在 `CookingRecipeSimulation` 增加 first-class fixed-step API，概念签名：

```csharp
CookingRecipeTickResult AdvanceFixedTick(
    CookingLevelScope levelScope,
    long hostFrameSequence);
```

语义：

- 每个 Running host Tick 恰好让 `LogicalTick` 增加 1，包括没有活动 Process 的帧。
- 命令先提交，固定 Tick 后提交；本帧成功创建的 Process 同帧获得第一个加工 Tick。
- 本帧开始 fixed-step 时存在的所有 Process 各推进最多 1 Tick。
- product identity 由可注入的纯 `ICookingProductIdAllocator.GetProductId(sequence)` 提供；production 保持 `product-{sequence}`，测试可以注入 fixed/recording allocator 触发 collision 和验证 ProcessId 排序，不使用 reflection 或 test-only mutable production hook。
- fixed Tick 是 **fixed-step delta 原子事务**：预验证 recipe/input/station/product identity，stage Process/Item/Product/counter/event/result，全部成功后一次 commit。它不回滚本帧此前已经成功提交的玩家命令；要实现 command+tick 全帧单事务需重构所有现有 command handlers，明确后置。
- AC8 的 collision/fault 验证帧不得包含 accepted mutating command representatives，以便断言 fixed-step 前后完整 simulation state 相等；command-then-tick ordering 由独立成功路径测试覆盖。
- fixed Tick 不调用 `ICookingOrderPort` 或任何外部 port；`SubmitOrder` 仍只在普通 command lane 中按既有 `Submit` 语义执行。外部 port side effect 不属于 fixed-step atomicity，且不得在 fixed-step preflight 探测。
- 任一 fixed-step 预验证、allocator、identity collision 或 invariant failure 必须让 LogicalTick、state version、Process、Item、product counter 和 fixed-tick event history 全部保持 fixed-step 前状态，并 fault host。
- 每帧只增加一次 simulation state version，不随 Process 数量变化。
- fixed Tick 不进入玩家 `_processedCommands` ledger。
- tick event 与 command event 共用 simulation `_eventSequence`；command events 在前，tick event 在后。

### R7 — Frame, event and generation watermarks

- `HostFrameSequence` 属于 `CookingLevelEtHost`/application host lifetime。每次 Running Tick 先计算 candidate，只有 fixed Tick 成功返回后才发布；first successful Tick 为 1。
- fixed-step failure 不消耗 HostFrameSequence、不发布 frame/tick event/result；host 随后 faulted，不能再次 Tick。此前 command-lane 已提交效果不回滚。
- Pause/Resume 不重置 HostFrameSequence、LogicalTick 或任何 Level watermark。
- 失败 retry 创建更高 LevelEpoch 时：
  - Level-local LogicalTick、batch/snapshot/event watermark 从新代际初始值重建；
  - HostFrameSequence 保持单调，不归零；
  - Scope + LevelEpoch 区分代际。
- tick result/event 至少携带 LevelScope、HostFrameSequence、Before/After LogicalTick、Before/After StateVersion 和稳定排序的 per-process results。
- 现有 `CookingRecipeSnapshot` 和 UDP schema 本任务不迁移；snapshot event watermark 和 tick-event transport 后置。

### R8 — Commands, events, references and DTO boundary

- 一次性命令是 immutable DTO/value object，进入 bounded queue；不创建 CommandEntity。
- event 是 commit 生成的 immutable DTO，保存在有水位的 tick/command histories；不创建 EventEntity。
- ET types、`Entity.InstanceId`、`EntityRef<T>` 和 parent tree 不进入 domain result、snapshot、wire 或 persistence DTO。
- 本任务新增的 LevelScope/result/event 均为纯 C# contracts，不引用 ET。
- 当前 task 不实现 Checkpoint；未来 Checkpoint 必须是专用 exporter 产生的 immutable DTO artifact，而不是序列化 ET tree。

### R9 — End, Retry, fault and Dispose

- End/Abort 先关闭新 admission，再处理 pending disposition，关闭 simulation，完成 Level outcome，最后移除 `CookingLevelComponent`。
- Pause 不清理 pending queue；Failed/Aborted ending 必须清理旧 Epoch pending queue。
- Retry 只有在 Failed + Ended 后允许；复用 LevelId、增加 Epoch，并安装全新的 Level Component generation。
- Success successor 只有在 Success + Ended 后允许；使用新 LevelId、更高 Epoch。
- 本任务只验证 lifecycle identity/epoch/queue isolation；失败标准供应重建、成功 Kitchen 延续和 upgrade migration 仍后置。
- ET System 捕获的 authority exception 必须由 host Tick 重新暴露并使 host faulted。
- fault 后不得继续 Tick/enqueue/lifecycle operation；idle owner-thread Dispose 仍允许清理。
- Dispose 将未完成 pending dispositions 固化为只读 `FinalDispositionHistory`；取消项不得进入 simulation ledger。

## Acceptance Criteria

- [x] AC1：新增 canonical `CookingLevelLifecycle`、LevelScope、LevelState、LevelOutcome、result/event/snapshot contracts；状态转换、非法转换和 scope/epoch rejection 有聚焦测试。
- [x] AC2：旧 `CookingMatchLifecycle*` facade 标记 obsolete 并委托 canonical Level lifecycle；旧 lifecycle tests 保持通过，且不存在第二套状态机或第二个 gameplay owner。
- [x] AC3：ET seam 使用正确 Attribute/后缀：Application/Runtime/Kitchen/Level/Driver 为 Component，Match 为 Registry 下 Entity；Prepare/Start 安装失败不会留下 Running lifecycle 或半初始化 driver。
- [x] AC4：Running 连续 Tick 三次时 LogicalTick 精确从 0 到 3；空帧也每帧只增加 1。
- [x] AC5：单帧只执行最小 SimulationBatch，future batch 保留；`batch <= committed watermark` 拒绝；capacity 按 unique pending identity；canonical fingerprint golden tests、stable representative、duplicate collapse、conflicting group terminal rejection 和 dispositions 均与到达排列无关。
- [x] AC6：当帧成功 StartProcess 后，同帧 fixed Tick 令 elapsed=1；RequiredTicks=3 时第三个 Running Tick 恰好完成并生成一个产品。
- [x] AC7：多 Process 同帧推进/完成时 LogicalTick/Version 各只加 1；结果、产品 ID、command/tick event sequence 和 snapshot hash 可重复。
- [x] AC8：通过 injected fixed product allocator 在无 accepted mutating commands 的帧触发 preflight collision；fixed-step state/hash/counter/event 和 HostFrameSequence 全部不变，host 进入 faulted，ET System 不得吞掉异常。
- [x] AC9：Paused 拒绝新命令、不推进 Tick、保留旧 queue 和全部水位；Resume 后旧命令继续正常仲裁。Pause/Resume 不改变 LevelEpoch。
- [x] AC10：Failed retry 复用 LevelId、增加 Epoch、取消旧 Epoch 全部 pending/frozen commands、重置 Level-local watermarks，HostFrameSequence 保持单调，旧命令不污染新代际。
- [x] AC11：Success successor 使用新 LevelId/更高 Epoch；旧 Level generation、simulation 和 driver 已关闭/移除。
- [x] AC12：新 host 拒绝外部 `AdvanceTicks` 且不占容量；旧 simulation direct behavior 由现有 regression 继续覆盖。
- [x] AC13：owner-thread、reentry、capacity、fault、Dispose 和 FinalDispositionHistory 有聚焦测试。
- [x] AC14：权威 task-local 证据为两个完整项目测试及落盘 TRX：`AbilityKit.Game.Cooking.Tests` 与 `AbilityKit.ET.Runtime.Tests`，任何失败均阻断交付；focused `Gate=CookingLevelRuntime` Trait 仅作分类/诊断。实现必须在交付前注册并通过 `cooking-et-level-runtime` gate（同样完整执行两个项目），`cooking-udp` 只作为 legacy compatibility evidence。

## Out of scope

- 完整 Match/Participant/Connection/SaveCandidate/SaveTransfer/SaveResult/Claim 产品实现；
- Profile/SaveSlot durable repository、PC/Android adapter、bearer authorization 和 claim protocol；
- Item/Station/Process/Order ET authority migration；
- checkpoint export/restore；
- 成功跨 Level Kitchen 延续的实际迁移实现；
- 失败标准供应重建；
- 工位升级与 Process 原子迁移；
- settlement/durable persistence；
- UDP/network-thread ingress 和两台物理 PC LAN；
- participant disconnect/reconnect、host exit/migration；
- 多 Match scene；
- 真实 30Hz scheduler、catch-up 和性能阈值；
- Cooking Unity 与 `world.entitas`/`world.ecs` 清退。

## Key decisions for final review

1. canonical lifecycle 使用 `CookingLevelLifecycle`；旧 Match-named family 只做 obsolete delegate facade。
2. LevelScope 显式包含 Match、RestaurantRuntime、Level 和 LevelEpoch；旧 `CookingScope.Match` 不再代表 Level。
3. Level 使用七状态加 Outcome；failed retry 复用 LevelId/提高 Epoch，success successor 使用新 LevelId。
4. Phase A ET seam 中 Application/Runtime/Kitchen/Level/Driver 是 Component，Match 是 Registry 下 Entity；gameplay state 仍由 Simulation 单权威持有。
5. Paused 拒绝新命令但保留暂停前队列；Pause/Resume 保持全部水位。
6. Failed retry 取消旧 Epoch pending queue，重置 Level-local watermarks，但 HostFrameSequence 持续单调。
7. 每帧执行一个最小 batch，然后 simulation-owned atomic fixed Tick 一次；外部 `AdvanceTicks` 在 host admission 拒绝。
8. 本 task 不实现完整目标 ET 树、存档协议、网络接入或 gameplay Entity authority migration。

## Blocking open questions

无。Owner 已批准本范围并完成实现；实际验证见 `research/validation.md`。
