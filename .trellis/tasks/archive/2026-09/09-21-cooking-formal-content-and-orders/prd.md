# PRD：Cooking 正式配方与订单内容

> 状态：in_progress（owner 已批准开始本任务；文档随实现进度同步更新）。
> 上游契约：`.trellis/spec/cooking/`（任务①②已修约并落地）；owner 决定来源：`.trellis/tasks/09-19-cooking-gameplay-business-discussion/research/discussion-notes.md` §10–§12。
> 本任务只覆盖纯 C#/.NET 单机范围：领域仿真、领域测试、fixed-tick 宿主及其测试。Unity 一切范围禁止；传输/联机不做。

## Goal

把"单输入/单工序/3 Tick fixture + 测试端口判定订单要求"的临时形态，替换为 owner 已确认的正式内容与订单归属：数据驱动并经 `cooking-definition-v2` 校验的正式 Recipe/Process/Appliance/Container/Order 内容与 timing；订单簿与提交/结算契约由领域拥有；番茄蛋花汤闭环从正式内容运行并保持确定可重放。

## 1. 背景与问题

任务①②落地了 schema v2、七项动作、容器即物品、两种完成形态与番茄蛋花汤闭环 fixture，但内容与订单仍是临时形态：

- **内容是测试内联字典**：`CookingKitchenLoopFixtureTests` 等各自手写 items/appliances/recipes 字典，没有单一、可校验、可复用的正式内容来源；烤面包配方用的是占位版本（`dough -> bread-slice`），与 owner 第十二轮采纳的"面包片（开局供应）→ 烤面包"不一致；
- **订单 owner 在测试里**：订单"要求"由测试端口 `ICookingOrderPort` 判定（`RequirementOrderPort` 按 recipe identity 接受/拒绝），领域只维护一个 `_acceptedOrders` 集合；订单没有定义、没有状态机、没有开单入口，前厅只能靠替换端口间接参与；
- **提交/结算契约不完整**：提交成功只有"订单完成一次 + 产物消耗 + 碗脏交 NPC"，没有可观察的结算记录；评分、收益、评价属小关结算（owner 明确推迟），但领域必须留下确定的结算事实供其消费；
- **timing 没有正式归属**：加工时长等数值由 owner 第六轮确认"一律由配置定义"，目前散落在各 fixture 的字面量里。

## 2. Requirements

- **R1 正式内容目录**：新增数据驱动的正式内容（JSON 文档 + 加载器），覆盖 owner 已确认的物品、工位、配方、订单模板与标准初始供应；内容经现有 v2 配置校验通过，身份 hash 确定且重复加载一致。内容包含：番茄、番茄块、鸡蛋、蛋液、水（默认供应）、蛋花汤、面包片、烤面包、碗、锅；砧板（cut）、灶台（heat）、烤箱（bake）、台面；切番茄、打蛋、番茄蛋花汤、烤面包四条配方；蛋花汤订单模板；标准初始供应（锅落灶台、番茄/鸡蛋/面包片入储藏、干净碗池上限 2）。
- **R2 v2 校验扩展**：订单模板与初始供应进入 v2 候选与校验面——订单模板的要求 recipe 必须存在、要求容器定义必须存在且带容器能力且接受该 recipe 的产物定义；供应项引用的物品定义必须存在、容器供应数量为正。诊断结构化、与声明顺序无关；canonical 与身份 hash 覆盖订单模板与供应，schema 字符串保持 `cooking-definition-v2`（任务①决定：身份字符串不变）。
- **R3 Order owner 移入领域**：仿真拥有订单簿（Open/Completed 状态、模板、要求）；开单是前厅注入入口（与 `CompleteWash` 同一模式）；提交校验完全在领域内按订单簿执行。`ICookingOrderPort`/`CookingOrderSubmission`/`CookingOrderAcceptance` 退役，旧→新映射在 design.md 写死。
- **R4 提交/结算契约**：`SubmitOrder` 按订单簿校验——订单存在且 Open、产物 recipe 匹配订单要求、容器定义匹配订单要求、产物在容器槽且容器可达；成功时原子地消耗产物、订单转 Completed（一次）、容器按可洗碗定义变脏并交 NPC 端口、追加一条结算记录。同 command identity 重放返回缓存结果；跨 identity 重复提交已消费产物或已完成订单返回结构化拒绝。任何失败零变更，不回滚已装盘菜品。
- **R5 闭环 fixture 改用正式内容**：番茄蛋花汤闭环测试从加载的正式内容出发（不再手写 fixture 字典），走完取料、预处理、多输入煮制、端走继续、倒出、提交、洗碗回池全链路；重放得到相同 canonical 状态与哈希；订单要求拒绝错误产物且零变更。
- **R6 timing 归内容**：正式配方的 `RequiredTicks` 等数值只存在于内容文档中，测试与仿真不引入第二条数值来源；业务规则（完成形态、工位绑定、匹配、锁输入）与数值分离的既有边界不回退。
- **R7 快照可观察**：订单簿与结算记录进入 `CookingRecipeSnapshot` 与 canonical，使"订单开了/完成了/结算了几次"可被快照与哈希观察。

## 3. Acceptance Criteria

- **K01 内容加载**：从内容文档加载的候选通过 v2 校验；身份 hash 非空、确定，两次加载一致；坏内容（坏外键/坏供应/坏订单模板）产生结构化诊断且注册表零变更。
- **K02 订单簿**：开单后订单 Open 可观察；提交成功后订单 Completed 且只完成一次；快照/canonical 含订单簿。
- **K03 提交契约**：未开单、订单已完成、产物 recipe 不匹配、容器定义不匹配、产物已消费分别返回结构化拒绝（含新增 reason），且每次拒绝前后 canonical 一致、无事件。
- **K04 幂等**：同 command identity 重放返回 `IsDuplicate` 且无二次变更；跨 identity 对已完成订单/已消费产物拒绝。
- **K05 结算记录**：每次成功提交追加一条结算记录（order/recipe/product/player/container/tick），记录数等于成功提交数；记录不含评分字段（评分范围外）。
- **K06 闭环**：从正式内容运行的 L01 全链路通过且最终快照哈希非空；L02 重放 canonical 与哈希一致；L03 错误产物提交被订单要求拒绝且已装盘菜品不回滚；碗脏/清洗回池/上限行为不变。
- **K07 烤面包修正**：正式内容的烤面包配方为"面包片（开局供应）→ 烤面包"，消耗输入并在烤箱生成；占位 `dough -> bread-slice` 从正式内容退役（dough 不再是正式物品）。
- **K08 wire 形状不变**：七项动作命令枚举与 `IsWellFormed` 不变；ET 二进制指纹三个金样（pickup/start-process/put-in）字节不变；命令路径不新增字段。
- **K09 门禁**：`cooking-kitchen-loop` gate 全绿（focused `Gate=CookingKitchenLoop`、Cooking 全量、ET runtime 全量），`cooking-et-level-runtime` 回归通过；check.jsonl 记录真实命令与结果。
- **K10 文档**：`cooking-recipe-loop.md` 记录 order owner 移入领域与正式内容状态（含未覆盖边界）；`progress.md` 与 `Todo.md` 状态表述更新，不宣称评分/失败/前厅已完成。

## 4. 范围外（明确不做）

评分、收益、评价与小关结算展示（owner 推迟，属小关营业/收尾结构）；失败条件与失败重试（owner 明确推迟）；过度加工与烧焦产物；前厅一切范围（顾客入座、NPC 询问过程、餐桌、用餐离席、订单生成节奏）；跨小关装修/道具/Buff；检查点；UDP/KCP 等一切传输与网络内容；ET Phase B 权威迁移；ID 从 string 迁 long；Unity 一切范围（package/asmdef/scene/authoring/projection/UI/EditMode）。

## 5. 来源冲突与显式审议

- **successor backlog P2** 把"明确 order owner、评分、结算、失败处理与产品 UX"写在同一条目里，但 owner 已明确推迟失败条件（notes §11.1 第六轮、§12.1 第六轮），评分/收益/评价属小关结算（notes §11.1 第四/五轮）且本工作区的阶段计划把"小关营业/收尾与成功条件"列为明确不做。本任务按后者执行：交付内容、order owner 与提交/结算**契约**（确定事实与记录），不交付评分、失败处理与产品 UX；三者将随各自 owner 决定另行立项。此处显式记录该取舍，不把 backlog 单条条目当作已批准范围。
- **烤面包配方版本**：notes §12.1 第九/十轮的采纳版本是"面包片（开局供应）→ 烤面包"，任务② fixture 因当时无正式内容采用了占位"dough → bread-slice"。本任务按采纳版本修正，占位版本退役。
- **固定伙伴人数**暂定 1 位且最终人数未定，本任务不引入任何伙伴依赖（订单开单由前厅注入，不模拟 NPC）。

## 6. 约束与风险

- 命令枚举是 wire 形状：本任务不新增/不改动七项动作，ET 指纹金样应保持字节不变；若实现中发现必须改命令形状，必须先重锚金样并在 design.md 记录。
- P0 交互仿真（`CookingSimulation`）与 EtBridge 投影不动；`CookingConfigurationCandidate` 的新增字段必须尾部可选，避免击穿既有候选构造点。
- 既有测试中依赖 `ICookingOrderPort` 的（Cooking 领域测试 11 个文件、ET 测试 2 个文件、退役中的 UDP harness/tests）必须迁移；退役 UDP 项目不在构建内，只保持源码可读，不为它们改产品 API。
- canonical 增加订单簿与结算记录会改变快照哈希：仓库内没有硬编码快照哈希金样（只有 run-to-run 比较与 3 个 ET 命令指纹金样），但 ET 侧若有依赖 canonical 文本的回归需同步检查。
- 所有验收必须有 `artifacts/` 下的 JSONL evidence 与 `check.jsonl` 记录的真实命令；独立复验不调用被测 C# 代码。
