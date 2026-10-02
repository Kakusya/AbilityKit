> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S06 初始设计

状态：执行设计收敛，等待 S01/S05 接口合入；不是已交付。

## 本次审议：前厅状态与工作

现有前厅扩展 Arriving→Queued→WalkingToTable→WaitingForInquiry→Ordered→Dining→Leaving→Departed；桌位为 Free/Reserved/Occupied/Dirty，顾客身份与桌号分开。有界 FIFO 排队，截止后不再生成客人，已到店者按既有等待规则排空；不满足订单仍自然结束。

询问、清桌、洗碗为前厅工作，记录稳定ID/目标/elapsed/required与 Available/Working/Paused/Completed/Cancelled，执行者为固定伙伴或 PlayerId。ClaimFrontWork/ContinueFrontWork/StopFrontWork 经同一 ET ingress、scope、幂等和几何验证；一工作一执行者，一玩家不能同时操作厨房手工和前厅工作。玩家停止/离开保留进度释放认领，伙伴只按既有询问优先/无询问洗碗次序取未认领工作，不增加优先级微操。

人工完成不增长伙伴经验；伙伴实际完成才按既有规则增长一次。顾客离开取消未完成询问，不开幽灵订单。清桌释放桌位，脏餐具进入定义的回收路径，不与洗原料混同。自然成功检查队列、行走客人、桌位和人工任务收口，不强制销毁后台半成品或所有脏碗。整局 Pause 不推进任何时钟。

路径读取 S01 的已配置初始几何/目标，不等待可变装修系统；S08 后来提供修改后的同一几何，因此不增加 S06→S08 依赖环。新增状态进入前厅snapshot/canonical/checkpoint，同Level恢复保留，失败丢现场，成功清营业与伙伴成长。具体证据和回归风险见 research/operation-review.md。

复用询问/队列/用餐/离席与洗碗，补人工接手及通路；停止接单与结束分离；未满足不业务失败。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Scope / Trigger

扩展现有 CookingFrontOfHouse，不另建顾客/订单。复用桌位、稳定顾客身份、固定伙伴询问优先/洗碗次之和 Level-local 成长；正常营业自然结束保持。前厅人工接手不是对伙伴做岗位分派或优先级微操。

## Signatures / Contracts

前厅人工命令通过 ET host 固定 Tick command ingress，携带 PlayerId、CustomerId/ItemId、操作、scope、命令ID/序号。Inquiry/Wash work 声明唯一执行者与 elapsed/required ticks；玩家认领尚未被占用工作，离开范围/松手保留进度，伙伴也不能重复认领该项。伙伴完成数只统计伙伴实际完成，不把玩家任务算成长。洗碗对具体脏容器实例而非每帧加计数。

顾客运行态补入口等待/队列/到桌/用餐/离席/待清桌，保存身份、桌引用、路径/进度；通路复用 S01/S08 空间，不能用模型动画结束推进领域时间。订单身份仍跟随顾客而非桌位。提交按明确服务方式（出餐口或桌）校验，放普通台面不等于交付。停止来客/接单与处理已有营业、收尾分别记录。

## Validation & Error Matrix

已离席/错误顾客桌引用→拒绝；被伙伴/他人占用工作→WorkOccupied；同玩家同时工作→PlayerAlreadyWorking；不在交互范围/非法杯碗→拒绝；重复询问→不重开订单；重复洗碗→不增加净容器；未满足不新增业务失败。关闭收尾只在已服务完/离席/必要清理完成时自然成功。

## Good / Base / Bad

Good：伙伴问一桌，玩家问另一桌，菜品可由任意玩家加工/核单送出；有人离岗，成果仍可识别接手。Base：无人工动作的旧固定伙伴场景结果保留。Bad：不可因为 manual wash 再完成一次已被伙伴洗掉的碗，或新顾客复用旧桌号串旧订单。

## Tests Required

ET命令与伙伴同帧冲突只有一个owner、未满足0分且0星完成、营业结束仍处理待交付/用餐/清桌、pause不推进。手工接续、顾客路径不可穿阻挡、重复询问/洗碗、厨具回收。前厅 snapshot/canonical/hash/checkpoint覆盖人工工作/顾客路径/队列；恢复与基线相等，success/reset和failed重开保持既有语义。

## Wrong vs Correct

Wrong：多个问单入口直接开同一顾客的订单；伙伴与玩家各有脏碗池。Correct：现有前厅 owner 唯一顾客和任务状态，复用现有厨房容器实例池，命令在同一固定 Tick 的明确顺序裁决。

## 2026-10-02：受限前厅增量接口（正在实施，尚非 S06 出口）

- `ConfigureFlow(CookingFrontOfHouseFlow)` 仅营业前；不可变路径输入携带同一 `CookingSpatialConfiguration` 和 `SpatialIdentity`，每格中心、每段连线按角色半径检查核心障碍，不接受无空间配置的路径输入。Flow 的入口→排队、排队→桌位、桌位/队列→出口明确且相邻；有界 FIFO 队列。完整 ET 初始空间/布局适配仍由主 owner 接入。
- 桌位 Free/Reserved/Occupied/Dirty 进入 snapshot；客人 Arriving/Queued/WalkingToTable/WaitingForInquiry/InquiryInProgress/Ordered/Dining/Leaving。只有完成真实清桌作业才释放 Dirty；洗碗仍引用唯一厨房容器实例，不新增脏碗池。
- `ConfigureManualWork(policyIdentity, Func<PlayerId,string,bool>)` 安装只读预检：玩家存在、厨房工作互斥、目标距离/朝向；不得在回调中改写模拟。`ClaimFrontWork(kitchen,player,workId)`、`ContinueFrontWork`、`StopFrontWork` 不推进时间；既有 Step 唯一推进进度。
- 工作 snapshot 包含稳定 ID/Kind/Target/Customer/Bowl/ElapsedTicks/RequiredTicks/Status/Player/Companion。人工和伙伴唯一互斥；停手或离开释放人工认领、保留进度；人工完成不增加伙伴成长，伙伴接手实际完成后增加一次。ET scope/去重/指纹与跨厨房工作校验由主 owner 统一接入。
- `FinishInProgress` 只在无 Flow、无人工策略的历史兼容路径保留原行为；新路径/人工不能通过该钩子跳过过程或幽灵开单。恢复包含路径配置、进度索引、桌位、工作与策略身份；手工恢复后必须重绑同身份只读谓词才能 Tick。非法候选不替换旧前厅。
- 当前聚焦检查：`dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingFrontOfHouse --nologo --verbosity quiet` 实际 exit 0，28 passed / 0 failed / 0 skipped；日志 `local/Logs/cooking-execution/front-house-component.log`。覆盖旧 21 项、新 7 项域增量；尚未覆盖完整 ET 适配和统一 Level checkpoint，不能标整个 S06 完成。后续修正后需要复跑。

### 最后域层自查与实际证据

- 新到桌的询问/新 Dirty 的清桌至少保留到下一 Tick 的人工认领窗口，伙伴不能在创建工作的同 Tick 抢走；旧无空间规则保持原节奏。
- 人工洗完立即删除真实洗碗队列项，恢复不会保留不存在的脏碗请求。洗碗工作按 `Cycle` 记录轮次；首轮 `wash:<bowl>`、以后 `wash:<bowl>:<cycle>`，旧 WorkId 不能作用于同碗的新脏轮。
- 已推进的洗碗任务保持原 required ticks；途中伙伴解锁加速只作用于新工作，换人不重置也不凭空加速。清桌不计旧询问/洗碗成长。新的成功 Reset 必须 `CanSucceed`，不能绕过队列、路径、Dirty 或人工任务。
- malformed flow、null work/table/space、缺桌字段、重号/多人占工、路径进度不一致、伪造工作编号均被恢复预检拒绝；旧对象 canonical 保持不变。恢复的人工回调按同策略身份重新绑定。
- 最后聚焦命令实际 exit 0，**36 passed / 0 failed / 0 skipped**（旧21 + 新15）；同一日志覆盖最终源码：`local/Logs/cooking-execution/front-house-component.log`。`git diff --check` 实际 exit 0，仅文档 CRLF 正常化提示。未 suppress 新警告；当前前厅源没有新的 nullable warning。
- 本受限增量尚未接入统一 ET ingress/指纹/跨厨房唯一 worker、Level 版本与网络 codec；路径配置与初始/新布局的自动适配由主 owner 完成。不同 logical 玩家争抢域测试不宣称真实联机。未提交、未合并、未改 S06 task metadata，不能据此标完整 S06 已通过。

## S06 实际 ET 接线设计（执行中）

追加 `ClaimFrontWork` / `ContinueFrontWork` / `StopFrontWork` 到 CookingRecipeOperation 末尾，保持既有 numeric ID。三者唯一动作参数为 `WorldAnchor=workId`（该字段在这三类操作里是稳定前厅工作编号，并不是空间 anchor）；其余 item/process/recipe/container/station/order/version/tick/move 参数必须为空/零。既有 WorldAnchor canonical 指纹已包含该值；执行时根据前厅当前 Work.Target 映射到受信空间锚点，不能把 workId 当场景位置。

仍通过 CookingLevelEtHost 同一 ingress/freeze/group/order/dedup 与 simulation.Submit 缓存结果；外部前厅委托只有在既有 authority frame 中被调用，不另建队列或时钟。新增已接受前厅指令同时生成厨房 command event 与版本，重复或冲突复用既有 ledger；纯前厅工作推进发生在同一 ET fixed Tick 之后。两个 owner 的工作排他均从当前状态只读判断，拒绝不能先释放另一份工作。

新增可选 `ICookingFrontOfHouseGameplayFactory` 由既有 factory 提供受信前厅配置（Schedule/Menu/Flow/manual policy/wash anchor），不是新的世界工厂。首次 Start 自动构造前厅并绑定当前 simulation 的只读 reach/worker predicate。Level checkpoint 格式6新增必需 FrontOfHouseConfigurationIdentity 字段（legacy可null），恢复比较受信factory身份及每项配置，不相信checkpoint自报hash。Flow还必须匹配同一配置和厨房实际 SpatialIdentity；恢复后重新绑定新simulation回调并检查不存在厨房/前厅双worker。

无可选配置的原factory保留历史自动前厅路径；具有 Flow 或手工策略的新checkpoint必须有可选factory受信配置才能恢复。旧格式5明确拒绝，Recipe checkpoint格式4不无故升级。恢复前厅后再Adopt宿主水位，任何阶段失败释放候选host；既有Running-only checkpoint导出语义保持。Pause阻止命令入队且不推进厨房/顾客/工作时间。新的正常Success开始/后续成功交接不得绕过前厅CanSucceed。

## F08 可信服务目的地实施合同（2026-10-02）

在既有 CookingFrontOfHouseConfiguration 追加可选 DeliveryPolicy，模式 ServingAnchor/CustomerTable。null 为历史兼容路径。ServingAnchor 必须提供非空唯一空间anchor ID；CustomerTable 不接受额外anchor，目标由当前前厅 Order→Ordered顾客→TableId 导出。Freeze 校验字段互斥与enum，Identity/canonical包含完整策略；绑定厨房时必须有同一权威空间及唯一目标anchor，缺失或跨Kind同名歧义拒绝。

host为当前 kitchen 绑定只读 Func<PlayerId,OrderId,CookingRecipeRejectionReason>，在SubmitOrder所有现有纯前置验证后、首个结算/容器修改前调用。前厅无该开放订单的Ordered顾客（已离席、用餐或未点单）返回OrderRejected；当前姿态对可信anchor距离/朝向不符返回TargetOutOfRange。订单参数足够，不增加command目的地字段，不信调用者桌号。不改变容器所有权、绑定要求、洗碗和结算；拒绝无业务状态变化，保留既有去重回执。

恢复/跨关BindFrontOfHouse重新绑定当前实例，可信factory配置身份涵盖策略。格式6仍未发布，复用其已required FrontOfHouseConfigurationIdentity验证，不默默放行不匹配。无前厅policy的旧S05与legacy通用fixtures保持原提交规则。仅补服务目的地，不强制贴票工位、不加正常营业失败、不声明S14或Unity完成。

新增专属ETtests走真实 ingress+固定Tick，使用从raw经工序得到的成品：远端/错误桌拒绝，正确桌/出餐口交付，旧订单离席拒绝、同identity拒绝重放不变、同Level restore重绑/配置身份拒绝，以及legacynull策略控制。实际结果另记。
