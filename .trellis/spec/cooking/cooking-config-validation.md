# P3 数据配置验证：cooking-config-validation
## 2026-09-21 容器即物品修约（任务 09-21-cooking-kitchen-loop-simulation）

来源：owner 已确认结构“容器是带容器能力的 Item”（ET 参考 product-lifetimes.md §6）在任务②落地为配置形状。本次修约把容器从“与物品并列的第二身份”改为“物品定义的容器能力”，并新增配方的工位要求字段。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| 候选/快照/canonical 的 `Containers` 段 | `CookingContainerDefinition`（ContainerId + Capacity）独立表，候选必须声明 | 退役：容器能力由 `CookingItemDefinition.Container` 声明（容量 + 可接受物品定义集合），候选、快照与 canonical 不再有独立 containers 段；canonical 的 recipe 段新增 `RequiresStation` | owner 已确认结构（容器即物品）＋任务② design §1/§3；canonical 增加字段但保持 `cooking-definition-v2` 身份字符串（任务①决定：v1 已封死，v2 内部形状扩展不升版本） |
| 诊断表 “Container” | 容器 ID 非空/重复/容量为正的结构诊断 | 删除；同类问题改由 ItemDefinition 的 `Container.Capacity` 与 `Container.AcceptedDefinitions` 诊断覆盖（含两遍序无关校验，任务① V11 回归保留） | 同上 |
| 布局容器校验 | `CookingLogicalLayout.Containers` 引用 `ContainerId`，对照配置容器表 | 布局声明容器物品**定义**（`DefinitionId`），校验“定义存在且带容器能力”，`ContainerNotFound` reason 保留 | 任务② design §7（连带处理布局与生命周期 reason 映射） |

## 2026-09-21 契约修约

来源：既有“迁移策略未获 owner 确认前 MUST 标记 Draft / Blocked”原则 + Trellis task `09-21-cooking-kitchen-loop-contracts` 的 R1，经 owner 批准该 task 的 `design.md` 后生效。本次修约把 v1→v2 的版本边界从原则落到具体条款，不写迁移、不静默转换；也不代表正式 schema、host/client compatibility 或 durable storage 已完成。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「配置更新必须保持版本边界」 | “旧版本兼容/迁移策略未获 owner 确认前 MUST 标记 Draft / Blocked” | 落到 v2：`CurrentSchema = "cooking-definition-v2"`；v1 配置与 v1 快照在 v2 下以结构化 blocked 诊断拒绝（schema 不等 → `MigrationPolicyNotApproved`，sha256 不等 → `IdentityMismatch`），无迁移转换路径 | 既有原则 + 本 task R1（经 owner 批准 design.md 生效） |
| Requirement「配置加载必须在提交前完成一致性验证」 | 只覆盖单输入配方与独立容器表 | 补齐多输入集合、默认供应、完成形态与物品容器能力的校验面：多输入外键缺失、输入集合为空、输入重复、默认供应与物品输入重叠、默认供应外键缺失、完成形态取值非法、容器能力引用缺失、容量非正；诊断仍一次性收集且确定性排序 | 本 task R1（经 owner 批准 design.md 生效） |

v2 数据模型（契约级）：`CookingItemDefinition` 尾部追加可选 `Container { Capacity, AcceptedDefinitions }`；`CookingRecipeDefinition.InputDefinition` 单值改为 `Inputs` 集合（非空、无重复），并新增可选 `DefaultInputs` 与 `Completion`（`ConsumeInputs` | `RetainInputs`）。`Inputs` 与 `DefaultInputs` 不得重叠：同一输入不能既是物品输入又是默认供应。默认供应不占物品、不占容量，但参与配方匹配与 canonical identity。独立的 `CookingContainerDefinition` 表在 v2 过渡期与物品级 `Container` 能力并存，其退役属后续任务。

升级后仍保持“新增配方/容器/工位只需数据扩展，不为单条内容增加规则分支”。

## 2026-09-16 收口状态

- 当前 definition 范围的纯 .NET registry/validation/hash 增量已验证并作为 limited delivery 收口；正式 schema、compatibility 与 migration policy 等仍未启动，见 successor backlog。
- 对应 `09-15-cooking-*` task 已按 `completed-limited-scope` 语义归档；`completed` 不表示完整 P3 或完整 P0-P6 产品出口。
- Cooking Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 scene smoke 长期禁止实施；原 Unity 场景及宿主无关不变量统一见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)。
- 本文以下 authority、identity、atomicity、sequence、stale-input、persistence 或 measurement 行为不变量继续有效；未完成的非 Unity 范围不得写成已实现，P1-P6 入口见 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md)。

> 交付状态：**completed-limited-scope**；原完整能力迁移状态为 `blocked`。本规范由只读来源快照 `.trellis/migration/legacy-cooking-changes/add-cooking-config-validation/specs/cooking-config-validation/spec.md` 转换；原始 SHA-256 见 [迁移清单](../../migration/legacy-cooking-changes/manifest.json)。
>
> 本文件保留行为规则与历史场景；只有顶部列明的 pure .NET limited delivery 已验证。未完成 non-Unity 工作见 successor backlog，Unity 见 prohibited future scope。

## 迁移边界

- 依赖：P2 的实际 Recipe/Process/Appliance/Level 数据类型。
- 阻塞：旧 config/snapshot 迁移策略与相关 owner 决策尚未确认。
- 规划验证：C01-C08（均为 future 规划，尚未执行）

## 迁移的行为草案

## Purpose

为阶段 3 产生的 cooking 数据建立开局前的可诊断验证与兼容门，确保新增菜谱或厨具能够通过同一数据驱动流程，而非法引用和不兼容配置不会启动会话。

## ADDED Requirements

### Requirement: 配置加载必须在提交前完成一致性验证
系统 SHALL 在配置批次提交前验证唯一 ID、必需字段、跨表外键、厨具能力与工序要求、容器/产物引用、配方拓扑及 Level 引用；任一失败 MUST 拒绝该批次并保留上一有效配置或明确禁止启动。

#### Scenario: 合法配置批次
- **WHEN** Item、Ingredient、Appliance、Process、Recipe 与 Level 的引用和拓扑均合法
- **THEN** 系统 SHALL 原子提交可查询配置，并产生可审阅的成功结果与配置身份

#### Scenario: 缺失外键或非法拓扑
- **WHEN** 配置包含缺失引用、重复 ID、循环/断裂工序、能力不匹配或无效产物
- **THEN** 系统 MUST 返回指出表、记录和关系的诊断结果，且不得提交部分批次或允许其启动

### Requirement: 增加菜谱或厨具应只需数据扩展
系统 SHALL 让符合既有 schema 的新增 Recipe 或 Appliance 通过同一加载、校验和运行时查询路径生效，不要求为单条内容增加专用规则代码；验证失败 MUST 仍阻断该数据批次。

#### Scenario: 仅修改数据增加可用菜谱与厨具
- **WHEN** 实施者新增一条引用既有 Ingredient/Process/Container 的 Recipe，或新增具备既有能力枚举的 Appliance，且不修改规则代码
- **THEN** 系统 SHALL 在同一 future fixture runner 中加载并验证该数据，并允许其进入阶段 3 recipe loop 验收

#### Scenario: 数据扩展使用未知能力
- **WHEN** 新 Appliance 声明运行时未支持的能力或 Recipe 引用不存在的 Process
- **THEN** 系统 MUST 给出稳定兼容/引用错误，不得以默认能力放行或产生运行时半成品

### Requirement: 配置身份必须支持启动兼容判断
系统 SHALL 为已提交配置生成稳定且可比较的身份/hash；需要协同的 host/client MUST 在 gameplay binding 或启动前拒绝不兼容配置，并输出双方身份和失败原因。

#### Scenario: Host 与 client 配置一致
- **WHEN** 双方使用相同有效配置身份并满足协议/能力窗口
- **THEN** 系统 SHALL 允许进入后续会话阶段并声明配置兼容

#### Scenario: Config hash 不一致
- **WHEN** host 与 client 配置 hash 不同或配置身份版本不可接受
- **THEN** 系统 MUST 拒绝启动/加入，不得先进入 gameplay 再静默纠正

### Requirement: 配置更新必须保持版本边界
系统 SHALL 区分配置 DefinitionId 与 Match 内 InstanceId；配置重载或失败不得改变既有运行时实例的身份语义。当前 schema 为 `cooking-definition-v2`：schema 不等 MUST 以 `MigrationPolicyNotApproved` 拒绝，同 schema 下 sha256 不等 MUST 以 `IdentityMismatch` 拒绝；在 owner 确认迁移策略前 MUST NOT 提供任何 v1→v2 转换路径，也不得自行发明迁移行为。

#### Scenario: 重载失败保留有效配置
- **WHEN** 新批次反序列化或验证失败且当前已有有效配置
- **THEN** 系统 SHALL 保留旧配置与其身份，报告新批次失败，不得部分替换表

#### Scenario: 旧快照或旧配置版本
- **WHEN** 输入引用 `cooking-definition-v1` 或更早的配置版本或旧快照
- **THEN** 系统 MUST 以 `MigrationPolicyNotApproved` 结构化拒绝并输出双方身份与失败原因，不得静默转换或先进入 gameplay 再纠正
