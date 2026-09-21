# Design：Cooking 厨房闭环仿真规则与闭环 fixture

> 状态：planning。本设计只落实 owner 已确认决定（09-19 notes §11.1）与任务①修约契约；凡属推定项均在 §9 显式列出，不得表述为 owner 已确认。

## 1. 容器即物品：数据模型

### 1.1 退役与保留

- **退役** `CookingContainerDefinition` 与 `CookingRecipeFixture.Containers`（`IReadOnlyDictionary<ContainerId, CookingContainerDefinition>`）。容器不再是与物品并列的第二身份。
- **保留并消费** `CookingItemDefinition.Container`（`CookingItemContainerCapability?`，任务①已加）：容量与可接受物品定义集合由物品定义声明。锅/碗/砧板容器形态全部走该字段。
- `ContainerId` 记录类型保留给布局层（`CookingLogicalLayout.Containers`）与配置候选层；**命令与仿真层的容器引用一律是容器物品实例 ID（`ItemId`）**。

### 1.2 仿真内部索引

- `CookingRecipeSimulation._containerItems` 从 `Dictionary<ContainerId, List<ItemId>>` 改为 `Dictionary<ItemId, List<ItemId>>`：键是容器**物品实例** ID，值是内容物物品 ID 列表。
- 容量读取：容器物品定义的 `Container.Capacity`；可接受集合：`Container.AcceptedDefinitions`。未知/已移除/无容器能力的物品作为容器 → `ContainerNotFound`。
- 槽位分配维持现有"最小空缺槽位"策略（`slot-0`、`slot-1`…），保证确定性与快照稳定。

### 1.3 快照与 canonical

- `CookingRecipeSnapshotContainer` 的 `Id` 从 `ContainerId` 改为 `ItemId`（容器物品实例 ID）；`Capacity` 来自该物品定义。
- `CookingRecipeSnapshot`/`CanonicalSnapshot` 增加容器物品的内容物定义列表（每槽 `{ ItemId, DefinitionId }`，按 ItemId 排序），使"蛋液在碗中""输入保留在锅中"可被快照与哈希观察。旧 canonical 只有容器 ID + 容量，观察不到内容——这是本任务的补齐项。
- `CookingSimulation`（P0）与 EtBridge 投影不动；P0 快照已含 ContainerSlot 位置信息，不受影响。

## 2. 七项动作：命令形状与旧→新映射

`CookingRecipeOperation` 现状：`Pickup, StartProcess, AdvanceTicks, Plate, SubmitOrder`。
`AdvanceTicks` 不是 owner 七项之一，但它是 fixed-tick 命令路径（legacy 多 tick 命令行为有回归测试保护），**保留**。

新枚举（wire 形状；UDP 已退役，但 ET 指纹金样仍要求尾插）：

```text
Pickup, StartProcess, AdvanceTicks, SubmitOrder,   // 既有，位置不动
Drop, PutIn, TakeOut, Pour,                          // 新增，尾插
```

`Plate` **不能**简单尾插保留（它在新模型里没有容器 ID 语义），删除它属于 wire 形状变化：ET 二进制指纹金样（`CookingLevelEtHostTests` 的硬编码十六进制向量）必须重锚，`CookingCommandFingerprint` 的 operation 编码随之改变；`IsWellFormed` 的 `Plate` 分支删除并补四个新分支。重锚计划见 §7。

| owner 动作 | 命令 | Item | Station | Container | 其他 |
|---|---|---|---|---|---|
| 拾取 | `Pickup` | 被拾取物品 | — | — | 来源：世界位置或工位槽（含端走端走锅） |
| 放下 | `Drop`（新） | 手持物品 | 目标工位 | — | 手→工位槽；工位须可达、可用、未满 |
| 放入 | `PutIn`（新） | 手持物品 | — | 目标容器**物品** | 手→容器槽；容器须有容器能力、可达、未满、声明接受该定义 |
| 取出 | `TakeOut`（新） | 被取出物品 | — | 所在容器**物品** | 容器槽→手；手须空 |
| 启动加工 | `StartProcess` | 锚点物品 | 可选（打蛋免工位） | — | 见 §3 |
| 倒出 | `Pour`（新） | — | — | 源容器**物品** | 目标容器经 `command.Item` 传入（倒入的容器物品） |
| 提交 | `SubmitOrder` | 被提交产物 | — | — | 产物须在容器槽中；端口带要求 |

`Plate` → `PutIn`/`Pour` 映射（写死在 domain）：旧"产物装入独立盘子状态"= 新"倒入碗"（`Pour`，源为已完成锅）或"放入碗"（`PutIn`，源为手持产物）。旧测试 R01/R03/R05 的 Plate 步骤迁移为 `Pour`（从锅倒出生成产物入碗）——注意这同时改变了旧测试的中间态（旧：AdvanceTicks 完成即在工位生成产物再 Plate；新：`RetainInputs` 到点完成只切"已完成"，倒出才生成）。旧 fixture 是单输入预处理类，按 owner 第八轮它属于 `ConsumeInputs`，因此迁移后旧测试的"完成即在工位生成产物"保持不变，Plate 步骤改为 `PutIn`（手持产物放入碗）。

### 2.1 倒出的目标容器字段

`Pour` 用 `command.Item` 传**源**容器物品 ID（被倒出的容器），`command.Container` 传**目标**容器；`ExpectedItemVersion` 校验源容器版本。理由：命令记录只有一个 `ItemId? Item` 字段，且 ET 指纹与 JSON 指纹都按字段序列化，不为新动作新增字段（避免第三套指纹分叉）；`Item` 在全动作集中一贯是"主要被变更物"，与版本校验语义一致。`IsWellFormed` 的 `Pour` 分支要求 `Item` 与 `Container` 均为物品标识。

### 2.2 放入即拒绝

`PutIn` 校验顺序（全部通过才提交）：

1. scope/player 基础校验；
2. `Item` 在手；`Container` 是活动物品且定义带容器能力；
3. 容器可达：容器在手（不可能，手被占）或在玩家 `ReachableStations` 内的工位槽；
4. 容器内容物数量 < 容量；
5. **容器定义的 `AcceptedDefinitions` 包含手持物品定义**（Ordinal 集合包含）；否则 `ContainerRejectsItem`（任务①已尾插该 reason）；
6. 手持物品不属于任何活动进程的锁定输入（§4）；
7. 不存在包含环：容器不是手持物品的后裔（容器在容器里、再把祖先放进去）。实现：沿 `Container` 的容器槽所有者链上走，若遇到 `Item` 即拒绝 `ContainerRejectsItem`。链上有界（每步上一层），状态不变成环，安全。

错误输入在放入阶段拒绝，不进入启动校验——这是 owner 第二轮决定，`StartProcess` 不再需要"配方外物品"分支（配方匹配失败自然 `RecipeNotMatched`，但正常路径下放入已保证内容 ⊆ 某配方输入并集……不，放入只保证被该容器接受，不保证被某配方接受；匹配失败仍走 `RecipeNotMatched`/`RecipeAmbiguous`，这是自动识别的正常拒绝路径，不是"错误输入漏到启动"）。

### 2.3 锁输入（不可取消）

- 新增 `Dictionary<ItemId, ProcessId> _inputsByProcessItem`（item→process 反查索引）与 `Dictionary<ProcessId, IReadOnlyList<ItemId>> _lockedInputsByProcess`。启动加工时登记：`ConsumeInputs` 登记输入物品；`RetainInputs` 登记容器锚点物品 + 全部内容物。
- 放下（`Drop`）、放入（`PutIn`）、取出（`TakeOut`）、倒出（`Pour`）对**属于活动进程锁定输入**的物品一律拒绝，reason 用 `ItemStale`（该物品被加工锁定，版本语义上不可动；不新增 reason，保持 wire 稳定）。对 `Pour`：源容器是锁定输入时拒绝（煮制中不能倒出）。
- 拾取（`Pickup`）对锁定输入：仅当该物品是某活动进程的**容器锚点**（`RetainInputs` 的锅）时放行——端走继续（owner 第七轮）；其余锁定输入拾取拒绝 `ItemStale`。打蛋的碗是 `ConsumeInputs` 的锚点但输入是碗中鸡蛋，碗本身不是锁定输入，拾取碗放行（打蛋完成后取碗）。
- 进程结束（完成或从索引移除）时清理反查索引；反查索引与 `_processesByStation`/`_stationsByProcess` 一样进 `ValidateProcessIndexesForFixedTick` 的一致性校验。

### 2.4 端走继续与 fixed-tick 校验

`ValidateProcessForFixedTick` 中"输入必须停在工位"的强约束替换为：

1. 每个锁定输入物品存在、未移除；
2. 锁定输入仍登记在该进程名下（反查索引一致）；
3. `ConsumeInputs`：输入位置 ∈ {世界位置, 工位槽, 容器槽}（手持不可——手持即脱离流程，属腐败）；
4. `RetainInputs`：容器锚点位置不限（端走继续：锅可在手/世界/工位/容器槽），但每个内容物输入必须仍在**该容器**的容器槽中（`Location.Kind == ContainerSlot && Location.OwnerId == 锚点 ID`）；
5. 现有 station/recipe/tick 不变量保持。

白名单保留腐败检测能力：测试用内部注入（仿照既有 `AddFixedTickReverseIndexEntryForTesting` 的 testing 后门）把锁定输入改到手持/移除，证明 fixed tick 抛结构化 `InvalidOperationException` 且零变更（K05）。

## 3. 加工启动：锚点、匹配与工位绑定

### 3.1 锚点与输入集合

`StartProcess` 的 `command.Item` 是**锚点物品**：

- 锚点带容器能力 → 容器锚定加工：候选输入集合 = 容器全部内容物的定义集合（去重）；`RetainInputs` 与多输入煮制走此路；
- 锚点不带容器能力 → 单物品加工：候选输入集合 = { 锚点定义 }；锚点须在工位槽（砧板切番茄）或容器槽（碗中打蛋，anchor 是鸡蛋……见下）。

打蛋路径的锚点选择：owner 第八轮"鸡蛋放入碗后启动加工得到蛋液，蛋液暂时留在碗里"。两种锚点设计：

- (a) 锚点 = 碗（容器），输入集合 = 碗内容 { 鸡蛋 }；
- (b) 锚点 = 鸡蛋（容器槽中的单物品）。

选 (a)：与煮制完全同构（容器锚定 + 内容匹配），domain 只有一条匹配路径；且"手持碗免工位启动"天然表达——碗在手，命令 `Item=碗, Station=null`。输出位置：`ConsumeInputs` 的产物生成在**输入所在容器**（碗的下一个空缺槽）——owner 第八轮"在工位或容器上生成输出"。(b) 需要额外表达"锚点在容器槽中"，规则更碎。采用 (a)。

### 3.2 工位绑定与免工位

`CookingRecipeDefinition` 增加 `bool RequiresStation = true`（纯数据字段，尾部可选参数，不击穿任务①的 V2 canonical——canonical 需同步加该字段并重算身份，见 §7）。

- `RequiresStation = true`（切番茄→砧板、煮制→灶台）：命令必须带 `Station`；工位须存在、可用、能力匹配（`RequiredApplianceCapability`）、可达、未被占用；
- `RequiresStation = false`（打蛋）：命令**不得**带 `Station`；进程的工位引用为 null。fixed-tick 索引从"按工位"扩展为"按锚点"：`_processesByStation` 保留（工位进程），新增 `_processesByAnchorItem`（免工位进程按锚点物品 ID）。一个锚点物品同时只允许一个进程；免工位进程同样参与 `AdvanceFixedTick` 与 `ValidateProcessIndexesForFixedTick`。

`ProcessState.Station` 改为 `StationSlotId?`；`CookingRecipeSnapshotProcess`/canonical 的 station 字段可空。`AdvanceTicks`（legacy 命令）与 fixed tick 统一按"工位进程 + 锚点进程"两套索引查找。

### 3.3 配方匹配

启动时用 `CookingRecipeMatcher.Match(容器内容定义集合, 工位能力或免工位能力, candidates)`：

- 工位进程：`applianceCapability` = 工位实际能力集合的匹配键——现有匹配器签名接收单个 `string applianceCapability`，改为按"工位能力集合 ∩ 配方能力"语义：配方候选过滤条件改为 `appliance.Capabilities.Contains(recipe.RequiredApplianceCapability)`，匹配器仍按配方自身能力过滤（保持任务①形状，调用方传入工位上任一副本领即可，语义等价因为工位已校验能力包含）。
- 免工位进程：传入配方要求的能力键（无工位约束，仅用于候选过滤的对称性；实现上传入 `recipe.RequiredApplianceCapability` 由 domain 在无工位时对每个候选逐一校验——简化：免工位时直接用全部候选，因为免工位配方没有工位能力可冲突；但为保持"同一输入集合唯一匹配"契约，仍走匹配器，能力键传空串并让匹配器的能力过滤在空键时跳过……**不**，这会悄悄改变任务①契约。更简单：免工位启动时候选 = 全部 `RequiresStation=false` 的配方，输入集合匹配 + `RecipeAmbiguous` 拒绝；工位启动时候选 = 全部配方，先按工位能力过滤再匹配。匹配器本体不动。）

匹配结果：唯一 → 建进程；零 → `RecipeNotMatched`；多 → `RecipeAmbiguous`（任务①已尾插）。水是默认供应：`DefaultInputs` 参与任务①的集合相等语义，不占物品。

## 4. 完成形态与倒出

### 4.1 ConsumeInputs（切番茄、打蛋）

fixed-tick 到点（或 `AdvanceTicks` 命令到点）原子提交：

1. 全部锁定输入标记移除（版本+1）；
2. 产物物品经统一 allocator 分配 ID，生成在：输入在工位槽 → 同一工位槽；输入在容器槽 → **同一容器的下一个空缺槽**（打蛋：蛋液落碗）；
3. 产物带 `Recipe`、`IsProduct=true`、`OriginStation`（工位进程为工位；免工位/容器产物为输入所在容器 ID……`OriginStation` 类型是 `StationSlotId?`，免工位进程没有工位。改为 `OriginStation` 仅工位进程有值；容器产物的可达性改按容器物品当前位置判定——见 §4.3）；
4. 进程从两套索引移除，反查索引清理。

### 4.2 RetainInputs（煮制）与倒出

到点原子提交：

1. 容器锚点物品切"已完成"：物品状态新增 `bool ContainerCompleted`（快照/canonical 可见）；
2. 输入（内容物）保留、保持锁定；
3. 进程从索引移除（煮制过程结束，"不可取消"针对的是过程进行中；到点后过程已结束）；
4. **此时不生成产物**。

`Pour`（源=已完成容器，目标=另一容器物品）：

1. 源是活动物品、带容器能力、`ContainerCompleted`；目标同理且 ≠ 源；
2. 源与目标都可达（源在手或可达工位；目标在手或可达工位）；
3. 目标有空缺槽且其 `AcceptedDefinitions` 接受产物定义（蛋花汤可入碗）；
4. 原子提交：产物经 allocator 生成进目标空缺槽；源的内容物全部移除（版本+1）；源 `ContainerCompleted` 置 false（锅倒空、复位可复用）；锁定索引早已随进程结束清理；
5. 幂等：重复倒出同一锅——第二次倒出时锅已空且非 completed → 拒绝 `ProductNotFound`（无可倒出物）；同一 command identity 重放走 processed-commands 缓存返回首次结果。"倒出至多生成一次产物"由此保证；
6. `Pour` 也可用于普通容器间转移（蛋液碗→锅）：源非 completed 时，把源**全部内容物**移入目标空缺槽（每个物品版本+1）；目标容量不足或拒绝某定义 → 整体拒绝、零变更（原子性）。碗倒空复用由此实现。

`Pour` 对锁定输入：源容器或其内容是锁定输入 → 拒绝（煮制中不能倒出；打蛋中碗受锁……打蛋进程的锁定输入是鸡蛋，碗是锚点，倒入锅应在打蛋完成之后，正常路径如此；若打蛋中把碗倒入锅，源内容是锁定输入鸡蛋 → 拒绝，正确）。

### 4.3 可达性判定改按容器物品当前位置

替掉 `ContainerIsReachableFromProductStation`（用 `OriginStation` 代理）：产物提交（`SubmitOrder`）时，装产物的**容器物品**必须在手或在玩家可达工位上。`OriginStation` 字段保留在快照中（历史证据），但不再参与可达性判定。

## 5. 提交、订单要求与碗池

### 5.1 订单要求

- `CookingOrderSubmission` 增加 `DefinitionId RequiredDefinition`（订单要求的产物定义）……不，订单要求属于订单侧数据。设计：`ICookingOrderPort` 契约不变（`Submit(CookingOrderSubmission)`），`CookingOrderSubmission` 增加 `ItemId Container`（承载产物的容器物品）与产物所在槽位，**要求匹配由端口实现负责**——fixture 的订单端口按订单 ID 查表得到要求的 recipe identity，与 `submission.Recipe` 比对。domain 只保证：提交物是容器槽中的产物、容器可达、订单未被本会话接受过。
- 提交成功：产物移除（版本+1）、`_acceptedOrders` 记录、端口被调用一次；提交的容器若是"可洗碗定义"（fixture 声明 `WashableContainerDefinitions`）→ 容器变脏并交 NPC（§5.2）。
- 订单端口在提交成功时"回报订单完成"：`CookingOrderAcceptance` 增加 `OrderId? CompletedOrder`……端口返回值已带 `ReasonCode`；fixture 端口在 accept 时记录完成事件。domain 侧 `CookingOrderAcceptance` 增加 `bool OrderCompleted` 字段（默认 false），由端口在受理时置 true，domain 原样透传给调用方观察。这是纯增加字段，不动端口方法签名。

### 5.2 碗脏/净与干净碗池

- 物品状态新增 `bool IsDirty`（快照/canonical 可见）。
- fixture 声明 `IReadOnlySet<DefinitionId> WashableContainerDefinitions`（碗）与 `IReadOnlyDictionary<DefinitionId, int> CleanContainerSupply`（每种可洗碗定义的干净总量=上限，owner 第六轮"干净碗总量由配置定义"）。
- 提交成功且容器是可洗碗定义 → 容器**移除出厨房**（NPC 端走清洗，`Removed=true`、版本+1）并调用注入端口 `ICookingBowlWashingPort.RequestWash(ItemId)`；该定义的干净在册数 -1。
- 测试注入"清洗完成"：`simulation.CompleteWash(ItemId bowl)`（公开方法，模拟 NPC 端口回填）：该物品定义仍是可洗碗、当前在册干净数 < 上限 → 物品复位（`Removed=false`、`IsDirty=false`、版本+1）并放回配置声明的"干净碗池"位置（fixture 的 `CleanPoolLocation`，世界位置）；在册数 +1。超上限 → 结构化拒绝（返回 bool 或抛 `InvalidOperationException`——选返回 `CookingWashCompletionResult` 记录，与命令结果风格一致；不抛，避免污染 fixed-tick 原子性）。
- 脏碗不可用：`PutIn`/`Pour` 目标为脏容器 → `ContainerRejectsItem`（脏碗不接东西）；从厨房移除的碗自然不可用。
- 初始供应：fixture 用现有 `AddItem` 把 N 个干净碗放入厨房（世界位置/工位），在册干净数初始化为 N。

### 5.3 NPC 清洗端口

```csharp
public interface ICookingBowlWashingPort
{
    void RequestWash(ItemId bowl, DefinitionId definition);
}
```

domain 只发请求；"NPC 走到池边、占用若干 tick 清洗"是前厅/NPC 范围（本任务范围外），测试直接调 `CompleteWash` 注入完成事件。

## 6. 多人争抢仲裁

- `CookingRecipeSimulation` 增加 `SubmitBatch(IEnumerable<CookingRecipeCommand>)`（对齐 P0 `CookingSimulation.ExecuteBatch` 形状）：封闭批次内按 **Tick（`LogicalTick` 分量……命令无 tick 字段）**。

命令没有 tick 字段；仲裁键用命令自带的稳定分量：**玩家 ID（Ordinal）→ 命令 ID（Ordinal）→ 命令在批次内的原始索引**。plan 里"按 Tick、玩家 ID、命令序号"的 Tick 分量在命令进仿真时统一为当前 `LogicalTick`（同批次同 tick），实际排序键即 (LogicalTick, Player, CommandId)——测试可验证：同批次同 tick 下玩家序决定胜负；把同玩家的两条命令交换 command id 也改变胜负；乱序传入结果一致。批次中任一命令的 `SimulationBatch` 不一致 → `ArgumentException`（与 P0 一致）。
- 每条命令独立走 `SubmitCore`（幂等键 = Session+Player+Command），争抢失败方得到结构化拒绝且零变更；批次返回 `IReadOnlyList<CommandExecution>`（命令、结果、前后快照），与 P0 同构，供 evidence 使用。
- 这不引入传输：只是把仲裁规则落在权威命令路径上（plan 原文："规则先落地，联机本身后续再说"）。

## 7. 兼容与重锚

- **枚举尾插**：`Drop, PutIn, TakeOut, Pour` 尾插；`Plate` 删除。ET `CookingCommandFingerprint` 的 operation 编码随枚举值变化 → `CookingLevelEtHostTests` 的 golden 十六进制向量与 SHA-256 重锚（任务①同法：先占位、跑出真实值、锚定）。
- **canonical v2**：`CookingConfigurationValidation` 的 canonical 增加 `RequiresStation` 字段（recipe）与容器内容观察所需的快照侧字段 → schema 身份从 `cooking-definition-v2` 升 `cooking-definition-v3`？**否**——任务①刚把 v1 封死，v2 是当前身份。配置 canonical 增加 recipe 字段属于 v2 数据模型扩展：保持 `cooking-definition-v2` 身份不变、canonical 文本增加字段会改变哈希——这与"v1 配置在 v2 下拒绝"不冲突（v2 是当前身份，canonical 形状变化只影响 v2 内部哈希，无历史 v2 配置需要兼容——任务①的 V2 测试随本任务更新期望值）。**决定：保持身份字符串 `cooking-definition-v2` 不变，canonical 增加 `RequiresStation`；任务①的 V2 测试断言随本任务更新（显式记录，不静默）。**
- **`CookingLogicalLayout.Containers`**：从 `IReadOnlyList<ContainerId>` 改为 `IReadOnlyList<DefinitionId>`（布局声明的是容器物品**定义**——锅/碗各出现在布局里）；`CookingLevelLifecycle.ValidatePreparation` 的容器校验改为"定义存在且带容器能力"，`ContainerNotFound` reason 保留；`CookingMatchLifecycle` 的 reason 映射不变（枚举名不变）。
- **配置候选层**：`CookingConfigurationCandidate.Containers`（`IReadOnlyList<CookingContainerDefinition>`）退役，容器能力已在 item 候选里；`ValidateContainers` 与 canonical 的 containers 段删除，`CreateSnapshot` 的 containers 字典删除。`CookingConfigurationSnapshot.Containers` 删除——连带 `CookingLevelLifecycle`/`CookingMatchLifecycle`/测试的引用全部迁移。
- **既有测试迁移**：`CookingRecipeLoopTests` R01-R06 与 fixed-tick 回归按 §2 映射表改写（Plate→PutIn/Pour、容器 ID→容器物品 ID）；行为断言只按 owner 决定变化，其余保持。
- **spec 修约**：本任务落地后需对 `.trellis/spec/cooking/cooking-recipe-loop.md`（装盘条款→放入/倒出、锁输入、端走继续）与 `cooking-config-validation.md`（containers 候选段退役、RequiresStation）追加修约记录，注明来源为本 task design 与 owner §11 决定。

## 8. 明确不动

- `CookingSimulation`（P0 交互仿真）与其测试；`CookingEtBridge` 投影；`CookingNetworkMeasurement`、`CookingPersistenceManagement`、`CookingSessionAuthority` 的语义；ET `CookingLevelEtHost`/`CookingRecipeTickHost` 的行为（除指纹金样重锚）。
- `CookingItemDefinition.AllowedPlayerCapabilities` 语义；`ItemLocation` 枚举。

## 9. 推定项（未获 owner 单独确认，实现中按此执行并在交付说明中显式列出）

1. `Pour` 用 `command.Item` 传源容器、`command.Container` 传目标容器（命令记录不新增字段）；
2. 锁定输入被放下/放入/取出/倒出拒绝时复用 `ItemStale` reason，不新增枚举值；
3. 免工位进程按锚点物品建索引（`_processesByAnchorItem`），一个锚点物品同时一个进程；
4. `RequiresStation` 作为 recipe 的可选布尔字段（默认 true）；
5. 提交成功后容器交 NPC 时直接移除出厨房，清洗完成按原物品 ID 复位回池；
6. `SubmitBatch` 仲裁键为 (LogicalTick, Player, CommandId)；
7. `CookingOrderAcceptance` 增加 `OrderCompleted` 透传字段；
8. `CompleteWash` 返回结果记录而非抛异常；
9. 普通容器间 `Pour` 转移全部内容物（倒空），不是单物品；
10. 配置 canonical 增加 `RequiresStation` 但保持 v2 身份字符串。
