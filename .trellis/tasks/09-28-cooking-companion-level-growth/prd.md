# Cooking 固定伙伴小关内成长

> 状态：`planning`。本任务只在 owner 审阅本轮最终规划摘要并另行明确批准实施后，才可执行 `task.py start`。

## Goal

为当前固定伙伴增加第一项可验证的 Level-local 成长：伙伴在本小关内成功完成 3 个询问或洗碗任务后解锁洗碗加速；之后新认领的洗碗任务耗时变为基础耗时的一半（向上取整，最低 1 Tick）。成长状态必须进入前厅 snapshot、canonical/SHA-256 与同 Level checkpoint，使纯 C# 领域状态和 ET host 恢复保持确定性。

用户价值：同一位固定伙伴在一关内持续工作会产生清晰、可观察的效率成长，同时不引入岗位微操、长期养成或新的网络协议范围。

## Background And Confirmed Facts

- 产品事实来源为 `09-19-cooking-productization-network-slice-planning`；该来源任务保持 `planning_only: true`，不直接承载实现。
- 当前 `CookingFrontOfHouse` 是顾客、伙伴工作、洗碗队列和前厅 checkpoint 的唯一领域 owner。
- 当前伙伴只有一位，固定处理询问和洗碗；询问优先于洗碗，任务按既有顺序认领。
- 当前 snapshot 的 `RequiredTicks` 从 schedule 即时推导，`CookingCompanionWork` 未保存任务启动时确认的耗时；本任务必须修正该接缝，避免解锁后进行中任务被追溯缩短。
- `CookingLevelEtHost` 已把 `CookingFrontOfHouseCheckpoint` 包入同 Level 宿主 checkpoint，并暴露 `FrontOfHouseSnapshot`；本任务不建立第二份 ET 成长状态。
- 正常产品流程不设置业务失败；既有失败重开能力仅作为兼容生命周期路径参与重置验收。

## In Scope

1. **Level-local 任务计数**
   - 每次询问成功开单后，伙伴完成任务数增加 1。
   - 每次洗碗成功完成后，伙伴完成任务数增加 1。
   - 取消、超时、中断、非法目标、开单失败、洗碗失败后重新排队、重复完成均不增加计数。
2. **解锁与洗碗耗时**
   - 阈值为 3 个成功完成的伙伴任务。
   - 解锁状态由 `CompletedTaskCount >= CompanionGrowthTaskThreshold` 唯一派生，不维护可漂移的第二份可变布尔状态。
   - 解锁后的新洗碗任务耗时为 `max(1, ceil(BaseWashTicks / 2))`，整数实现等价于 `max(1, (BaseWashTicks + 1) / 2)`。
   - 已开始的询问或洗碗保存认领时的 `RequiredTicks`，之后解锁不改变该任务耗时。
   - 第三个任务完成后，同一固定 Tick 后续认领的新洗碗任务立即使用加速耗时。
3. **观察、哈希与恢复**
   - 前厅 snapshot 暴露成功完成任务数、派生解锁状态和进行中任务已确认的 `RequiredTicks`。
   - canonical 文本和 SHA-256 包含上述字段，字段顺序稳定。
   - 同 Level checkpoint 在解锁前、解锁后和进行中洗碗状态均可往返恢复。
   - checkpoint 校验拒绝负任务计数、计数与派生解锁状态不一致、以及不符合当前任务类型/成长状态的 `RequiredTicks`；拒绝必须保持目标前厅零变更。
4. **生命周期重置**
   - 同 Level checkpoint 恢复保留任务计数，并从计数确定解锁状态。
   - 下一 Level 重置任务计数和解锁状态。
   - 失败重开重置任务计数和解锁状态。
   - 成功收口时按既有规则真正完成的询问或洗碗仍计数；随后进入下一 Level 时统一清零。
5. **实施边界**
   - 修改纯 C# Cooking 领域代码和测试。
   - 验证 ET host checkpoint 导出/恢复透传新状态；仅在现有透传不足时做最小 ET host 调整。

## Requirements

### R1 — 单一计数来源

`CookingFrontOfHouse` 持有一个非负的 Level-local 已完成伙伴任务计数。解锁状态只从该计数和 schedule 阈值派生；不得在领域层、ET host 或测试夹具中复制第二套可变成长状态。

### R2 — 只统计成功终点

计数更新只能发生在 `OpenOrder` 或 `CompleteWash` 返回成功之后。失败洗碗重新入队但不计数；无顾客、目标已取消、订单未打开或重复完成不得产生计数。

### R3 — 启动时冻结任务耗时

`CookingCompanionWork` 在认领任务时保存正数 `RequiredTicks`。`AdvanceCompanion` 和 snapshot 使用该值，不再每帧从 schedule 重算。询问始终使用基础 `InquiryTicks`；洗碗按认领瞬间是否已解锁选择基础或加速耗时。

### R4 — 同 Tick 解锁生效

一个 Tick 内先推进并成功完成当前任务、再按既有流程认领下一任务。若前一任务使计数达到阈值，则同一 Tick 后续认领的洗碗立即使用加速耗时；已经开始的任务不变。

### R5 — 稳定投影与哈希

`CookingCompanionSnapshot` 或等价的伙伴投影必须包含 `CompletedTaskCount`、派生的洗碗加速解锁状态、当前工作 `ElapsedTicks` 和已冻结的 `RequiredTicks`。canonical/SHA-256 必须对这些值敏感。

### R6 — 原子 checkpoint 恢复

前厅 checkpoint 必须完整恢复计数和进行中任务耗时。所有新增校验在 `Apply`/`CopyFrom` 之前完成；任一校验失败均返回结构化原因且不修改现有前厅。

### R7 — Level 生命周期清晰

`DropFailedScene` 和 `ResetForNextLevel` 最终都把任务计数归零。`FinishInProgress` 只对真正成功完成的工作计数，不把取消或失败任务伪装成完成。

### R8 — 不扩大网络和表现范围

本任务不修改 Cooking LAN 消息、Host/Client session snapshot、UDP transport、Unity、UI、动画或寻路。ET 只复用领域 snapshot/checkpoint。

## Acceptance Criteria

- [ ] **G1 基础耗时**：完成 0、1、2 个伙伴任务时，新认领的洗碗任务 `RequiredTicks == BaseWashTicks`。
- [ ] **G2 第三次解锁**：第三个成功完成的询问或洗碗任务使 `CompletedTaskCount == 3` 且派生解锁状态为 `true`；不要求任务类型组合固定。
- [ ] **G3 加速公式**：解锁后新洗碗任务使用 `max(1, ceil(BaseWashTicks / 2))`；至少覆盖奇数基础耗时和 `BaseWashTicks == 1`。
- [ ] **G4 精确计数**：成功询问、成功洗碗各只增加一次；取消询问、未完成任务、失败洗碗重排和重复路径不增加。
- [ ] **G5 进行中稳定**：洗碗任务开始后即使后续状态达到解锁条件，该任务 `RequiredTicks` 不变化；第三次完成后同一 Tick 新认领的洗碗使用加速值。
- [ ] **G6 投影共识**：snapshot/canonical/SHA-256 对任务计数、解锁状态和进行中任务 `RequiredTicks` 敏感；相同输入重放保持一致。
- [ ] **G7 恢复与投毒**：解锁前、解锁后及进行中洗碗 checkpoint 往返后继续运行与不中断基线一致；负计数、计数/解锁不一致、非法 `RequiredTicks` 被原子拒绝。
- [ ] **G8 生命周期与门禁**：下一 Level 和失败重开后计数为 0、未解锁；Cooking 领域与 ET runtime 权威门禁通过，且无 LAN/Unity 文件改动。

## Out Of Scope

- Cooking LAN/session 协议、Host/Client 前厅投影和双端 SHA-256 网络共识。
- Unity 场景、伙伴可视化、UI、动画、寻路和空间移动。
- 第二项或更多成长能力、能力选择、技能树、数值平衡扩展。
- Match/Restaurant/Profile/SaveSlot 级长期成长或跨 Level 保留。
- 收益、小费、评价、经济、装修或餐桌布局与伙伴能力联动。
- 多伙伴人数、岗位分配、玩家优先级微操。
- durable store、进程崩溃恢复和旧 checkpoint 格式迁移。

## Risks And Deferred Items

- `CookingFrontOfHouseSchedule` 当前有多个位置参数调用点；新增阈值必须使用尾部可选参数或同步改为命名参数，避免静默错位。
- `StartNextJob` 会在认领后立即推进 1 Tick；测试必须按该既有口径断言 elapsed/required，不修改当前节奏。
- `FinishInProgress` 可能在成功收口时完成多个尚未开单的询问；每个真正成功的开单分别计数，随后下一 Level 重置。
- 任意非负任务计数在缺少完整历史日志时不一定可证明被篡改；本任务的“投毒计数”验收明确覆盖负值与计数/派生解锁不一致，不引入任务历史账本。

## Blocking Open Questions

无。产品范围、阈值、公式、计数口径、生效时点、重置语义和实施边界均已由 owner 确认。
