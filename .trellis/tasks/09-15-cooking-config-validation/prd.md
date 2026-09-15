# P3 数据配置验证

## 当前实施状态

- Trellis task status：`in_progress`；完整 P3 迁移元数据仍为 `blocked`。
- 已实现并验证受限的纯 .NET definition 配置批次：`CookingItemDefinition`、`CookingApplianceDefinition`、`CookingRecipeDefinition` 与 `CookingContainerDefinition` 的候选构建、全量诊断、原子提交、definition-only identity/hash 和未批准 schema migration 的显式 blocked 结果。
- 已实际通过 C01、C02、C04、C05、C07（仅 blocked/rejected 行为）与 C08 的当前可表达部分；结果不代表正式菜谱、完整 P3 或联机验收完成。
- 依赖：当前 P2 实际 fixture 定义。Process 图、Level 表、正式内容/订单/结算 owner、C03 图拓扑与 C06 host/client binding 仍未落地。
- 阻塞：旧 config/snapshot 的实际迁移策略与相关 owner 决策尚未确认；本轮不会转换旧数据。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-config-validation/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。

## 迁移的需求说明

## Why

阶段 3 的 recipe/process 数据需要在开局前可诊断地验证，否则外键缺失、厨具能力不匹配或非法拓扑会在运行时变成不可解释的失败。阶段 4/P3 扩展菜谱与厨具主要数据，但不大量生产内容；本 change 建立加载、引用校验、启动阻断和 host/client 配置 hash 兼容契约。

## What Changes

- 新增 Item、Ingredient、Appliance、Process、Recipe、Level 等 cooking 配置目录的最小验证契约，具体内容以阶段 3 实际数据类型为输入。
- 在提交/启动前执行外键、厨具能力、工序拓扑、容器/产物、Level 引用及重复 ID 校验，失败 MUST 保持上一版本配置可用或明确阻断启动。
- 生成稳定的配置身份/hash，并在需要 host/client 协同时拒绝不兼容配置；不决定版本迁移和旧快照保留策略。
- 提供至少一个真实可操作的验收：新增 recipe 或 appliance 仅修改数据并通过同一运行时校验/闭环 runner，不创建通用配方编辑器或大量内容。
- 明确阶段依赖：配置校验依赖阶段 3/P2 的实际数据类型；若仅做纯 C# 加载/校验可独立验证，联机 hash/启动验收需阶段 2/P1 handshake seam；阶段顺序出口仍保留 P1 LAN/决策门。
- 后续阶段 5/P4 依赖本 change 的可用 config/layout 校验；阶段 6/P5 与阶段 7/P6 只作为后续依赖指针，不假定其文件存在。

## Capabilities

### New Capabilities
- `cooking-config-validation`: 定义 cooking 配置加载、跨表引用/能力/拓扑校验、版本 hash 和不兼容启动阻断。

### Modified Capabilities
- 无；阶段 3/P2 仍为规划，不复制或修改其未毕业契约。

## Impact

- 未来影响应用层 cooking config registry、validator、启动 compatibility gate 与测试 fixtures；复用通用 ConfigDatabase 的加载/构表能力，不把 Item/Recipe 等业务表加入通用框架默认目录。
- 实施时检查 `Unity/Packages/` 应用包、对应 .NET Compile Include 与 Unity `.asmdef`；未知测试宿主路径标为 future，不声称存在或已通过。
- 跨阶段 owner 未决事项按 [ADR/long-term-goals.md](../../../ADR/long-term-goals.md) 与 [delivery-plan.md](../../../Docs/design/CookingGame/delivery-plan.md) 的 owner 指针处理。
