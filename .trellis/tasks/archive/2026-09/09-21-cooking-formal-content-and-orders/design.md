# Design：Cooking 正式配方与订单内容

> 状态：planning。本设计只落实 owner 已确认决定（09-19 notes §10–§12）与任务①②的已落地契约；凡属推定项均在 §9 显式列出，不得表述为 owner 已确认。

## 1. 正式内容目录：数据模型与加载

### 1.1 内容文档

- 位置：`src/AbilityKit.Game.Cooking/Content/cooking-content-v2.json`，经 csproj `Content`/`CopyToOutputDirectory` 随程序集输出，加载器从输出目录或传入流读取。文档是**唯一**的正式内容来源：物品、工位、配方、订单模板、标准初始供应。
- schema 标识：文档内 `"schema": "cooking-definition-v2"`，与 `CookingConfigurationIdentity.CurrentSchema` 一致；身份 hash 由 v2 canonical 覆盖全文（见 §2）。
- 文档形状（JSON，camelCase）：

```text
{
  "schema": "cooking-definition-v2",
  "supportedApplianceCapabilities": ["cut", "heat", "bake", "beat"],
  "items": [ { "id", "allowedPlayerCapabilities": [...], "container": { "capacity", "acceptedDefinitions": [...] }? } ],
  "appliances": [ { "station", "capabilities": [...], "isAvailable" } ],
  "recipes": [ { "id", "inputs": [...], "defaultInputs"?, "productDefinition", "process",
                 "requiredApplianceCapability", "requiredTicks", "completion", "requiresStation" } ],
  "orderTemplates": [ { "id", "requiredRecipe", "requiredContainerDefinition" } ],
  "standardInitialSupply": [ { "definition", "count", "location": "world:pantry" | "station:stove-a" | "cleanPool" } ]
}
```

- `standardInitialSupply` 的 location 语法：`world:<position>`（世界位置）、`station:<stationId>`（工位槽）、`cleanPool`（干净容器池，见 §1.3）。供应是**定义级数量**，实例 ID 由加载器按 `<definition>-<n>` 确定生成（与既有 fixture 的 `tomato-1`/`egg-1` 约定一致，保证可重放）。
- 加载器 `CookingContentCatalog`（静态类）：
  - `CookingContentCatalog.Load(string json)` / `Load(Stream)` → `CookingContent`；
  - `CookingContent` 记录：`Schema`、`Candidate`（`CookingConfigurationCandidate`，含新增的订单模板与供应）、`OrderTemplates`、`StandardInitialSupply`；
  - 加载即校验：`CookingConfigurationRegistry.Validate(candidate)`，无效时抛 `ArgumentException` 并附首条结构化诊断（内容文档是内部受信数据，加载失败属于开发期错误，不走 blocked result）；
  - `BuildFixture(scope, players, poolLocation)` 帮助方法：从内容构造 `CookingRecipeFixture`（players/layout 属宿主与测试关注点，不进内容文档——地图/关卡 schema 是 P3 范围）。
- 内容数值（owner 第六轮"数值归属配置"）：切番茄 2 tick、打蛋 2 tick、煮制 6 tick、烤面包 2 tick；干净碗池上限 2。这些数字只存在于 JSON 中。

### 1.2 正式内容（owner 决定落地）

| 类别 | 内容 | 来源 |
|---|---|---|
| 物品 | tomato、chopped-tomato、egg、beaten-egg、water、tomato-egg-soup、bread-slice、toasted-bread、bowl（容器，容量 1，接受 soup/beaten-egg/egg/toasted-bread）、pot（容器，容量 4，接受 chopped-tomato/beaten-egg） | notes §11.1 第一/二/三轮；§12.1 第十轮 |
| 工位 | board-a（cut）、stove-a（heat）、oven-a（bake）、counter-a（无能力，放置/中转） | notes §11.1 第七轮；§12.1 第五轮 |
| 配方 | chop-tomato（tomato→chopped-tomato，cut，2，ConsumeInputs，需工位）；beat-egg（egg→beaten-egg，beat，2，ConsumeInputs，免工位）；tomato-egg-soup（{chopped-tomato, beaten-egg}+water 默认供应→soup，heat，6，RetainInputs，需工位）；bake-bread（bread-slice→toasted-bread，bake，2，ConsumeInputs，需工位） | notes §11.1 第一/二/八轮；§12.1 第十轮 |
| 订单模板 | tomato-egg-soup-order（requiredRecipe=tomato-egg-soup，requiredContainerDefinition=bowl） | notes §11.1 第一/二/四轮（订单要求"碗中的汤"） |
| 标准初始供应 | pot×1 落 stove-a；tomato×2、egg×2、bread-slice×2 入 world:pantry；bowl 干净池上限 2 | notes §11.1 第二轮（干净碗有限）；§12.1 第十轮（面包片开局供应） |

`dough`/`bread-slice-as-product` 占位退役：烤面包的输入是面包片、产物是烤面包；dough 不再是正式物品。

### 1.3 干净碗池与供应实例化

- `cleanPool` 供应项按 `CleanContainerSupply` 语义实例化：池物品 ID 为 `pool-<definition>-<n>`，位置为 `clean-pool`，在册干净数计入上限（与任务② `CookingRecipeSimulation` 构造逻辑一致，不引入第二套池规则）。
- 台面（counter-a）不进标准初始供应：它是中转位，由布局/测试按需放置物品（既有 fixture 的 `StageBowlOnCounter` 行为保留）。

## 2. v2 候选与校验扩展

- `CookingConfigurationCandidate` 尾部追加两个可选参数：`IReadOnlyList<CookingOrderTemplateDefinition>? OrderTemplates = null`、`IReadOnlyList<CookingSupplyEntryDefinition>? StandardInitialSupply = null`。缺省为空，既有构造点（含 ET 测试与退役 UDP 源码）行为不变。
- 新定义：
  - `CookingOrderTemplateDefinition(OrderTemplateId Id, RecipeId RequiredRecipe, DefinitionId RequiredContainerDefinition)`；
  - `CookingSupplyEntryDefinition(DefinitionId Definition, int Count, string Location)`；
  - `OrderTemplateId` 记录结构（同 `RecipeId` 模式）。
- 校验（`CookingConfigurationRegistry.Validate`）新增两张表：
  - `OrderTemplate`：ID 非空且唯一；`RequiredRecipe` 存在（MissingReference）；`RequiredContainerDefinition` 存在、带容器能力（MissingReference/InvalidValue）、且其 `AcceptedDefinitions` 包含该 recipe 的 `ProductDefinition`（MissingReference，relation 为产物定义）；
  - `StandardInitialSupply`：`Definition` 存在；`Count > 0`；`Location` 匹配 `world:*`/`station:*`/`cleanPool` 且 station 引用存在于 appliances（缺省诊断码沿用既有集合，不新增码值——除非实现证明必须，见 §9）；
  - `cleanPool` 供应项隐含"该定义必须是容器"（InvalidValue），因为池语义要求可洗碗容器；`WashableContainerDefinitions` 与 `CleanContainerSupply` 仍是 fixture 级参数，由加载器从供应项派生（cleanPool 项 → 可洗碗 + 上限）。
- canonical 与身份：`CanonicalConfiguration` 增加 `orderTemplates` 与 `standardInitialSupply` 两段（排序稳定）；schema 字符串保持 `cooking-definition-v2`；hash 随之变化（无硬编码 hash 金样依赖，见 PRD §6）。
- **既有校验规则的两处显式放宽**（正式内容所必需，随本任务 spec 修约记录）：
  1. `ValidateAppliances` 删除"能力集合为空"的 `RequiredFieldMissing` 诊断：台面（counter-a）是放置/中转面，不声明任何能力；空白能力字符串仍拒绝；
  2. `ValidateRecipes` 的 `CapabilityUnavailable` 检查对 `RequiresStation == false` 的配方豁免：免工位加工（打蛋）的能力只是标识，`StartProcess` 从不拿它比对工位；"能力必须在 supportedApplianceCapabilities 中"的检查保留。
- `CookingConfigurationSnapshot` 同步暴露 `OrderTemplates` 与 `StandardInitialSupply`，使 level 生命周期经配置身份拿到同一份内容。

## 3. Order owner 移入领域：订单簿

### 3.1 端口退役与旧→新映射

| 旧 | 新 |
|---|---|
| `ICookingOrderPort.Submit(CookingOrderSubmission)` 判定"要求"并回报完成 | 领域订单簿判定要求；端口接口、`CookingOrderSubmission`、`CookingOrderAcceptance` 全部退役 |
| 测试端口 `RequirementOrderPort`/`AcceptingOrders`/`ThrowingOrderPort`/`ReenteringOrderPort` | 删除；提交路径不再调用任何端口，重入风险随端口消失；对应测试改为领域行为测试（§6） |
| `_acceptedOrders`（HashSet） | 订单簿 `Dictionary<OrderId, OrderState>`（Open/Completed），快照可观察 |

### 3.2 订单簿模型

- `OrderState`：`OrderId`、`OrderTemplateId Template`、`RecipeId RequiredRecipe`、`DefinitionId RequiredContainerDefinition`、`OrderStatus Status`（Open/Completed）、`long CompletedAtLogicalTick`（完成时的 LogicalTick，未完成为 null）。
- 开单（前厅注入，对称 `CompleteWash`）：`CookingOrderResult OpenOrder(OrderId order, OrderTemplateId template)`：
  - 模板必须存在于 fixture 的 `OrderTemplates`（否则 `OrderTemplateNotFound`，新 reason）；
  - order 身份未被使用过（开过或完成过都拒绝 `OrderIdentityConflict`，新 reason——订单身份与命令身份一样是全局长久身份）；
  - 成功：订单 Open 进簿，`_stateVersion++`，返回 Accepted。开单**不是**玩家命令，不进 `CookingRecipeCommand` 路径、不进事件序列（与 `CompleteWash` 一致：它是世界演化注入，不是玩家原子动作）。
- Fixture 扩展：`CookingRecipeFixture` 尾部追加 `IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition>? OrderTemplates = null`（默认空）。内容加载器从 supply/模板填充。

### 3.3 提交契约（`SubmitOrder` 重写）

校验顺序（全部通过才提交；任一失败 `Reject` 且零变更）：

1. scope/player 基础校验；
2. 产物存在、未移除、`IsProduct`、版本匹配；已消费 → `ProductAlreadyConsumed`；
3. `command.Order` 对应订单存在于簿；不存在 → `OrderNotFound`（新 reason）；状态 Completed → `OrderAlreadyCompleted`（新 reason）；
4. 产物在容器槽（`ProductNotPlated`）且容器可达（`TargetOutOfRange`）；
5. 产物 `Recipe` == 订单 `RequiredRecipe`，否则 `OrderRequirementMismatch`（新 reason）；
6. 容器物品定义 == 订单 `RequiredContainerDefinition`，否则 `OrderRequirementMismatch`；
7. 订单与命令身份去重：同 identity 重放由 `SubmitCore` 缓存层返回 `IsDuplicate`（既有机制，不变）。

成功提交的原子效果（顺序确定）：

1. 产物移除、版本+1、进 `_consumedProducts`；从容器内容物列表移除；
2. 订单转 Completed，记录 `CompletedAtLogicalTick = LogicalTick`；
3. 追加结算记录 `CookingOrderSettlement(OrderId, OrderTemplateId, RecipeId, ProductId, PlayerId, ContainerId, LogicalTick, long Sequence)`（Sequence 按追加顺序递增，只含确定事实，无评分字段）；
4. 可洗碗容器变脏、离开厨房、在册干净数 -1、`RequestWash`（任务②行为不变）；
5. `Commit(...)` 发布一次 `order-submitted` 事件（summary 保持）。

### 3.4 结算记录与快照

- `CookingOrderSettlement` 记录 + `IReadOnlyList<CookingOrderSettlement> SettlementHistory`（与 `EventHistory` 并列的只读历史）。
- `CookingRecipeSnapshot` 增加 `Orders`（簿内全部订单，按 OrderId 排序）与 `Settlements`（按 Sequence 排序）；`CanonicalSnapshot` 同步增加两段。旧字段 `AcceptedOrders`（`IReadOnlyList<OrderId>`）**保留**（= Completed 订单 ID 列表，语义不变），避免击穿既有断言面。

## 4. fixture 与闭环测试迁移

- `CookingRecipeFixture` 构造参数追加 `OrderTemplates`（尾部可选）。构造函数既有校验（recipe 外键、ticks、能力）保持不变；订单模板外键校验在加载器/注册表层（§2），fixture 层只查模板字典非空引用。
- `CookingKitchenLoopFixtureTests` 的 `CreateFixture()` 改为从 `CookingContentCatalog.Load` 构造（players/layout 仍由测试提供），闭环步骤与断言结构不变；L03 的"要求拒绝"从 `RequirementOrderPort` 改为领域订单簿拒绝；L01 增加开单步骤（前厅注入）与结算记录断言。
- 其余依赖 `ICookingOrderPort` 的测试（`CookingRecipeLoopTests`、`CookingCompletionKindTests`、`CookingContainerAsItemTests`、`CookingKitchenActionsTests`、`CookingProcessAnchorTests`、`CookingCommandArbitrationTests`、`CookingRecipeCommandShapeTests`、`CookingLevelLifecycleTests`、`CookingMatchLifecycleTests`、ET 侧 `CookingLevelEtHostTests`、`CookingVerticalSliceTests`）：端口替为 fixture 订单模板 + 需要时 `OpenOrder` 注入；`ThrowingOrderPort`/`ReenteringOrderPort` 语义的重入测试改验证"提交路径不调用外部端口"这一结构事实已消失，改为提交路径重入防护测试（`_mutationInProgress` 既有守卫经 `AdvanceFixedTick` 重入拒绝覆盖，见既有测试）。
- 退役 UDP 源码（`AbilityKit.Game.Cooking.Udp*`）不随产品 API 改动；它们在构建外，只在设计记录中注明"退役源码引用已退役端口，不维护"。

## 5. ET 侧影响

- 命令形状、枚举、指纹金样：不变（K08）。
- `CookingLevelEtHostTests` 的 fixture 工厂追加订单模板参数；任何"提交后被端口接受"的断言改为订单簿 Completed + 结算记录断言。
- EtRuntime/EtBridge 投影：不引入订单实体（ET Phase B 范围外），只经 snapshot 观察。

## 6. 风险与不变量

- 装盘、提交、幂等、原子失败不变量（spec R02/R03）必须维持：订单簿化只能加强要求判定（从端口挪进领域），不能放松。
- 开单注入与命令路径分离：开单不产生命令事件、不进批次仲裁；否则会改变 `SubmitBatch` 语义。
- canonical 新增两段会改变所有 run-to-run 之外的哈希消费方——已确认无硬编码金样；ET 指纹金样与 canonical 文本无关。

## 7. 验收映射

| K | 实现位置 |
|---|---|
| K01/K02 | `CookingContentCatalog` + 注册表校验测试（新 `CookingContentCatalogTests`、扩 `CookingConfigurationValidationTests`） |
| K03/K04/K05 | 新 `CookingOrderBookTests`（提交契约、幂等、结算记录） |
| K06/K07 | `CookingKitchenLoopFixtureTests` 迁移 + 烤面包路径测试 |
| K08 | 既有 `CookingLevelEtHostTests` 三个金样测试不改断言 |
| K09 | gate `cooking-kitchen-loop`（既有，不新增 gate） |
| K10 | spec 修约 + progress/Todo |

## 8. 明确不做（同 PRD §4，不重复）

## 9. 推定项（无单独 owner 裁决原文，实现按本 design 执行）

- 供应 location 语法（`world:`/`station:`/`cleanPool`）与实例 ID 生成规则（`<definition>-<n>`、`pool-<definition>-<n>`）；
- 订单模板只声明"要求 recipe + 要求容器定义"两项，不带耐心/时限/奖励字段（分别属前厅与评分，范围外）；
- 开单失败使用新 reason `OrderTemplateNotFound`/`OrderIdentityConflict`，提交失败使用新 reason `OrderNotFound`/`OrderAlreadyCompleted`/`OrderRequirementMismatch`，全部尾插入 `CookingRecipeRejectionReason`；
- 供应项 station 引用缺失时的诊断码沿用 `MissingReference`（表名 `StandardInitialSupply`）；
- 结算记录字段集（order/template/recipe/product/player/container/tick/sequence）与 `SettlementHistory` 命名；
- `CookingRecipeSnapshot.AcceptedOrders` 保留为 Completed 订单 ID 列表；
- 内容文档放 `src/AbilityKit.Game.Cooking/Content/` 并随程序集输出；地图/关卡 schema、Level 对内容的引用属 P3，本任务的内容文档只服务"代表菜闭环"这一张小关内容。
