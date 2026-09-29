# Cooking 单机多订单菜单纵切

> 状态：`in_progress`。Owner 已于 2026-09-29 批准按最终规划实施；实现边界仍限于纯 C#/ET 单机多订单菜单。

## Goal

将现有烤面包提升为第二种可直接点单的顾客订单，使单机前厅不再只生成番茄蛋花汤，并在纯 C#/ET fixed-tick 路径中验证菜单选择、制作、提交、评分和同 Level checkpoint 恢复。

用户价值：同一小关内出现两种制作路线，玩家需要在灶台汤品与烤箱面包之间分配时间和供应，而不是重复处理唯一菜品。

## Confirmed Product Decisions

- Owner 于 2026-09-29 批准把现有 `bake-bread` / `toasted-bread` 从配方与半成品验证内容提升为可直接点单的第二种顾客订单。
- Owner 于 2026-09-29 批准新增盘子作为烤面包的正式上菜容器；烤面包装盘后提交，盘子提交后变脏并由固定伙伴走既有清洗流程归还干净池。
- Owner 于 2026-09-29 批准第一版按顾客 Level-local 到店序号固定轮换订单：`customer-1` 点番茄蛋花汤、`customer-2` 点烤面包，之后循环。**该规则只是单机多订单纵切的临时确定性方案**，用于保证两种菜品稳定进入验收；不代表长期顾客偏好、菜单权重或正式订单生成设计。
- Owner 于 2026-09-29 确认本任务所有数值均为临时方案。第一版采用：番茄蛋花汤 100 分、烤面包 50 分、星级阈值继续使用 100/200/300、标准供应 2 个干净盘；现有配方 Tick、供应数量和前厅时序也不因此升级为长期平衡承诺。
- 本任务只做单机纯 C# Cooking 领域与 ET host/checkpoint；不修改 LAN/session wire、UDP/KCP、Unity 或长期存档。
- 正常小关仍自然完成，不新增业务失败条件或失败界面。
- 第一版继续使用订单模板固定基础分；成功提交只计分一次，未满足订单 0 分且不倒扣。

## Repository Facts

- 正式内容目录已有 `bread-slice`、`toasted-bread`、`oven-a` 与 `bake-bread`，标准供应包含 2 片面包；当前只有 `tomato-egg-soup-order` 一个订单模板。
- 订单领域已经支持多个 `CookingOrderTemplateDefinition`，订单实例和 settlement 都保存实际模板、recipe 与固定基础分。
- 当前 `CookingFrontOfHouse.Step(...)`、`FinishInProgress(...)`、`ResetForNextLevel(...)` 和 checkpoint 只接收/保存一个 `ActiveOrderTemplate`；前厅尚无菜单选择状态。
- 当前订单模板必须声明 `RequiredContainerDefinition`。本任务增加 `plate` 容器定义和有限干净盘供应，并把烤面包订单绑定到盘子。
- 当前清洗实现的部分业务类型和公开方法仍使用 bowl 命名，但 fixture 已以 `WashableContainerDefinitions` 表达可洗定义集合。第一版优先复用现有通用集合与 `CompleteWash(ItemId)`，不为盘子复制第二套清洗状态机，也不为了术语整洁做大范围重命名；只有实际阻塞盘子接入的局部命名才在本任务内修正。
- ET host 只保存一个 `_frontOfHouseTemplate`，恢复时从前厅 checkpoint 取回该单一模板。
- 临时固定轮换可由顾客 `ArrivalOrder` 和有序菜单直接派生，不需要第二个可变菜单游标；checkpoint 已保存顾客序号水位，因此恢复后可以继续同一序列。

## Source Conflict Resolution

- `09-19` 较早决定把烤面包定位为配料/半成品，并明确“首个闭环内不直接对应订单”。
- Owner 于 2026-09-29 的最新决定明确把现有烤面包提升为第二种可直接点单的顾客订单，并批准新增盘子。
- 本任务按来源优先级采用最新 owner 决定；旧决定只保留为历史背景，不再约束本纵切。该覆盖不自动推导第三种菜品或长期菜单规则。

## Requirements

- 前厅必须从获批菜单中为每位完成询问的顾客确定一个订单模板，并把实际模板写入领域订单。
- 第一版菜单固定为有序的 `[tomato-egg-soup-order, toasted-bread-order]`，按顾客到店序号轮换；这是临时验收规则，不扩展随机、权重或顾客偏好。
- 临时菜单由当前 Level/ET host 显式提供并进入前厅 snapshot/checkpoint；不在本任务中创建长期正式 Level schema，也不把菜单列表硬编码到通用领域规则。
- 菜单选择必须可重放、可进入 snapshot/canonical/SHA-256，并在同 Level checkpoint 恢复后继续产生相同后续序列；不得为可派生轮换位置维护独立可变游标。
- 番茄蛋花汤与烤面包必须分别按各自 recipe、容器和基础分提交，错误菜品或错误容器保持 mutation-free 拒绝。
- `plate` 是具有容器能力的物品定义，容量 1，只接受 `toasted-bread`；标准供应中的干净盘数量有限。
- 第一版临时数值为 2 个干净盘；烤面包订单 `BaseScore = 50`，番茄蛋花汤保持 100，星级阈值保持 100/200/300。
- 烤面包订单提交成功后盘子变脏，进入与碗共享的伙伴清洗队列和成长计数；清洗成功后回到盘子自己的干净池计数。
- 多种订单的 settlement、总分、星级、超时未满足和自然完成语义复用现有合同。
- 供应必须足以在验收场景中完成至少一份汤和一份烤面包，不引入无限供应。
- ET host 必须复用领域菜单选择状态，不维护第二套模板游标。

## Acceptance Criteria

- [x] 正式内容加载后同时存在番茄蛋花汤和烤面包订单模板，配置 canonical/hash 对第二模板敏感。
- [x] 正式内容包含盘子定义和有限标准供应；盘子只接受烤面包，碗不再接受烤面包。
- [x] 配置读取出的临时数值为汤 100 分、面包 50 分、干净盘 2 个；canonical/hash 对这些配置变化敏感。
- [x] 单机前厅在确定输入下能生成两种模板的订单，顾客/订单身份保持现有 Level-local 单调规则。
- [x] 临时轮换严格为汤、烤面包、汤、烤面包；同一 checkpoint 恢复点之后的模板序列与不中断基线一致。
- [x] 玩家能完成并提交至少一份汤和一份烤面包；两笔 settlement 保存正确模板和 recipe，得分按各自配置累计。
- [x] 用另一菜品、错误容器或已完成订单重复提交时结构化拒绝且状态不变。
- [x] 烤面包提交后盘子进入可洗队列，伙伴清洗后只恢复盘子池；碗池与盘子池计数互不串用。
- [x] 前厅 snapshot/canonical/SHA-256 可观察每位顾客的实际订单模板及菜单选择水位。
- [x] 同 Level checkpoint 在下一订单生成前后往返恢复，恢复臂与不中断基线的订单模板序列、前厅 canonical、厨房 canonical、总分和星级一致。
- [x] 失败重开与下一 Level 按既有生命周期重置本关菜单选择水位；同 Level 恢复保留。
- [x] Cooking 与 ET runtime 权威门禁通过；无 LAN/session/Unity 修改。

## Out Of Scope

- LAN/session 协议、远端客户端投影和双端菜单共识。
- Unity 菜单、图标、顾客点单动画、寻路和表现。
- 第三种菜品、复杂菜单权重、顾客偏好、套餐、连击、速度倍率或经济系统。
- Profile/SaveSlot、跨 Level 菜单解锁、durable/process-crash 恢复。
- 新业务失败条件。

## Deferred Product Design

- 固定轮换是临时方案。正式版本应另立任务审议配置权重、顾客类型/偏好、菜单可用性、随机种子或剧本序列；不得把本任务的奇偶轮换固化为长期产品承诺。
- 本任务中的基础分、星级阈值、供应数量、配方 Tick 和前厅时序全部是临时可测试基线。长期数值平衡必须基于玩法测试另立任务，不得从本 task 的门禁通过推导“数值已定稿”。

## Planning Constraint

规划产物完成并通过最终审阅前不启动实现；即使产物完成，也必须等待 owner 对最终规划摘要作出新的明确实施批准。
