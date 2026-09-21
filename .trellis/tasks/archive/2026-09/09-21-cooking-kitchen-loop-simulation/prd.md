# PRD：Cooking 厨房闭环仿真规则与闭环 fixture

> 状态：planning（待 owner 批准后进入 in_progress）。
> 上游契约：`.trellis/spec/cooking/`（任务①已修约）；owner 决定来源：`.trellis/tasks/09-19-cooking-gameplay-business-discussion/research/discussion-notes.md` §11。
> 本任务只覆盖纯 C#/.NET 单机范围：领域仿真、领域测试、fixed-tick 宿主及其测试。Unity 一切范围禁止；传输/联机不做。

## Goal

在单机纯 C# 范围内，把权威仿真改造为 owner 已确认的厨房闭环形态（容器即物品、七项动作、放入即拒绝、锁输入、端走继续、两种完成形态、统一产品 ID 分配），并以番茄蛋花汤闭环 fixture 与订单/碗池规则验收。

## 1. 背景与问题

任务①落地了 schema v2、多输入配方命令形状、两套指纹与 spec 修约，但仿真本身仍是旧形态：

- 容器是与物品并列的第二身份（`CookingContainerDefinition` + `CookingRecipeFixture.Containers`），与"容器是带容器能力的物品"的已确认结构冲突；
- 动作只有拾取、启动、推进、装盘（Plate）、提交五项，缺放下、放入、取出、倒出；`Plate` 用容器 ID 而非容器物品；
- 完成形态枚举已在配置层存在，但 `CookingRecipeSimulation` 只实现"消耗输入、在工位生成产物"一条路径，`RetainInputs` 与倒出生成不存在；
- 错误输入在启动校验才拒绝；活动进程输入可被随意移动；`ValidateProcessForFixedTick` 强约束"输入必须停在工位"，与"端走继续"矛盾；
- `AdvanceTicks` 命令路径里硬编码 `product-{n}`，绕过统一 allocator；
- 没有番茄蛋花汤闭环 fixture，订单端口没有"要求"概念，碗的脏/净与干净碗池不存在；
- 多人争抢在 recipe 命令路径上没有稳定仲裁（`Submit` 单命令路径无批次排序）。

## 2. Requirements

- **R1 容器即物品**：容器是带容器能力的物品实例；退役 `CookingContainerDefinition` 与 fixture 的独立容器表；容器内容以容器物品 ID 为键；快照与 canonical 增加容器物品的内容物。
- **R2 七项动作**：拾取、放下、放入、取出、启动加工、倒出、提交，全部是权威原子命令；`Plate` 由"放入/倒出"取代，映射关系写死在 domain。
- **R3 放入即拒绝**：容器允许放入哪些物品由该容器物品定义的配置声明；配方外物品在放入阶段拒绝，不进入启动校验。
- **R4 不可取消 = 锁输入**：新增 item→process 反查索引；放下、放入、取出、倒出拒绝属于活动进程输入的物品；拾取仅放行活动进程的容器锚点（端走继续）。
- **R5 端走继续**：`ValidateProcessForFixedTick` 的"输入必须停在工位"改为输入位置白名单（世界、工位、容器槽可，手持不可）加"输入未被移除且仍属该进程"；保留为腐败检测器，不得退化成永不触发。
- **R6 两种完成形态进 fixed tick**：`ConsumeInputs` 消耗输入并在工位或容器上生成输出；`RetainInputs` 保留输入、容器切"已完成"，成品在倒出时才生成；两者都走 `FixedTickPlan` 原子交换；倒出至多生成一次产物。
- **R7 统一产品 ID allocator**：消除 `AdvanceTicks` 里硬编码 `product-{n}` 的第二条路径。
- **R8 闭环 fixture**：番茄、鸡蛋、番茄块、蛋液、锅、碗、蛋花汤、面包片 + 砧板、灶台、烤箱的标准初始供应；打蛋路径（鸡蛋入碗、手持碗免工位启动、蛋液留碗、碗倒空复用）；工位绑定按加工区分（切→砧板、煮→灶台、打蛋→免工位、烤→烤箱）；订单端口带"要求"（碗中的蛋花汤）并在提交成功时回报订单完成；碗脏/净状态与干净碗池上限，NPC 清洗做成注入端口，测试直接注入"清洗完成"。
- **R9 多人争抢仲裁**：同一封闭批次内对同一物品/工位/容器的争抢按 Tick、玩家 ID、命令序号稳定排序（规则先落地，联机本身后续再说）。
- **R10 新 gate**：`cooking-kitchen-loop`（P1，`Gate=CookingKitchenLoop`），同步 `tools/test-gates.json` 与门禁文档 §9。

## 3. Acceptance Criteria

- **K01 容器即物品**：fixture 只声明物品定义；容器物品可被拾取/放下/放入/取出/倒出；`CookingContainerDefinition` 与 `CookingRecipeFixture.Containers` 不再存在；快照与 canonical 能观察到容器内容物；旧"容器 ID 直传"的测试全部迁移。
- **K02 七项动作**：每项动作一条 happy-path 原子命令测试加至少一条 mutation-free 拒绝测试；`Plate` 操作从枚举与命令形状中消失，`IsWellFormed` 同步更新；ET 二进制指纹金样重锚。
- **K03 放入即拒绝**：锅声明可接受集合；放入配方外物品在放入阶段被拒（结构化 reason），启动校验不被触发，权威状态零变更。
- **K04 锁输入**：活动进程输入被放下/放入/取出/倒出拒绝；活动进程的容器锚点允许拾取（端走继续），拾取后 fixed tick 继续推进且进度保留。
- **K05 端走继续**：输入位置白名单生效——输入在世界/工位/容器槽时 fixed tick 正常；输入被移除或不再属于该进程时 fixed tick 抛结构化腐败错误且零变更；测试证明该检测器可达（不是永不触发）。
- **K06 两种完成形态**：`ConsumeInputs`（切番茄：消耗番茄、在砧板生成番茄块；打蛋：消耗鸡蛋、在碗中生成蛋液）与 `RetainInputs`（煮制：输入保留、锅切"已完成"）都在 `AdvanceFixedTick` 原子提交；重复倒出至多生成一次产物。
- **K07 统一 allocator**：命令路径与 fixed-tick 路径的产品 ID 来自同一 allocator；allocator 故障/碰撞/空白输出在两条路径都 mutation-safe。
- **K08 闭环 fixture**：一条端到端测试从标准初始供应出发，走完"取番茄→砧板切→鸡蛋入碗→手持碗打蛋→蛋液入锅→番茄块入锅→启动煮制→端走继续→到点完成→倒出入碗→提交订单→订单完成→碗脏→注入清洗→碗回池"，最终快照哈希确定且可重复。
- **K09 订单要求**：订单端口按"要求"（recipe identity）接受/拒绝；提交错误 recipe 拒绝且已装盘菜品不回滚；提交成功时端口观察到订单完成一次。
- **K10 碗池**：干净碗总量由配置声明；提交后碗脏并离开厨房（交给 NPC 端口）；注入"清洗完成"后碗回池且不超过上限；超上限注入被拒。
- **K11 争抢仲裁**：同一批次两名玩家争抢同一物品/工位/容器，只有 Tick、玩家 ID、命令序号排序胜出者成功；乱序抵达与重复投放结果一致。
- **K12 门禁**：`cooking-kitchen-loop` gate 在 `tools/test-gates.json` 存在且可运行；门禁文档 §9 同步；新测试带 `Gate=CookingKitchenLoop` 分类。

## 4. 范围外（明确不做）

UDP/KCP 等一切传输与网络内容；前厅（顾客入座、NPC 询问过程、餐桌、用餐离席、收益评价、订单生成节奏）；小关营业/收尾时间结构与成功条件；失败条件与失败重试；过度加工与烧焦产物；跨小关装修/道具/Buff；检查点；ET Phase B 权威迁移；ID 从 string 迁 long；Unity 一切范围（package/asmdef/scene/authoring/projection/UI/EditMode）。

## 5. 约束与风险

- P0 交互仿真（`CookingSimulation`）与 EtBridge 投影明确不动；`CookingItemDefinition` 的容器能力字段已在任务①尾部追加，本任务只消费它。
- 既有 `CookingRecipeLoopTests` 的 R01-R06 与 fixed-tick 回归必须继续通过；行为变化只能来自 owner 已确认决定，且在 design.md 显式记录旧→新映射。
- 命令枚举是 wire 形状：UDP 已退役，但 ET 二进制指纹金样与 evidence 记录仍要求"枚举只能尾插"；新增动作若不能全部尾插，必须在 design.md 给出重锚计划并在实现中执行。
- 所有验收必须有 `artifacts/` 下的 JSONL evidence 与 `check.jsonl` 记录的真实命令；独立复验不调用被测 C# 代码。

## Notes

- 需求唯一来源：owner 逐轮决定（09-19 notes §11.1）与任务①修约后的 `.trellis/spec/cooking/`；参考文件不替代本 PRD。
- 交付状态表述：本任务完成前，不得把容器即物品、七项动作或闭环 fixture 写成已实现能力。
