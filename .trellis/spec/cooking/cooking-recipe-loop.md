# P2 一条完整配方：cooking-recipe-loop
## 2026-09-21 仿真落地修约（任务 09-21-cooking-kitchen-loop-simulation）

来源：owner 逐轮决定（09-19 notes §11.1 第一至九轮）与任务①修约契约，经实现落地为可验证行为。本次修约把上一轮“属后续任务”的容器即物品、七项动作与闭环 fixture 从“未实现”改为“已在单机纯 C# 范围实现并验证”，验证证据见 task `check.jsonl` 与 `artifacts/cooking-kitchen-loop-domain/`。前厅（顾客、NPC 询问过程、订单生成节奏、用餐离席）、小关时间结构、失败条件仍范围外。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「装盘与提交订单是独立的权威原子步骤」 | “将完成产物装入合法容器/槽位” 的 `Plate` 操作（容器 ID 直传） | `Plate` 退役，由七项动作中的“放入”（`PutIn`，手持物品进容器槽）与“倒出”（`Pour`，容器间转移或从已完成容器生成成品）取代；提交的产物必须在容器槽中，容器按当前位置判定可达 | owner 决定：notes 11.1 第六轮（七项动作：拾取、放下、放入、取出、启动加工、倒出、提交）、第九轮（蛋液倒进锅后碗即空出） |
| Requirement「取料与加工必须复用权威物品交互边界」 | 未规定活动进程输入的移动限制 | 启动加工即锁定输入：放下、放入、取出、倒出拒绝属于活动进程输入的物品；拾取仅放行活动进程的容器锚点（端走继续） | owner 决定：notes 11.1 第三轮（启动煮制后不可取消）＋第七轮（可以把锅从灶台端走、加工继续并保留进度） |
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | 完成时“输入必须停在工位”的强约束（`ValidateProcessForFixedTick`） | 输入位置白名单：世界、工位、容器槽可，手持不可；加“输入未被移除且仍属该进程”。容器锚定加工的内容物必须仍留在该容器槽中。白名单保持腐败检测器可达（测试注入证明） | owner 决定：notes 11.1 第七轮（端走继续）＋实现契约（plan 任务②“保留它作为腐败检测器，不能退化成永不触发”） |
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | 完成形态只在配置层存在 | 两种完成形态都经 `FixedTickPlan` 原子提交：`ConsumeInputs` 消耗输入并在工位或容器上生成输出（打蛋的蛋液落碗）；`RetainInputs` 保留输入、容器切“已完成”，成品在倒出时才生成；倒出至多生成一次产物（重复倒出拒绝） | owner 决定：notes 11.1 第二/三/八轮 |
| Requirement「取料与加工必须复用权威物品交互边界」 | 未规定工位绑定 | 工位绑定按加工定义声明（`RequiresStation`）：切→砧板、煮→灶台、烤→烤箱、打蛋→免工位；免工位加工不得携带工位，需工位加工缺工位即结构化拒绝 | owner 决定：notes 11.1 第三/七/八轮（三种加工工位要求并存） |
| Requirement「多输入下达与匹配」 | 匹配在单物品命令上表达 | 容器锚定加工按容器内容集合匹配；水是默认供应（`DefaultInputs`）不占物品不占容量；显式 Recipe 与自动识别都要求输入集合齐全 | owner 决定：notes 11.1 第一/七轮 |
| Requirement「争抢仲裁」（P0 交互基础延伸） | recipe 命令路径无批次仲裁 | 封闭批次按 (LogicalTick, 玩家 ID, 命令 ID) 稳定排序；乱序抵达与重复投放结果一致；同一 command identity 重放返回缓存结果 | 实现契约：plan 任务②“多人争抢仲裁按 Tick、玩家 ID、命令序号稳定排序，规则先落地” |
| Requirement「装盘与提交订单」 | 订单端口无“要求”概念，碗无脏/净与池 | 订单端口按要求（recipe identity）接受/拒绝，提交成功回报订单完成一次；可洗碗定义提交后变脏并交 NPC 端口，注入“清洗完成”后回池且不超过配置上限 | owner 决定：notes 11.1 第二/六轮（碗脏由 NPC 清洗归还、干净碗总量由配置定义）＋plan“NPC 清洗做成注入端口” |

### 实现状态声明

- 已实现并验证（单机纯 C#）：容器即物品（`CookingContainerDefinition` 与 fixture 独立容器表已退役）、七项动作、放入即拒绝、锁输入、端走继续、两种完成形态、统一产品 ID allocator、番茄蛋花汤闭环 fixture（L01 端到端 + L02 确定性重放）、订单要求、碗池、批次争抢仲裁。
- 仍未实现（范围外）：前厅顾客/NPC 过程、订单生成节奏、小关时间结构与成功条件、失败条件、Unity 一切范围。
- 推定项（无单独 owner 裁决原文，实现按 task design §9 执行）：Pour 的源/目标用 command.Item/Container 表达、锁定拒绝复用 `ItemStale`、免工位进程按锚点物品建索引、`RequiresStation` 布尔字段、提交后容器直接移除出厨房、仲裁键 (LogicalTick, Player, CommandId)、`OrderCompleted` 透传、`CompleteWash` 返回结果记录、普通容器间 Pour 转移全部内容物、canonical 增加 `RequiresStation` 但保持 v2 身份字符串。

## 2026-09-21 契约修约

来源：owner 在 `.trellis/tasks/09-19-cooking-gameplay-business-discussion/` 的逐轮决定（notes 11.1 第二/三轮），经 owner 批准由 Trellis task `09-21-cooking-kitchen-loop-contracts` 的 `design.md` 落地为契约。本次修约不解除 2026-09-16 收口状态中"未完成的非 Unity 范围不得写成已实现"的约束，也不代表容器即物品、七项动作或闭环 fixture 已实现——那些属后续任务。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | “在完成条件满足时一次性消耗声明的输入、创建声明的产物” | 按加工定义的完成形态原子提交：`ConsumeInputs` 消耗输入并生成输出；`RetainInputs` 保留输入、容器切“已完成”，成品在倒出时才生成 | owner 决定：09-19 notes 11.1 第二/三轮（锅保留输入、倒出时才生成成品；单步预处理类加工消耗输入生成输出；两种形态并存） |
| Scenario「重复完成命令」 | 幂等只覆盖“工序完成”一次 | 幂等延伸到倒出：倒出至多生成一次产物 | owner 决定 + 经 owner 批准本 design 生效的实现契约 |
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | 未规定多输入如何下达与匹配 | 多输入靠多条“放入”把输入逐个累积进容器，一次“启动”按容器内容集合匹配配方；输入顺序不重要，匹配按 DefinitionId 排序后比较以保证确定性与可重放 | owner 决定：notes 11.1 第一/七轮（多条放入加一次启动、配方自动识别、顺序不重要） |

“同一输入集合 + 同一工位能力必须唯一匹配一个配方”是**实现契约而非 owner 裁决**：它由“配方自动识别必须可判定”推导，命中多个配方返回结构化拒绝 `RecipeAmbiguous`，不是运行时异常。

## 2026-09-16 收口状态

- R01-R06 纯 .NET fixture loop 已验证并作为 limited delivery 收口；正式内容、订单/结算和真实 LAN 等仍未启动，见 successor backlog。
- 对应 `09-15-cooking-*` task 已按 `completed-limited-scope` 语义归档；`completed` 不表示完整 P2 或完整 P0-P6 产品出口。
- Cooking Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 scene smoke 长期禁止实施；原 Unity 场景及宿主无关不变量统一见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)。
- 本文以下 authority、identity、atomicity、sequence、stale-input、persistence 或 measurement 行为不变量继续有效；未完成的非 Unity 范围不得写成已实现，P1-P6 入口见 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md)。

> 交付状态：**completed-limited-scope**；原完整能力迁移状态为 `blocked`。本规范由只读来源快照 `.trellis/migration/legacy-cooking-changes/add-cooking-recipe-loop/specs/cooking-recipe-loop/spec.md` 转换；原始 SHA-256 见 [迁移清单](../../migration/legacy-cooking-changes/manifest.json)。
>
> 当前已实现并验证受限 pure .NET fixture：单输入/单工序/3 Tick、container slot、注入式 accept/reject order port，覆盖 R01-R06。正式 recipe/order/settlement/score 与真实 LAN R07 是 successor backlog 中未启动、未批准的 non-Unity 范围；Unity 是 prohibited/not-run future scope。

## 当前边界

- 已验证：R01-R06 pure .NET fixture contract；实际命令和 JSONL evidence 见 P2 task `check.jsonl`。
- Fixture：单 input、单 process、3 logical ticks、injected accept/reject order port；不代表正式 content 或产品 timing。
- 联机 R07、正式 content/order/settlement/score/failure UX、production transport、two-PC LAN、benchmark 与完整 P2 exit：未完成并进入 successor backlog；Unity 未运行并进入 future scope。

## 迁移边界

- 依赖：P0；联机验收时还依赖 P1 的稳定 session。
- 阻塞：正式配方、订单/结算 owner、评分与失败处理均待确认。
- 验证状态：R01-R06 已实现并通过 focused pure .NET tests；R07 two-PC LAN 与正式内容属于 successor backlog，未启动、未批准、未执行。

## 迁移的行为草案

## Purpose

为做菜经营游戏提供一条可由纯 C# 权威模拟验证的最小数据驱动闭环，覆盖取料、加工、装盘与提交订单，同时把未决的正式内容和 owner 决策明确隔离。

## ADDED Requirements

### Requirement: 取料与加工必须复用权威物品交互边界
系统 SHALL 允许玩家从合法来源取得配方所需食材，并仅在身份、当前位置、资格、范围、容量和生命周期检查全部通过时创建加工动作；失败 MUST 保持权威状态不变。

#### Scenario: 合法取料并开始加工
- **WHEN** 玩家在有效 Match 中取得满足配方输入的食材，并向具备所需能力的厨具提交加工命令
- **THEN** 系统 SHALL 原子地锁定输入、创建加工进度，并记录可观察的 recipe/process identity；不得产生重复位置或所有权

#### Scenario: 取料或厨具资格失败
- **WHEN** 食材缺失、玩家无资格、距离不可达、厨具能力不匹配或输入已被占用
- **THEN** 系统 SHALL 返回稳定失败原因，且加工进度、输入位置、厨具占用和事件序列均保持不变

### Requirement: 加工进度与产物必须由模拟逻辑驱动
系统 SHALL 使用明确的模拟 Tick 或逻辑时钟推进已接受的工序，并按加工定义的完成形态原子提交结果：完成形态为 `ConsumeInputs` 时消耗声明的输入并在工位或容器上生成输出；完成形态为 `RetainInputs` 时保留输入、把容器切换为“已完成”状态，成品在倒出时才生成；两种形态由加工定义声明，不由运行时推断。完成 MUST NOT 依赖 Unity 动画回调或墙钟作为完成依据。

#### Scenario: 工序按逻辑时间完成
- **WHEN** 已开始工序经过不足完成阈值的 Tick，再经过达到阈值的 Tick
- **THEN** 系统 SHALL 在前者保持进行中，在后者只提交一次完成结果，并使产物身份、数量与配方定义一致

#### Scenario: 重复完成命令与重复倒出
- **WHEN** 相同 command identity 或同一工序完成请求被重复提交，或同一“已完成”容器被重复倒出
- **THEN** 系统 SHALL 返回幂等结果，输入只消耗一次、产物只创建一次、事件只发布一次；倒出至多生成一次产物

### Requirement: 装盘与提交订单是独立的权威原子步骤
系统 SHALL 将完成产物装入合法容器/槽位作为独立的权威原子命令，并将已装盘且仍可交单的菜品提交给订单 owner 作为另一独立的权威原子命令。装盘成功后状态 MUST 保留并可被拿取或后续提交；订单校验失败只拒绝本次提交，不得回滚已装盘菜品或产生其他部分变更。

#### Scenario: 合法装盘并保留
- **WHEN** 完成产物、容器容量、装盘资格和当前位置均有效
- **THEN** 系统 SHALL 原子地完成装盘并发布一次装盘结果；菜品保留在容器/槽位中，可被拿取或作为后续订单提交物，订单状态不因装盘命令改变

#### Scenario: 装盘失败
- **WHEN** 容器已满、产物不适配、当前位置不符或装盘范围无效
- **THEN** 系统 SHALL 拒绝本次装盘，产物、容器、订单进度和事件保持不变

#### Scenario: 订单校验失败但装盘已成功
- **WHEN** 已装盘菜品提交时订单不可接受、菜品不匹配或提交范围无效
- **THEN** 系统 SHALL 只拒绝本次订单提交，已装盘菜品和容器状态保持不变，不得回滚装盘

#### Scenario: 合法订单提交
- **WHEN** 已装盘菜品、订单所需 recipe identity、提交资格和订单状态均有效
- **THEN** 系统 SHALL 原子地消费该提交物并更新订单，产生一次可关联的提交结果

#### Scenario: 提交命令重复与跨身份重放
- **WHEN** 相同 command identity 重复提交，或不同 command identity 再次提交已消费的菜品
- **THEN** 系统 SHALL 对相同 command identity 返回已处理结果且不产生二次变更；对不同 command identity 拒绝已消费菜品，且不得重复更新订单

### Requirement: 正式配方与订单语义必须经过决策门
系统 SHALL 将正式配方内容、订单 owner、评分/结算、计时边界和失败处理标记为 Draft / Blocked，未获 owner 确认前不得把路线图示例升级为产品承诺；测试 fixture MUST 明确仅用于验收。

#### Scenario: 决策未确认时创建闭环 fixture
- **WHEN** 实施者使用最小测试配方验证领域流程，但正式配方或订单 owner 尚未确认
- **THEN** 系统 SHALL 允许以 fixture 验证结构和原子性，但交付状态保持 Draft / Blocked，不得宣称正式内容或结算已接受

### Requirement: 联机验收必须依赖稳定阶段 2 会话
阶段 3 的纯 C# 领域闭环已按 limited scope 验证。若 successor 将来声称 host/client 联机一致性，则 MUST 新建 task，并依赖稳定 session、共享 command path、snapshot seam、LAN 集成证据及 D1-D4 决策。

#### Scenario: 纯 C# 闭环验收
- **WHEN** 阶段 1 证据齐全且执行最小 fixture 的取料、加工、装盘流程
- **THEN** 系统 SHALL 能验证最终状态、产物与失败原子性，而不要求真实 transport

#### Scenario: 联机闭环验收前置缺失
- **WHEN** 阶段 2 session 或 LAN 集成证据、transport/D4 决策门尚未满足
- **THEN** 系统 MUST 将该 successor 验收标记为未获批准/未执行，不得以同机或 in-process 结果替代真实 LAN 出口
