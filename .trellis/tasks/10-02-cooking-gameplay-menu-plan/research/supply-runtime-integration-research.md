# S07 供应接入精准研究

日期：2026-10-02。只读基线：integration `901f465b57230063466d65036e69df974aaa382f`（检查时 clean）。读取 S07 prd/design/implement 与总任务 research/operation-review.md；没有改源代码，没有运行 .NET。S06 host/lifecycle/checkpoint/manual 正由其他 worker 独占，下面是接线方案，不是实现或验证声明。

## 唯一权威与现有可用字段

`CookingSupply.cs` 的 CookingSupplyState 已具备 Request、AdvanceFixedTick、PreviewReceive/CommitReceive、PreviewInfiniteTake/CommitInfiniteTake、StopNewRequests、ExportCheckpoint/TryRestore。计划令牌绑定具体 state 实例与 delivery，不能跨 clone 使用。检查点已有供应配置身份、外部余额、DeliveryId/sequence/request/supplier/phase/remainingTicks、请求回执、nextDelivery/nextInfinite、Closing；没有厨房 item 关联。

厨房唯一 authority 仍是 CookingRecipeSimulation。`CookingRecipeLoop.cs:390` ICookingProductIdAllocator / `:413` SequentialCookingProductIdAllocator 与 `_nextProductId` 是已有统一实例 ID 水位，应复用，不再建厨房或原料 allocator。名称 product 不意味着供应原料必须 IsProduct=true；新原料保持 IsProduct=false、Recipe=null。现有 `AddItemCore` 维护手持、容器与位置，但它逐个写入 live 字典，不能用连续 AddItem 充当原子收货。

`CookingRecipeCheckpoint.cs:76` 厨房 checkpoint 已有完整 items/location/version/removed、容器索引、hands、processes、poses、各水位与命令去重；schema 4。没有 Supply。ContentDocument 的 StandardInitialSupply 是初始化物件列表，不是采购 ledger；不要混用。Supply configuration 应经内容/config/fixture 冻结及 canonical identity，验证单位定义存在、包装容量与 AcceptedDefinitions 真正兼容、source/receiving anchor 存在且单槽可信。

## 可实现的收货事务

新增 simulation 内部供应字段与命令处理分支，接收命令必须先走 scope/player/lifecycle、实际 receiving anchor 距离/朝向及单槽预检。在 candidate SupplyState 上 PreviewReceive；先完整创建 replacementItems/replacementContainerItems，复用 allocator 在局部 nextProductId 上分配 1 个 package 和 N 个 raw ID。所有 ID 非空、相互唯一、未与包含 tombstone 的 registry 冲突；检查序列/version 溢出和包装能力，包装放 receiving world anchor，raw 使用各自唯一 ContainerSlot。手持不变。

candidate 内 CommitReceive(plan) 成功之后，才在无后续可能失败的安装段同时替换物件索引、SupplyState、水位和事件/版本。任一 allocator 或验证故障不得消耗 live ledger、水位或槽位。可借鉴 `BuildFixedTickPlan`→`CommitFixedTick`（RecipeLoop:1040/1347）的 replacement-state 模式。不能在 live Supply 上先 commit 再逐个 AddItem，也不能在创建包装后才发现原料 ID 冲突。

收到的物件和 delivery 必须持久关联；现有组件没有 PhysicalPackageId/UnitIds。建议 authority checkpoint 增加 delivery→包装及初始原料 ID 的来源记录（随正常 tombstone 留存），并把关联 ID 放入收货回执/result，支持不同命令 ID 重试已接收 delivery 返回原收货结果。全局命令 dedup 仅覆盖同一 command 身份，不替代 delivery 的重复收货幂等。恢复检查关联唯一、定义/原始量一致，允许原料随后迁移、加工或丢弃；不要要求 Received 的 raw 永远还留在包装内，也不要将该来源记录变成另一份可扣库存。

## 有限取料与无限取料的不同原子边界

S07 design 和 operation-review 明确有限材料在 Receive 时成为实际包装内的 N 个实例。之后使用既有 Pickup/Drop/TakeOut：从 package 的索引 detach 一个现有 ItemId，更新位置/version 和空手。厨房库存由实际未移除物件导出。Request 已扣外部余额，TakeOut 不再扣外部余额、不再 spawn。不需要新增有限取料生成器；“扣供应并 spawn 一份”只适用于初始有限供应若明确采用尚未实体化 source 的另一种表示，本轮不新增该表示。

无限 source 才需要 TakeSupply：source 可达且玩家空手，candidate PreviewInfiniteTake、allocator 分配一个 raw、candidate CommitInfiniteTake，最后同时安装 receipt、raw、hand、水位/事件。相同请求重试不得新分配；payload 冲突必须零变更。StopNewRequests 只关闭采购，既有订单允许无限 source 取料；生命周期权限仍由 simulation/host 决定。

## 固定 Tick、准备态与关闭

Supply 到货推进放入厨房 BuildFixedTickPlan 的 candidate，并与现有 tick 一次安装；不要在 ET 调用 simulation.AdvanceFixedTick 后单独推进 live Supply，避免厨房 tick 成功但供应更新抛错。暂停自然不 tick；不能新增 timer。

`CookingLevelLifecycle.cs:475..537` 首次 simulation 目前在 Ready→Start 才 factory.Create；`CookingLevelEtHost.cs:457..475` admission 只允许 Running。所以仅在 RecipeOperation 加采购不足以满足准备态采购。与 S06 协作：Preparing 建立并保存同一候选 kitchen，Start 发布同一实例（不能再 factory.Create 第二套）；准备命令进入同一权威与去重路径，使用显式每操作允许阶段，不整体开放所有 gameplay。到货只有已定义固定步驱动；不要私自给 Preparing 加墙钟。此处需由 S06 owner 在既有 preparation/host 接口落地，不是供应 worker 修改其独占段的许可。

营业停止接新单的 authority transition 同时调用 StopNewRequests；已批准 pending 仍可推进，不强迫 Receive 才能正常完成。失败/结束后不开放供应修改。

## 恢复与成功交接

Same-Level checkpoint 必须包含 Supply checkpoint 与来源关联；TryRestore Supply 到 candidate，再与 kitchen 完整验证，一次安装。保留 Closing、remainingTicks、请求回执、所有 ID 水位。配置身份不匹配、received 来源缺失/重复、pending 被伪造成 physical stock 均拒绝且不改 live。明确 required 新字段/schema，不用缺省空供应静默恢复旧 JSON。

`CookingRecipeCheckpoint.cs:333` ExportSuccessHandoff 保留物件与实例水位、清本关 orders/bindings/dedup/tick、清 worker；供应 pending/arrived/received 与外部余额应原样保留，不能跟 LogicalTick 清零。AcceptSuccessHandoff 在确认真正成功交接形状后重新开放采购；普通 same-Level Restore 不可重置 Closing。当前 Supply 仅有 StopNewRequests，因此需要受限成功交接 reopen hook，或构造 Closing=false 的 handoff candidate（只在成功导出/接入通路允许）。

`CookingLevelLifecycle.cs:594` CreateRetry 生成新 epoch：失败新采购不进入成功 store。沿既有标准初始化/最近成功 kitchen baseline恢复其对应供应状态；初始配置也必须提供标准 supply baseline。成功 durable store 当前经 host:659 保存 ExportSuccessHandoff，供应应随 kitchen payload 保存，不另建旁路文件。与 S06 owner 协调 Level checkpoint/config identity/store 格式升级与 JSON required 字段，保证 journal 回滚同时覆盖物件与采购。

## 精确接线清单与验收

1. RecipeLoop：fixture 供应配置、private state；RequestSupply/ReceiveSupply/TakeSupply enum + 显式 SupplierId/DeliveryId/request payload（每请求仍一包）；Execute 分支、局部事务、结果关联；Snapshot 公开只读供应状态；FixedTickPlan candidate supply。
2. RecipeCommandValidation：新操作的必需/禁止字段与合法身份；Recipe 内命令指纹同步。ET `CookingLevelEtHost.cs:57..92` CanonicalBytes 目前显式逐字段写入，新增 payload 必须加入，否则不同供应请求会被误去重。命令 checkpoint/store/replay 也需包含字段，不复用 Station/Recipe 承载 supplier 语义。
3. ContentCatalog/ConfigurationValidation：供应配置和真实包装兼容性、锚点验证、冻结、canonical/config identity。RecipeCheckpoint/ExtendedValidation：Supply + origin associations、版本化、完整 candidate restore。
4. S06 lifecycle/host/store：Preparing 单一实例和允许采购入口、fixed step、关闭采购、success reopen、retry baseline、暂停/故障/持久恢复。文件归属需协调，勿并发编辑。
5. 必须真实测试：ET Request→Tick→Arrived→Receive→搬 package→TakeOut→加工→交付；槽满/超距/空手/同 delivery 双玩家；ID allocator 第 N 次失败与冲突零变更；重复 command 与不同 command 同 delivery；pause；same-Level Closing/在途剩余步恢复；成功跨关采购 reopen/stock+pending 保留；失败新采购不保留；缺 schema/身份/别名关联拒绝。已有组件通过不能替代此链路。
