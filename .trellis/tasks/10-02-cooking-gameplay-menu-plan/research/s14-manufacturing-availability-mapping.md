# Research: ManufacturingAvailability 最小数据映射

- Query: 菜单节点对实际RecipeFixture的readonly结构校验怎样实施，哪些数据仍需可信补足？
- Scope: internal；当前主源码只读，不.NET。
- Date: 2026-10-02

## Findings

### 最小只读 API 与冻结边界

建议 simulation 新增 `DescribeManufacturingAvailability()`，返回 `CookingManufacturingAvailability`：Scope、ConfigurationIdentity（若simulation本身不拥有则host聚合）、Recipes、ItemDefinitions、Appliances、Players、OrderTemplates、CurrentSpatial、SupplyConfiguration、PhysicalItems/Containers、CleanContainerDefinitions/PoolCounts。后五项尽量复用现有Snapshot与SupplySnapshot，不返回活 `_fixture` 或 `_supply`。

所有嵌套集合须复制：Recipe.Inputs/DefaultInputs只读数组；Item.AllowedPlayerCapabilities和Container.AcceptedDefinitions冻结set；Appliance.Capabilities与Player.Capabilities/ReachableStations冻结set；maps冻结dictionary；Spatial Freeze；supplier配置按现有ValidateAndFreeze；物件使用Snapshot已投影数组，容器ItemIds复制。record和IReadOnlyDictionary本身不能防止原dict/set被外部修改。

既有 `CookingRecipeSupply.cs` 有 `CookingSupplySnapshot(Ledger,Origins)`，simulation `SupplySnapshot()`返回冻结来源与ledger，可复用actual库存来源但其本身不包含supplier.UnitDefinition/PackageDefinition，必须补只读已冻结SupplyConfiguration。无需新增任何库存写接口。

### 节点逐字段实际对照

| Catalog node字段 | 实际配置 | 验证 |
|---|---|---|
| Step.Id | RecipeFixture.Recipes key + Recipe.Id | 一致，未知返回MissingRecipe |
| Inputs(definition,portions) | Recipe.Inputs multiset | 按数量Expand比较，禁止Distinct；DefaultInputs必须与catalog约定一致（当前投影无default） |
| Output | ProductDefinition | 一致且真实ItemDefinition存在/授权 |
| Process | Process | 一致，不只靠output同名选recipe |
| Capability | RequiredApplianceCapability | 一致；由真实IsAvailable appliance支持，不由Supported vocabulary推断 |
| Carrier | RequiredProcessingContainerDefinition | 一致；容器定义存在、Capacity>=完整输入数量、AcceptedDefinitions覆盖每输入；carrier自己不是投料 |
| ExecutionKind | Execution | Manual/Automatic一致；当前RecipeExecution为同语义不同enum明确转换 |
| YieldPortions | YieldPortions、Completion | 一致；多份RetainInputs，单份按catalog Completion(step)，不能只看产物definition |
| RequiredTicks | RequiredTicks | 属fixture映射完整性，正数且当前投影一致；不把研究值重设正式平衡 |
| OutputStorageContainer | 实际Item.Container +来源 | 兼容该Output，容量正且有真实/基线/补货来源；Recipe无独立storage字段，需catalog和实例结构联合验证 |
| FinalRecipe/Product/ServingContainer | OrderTemplate.RequiredRecipe/RequiredContainerDefinition + finalRecipe.ProductDefinition | 三者一致，容器能装finalOutput且不是仅工作器具 |
| RequiresBinding | OrderTemplate.RequiresBinding +可信command-enabled policy | 一致；不添加物理贴单站距离 |

`CookingMenuCatalog.cs:337` ToContentDocument已有适配器字段一致验证，是生成侧证据；Ready再次读实际runtime可防factory载入错content/错scope，不重造mapper。Step.MustLast/Operation、Entry.RequiresStagedFinal/FinalAdditions主要由catalog生产图和既有87菜实际执行验证承载，runtime Recipe没有这些独立字段；validator确认整个依赖图对应正确节点即可，不虚构新状态/执行规则。

### 玩家与路径数据足够/不足

`CookingDomain.cs:135/140` Item.AllowedPlayerCapabilities与Player.Capabilities是资格overlap；玩家**并不需要**拥有recipe appliance capability字符串（其属于Appliance）。按 `CookingRecipeLoop.cs:1545` 实际StartProcess检查输入item资格，需要存在至少一个available玩家具备本工序全部需要项的资格与可到达工位，而不同节点可以不同玩家，符合自然分工。不能要求一个玩家能做全菜，也不能把多玩家能力union误当同一工序可操作。

无Spatial legacy路径可使用Player.ReachableStations。真实空间Ready检查静态连通/半径净空，从合法入口/当前位置可通行区域到交互面；**不是**要求玩家在准备结束瞬间已经站在每台设备附近，否则多数合法厨房无法Ready。当前ValidateSpatialReach检查当前距离/朝向，是执行predicate，不适合作Ready可达结构检验；需复用S08 geometry reachable results或只读通路查询。

现有Appliances没有设备Definition映射，S08可信StationBindings与Footprints提供设备权限，勿从StationSlot字符串猜definition。Recipe数据没有selectedmenus/原料许可scope，需前一报告建议trustedfactory菜单policy；当前物件位置不能赋予禁止definition制作权。

### 结构缺项与数量提示

阻Ready：未知菜单/节点；runtime图不一致；未注册或未授权定义；无兼容容器定义/取得途径；无合法supplier或初始物件来源；不可达/不可用加工设备；无合格玩家；单批输入超carrier容量；可信绑定命令被禁而菜单要求binding。

数量提示：有限余额不足、当前原料0、到货尚在pending/arrived、器具当前占用、批次不足一份、存储槽临时满。数量状态不改变普通自然成功，不作为新订单失败机制；玩家可等待/补货/取器具继续。容器“目前有一只被占用”仍表示合法来源存在。

只有注册definition而零baseline/零现存/无supplier取得路线属于结构缺项；当前结构存在但有限余额已耗尽属于数量状态，提示SupplyExhausted，不擅自把营业判失败。永久耗尽也不应通过偷偷新增无限source补救。

### 最小实施边界与例

独立partial readonly访问器+纯validator/diagnostic DTO+专属测试；hostowner串行Ready接线与可信factorypolicy/checkpoint身份。别改Recipe执行、S07供应commit、S05Bind距离、87菜生成内容。测试实runtime错multiset/产物/carrier/yield、工位词汇存在但实体缺失、player资格错误、静态可达但当前远离仍Ready、原料量0但来源存在只提示，以及nested集合外部改写不能影响availability或authority。

## Caveats / Not Found

本报告没有新增实际执行证据。clean pool与容器基线来源信息当前不全在RecipeSnapshot，需要readonly配置查询，不能把pool隐藏容器当不存在。当前单份RetainInputs或legacy特殊recipe映射必须以catalog Completion(step)与已接受配方契约为准，不普遍强制单份ConsumeInputs。
