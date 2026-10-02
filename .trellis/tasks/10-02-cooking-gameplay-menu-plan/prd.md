> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# Cooking 功能基线与菜单分阶段规划

2026-10-02；最新 owner 授权：实施总 plan 的单机 S01–S14 与网络 N01–N03，使用 Orca/worktree 并行、逐批验证并合并 master。Unity U01/U02 后置不执行；S15 保持扩展审议池。下列段落保留最初规划记录，涉及“未授权实施”的句子由本条更新；新增交付验收见 execution.md，不以规划检查冒充产品通过。

## 目标

把 owner 提供的 A–K 功能清单和87款候选与现有能力逐项审议，沿当前架构实施S01–S14完整单机玩法及N01–N03网络闭环，验证后合并本地master。记录功能、架构、Task和真实证据，避免遗失决定、重复实现或把局部通过当作完整交付。

## 已确认授权

- 不改变当前架构；当前仍专注单机。
- 顺序为单机、网络、单机 Unity、网络 Unity。
- 最新明确授权是全部议题审议后实施直至完成；未讨论细节交由助手补足并及时落盘，不重复请求常规确认。
- 更新 AGENTS.md 路由；在 D:/MyDownload 搜索并保存菜品资料。
- 87 款是完整候选而非首关/MVP，不以冷热糖度杯型重复计数；创新和复杂操作小游戏不进入基础。

## 交付范围

1. A01–K14 全部 124 项的状态、来源与后续归属；额外机制及餐厅扩建单列。
2. 两个原始附件及来源摘要，编号核对；87 菜映射、共享准备、工序和容器差异的导入规划。
3. 保持纯 C# 权威/固定 Tick/ET 宿主结构，记录新能力 owner、snapshot/checkpoint 边界和旧来源冲突。
4. 保留总plan和20个Task，维护依赖、设计、实施清单、上下文及真实执行状态。S01–S14/N01–N03经审阅后按依赖实施；S15参考池、U01/U02独立执行门保持。
5. AGENTS、spec index、reference index、路线/进度入口与 Todo 指向对应正文。
6. 完整产品行为与验证按 research/completion-contract.md 逐项证明，包括全部87款的公开命令制作交付、ET营业/供应/布局及恢复，并进一步验证完整网络状态和真实拓扑。

## 验收

- A–K 编号唯一且无漏项；记录已有领域、部分、待实现、后置，不把表现或真实网络标为已交付。
- 原始附件 SHA-256 与下载文件一致；Markdown 87 个独立菜品行和 Excel 87 个编号完整覆盖 F01–F44/S01–S12/D01–D31。
- Task真实状态与实施授权一致；依赖无环、引用可解析；不得从状态文件推导功能完成。
- 四阶段顺序明确，Unity独立执行门保持；生产/schema/宿主变更与对应spec及实际测试一起记录。
- 产品细节在总 plan 和菜单整合中可追溯；架构记录不建立平行 roadmap。
- 文档链接与上下文清单检查通过；测试报告区分本轮静态检查与历史领域测试证据。

## 范围外与后续

不解除Unity禁令，不增加创新“食客也是食材”、复杂操作小游戏或S15参考池机制，不实现采购经济/正式平衡/主机迁移/durable-storage扩展。产品验证使用显式fixture数值，完整候选不是首关菜单。网络遵循已确认通用LiteNet reliable-UDP方向，不能悄悄重选传输。

## 产物

- [总 plan](../../spec/cooking/gameplay-menu-plan.md)
- [功能审计](research/feature-audit.md)
- [架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md)
- [Task 注册表](task-register.md)
- [菜单整合](../../../Docs/design/CookingGame/reference/menu-integration.md)
- [源数据核对](research/source-audit.json)

初始规划与暂停属于历史；本次owner明确要求审议后执行。已完成的审阅和规则裁定见 research/final-review.md，执行回执见execution.md，当前无须再问同样的授权、架构或阶段边界问题。
