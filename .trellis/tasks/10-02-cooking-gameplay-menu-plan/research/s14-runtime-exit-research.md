# Research: S14 关卡许可和只读观察的单机完整出口

- Query: 复用现有快照/菜单依赖图，补足关卡许可及 ET 自然营业、跨关、重放恢复的精确出口。
- Scope: internal；主工作区静态审阅，不执行 .NET，不修改源代码。
- Date: 2026-10-02

## Findings

### 已核对文件与当前复用点

读取 S14 prd/design/implement、`research/completion-contract.md`，并承接 `research/operation-review.md`；下述源码事实来自本次主工作树读取，已发生的生产/恢复计数以完整出口表及对应检查记录为准，本报告不复跑或代证。

| 来源 | 可复用 readonly 数据 | 限制 |
|---|---|---|
| `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs:2189` | Scope/Version/LogicalTick、Items、Processes、Containers、Orders、Settlements、Poses、已有评分/关闭状态 | 不是完整 Level 快照；不含供应、前厅、有效布局/许可 |
| 同文件 `:2268` | 每物件 Definition/Recipe/Location/IsProduct/ContainerCompleted/Dirty/RemainingPortions/BoundOrder | 物件加工状态主要由 Definition 表达；不可再维护另一套“熟度” |
| 同文件 `:2272` | 进度 elapsed/required，原始 Player 与当前 ActiveWorker，anchor/station | ActiveWorker 才能表达当前手工认领，原始 Player 不能当当前负责人 |
| 同文件 `:2275`、`:2260` | 容器容量和内部ID；订单号、模板、菜品、所需容器、状态和完成Tick | 内容需按ID关联物件；订单绑定由 Items.BoundOrder 反向关联，不加第二绑定账本 |
| `CookingFrontOfHouse.cs:1312` | 顾客、伙伴、营业时钟/Closing、未满足订单、WashQueue、Flow、ManualPolicyIdentity、Work、Tables | 现有路径/人工工作 domain DTO 可复用；可信ET接线及恢复须等待 S06，不能只观察组件证明功能 |
| 同文件 `:1281`、`:1288` | 工作目标/执行者/状态/进度/周期；顾客phase、table、PathIndex、RouteTable | 顾客坐标按 Flow 稳定路径和 PathIndex 派生，不创建观察侧路径模拟 |
| `CookingSupply.cs:189` | ExportCheckpoint 的冻结 balances/deliveries/receipts及configuration identity | 是供应外部账，不是厨房库存。可做只读摘要输入；不要向产品提示暴露内部幂等 receipts/allocator |
| `CookingMenuCatalog.cs:288` | Requirements 递归 supplies/recipes/production+delivery capabilities/carriers/refill containers | 真实来源依赖闭包已有，数量仍要看 step.Inputs.Portions；集合不等同库存够用 |
| 同文件 `:322` | ValidateLevel 缺供应/能力/容器诊断 | caller 提供 availability；当前不查解锁交集、真实设备、通路或数量，Record统一Level丢失逐菜归属 |
| `CookingLevelEtHost.cs:378` | FrontOfHouseSnapshot | 当前无组合 readonly Level observation；TryPeekBoundKitchen `:384` 返回活的 simulation，非安全外部只读DTO接口 |
| 同文件 `:416`、`:556`、`:767` | Prepare、自然成功 gate、成功前厅重置 | Prepare立即完成准备；新许可验证需置于 CompletePreparation 前，不能Ready后再发现缺物 |
| `CookingLevelLifecycle.cs:23/62` | 现有logical layout/preparation/config identity | 有效几何安装等待S08；不自行新建Layout authority |

相关规范为 `.trellis/spec/cooking/cooking-recipe-loop.md`、`cooking-config-validation.md`、`cooking-match-lifecycle.md`；固定Tick/ET树/作用域沿既有架构。没有外部引用或新增库选型。

### 最小 API 提议

1. 在既有 Level preparation 添加显式不可变 `CookingLevelContentPolicy`：SelectedMenuIds、AllowedDefinitions、AllowedRecipeIds（若需要独立关卡工序限制）。有效定义 = 关卡允许 ∩（初始基础授权 ∪ majorProgress.Unlocks），基础初始集必须显式传入，不能空unlock禁掉现有关卡。policy本身纳入配置/准备identity、checkpoint canonical。
2. `CookingLevelContentValidator.Validate(catalog,policy,effectiveDefinitions,actualAvailability)` 返回 `{Accepted, Diagnostics}`。actualAvailability从已安装厨房/布局/供应定义读取，不接受 UI 自报“有煎台”。复用 Requirements + 每菜 ValidateLevel；诊断 Record=菜单ID，Relation=缺失definition/capability/recipe/station，补 `LockedDefinition`、`LevelDefinitionForbidden`、`MissingReachableStation`、`InvalidServingContainer`、`InvalidSupplier`。先识别未知menu返回结构化UnknownMenu，避免 Requirements 抛异常成为准备崩溃。
3. 有限库存的“余额不足做所有订单”不阻断Ready：本轮订单数量未确定且允许补货。只要求合法可恢复供应通路和单批结构兼容（投料数量、carrier容量、产出与盛装关系），不能把菜单全部原料求和作为首关采购量。实际材料耗尽输出缺料/补货反馈，不新增业务失败。
4. `CookingLevelEtHost.Observe(PlayerId?)` 返回冻结 `CookingLevelObservation`：LevelScope、State、HostFrameSequence、KitchenSnapshot、FrontSnapshot、SupplyView、LayoutIdentity、PolicyIdentity；可选玩家交互预览。必须在完整帧提交之后同一ET上下文捕获，Paused/Created也可读，dispose或无厨房时结构化Unavailable。不可用3个异步getter拼出不同帧的数据。
5. 观察是派生DTO，不参与业务写入、不新增第三snapshot owner；canonical比较使用所属authority已有canonical和新增policy/geometry字段。SupplyView至少supplierID/definition/infinite/externalAvailable、deliveryID/phase/remainingticks/units、physical库存摘要。库存从 Kitchen.Items（包括包装内部）推导，processed产品不算原料余额，removed/tombstone不入库存。
6. 提示若确需统一形状，可纯函数 `CookingObservationProjection.Build(observation,contentMetadata)` 产生 item/process/order/work/stock hints；稳定图标键优先已有definition/recipe/menu-source-ID，不嵌Unity资源路径。玩家颜色仅identity/稳定marker key；真实颜色、动画、音效和高亮视觉归Unity。动作成功/失败复用实际command disposition与结构化拒绝，不以Observe时预测覆盖执行结果。

### 权限与闭包的实际接入

- validator置于host Prepare的BeginPreparation之后、CompletePreparation之前，或更早候选预检，失败不能Ready且不能偷偷留部分新layout/厨房。Prepare之前布局设置和policy读取保持现有Created准备路径；不要把长期进度owner搬入catalog。
- 校验每道选定菜的完整production以及delivery能力，饮品还要BindingCapability，正餐甜品无贴票工序。输出储存容器也列入Requirements，消耗杯子须有refill合法来源。station capability仅从实际已安装并被本关允许且可到达的工位导出。
- 关卡改变时清理订单/绑定/前厅短态沿原success handoff；厨房保留内容若下一关禁用，仅“不能新采购/新加工/新开该菜单”与“已有物件可保留/清理”应分开。不能因permission过滤直接消失物件。具体下一关政策必须与S08迁移一致，非法继承布局在开始新关前拒绝而不删锅内容。
- 新policy/geometry/supply/front payload须协调一个checkpoint格式owner；不为只读hint单独发明存档。恢复时按可信配置验证policy、layout、供应与前厅引用，再原子安装，不能以payload自报available集通过。

### 可立即推进与必须等待的接口

可立即推进：菜单逐菜许可诊断与结构化 UnknownMenu、供应/容器/capability缺项测试；纯只读 hints 接收现有Recipe/Front DTO，关联容器/订单/进度和身份；定义aggregate observation的数据合同和冻结行为测试；复用现有ET fixture构造工序/入队driver。以上无需改S06/S07/S08 owner内部实现。

等待 S06：固定Tick frontend命令lane、人工任务跨厨房排他、可信manual policy恢复、路径生命周期、收尾CanSucceed真实gate。

等待 S07：simulation.Supply或等价公开只读owned-view、ET供应命令lane、包装实例生成与TakeOut、paused推进、供应checkpoint和跨关handoff。不靠外部CookingSupplyState对象单独计时造成功能。

等待 S08：真实effective layout安装API、geometry identity/authority可读、实际reachable station索引、supplier/customer目标锚点和恢复。静态BFS集合不能冒充当前simulation有效通路。

### ET 完整验收 fixture 组合（计划，未执行）

复用 `src/AbilityKit.ET.Runtime.Tests/CookingMenuProductionEtTests.cs` 已有87菜ET制作/恢复driver，`CookingLevelEtHostTests.cs` 入队/生命周期/失败重开fixture，以及 `CookingLevelCheckpointTests.cs` 销毁恢复方法。不要再做一份旁路simulation来伪造营业输出。

1. 一轮最小真实经营：选择F01田园沙拉和D31奶盖绿茶（覆盖多原料、共享准备、最后收尾、杯与plate和绑定），另配小份有限原料supplier与一个显式无限source；短营业/到货/询问/用餐/清桌Tick均标fixture值，非正式平衡。
2. Created安装含入口→队列→两桌→出口以及厨房/收货/贴票/清洗路线的实际layout；合法policy允许Ready，移除茶桶/供应/杯/贴票或锁住必要definition的候选分别拒绝且原hash不变。能力同名但工位不可达也必须拒绝。
3. 所有玩家动作经host.TryEnqueue→Tick，包括采购接收、拿包拆份、移动、切配/手工接续、设备加工、分装、饮品绑定与提交；伙伴询问/洗碗按现有规则工作，人工短暂接手并互斥。每关键帧Observe断言手持和材料状态、容器内容/份数、当前worker和进度、未绑定/绑定/完成订单、在途和实际库存、顾客路径/桌位/收尾。
4. 开单至少两张：完成一张，另一张自然未满足并离开；停接客、剩客离开/清桌、CanSucceed后调用现有自然成功入口，不手动BeginEnd冒充自然判定。保留0星也可成功，原评分只回归不定义新失败。
5. 在有Pending供应、已组装未绑定饮品、Paused人工进度、排队顾客的一个中间运行帧导出Level checkpoint；彻底Dispose旧host，可信same-config restore后按完全相同后续envelopes/Ticks运行。比较每步aggregate各owner canonical、disposition、最终厨房/前厅/供应、星级、事件/allocator水位；先测试运行态导出支持的边界，暂停代际导出不能从无支持入口硬承诺。
6. 成功创建下一小关检查：厨房物件和在途供应的既定继承完整，订单/绑定/短态/伙伴level成长按契约清，Created可读而gameplay关闭，新scope拒绝旧命令；新关禁止某菜单时保留已有成果但不能新开/新采购禁止材料。
7. 失败仅用既有技术Failed入口制造：记录先前成功基线后，在本关新采购/认领/加工，再CreateRetry；验证失败现场不泄漏、major选择与原cook-faster保留、前厅和伙伴成长重置、旧命令拒绝。不要通过未满足订单触发Failed。
8. 87菜全覆盖采用现有生产fixture逐条增加policy/observation/最后交付断言，其中31饮品等待binding worker接口。完整自然经营可由上述代表组合承载，不强迫一轮87菜全部开单；两组测试共同证明内容范围和经营出口。

## Caveats / Not Found

- 未运行 .NET 或Git操作，未改源；本文件不是新gate通过证据。
- 本次实际代码中 readonly aggregate observation、有效许可交集准备入口尚未找到。现有catalog ValidateLevel不是完整真实准备门。
- S14 PRD依赖仍只写S06/S08/S13；design已补S05/S07及S09–S13。主会话应同步metadata/PRD/manifests，而非把较窄旧依赖当最终scope。
- S06/S07接线并行可能随时新增公共入口，本报告以本次读取为准；实现前重新核对新commit接口，保持跨owner源文件独占，不覆盖他人。
- completion-contract 中“S01主分支未实现”与已合并核心增量描述存在文字口径冲突，应按实际scoped证据修正，不在本研究重审稳定仲裁。
