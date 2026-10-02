# Research: S07 allocation 独立只读审阅

- Query: source chain a48b17806 → 6022ab04644e48f35d37fce8e728326415eeedb2 → 11f66028739daad7b3c740d316327a543f06f812 的统一allocator、水位与恢复原子性。
- Scope: internal；审阅 `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-supply-s07` 当前源码；不执行git、不跑.NET、不修改产品。
- Date: 2026-10-02

## Findings

### 结论：存在恢复 blocker

`CookingRecipeCheckpoint.cs:440-444` 接受所有 AllocationSequence=0；普通 allocated product 没有 SupplyProvenance 的正数约束。因此只修改普通成品的 AllocationSequence 与 NextProductId 为0，即可绕过新的全球水位约束。这个具体反例不要求伪造其他字段或清空事件：在没有供应分配的合法厨房产生第一个普通产品后，所有初始及AddItem种子本来为0，唯一正序号产品改0、水位改0，其他恢复约束仍有效。

`ValidateEventHistory`（同文件:651-718）只校验事件序号、帧与Tick连接，不检查产品分配水位；`ValidateExtendedCheckpoint`不约束普通产品的零分配序号。因此从代码逻辑判断，此篡改可恢复成功。恢复后再次分配sequence1，默认/确定性opaque allocator可能返回旧产品ID；重复检测虽防复制，但合法后续生产发生冲突/拒绝，水位恢复不可信。本报告未运行该反例，要求实施补真实测试。

### 四条实际分配路径

| 路径 | 代码位置 | 观察 |
|---|---|---|
| 固定Tick ConsumeInputs | CookingRecipeLoop.cs:1068,1113,1136,1382 | 候选状态内计数并给产品AllocationSequence；最终提交计数。 |
| 旧显式AdvanceTicks ConsumeInputs | CookingRecipeLoop.cs:1643-1661 | checked下一水位、allocator空/重复检查和版本/事件/输入version预检后才改水位，产品设置AllocationSequence。 |
| ServePortion / 完成Pour的单份实现 | CookingPortions.cs:130-158 | 私有item/container字典和局部sequence，生成产品序号，完成后统一提交。 |
| ReceiveSupply / TakeSupply | CookingRecipeSupply.cs:120-158 | package与units共用_nextProductId；同时记录item AllocationSequence和SupplyProvenance；失败丢掉candidate与暂存字典。 |

以上均使用同一 `_productIdAllocator.GetProductId(sequence)`，没有供应独立allocator。固定Tick与Supply联合失败的候选先后提交还须依现有 `CookingRecipeSupplyTests.cs:395` 及实际门禁验证，不把读到测试定义等同通过。

### checkpoint 正向约束已接线

`CookingRecipeCheckpoint.cs:25` item AllocationSequence 标JsonRequired（默认值0不等于JSON缺字段被允许），:118 canonical包含，:265导出，:765安装，:815恢复水位。:440-444检查所有items，包括removed tombstones的负数、超水位和正序号重复，覆盖供应与普通产品交叉碰撞。

`CookingRecipeSupply.cs:190-207` 校验供应来源Request/Supplier/Delivery/UnitIndex与对应receipt、unit定义、package/units列表；provenance序号必须等于item序号、>0且≤水位；package与units严格连续。供应tombstone仍保留来源链，不能因消耗而从序号唯一性集合移除。来源孤儿及receipt缺origin也拒绝（:210-214）。

恢复校验不调用 GetProductId：ID可能是opaque，不能重算ID并把default allocator当统一格式。水位检查应基于持久化分配证据而非ID字符串。

### seq0合法边界与最小改进

合法AddItem（CookingRecipeLoop.cs:690-719）创建 `IsProduct=false,Recipe=null,AllocationSequence=0`；RetainInputs仅在原容器anchor上设置 completed/recipe/portions（:1665-1668），不设置IsProduct=true，不创造identity，因此seed0合法。供应起源单位与容器也可能随后成为加工anchor，已有正sequence必须原样保留。

**最小修补：** 对 `IsProduct && AllocationSequence <= 0` 明确 CounterInvalid；所有真实产生的普通成品路径都设置IsProduct=true且正序号，AddItem与Retain anchor不受影响。SupplyProvenance已有额外正数检查应继续保留。加入普通产品与NextProductId双降0、已消费普通产品tombstone双降0、seed0与Retain anchor合法恢复、供应正序号保留、opaque恢复零调用用例。反例若checkpoint还有其他正序号，则仅降低水位到现存其他最大值并把较大普通成品降0，仍可重复较大序号，不应只测全0。

fixture没有可信的完整seedID账本（AddItem支持任意调用方ID）；不能凭seed形似ID判断。用process事件反查产品可以增强一致性，但成功handoff本来清事件，不能仅靠事件作为唯一来源。不在本轮宣称能防“把IsProduct、Recipe、seq及全部来源同时伪造”的任意一致伪造；这是与具体两字段反例不同的完整信任模型议题。若必须区分所有seed/allocated物件，独立显式origin类型及对应持久化seed登记才是完整方案，需正式schema审议，不能恢复时调用allocator猜测。

### 失败原子性边界

Supply在局部candidate分配全部物件，捕获非OOM异常返回SupplyAllocationFailed后不安装item/container/origin/supply/watermark（CookingRecipeSupply.cs:131-158）；预检state/event水位溢出（:90）。可见测试覆盖空ID、重复/墓碑冲突、首末allocator异常及水位溢出（CookingRecipeSupplyTests.cs:216-224），只是覆盖定义，独立复跑仍必要。

allocator是外部对象，调用本身可能具有外部副作用；领域可以保证自己的状态未提交，不能保证任意有状态allocator调用次数回滚。opaque deterministic allocator需以sequence为稳定输入。恢复不得调用它这一要求当前静态逻辑满足。

## Caveats / Not Found

- 当前只证明静态接线，未核验Git链对象或独立复跑，不报告通过数。
- seq0普通成品缺口是domain increment的blocker，修补及复跑前不能限定通过。
- 即使修补domain也不能宣布S07完成：Preparing/ET ingress与固定Tick生产供应流程仍须单独实现和验收；本次未审阅其完成。
- 外部参考未用；相关规范是Cooking checkpoint/authority与S07任务契约，未加载角色隔离禁止的implement/check manifests。
