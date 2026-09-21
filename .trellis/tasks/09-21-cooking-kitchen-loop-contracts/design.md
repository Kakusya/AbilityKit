# 技术设计：厨房闭环契约层

> 状态：`planning` 设计稿，未经 owner 批准不得实施。契约以本文件与 `prd.md` 为准；实现清单见 `implement.md`。

## 1. 设计目标

把 owner 已确认的厨房闭环玩法规则，转成不依赖任何具体菜谱的稳定契约：配置数据模型、命令形状、指纹与身份。本设计只定契约，不实现运行时规则（任务②③）。

约束优先级：确定性 > 单一权威 > 数据驱动 > 兼容既有测试。

## 2. 配置 schema v2

### 2.1 身份

```text
CookingConfigurationIdentity(Schema, Sha256)
Schema = "cooking-definition-v2"
```

- `CurrentSchema` 是 schema 版本唯一入口，只改这一处常量。
- canonical 文本继续用 camelCase、无缩进的稳定 JSON，键序固定，集合按 Ordinal 排序。
- v1 → v2：`CookingConfigurationCompatibility.Evaluate` 对 schema 不等返回 `Blocked/MigrationPolicyNotApproved`，sha256 不等返回 `Blocked/IdentityMismatch`；不新增任何转换路径。

### 2.2 数据模型变更（契约级）

```text
CookingItemDefinition
  Id
  AllowedPlayerCapabilities
  Container?                     新增，可选；尾部追加参数，保持旧构造编译
    Capacity                      容量，> 0
    AcceptedDefinitions           可放入的物品定义集合；空集表示不接受任何物品

CookingRecipeDefinition
  Id
  Inputs                         由单个 InputDefinition 改为输入定义集合（非空、无重复）
  ProductDefinition
  Process
  RequiredApplianceCapability
  RequiredTicks
  DefaultInputs                  新增，可选；默认供应（水）的定义集合，不占物品与容量
  Completion                     新增；ConsumeInputs | RetainInputs

CookingContainerDefinition       退役（任务②落地），其容量与接受集合语义并入物品的 Container 能力
```

不变量：

- `Inputs` 与 `DefaultInputs` 都必须存在于物品定义表；两者不得重叠（同一输入不能既是物品又是默认供应）。
- `Completion = RetainInputs` 时 `ProductDefinition` 仍必须存在（倒出时生成），`RequiredTicks` 仍 > 0。
- `Container.AcceptedDefinitions` 中的每个定义必须存在；`Capacity` > 0。
- canonical 文本包含上述全部新字段，因此 v2 身份与 v1 必然不同——这是预期，不是缺陷。

### 2.3 校验

沿用“一次性收集全部诊断 + 确定性排序”的既有模式，新增诊断码走稳定常量，不为单条内容加规则分支：

- 多输入外键缺失、输入集合为空、输入重复；
- 默认供应与物品输入重叠、默认供应外键缺失；
- 完成形态取值非法；
- 容器能力引用缺失、容量非正。

## 3. 多输入配方的命令形状

### 3.1 匹配契约（纯函数，本任务实现并测试）

任务①不实现容器即物品，因此“按容器内容集合匹配”不能挂在仿真状态上。本任务把匹配定义为**纯函数契约**并实现、测试它；仿真接线留给任务②。

```text
CookingRecipeMatcher.Match(
    IReadOnlyList<DefinitionId> presentInputs,     // 容器内物品定义（调用方提供，顺序无关）
    IReadOnlyList<DefinitionId> defaultInputs,     // 配方声明的默认供应，由调用方按候选配方带入
    string applianceCapability,
    IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> recipes)
  -> MatchResult
     | Matched(RecipeId)
     | NotMatched            -> 命令拒绝 RecipeNotMatched
     | Ambiguous             -> 命令拒绝 RecipeAmbiguous
```

规则：

- 比较前把 `presentInputs` 与 `defaultInputs` 合并后按 DefinitionId 的 Ordinal 排序，顺序无关且确定；
- 默认供应不占物品、不占容量，但参与匹配（调用方按每个候选配方把它的 `DefaultInputs` 并入再比较）；
- 命中多个配方返回 `Ambiguous`；这是配置错误的结构化表现，不是运行时异常。

**配方唯一性是实现契约而非 owner 裁决**：owner 确认的是“配方自动识别、按输入集合匹配、顺序不重要”（09-19 notes 11.1 第一/七轮）；“同一集合 + 工位能力必须唯一”是由“自动识别必须可判定”推导的实现层约束，本任务以 v2 校验强制它，并在 spec 修约中注明推导来源。

### 3.2 命令流与 DTO

任务①落地的命令流（任务②才补 `PutInto` 等操作）：

```text
StartProcess(station[, recipe])
  -> 携带 recipe：按显式配方校验（保留旧语义，供定向测试）
  -> 不携带 recipe：本任务一律返回结构化拒绝 RecipeNotMatched
                     （容器内容集合匹配的接线属任务②，见 3.1 纯函数契约）
```

`CookingRecipeCommand` 保持不可变 record；`Recipe` 字段本就是 `RecipeId?`（可空），本任务**不改 record 形状**，只放宽 `CookingRecipeCommandValidation.IsWellFormed` 中 StartProcess 分支对 `Recipe` 的强制。`Item` 仍为单数字段——多输入靠任务②的多条 `PutInto` 累积，本任务不引入输入集合字段。

### 3.3 拒绝原因（新增，尾部追加）

- `RecipeNotMatched`：集合匹配不到配方；本任务中“不携带 Recipe 的 StartProcess”也返回它（容器内容匹配的接线属任务②）。
- `RecipeAmbiguous`：集合匹配到多个配方（配置错误的结构化表现）。
- `ContainerRejectsItem`：放入阶段被容器定义拒绝（任务②实现，契约在本任务定义）。

既有枚举值一律尾插，不重排、不插入。

## 4. 指纹与身份

### 4.1 三处必须同步

| 位置 | 机制 | 本任务的变更 |
|---|---|---|
| `CookingRecipeSimulation.SubmitCore` | 整条命令 JSON 序列化作为指纹 | 不改字段；`Recipe` 为空与携带值的两种命令指纹天然不同，需有测试锁定 |
| `CookingRecipeCommandValidation.IsWellFormed` | 形状校验 | StartProcess 分支放宽对 `Recipe` 的强制 |
| `CookingLevelEtHost.CookingCommandFingerprint.CanonicalBytes` | 手写二进制序列化 | **无字段变化**：本任务不给命令 record 加字段，逐字节输出不变 |

- 去重键 `(Session, Player, Command)` 与 `CommandIdentityConflict` 语义不变。
- ET 层 fingerprint 排除 transport 元数据的规则不变。

### 4.2 golden 向量：验证不变 + 新增向量

`src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs` 中的 golden 十六进制向量（基于 Pickup 命令）在本任务内**逐字节不变**，因为命令 record 没有新字段。因此本任务做两件事：

1. 保留既有 golden 断言，作为“指纹未被意外改动”的回归锚；
2. 新增一条 **recipe-less StartProcess 命令**的 golden 向量，这才是新命令形状的真实金样。

不得为了让测试通过而反向给 fingerprint 加字段或裁剪既有字段。

### 4.3 产品 ID 分配

契约：产品 ID 只能由 `ICookingProductIdAllocator` 产生。

现状有两条路径：fixed tick 走注入 allocator，`AdvanceTicks` 内联 `product-{n}`。**本任务的测试只覆盖 fixed-tick 路径**，并把 `AdvanceTicks` 的内联分配显式记为任务②的待收敛缺口（写进 implement.md 的 Explicitly deferred）；不在本任务写一条现在会失败的契约测试。

## 5. spec 修约

| 文件 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| `cooking-recipe-loop.md` | “在完成条件满足时一次性消耗声明的输入、创建声明的产物” | 按加工定义的完成形态原子提交：`ConsumeInputs` 消耗输入并生成输出；`RetainInputs` 保留输入、容器切“已完成”，倒出时生成成品；幂等延伸到倒出 | owner 决定：09-19 notes 11.1 第二/三轮 |
| `cooking-interaction-foundation.md` | “位置 MUST 属于配置允许的 WorldPosition、PlayerHand 或 StationSlot” | 补 ContainerSlot；容器是带容器能力的物品；保留唯一位置、无重复占有、无包含环不变量 | owner 对具体行为的裁决：notes 11.1 第三轮（端走锅）、第八轮（手持碗打蛋、蛋液留碗）、第九轮（碗倒空复用）；另注：代码 `LocationKind` 已含 ContainerSlot（`CookingDomain.cs`），本修订是让规范追上代码与行为 |
| `cooking-config-validation.md` | “配置更新必须保持版本边界……迁移策略未获 owner 确认前 MUST 标记 Draft/Blocked” | 落到 v2：v1 配置与快照在 v2 下 blocked 拒绝、无迁移；补 v2 数据模型与校验面 | 既有原则 + 本任务 R1（经 owner 批准本 design 生效） |
| `index.md` | P2 行“R01-R06 fixture loop，27/27”与 P3 行“当前 definition 范围……”及其“未完成范围入口”列 | 更新为“多输入集合 + 两种完成形态契约已修约，运行时规则在后续任务”；P3 行同步 v1→v2 边界 | 本任务（经 owner 批准本 design 生效） |

补充义务（implement.md §5 执行）：每份被修订的 spec 头部留一条带日期与来源的修约记录，避免新条款与既有“2026-09-16 收口状态”声明静默并存。

“容器是带容器能力的物品”这一**一般化表述**没有单独的 owner 裁决原文（09-19 notes 6.2 至今仍把它列为形式问题）；它由上述具体行为裁决复合推出，并随本修约程序一并提交 owner 批准。design 不得把它写成 owner 已单独确认的项。

## 6. 兼容边界与必改生产代码

`CookingRecipeDefinition` 输入由单值改集合是**破坏性签名变更**，必改的生产代码（不是“仅测试构造点”）：

| 位置 | 改动 |
|---|---|
| `CookingConfigurationValidation.cs` `CreateSnapshot` | 逐条重建 recipe 与 item；**必须透传新增的 `Container` 参数**，否则快照静默丢失容器能力 |
| `CookingConfigurationValidation.cs` canonical | `CanonicalRecipe` 增加输入集合、默认供应、完成形态；`CanonicalItem` 增加容器能力 |
| `CookingConfigurationValidation.cs` 校验 | 输入集合外键/非空/去重、默认供应重叠、完成形态、容器能力引用 |
| `CookingRecipeLoop.cs` fixture 构造校验 | 输入集合外键与正 tick 校验 |
| `CookingRecipeLoop.cs` `ValidateProcessForFixedTick` | 单输入成员判定改为 `Input ∈ recipe.Inputs`（过渡语义，任务②引入多输入进程前保持单输入进程） |
| `CookingRecipeLoop.cs` `StartProcess` | 显式配方模式下改为 `item.Definition ∈ recipe.Inputs` |

canonical 归一化规则：可空集合字段（`DefaultInputs`、`Container`）在 canonical 中统一归一化为**空集**，不得有时缺席有时为空——否则语义相同的配置会得到不同 sha256，破坏加载顺序无关性。

v2 过渡期：旧 `Containers` 表与物品级 `Container` 能力在 v2 中并存，`CookingContainerDefinition` 的退役在任务②；过渡期不要求两者交叉校验，但 canonical 同时包含两处，身份自然区别于 v1。

其他兼容边界：

- `CookingItemDefinition` 只尾部追加可选参数：旧 P0 交互仿真 `CookingSimulation` 只消费 `AllowedPlayerCapabilities`，EtBridge 投影不引用该类型，均不受影响（已核实全部构造点为两参位置构造）。
- 归档任务与 `artifacts/` 中的历史 evidence 不回改；新 evidence 只追加。
- `AbilityKit.Game.Cooking.EtBridge.Tests` 不在任何 gate 步骤内；本任务对它零改动，若误改 `CookingItemDefinition` 形状不会被 gate 兜住，需人工核对。

## 7. 验证设计

- v2 身份与 v1 blocked：schema 不等与 sha256 不等两条路径各有测试；v1 候选与 v1 快照都被拒绝、无迁移转换的测试。
- 纯匹配函数：顺序无关（两种输入顺序得到同一匹配结果）、默认供应并入后命中、无命中与多命中的结构化拒绝。
- `Recipe` 字段两种模式：携带时按显式配方校验、不携带时返回 `RecipeNotMatched`；两种命令的 JSON 指纹不同且各自幂等。
- 指纹：既有 golden 向量断言不变（回归锚）+ 新增 recipe-less StartProcess 向量；同一命令在三处得到相同身份结论。
- 回归：`cooking-et-level-runtime` gate 全量通过。

## 8.  Planned gates

- 本任务验收 gate：`cooking-et-level-runtime`（build RelationAnalyzer + build Cooking.EtRuntime + Cooking.Tests 全量 + ET.Runtime.Tests 全量）。
- 新 gate `cooking-kitchen-loop`（P1，`Gate=CookingKitchenLoop`）在任务②引入，本任务只预留 trait 命名，不建 gate。
