# Cooking 厨房闭环契约层

> 状态：`in_progress`。本任务只做契约层，不实现玩法规则；实现授权来自 owner 2026-09-21 对 prd.md 与 design.md 的批准。

## Goal

在单机范围内，为厨房闭环（代表菜番茄蛋花汤）收敛四类契约：配置 schema v2、多输入配方的命令形状、两套命令指纹的兼容、以及对既有 spec 的显式修约。本任务不实现容器即物品、七项动作和闭环 fixture——那些属于后续任务②③。

## Background and confirmed facts

### 需求来源（owner 已确认）

以下决定来自 `.trellis/tasks/09-19-cooking-gameplay-business-discussion/` 的 `prd.md` 与 `research/discussion-notes.md`，均为 owner 逐轮明确决定，不是助手建议：

- 代表菜为番茄蛋花汤；首个闭环必须同时覆盖多输入、半成品、持续加工、装盘。
- 预处理（切番茄、打蛋）消耗原物品，生成新的半成品物品实例。
- 多输入加工由玩家显式启动；配方自动识别，不由玩家手动选择。
- 配方中的水视为默认供应，不占物品、不占容器容量、不需要玩家操作。
- 煮制到点完成时输入保留在锅中，锅切换“已完成”状态，成品在倒出时才生成；单步预处理类加工则消耗输入、在工位或容器上生成输出。**两种完成形态并存。**
- 配方按输入集合匹配，输入顺序不重要。
- 锅允许放入哪些物品由锅自己的配置定义声明；配方外物品在放入阶段即被拒绝。
- 锅和碗的容量由配置定义；同类物品不堆叠，每个物品独立占位。
- 固定工位需要砧板和灶台；一个工位同一时刻只进行一个加工；动作必须在对应工位上执行，但打蛋手持碗即可启动、不需要工位。
- 启动煮制后不能停止加工，但可以把锅从灶台端走，加工继续并保留进度。
- 玩家基础动作集为七项：拾取、放下、放入、取出、启动加工、倒出、提交，均为权威原子命令。
- 烤箱是另一种加工工位；其首个配方为烤面包（面包片 → 烤面包，配料/半成品，不直接对应订单）。
- 加工时长、tick 数、干净碗总量等数值一律由配置定义。
- 多位玩家共享厨房，争抢按 Tick、玩家 ID、命令序号稳定排序。

### 当前代码事实

- `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs`：`CookingRecipeDefinition` 只有单个 `InputDefinition`；`CookingContainerDefinition(ContainerId, Capacity)` 是独立资产而非物品能力；`CookingRecipeOperation` 只有 Pickup/StartProcess/AdvanceTicks/Plate/SubmitOrder；`CookingRecipeCommand` 只有一个 `Item` 字段；`StartProcess` 要求显式 `RecipeId`；`ValidateProcessForFixedTick` 要求输入物品必须停在工位。
- `src/AbilityKit.Game.Cooking/CookingConfigurationValidation.cs`：`CurrentSchema = "cooking-definition-v1"`；canonical 覆盖 item/appliance/recipe/container 四张表。
- 命令指纹有三处：仿真层 JSON 序列化（`SubmitCore`）、`CookingRecipeCommandValidation.IsWellFormed`、ET 层 `CookingCommandFingerprint.CanonicalBytes` 二进制序列化（`src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`），后者在 `src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs` 有硬编码 golden 十六进制向量。
- 产品 ID 分配有两条路径：fixed tick 走注入的 `ICookingProductIdAllocator`，`AdvanceTicks` 内联 `product-{n}`。
- 旧 P0 交互仿真 `CookingSimulation` 与 EtBridge 投影共享 `CookingItemDefinition`、`ItemLocation` 等类型，明确不在本任务改动范围；已核实 `CookingSimulation` 只消费 `AllowedPlayerCapabilities`，EtBridge 投影不引用 `CookingItemDefinition`。
- Cooking 的三个 UDP 项目与 `cooking-udp` gate 已于 2026-09-21 退役（owner 决定传输改用 KCP），源码与 artifact 保留；本任务不得以任何形式恢复或依赖它们。
- `CookingConfigurationCompatibility.Evaluate` 当前没有生产调用方（仅测试使用）；v1 拒绝的实际路径是 lifecycle 层的身份字符串比较（`ConfigIdentityMismatch` / `SnapshotIdentityMismatch`）。

### 既有规范的冲突（必须走修约程序）

- `.trellis/spec/cooking/cooking-recipe-loop.md` 写“完成条件满足时一次性消耗声明的输入、创建声明的产物”，与 owner 决定的“锅保留输入、倒出时才生成成品”字面冲突。owner 已裁决以玩法决定为准，规范文本必须修订。
- `.trellis/spec/cooking/cooking-interaction-foundation.md` 的位置枚举只列 WorldPosition、PlayerHand、StationSlot，没有 ContainerSlot；容器即物品后物品会位于另一物品内。规范文本必须修订。

## Requirements

### R1 配置 schema v2

- `CurrentSchema` 升级为 `cooking-definition-v2`。
- canonical 增加：物品的容器能力（容量、可接受物品定义集合）、配方的多输入集合与默认供应声明、加工完成形态。
- v1 配置与 v1 快照在 v2 下以结构化 blocked 诊断拒绝（`MigrationPolicyNotApproved` 或等价的稳定诊断码），不写迁移、不静默转换。
- v2 校验补齐：多输入外键存在性、容器能力引用存在性、输入集合非空、完成形态取值合法、默认供应不得与物品输入重复；诊断一次性收集且确定性排序。
- 升级后仍保持“新增配方/容器/工位只需数据扩展，不为单条内容增加规则分支”。

### R2 多输入配方的命令形状

- 保持单物品命令：用多条“放入”把输入逐个累积进容器，一次“启动”按容器内容集合匹配配方。**本任务只落地契约与纯匹配函数，`PutInto` 与仿真接线属后续任务②。**
- 匹配按输入集合进行，顺序无关；实现必须按稳定键（DefinitionId 排序）比较，保证确定性与可重放。匹配逻辑在本任务实现为纯函数并测试（输入：物品定义集合 + 候选配方的默认供应 + 工位能力；输出：唯一配方 / `RecipeNotMatched` / `RecipeAmbiguous`）。
- `CookingRecipeCommandValidation.IsWellFormed` 的 StartProcess 分支不再强制 `Recipe` 字段（record 中 `RecipeId? Recipe` 本就是可空）；携带时按显式配方校验，不携带时本任务返回结构化拒绝，容器内容匹配的接线属任务②。
- 匹配失败与歧义返回结构化拒绝，不得抛异常污染帧路径。

### R3 默认供应

- 配方可声明默认供应输入（水），该输入不占物品、不占容器容量、不需要玩家操作，但参与配方匹配与 canonical identity。

### R4 两套指纹与金样兼容

- 命令 record 形状不变，因此 ET 层二进制指纹输出逐字节不变：以既有 golden 向量作为“指纹未被意外改动”的回归锚，并新增一条 recipe-less StartProcess 命令的 golden 向量。
- 仿真层 JSON 指纹、`IsWellFormed`、ET 层 `CookingCommandFingerprint.CanonicalBytes` 三处对命令语义的变化保持同步；任一处漏改都构成任务失败。
- 命令去重键 `(Session, Player, Command)` 与“同键不同内容 → CommandIdentityConflict”语义不变。

### R5 spec 修约（交付物的一部分）

修订四份文件：三份 spec 加 `index.md`。

- 修订 `.trellis/spec/cooking/cooking-recipe-loop.md`：完成语义改为“按加工定义的完成形态原子提交”，幂等契约延伸到倒出（至多生成一次产物）。
- 修订 `.trellis/spec/cooking/cooking-interaction-foundation.md`：位置枚举补 ContainerSlot 与“容器是带容器能力的物品”的表述，保留唯一位置、无重复占有、无包含环不变量。
- 修订 `.trellis/spec/cooking/cooking-config-validation.md`：v1→v2 blocked、无迁移的表述落到具体条款。
- 修订 `.trellis/spec/cooking/index.md`：P2/P3 行的边界表述与“未完成范围入口”列。
- 每处修订必须显式记录“旧条款 → 新条款 → 来源”，不得静默改写；来源分两类：owner 决定（可逐条追溯到 09-19）与经 owner 批准本 design 生效的实现契约，二者不得混淆。
- “容器是带容器能力的物品”作为一般化表述没有单独的 owner 裁决原文，由具体行为裁决复合推出，随本修约程序提交 owner 批准。

### R6 明确的非目标

- 不实现容器即物品、七项动作（`PutInto`/`Drop`/`TakeOut`/`Pour`）、两种完成形态的运行时推进逻辑、闭环 fixture、订单簿、前厅。
- 不把 `CookingRecipeDefinition` 的输入集合语义接到仿真状态：fixed-tick 校验与 `StartProcess` 只做“成员判定 / 显式配方校验”的过渡实现，多输入进程属任务②。
- 不恢复 UDP/KCP 等任何传输内容。
- 不改旧 P0 交互仿真 `CookingSimulation` 与 EtBridge 投影的行为；`CookingItemDefinition` 只能以尾部追加可选参数的方式扩展，避免击穿其编译（已核实 `CookingSimulation` 只消费 `AllowedPlayerCapabilities`，EtBridge 不引用该类型）。

## Acceptance Criteria

- [ ] `cooking-definition-v2` 生效；v1 候选与 v1 快照得到结构化 blocked 诊断，无迁移转换。
- [ ] 多输入配方可用纯数据声明；纯匹配函数实现并按集合匹配、顺序无关、默认供应并入；容器内容接线明确留给任务②。
- [ ] 命令语义变更后，仿真 JSON 指纹、`IsWellFormed`、ET 二进制指纹三处一致；既有 golden 向量断言不变，新增 recipe-less StartProcess golden 向量。
- [ ] `Recipe` 字段两种模式（携带时校验、不携带时结构化拒绝）都有测试。
- [ ] 四份文件（三份 spec + index.md）修订落地；owner 决定来源的修订逐条可追溯，其余修订注明经 owner 批准本 design 生效。
- [ ] `cooking-et-level-runtime` gate 实际运行并通过（覆盖 Cooking.Tests 与 ET.Runtime.Tests 全量）。
- [ ] 证据落 `artifacts/cooking-kitchen-loop-contracts/`，check.jsonl 记录真实命令与结果。

## Out of scope

- 容器即物品与七项动作的运行时实现、闭环 fixture、订单簿与前厅。
- 跨小关装修/道具/Buff、检查点、失败条件（owner 明确推迟）。
- ET Phase B 权威迁移、ID 从 string 迁 long。
- UDP/KCP 等一切传输与网络内容。
- Cooking Unity 与一切 Unity 范围。

## Blocking open questions

- 无产品阻塞项。实现层面的两个前置决策已在 design.md 定为契约：多输入用“多条放入 + 一次启动”；`Recipe` 保留为可选字段而非删除。
