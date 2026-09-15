# P3 数据配置验证：cooking-config-validation

> 迁移状态：**blocked**。本规范由只读来源快照 `.trellis/migration/legacy-cooking-changes/add-cooking-config-validation/specs/cooking-config-validation/spec.md` 转换；原始 SHA-256 见 [迁移清单](../../migration/legacy-cooking-changes/manifest.json)。
>
> 本文件保留待审阅的规则和验收场景，但不使它们自动成为实施批准、测试通过或已归档的事实。开始实现前必须审阅对应 Trellis task 的 PRD、design 和 implement checklist。

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
系统 SHALL 区分配置 DefinitionId 与 Match 内 InstanceId；配置重载或失败不得改变既有运行时实例的身份语义，旧版本兼容/迁移策略未获 owner 确认前 MUST 标记 Draft / Blocked。

#### Scenario: 重载失败保留有效配置
- **WHEN** 新批次反序列化或验证失败且当前已有有效配置
- **THEN** 系统 SHALL 保留旧配置与其身份，报告新批次失败，不得部分替换表

#### Scenario: 旧快照或旧配置版本
- **WHEN** 输入引用不兼容的配置版本或旧快照
- **THEN** 系统 MUST 按尚未批准的迁移策略拒绝或标记 blocked，不得自行发明迁移行为
