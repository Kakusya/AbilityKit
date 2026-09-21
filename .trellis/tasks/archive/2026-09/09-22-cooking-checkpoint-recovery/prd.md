# PRD：Cooking checkpoint 与恢复契约

> 上游：`Docs/Todo.md` P0-C1 前两条未勾项；产品语义来源 `Docs/design/CookingGame/reference/product-lifetimes.md`（§4 Level、§5 Kitchen 与 Level 的关系）与 09-19 讨论 PRD“小关成功检查点”节。本任务只交付单机纯 C#/.NET 的运行时 checkpoint 契约与宿主边界恢复验收，不含 durable storage、跨小关规则、前厅、传输与 Unity。

## 1. 目标

当前运行的权威状态只有两条既有出口：

- `CookingRecipeSimulation.Snapshot()`：**同步投影**，缺 tombstone、去重账本、事件与 tick 历史、ID 计数器、干净碗池计数、消耗产物集合与玩家持物索引，按定义不能承担恢复职责；
- `CookingProgressPersistence`：**长期经营进度**（settlement/progress ledger），不是一局运行态的恢复载荷。

本任务新增第三条出口——**恢复 checkpoint**：脱离宿主后仍自包含（自描述、带格式版本与完整性校验），覆盖继续运行所需的全部权威状态，并证明“导出 → 销毁 host → 重建 → 继续运行”与不中断基线不可区分。

## 2. 需求

### R1 区分同步 snapshot 与恢复 checkpoint

- checkpoint 是恢复载荷，snapshot 是每帧同步投影；两者覆盖范围、稳定性要求与消费者不同，文档与实现显式区分，不得互相替代。
- checkpoint 必须自包含：不引用活对象、不依赖宿主实例、可从序列化信封完整重建。

### R2 覆盖范围

checkpoint 至少覆盖（对应 Todo 条目逐项）：

| Todo 覆盖项 | 仿真侧来源 |
|---|---|
| 物品/tombstone | `_items` 全量（含 `Removed=true` 墓碑） |
| 加工 | 活动 `ProcessState`（recipe/player/anchor/station/elapsed/required/completion/container/lockedInputs） |
| 容器 | 各容器**有序**内容物列表（顺序决定 `slot-N` 分配）+ 干净碗池计数 |
| 订单 | 订单簿（模板、要求、状态、完成 tick） |
| 逻辑 Tick | `LogicalTick` |
| 命令水位 | 宿主 `LastCommittedSimulationBatch`（Level-local，重建时恢复） |
| 去重账本 | `_processedCommands`（session/player/command → fingerprint + 缓存结果） |
| 事件序列 | `_events` 历史 + `_eventSequence` + `_tickEvents` |
| ID 计数器 | `_nextProcessId` / `_nextProductId` / `_nextSettlementSequence` |
| Match/epoch/config identity | `CookingLevelScope`（session/world/match、RestaurantRuntime、LevelId、LevelEpoch）+ config identity |
| lifecycle 状态 | Level 状态/outcome/version |

可派生索引（玩家持物、station/anchor 进程索引、锁输入反查）由载荷重建，不进信封；消耗产物集合等不可派生状态显式进信封。

### R3 导出前置条件

- 导出只接受 Level `Running` 且该代际 admitted 命令已全部有终态（pending 为空）；否则结构化拒绝，不产出半个 checkpoint。
- 理由：宿主 Dispose 对未决命令的既有语义是取消并返回有序 disposition；checkpoint 记录权威状态，不搬运投递元数据。

### R4 恢复路径

- 恢复 = 新宿主对象：同一 Level scope/epoch、同一 config identity、同一 standard preparation；仿真由工厂创建后整册换入 checkpoint 载荷（fresh 实例的构造期状态被完全替换）。
- 恢复后行为与不中断继续不可区分；`HostFrameSequence` 单调不重置（`reference/product-lifetimes.md` §4.1 已确认），Level-local 水位按 checkpoint 恢复。
- 宿主级终态账本（terminal disposition 缓存）是单宿主投递簿记，不进 checkpoint；跨宿主重投的重复命令由仿真去重账本兜底（结果一致，disposition 标签口径见 design §7）。

### R5 恢复校验（结构化拒绝，零变更）

- 信封：格式版本、完整性、记录大小；载荷：scope 匹配、epoch/config identity 匹配、物品定义存在、订单模板/recipe/工位/容器外键有效、墓碑与锁输入一致、计数器单调。任一失败返回结构化诊断，目标仿真保持调用前状态。
- 不同 scope/epoch/config identity 的 checkpoint 不得被应用到目标（防串代际）。

### R6 验收：不中断基线等价

- 基线臂与恢复臂跑同一条番茄蛋花汤闭环（正式内容、同一命令序列），恢复臂在**加工进行中**导出并销毁重建，继续跑完。
- 终态比较：canonical 文本与 Sha256、state version、logical tick、ID 计数器（下一步产物 ID 可观察）、去重结果（重放已执行命令：结果重复标记与状态不变）、订单提交次数（settlement 计数）；复合证明为两份终态 checkpoint 的 canonical 相等。
- 拒绝零变更：恢复被拒时目标状态不变（复用命令路径同款纪律）。

### R7 不证明的事

durable storage 与进程崩溃恢复、跨小关成功/失败/升级与 checkpoint 产品语义（保存什么/清除什么）、前厅与订单生成节奏、评分/收益/评价、失败条件、生产传输、真实 LAN、Unity、ET Phase B。Pause/Resume 期间的 checkpoint 导出不在本任务（仅 Running）。

## 3. 验收标准

- [ ] A1 覆盖：导出的 checkpoint 字段对 R2 表格逐项可在测试中断言（tombstone、活动加工与进度、容器有序内容、干净池计数、去重账本条数、事件/tick 历史长度、三个 ID 计数器、scope/epoch/config identity、lifecycle 状态）。
- [ ] A2 等价：R6 全部比较项在两臂一致；终态 checkpoint canonical 相等。
- [ ] A3 校验：格式版本/完整性/身份不匹配/外键缺失/墓碑或锁输入不一致均结构化拒绝且零变更。
- [ ] A4 前置条件：非 Running 或 pending 非空的导出被结构化拒绝。
- [ ] A5 门禁：`cooking-et-level-runtime` 与 `cooking-kitchen-loop` 全步骤通过；既有测试零回归（含三个二进制指纹金样字节不变）。

## 4. 范围边界

- 做：领域 checkpoint 记录与 codec、仿真导出/恢复、宿主级导出/恢复入口、域内与宿主级测试、spec/progress/Todo 修约。
- 不做：写入磁盘或任何 durable store、跨小关规则、检查点的产品保存/加载流程、Pause 态导出、前厅、评分、失败条件、传输、Unity。

## 5. 权威来源

- `Docs/Todo.md` P0-C1（本任务对应两条未勾项）
- `Docs/design/CookingGame/reference/product-lifetimes.md`（§4.1 Pause 保留与水位口径、HostFrameSequence 单调）
- `.trellis/spec/cooking/cooking-recipe-loop.md`（闭环行为契约）、`cooking-persistence-management.md`（envelope/codec 既有形态）
- `.trellis/tasks/archive/2026-09/09-21-cooking-et-closed-loop-acceptance/`（宿主闭环验收的接法与口径先例）
