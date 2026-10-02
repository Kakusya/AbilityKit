# Research: 单机经营、供应、布局与观察闭环审议

- Query: S06/S07/S08/S14 如何沿既有 authority 实现完整经营而不改架构？
- Scope: internal；主工作区与保留的 cooking-integration-s06-s14 工作区只读比较。
- Date: 2026-10-02

## Findings

### 来源、文件与当前事实

- `src/AbilityKit.Game.Cooking/CookingFrontOfHouse.cs:83`：自然成功为停止接客、无人入座、伙伴空闲；未满足订单不是业务失败。`:157` 固定顺序推进顾客与伙伴；`:367` 当前客人直接入座，满桌时不产生排队客人；`:306` 离席直接清空桌位，没有路径和清桌阶段。
- 同文件 `:191` 成功下一关保留厨房、清营业态与伙伴关内成长；`:175` 失败丢前厅，不给新厨房开单/洗旧碗；`:212` 成功强制收正在执行的询问/洗碗，这是现有行为，新增人工工作不能无意改掉。
- 同文件 `:246`/`:264` 完成询问/洗碗直接计伙伴成长。人工接手复用这两个方法会错误增加伙伴经验，必须显式区分执行者。
- `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs:452` 现有幂等入队；`:1017` 厨房固定步后推进前厅。不能用 UI、供应计时器、异步回调另外推进这些状态。
- 同文件 `:611` Created 装修迁移；失败仅 previous.Count>0 才回滚选择，空旧选择存在回滚缺口。`:629` 先 PlaceUnlock 再 progress.Unlock，若 progress 已锁，存在厨房先变化而权限拒绝的风险。新布局/供应应完整预检后提交，不能照抄此顺序。
- `src/AbilityKit.Game.Cooking/CookingLevelLifecycle.cs:23` 逻辑布局只有工位/容器 ID；`:62` 准备配置；`:68` 生命周期已有 Created/Preparing，不需要创造新准备 phase。
- `src/AbilityKit.Game.Cooking/CookingLevelCheckpoint.cs:32` canonical 只收布局 ID、工位和容器，不含几何；`:85` 主工作区格式3。新增几何、供应、人工工作必须版本化，不可只把新字段放 DTO。
- `CookingContentCatalog.cs:162` 只有标准初始化供应；`CookingRecipeLoop.cs:444` 清洗、`:655` 工位迁移、`:697` 解锁放置。检索未发现采购/到货/收货/有限原料库存 authority，CleanContainerSupply 是有限餐具池，不能冒充采购库存。
- 保留工作区 `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-integration-s06-s14/src/AbilityKit.Game.Cooking/CookingRestaurantLayout.cs:156` 检查出入口和 BFS，`:194` 旋转交互面，`:202` 寻路。它仅是静态辅助类，不证明 ET 安装、更改实际交互、checkpoint 或顾客路径功能。
- 读取的规范：`.trellis/spec/cooking/gameplay-menu-plan.md`、`cooking-match-lifecycle.md`、`cooking-persistence-management.md`；设计：`Docs/design/CookingGame/progress.md`、`reference/README.md`、`reference/product-lifetimes.md`；ADR：`ADR/long-term-goals.md`/`ADR/README.md`。原网络协议争议不由本审议处理。

### S06 建议合同：同一前厅 owner，人工与固定伙伴互斥

1. 扩展顾客状态为 Arriving → Queued → WalkingToTable → WaitingForInquiry → Ordered → Dining → Leaving → Departed；桌位单独 Free/Reserved/Occupied/Dirty。客人 ID 不绑定桌号，预订与实际入座分开；容量满保持有界 FIFO 等候，不无限生成实体。到营业截止停止生成，已进入者按既有等待语义自然排空。
2. 询问与洗碗成为前厅内统一工作记录：工作ID、种类、稳定目标ID、Available/Working/Paused/Completed/Cancelled、执行者（伙伴或 PlayerId）、elapsed/required。记录仅引用厨房餐具实例，不复制容器。
3. 命令建议 ClaimFrontWork/ContinueFrontWork/StopFrontWork；与厨房共享 ET scoped ingress、稳定批次排序、幂等键和指纹。合法性含 Running、玩家存在、目标有效、S01距离与朝向、未被占用。每任务一个执行者，同 tick 争抢仅一人成功；伙伴只取未认领工作，不新增优先级微操。
4. 人工停手/离开保留进度并释放认领，伙伴随后可接手；整局 Paused 保留认领且所有时钟不动。人工完成不增长伙伴经验，伙伴接手仅在实际完成时按原规则增长一次。任务是否已满足用厨房真实订单/脏碗状态再校验。
5. 离开或等待终止取消该客人询问，不能随后补开幽灵订单；洗碗失败重新可领取且给理由。清桌作为场景经营任务清理桌占用，不等同洗碗；脏碗必须落入合法回收位置或已定义餐具池，不能直接消失。
6. 自然成功额外检查队列、路径中客人、人工工作及必须清理的桌位已排空，保留0星成功和现有不满足订单记录。剩余半成品与正常厨房脏餐具不因此强制销毁；不要要求所有后台厨房永远为空。

### S07 建议合同：有限供应和物件守恒

1. 内容定义 Supplier/SourceAnchor/ReceivingAnchor/UnitDefinition/PackageDefinition/UnitsPerPackage/DeliveryTicks/FiniteAvailable 或显式 Infinite；采购价格留出不实现经济。采购和有限取料不是标准初始化物件的别名。
2. RequestSupply(quantity) 原子预留外部有限供应并分配 DeliveryId；FixedTick 变 Pending → Arrived；ReceiveSupply 仅抵达且收货槽空时创建一个实际包装容器和 N 个材料实例，改为 Received。空间不足保留 Arrived，不吞货。重复请求/接收使用原结果且不新分配实例。
3. 有限原料的厨房库存从实际实例/包内子项推导，不同时维护独立可消费计数。包装搬运使用现有拿放；单份拆取用 TakeOut，不让所有包装成为无限背包。无限源 TakeSupply 仅生成一份进入空手，并显式标为无限。
4. 守恒账区分外部可供、预留在途、已接收、厨房当前、加工消耗、已交付、丢弃。份数转换按配方 yield，不要求物理现实质量；供应单位与加工产出份数不能共用含糊字段。清空不退款、不回库。
5. pending/arrived/received ID、剩余等待、外部余额、nextDelivery 和厨房 nextItem 必须 checkpoint；暂停停止到货，失败重新从既有成功基线/标准初始供应恢复，不保留失败场景新采购；成功保留厨房成果以及与其一致的供应状态。
6. 必须提前确定收尾的在途规则：建议营业截止禁止新增采购，已批准货照常到货；未接收的到货可跨小关保留，不强制玩家搬尽所有采购才能自然成功。取消采购若保留此能力须仅 Pending、原子退回外部预留一次；不引入违约惩罚。

### S08 建议合同：准备态整体安装，而非孤立验证器

1. 沿既有 preparation owner 在 Created/Preparing 允许更换完整布局，Ready/Running/Paused/Ending/Ended 一律拒绝。已解锁 ∩ 本关允许判断设备权限；基础必需内容须明确默认授权集，不能让空 unlock 把初始工位全部禁用。
2. 布局记录地板区域、墙、设备占地及旋转、交互面、入口/队列/桌位/出餐/收货/仓储/清洗目标。设备占地由定义权威提供，不能接受调用方随意缩小 Width/Height 欺骗碰撞；扩建是地板集合，无购买成本或永久解锁暗示。
3. 安装前同一候选上校验几何、半径净空、连通、权限、容器/工位迁移、玩家位置保留或确定性重定位、当前工作引用。任何失败 layout/majorprogress/kitchen/pose/allocator/hash 均不变。成功同时更新厨房障碍、目标锚点与有效几何 identity，后续交互距离实际改变。
4. BFS 只能证明格中心可达；必须与 S01 定点单位、角色半径、旋转后交互面和碰撞规则一致。顾客使用相同地形图，稳定邻居顺序和每 tick 速度；暂不把动态角色阻挡做成随机重寻路。有效地图须有实际入口到桌再到出口，工作入口可达所有必需工位；不必强迫玩家进入仅顾客入口。
5. checkpoint 包含有效几何及 hash，不仅布局ID；恢复先校验并安装布局，再验证物件/玩家/前厅引用，任何阶段失败不能替换当前 host。成功跨关保留可继承布局，但新关不允许的设备处理必须显式拒绝/换布局，不静默删除带内容容器。

### S14 建议合同：许可闭合与可观察数据是实际端到端验收

1. 本关可供应菜单在初始和布局变更后计算闭包：每条工序的供应材料、状态/份数、容器类型、设备能力和可达工位全部存在。共享半成品可并行，不只检查最终 RecipeId 已注册；缺任意依赖拒绝 Ready，并报告菜品ID、缺失节点和原因。
2. 投影只读当前 authority snapshot：玩家身份/手持物、原料状态、容器成分/剩余份数、设备进度、订单菜品/编号/绑定、待出餐、工作认领、库存/在途、营业/收尾。稳定 definition/state 图标键让 Unity 后续复用；不创建 UI 第二账本。
3. 练习模式用现有规则配置做有限可重放场景，不新增奖励或定价系统。保留固定伙伴和现有 level-local 成长，仅覆盖新任务不会错误增长/跨关残留。
4. dependency 注册表 S14 当前依赖 S06/S08/S13，但真实闭环还必须依赖 S07（供应）和 S05（订单绑定），并覆盖 S09–S13 的全部内容批次，不可仅验最后茶饮批次。

### 真正验收矩阵（计划，尚未执行）

| 范围 | 必须由真实行为证明 |
|---|---|
| S06 | 玩家和伙伴同tick抢工作；人工半途停手由伙伴接续；人工完成不加伙伴成长；客人离开取消询问；满桌队列/行走/清桌后自然成功；不满足订单仍成功 |
| S07 | request→wait→arrival→receive→搬包→拆份→加工→交付；收货槽满不吞货；重放重复接收不复制；有限库存耗尽与营业补货；清空仅记损耗；暂停不推进在途 |
| S08 | 从 ET preparation 实际安装后，原工位位置交互失败而新位置成功；非方形旋转、角色半径堵路、顾客桌路断开；非法候选全状态hash不变；有内容锅迁移不丢料 |
| S14 | ET 命令→fixedTick→全部提示数据→营业收尾→成功下一关；许可交集及缺工具不能Ready；每新增状态 checkpoint 重建后的继续运行与不中断基线等价 |
| 跨系统 | 同场景供应/玩家/顾客/伙伴只共享一个逻辑时钟；完整payload改动参与指纹和canonical；旧scope命令拒绝；新版本缺字段/非法引用结构化拒绝；失败重开不泄漏采购或认领 |

## Caveats / Not Found

- 本报告没有执行产品测试，未修改代码、规范或其他task。静态layout测试不等于 S08功能通过；未合并分支不作为 master已实现能力。
- 准备态范围当前代码只支持 Created，新规划希望 Created/Preparing，必须显式修约，不能以“已有准备态”掩盖差异。
- 跨关供应在途保存、顾客等待与清桌、人工任务成功收口行为是建议补足，应由主协调会话纳入最终审议设计；不以研究报告替代已批准契约。
- 需协调单一checkpoint版本 owner，避免核心格式4与布局/供应新增字段各自抢编号；整套完整payload一并版本化、旧格式明确拒绝或显式迁移。
- 最新 owner 的继续审计后实施指令与旧 planning-only 文案有冲突；本研究只记录来源，授权路由由主会话统一更新，本角色不擅自进入实施。
- External references: 无新增外部资料；此审议基于仓库实际代码及项目规范，不声称原作内部实现。
