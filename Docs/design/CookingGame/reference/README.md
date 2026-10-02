# Cooking 设计参考资料

本目录保存 Cooking 应用在设计讨论中已经确认、但尚未全部进入正式 ADR、行为规范或实现任务的结构化参考资料。

## 资料性质

- 本目录中的所有 Markdown 文件都是**设计参考文件**。
- 参考文件用于统一术语、记录 owner 已确认的产品语义，并帮助后续 task 规划与实现避免重新发明结构。
- 参考文件不表示对应能力已经实现、测试、验证或交付。
- 参考文件不能代替活动 Trellis task 的 PRD、设计、实施清单和检查证据。

## 来源优先级

发生冲突时按以下来源处理：

1. owner 在当前审议中明确作出的最新决定；
2. 已接受的 ADR；
3. `Docs/design/CookingGame/technical-roadmap.md` 中的长期产品路线；
4. `.trellis/spec/cooking/` 中的稳定行为契约；
5. 活动 Trellis task 的已批准范围；
6. 本目录中的设计参考文件。

发现冲突时不得自行合并出新的产品语义，应在 task 中指出并重新审议。

## 文件索引

- [`product-lifetimes.md`](product-lifetimes.md)：Match、Connection、Participant、存档传输、RestaurantRuntime、Kitchen 与 Level 的产品语义和生命周期。
- [`et-entity-tree.md`](et-entity-tree.md)：ET `[ComponentOf]`/`[ChildOf]` 规则、命名约定、目标所有权树与禁止模式。
- [`save-storage.md`](save-storage.md)：PC/Android 存档位置、纯 C# repository 边界、授权副本与写入安全参考。
- [`menu-v0.1/README.md`](menu-v0.1/README.md)：87 款候选菜单的原始 Markdown/Excel、副本 SHA-256 与只读核对范围。
- [`menu-integration.md`](menu-integration.md)：候选菜单映射、批量/阶段/贴单缺口和分批导入规划；未实施，完整功能与 Task 路由见 [总 plan](../../../../.trellis/spec/cooking/gameplay-menu-plan.md)。

## 状态标记

参考文件使用以下词语区分成熟度：

- **已确认**：owner 已明确选择，可作为后续规划输入。
- **现有事实**：由当前代码、测试、ADR 或规范直接证明。
- **建议**：尚未获得 owner 最终确认，不得直接当作实现要求。
- **未决**：必须在对应 task 实施前解决。
