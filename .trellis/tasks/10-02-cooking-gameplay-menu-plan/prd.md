# Cooking 功能基线与菜单分阶段规划

2026-10-02；最新 owner 授权：实施总 plan 的单机 S01–S14 与网络 N01–N03，使用 Orca/worktree 并行、逐批验证并合并 master。Unity U01/U02 后置不执行；S15 保持扩展审议池。下列段落保留最初规划记录，涉及“未授权实施”的句子由本条更新；新增交付验收见 execution.md，不以规划检查冒充产品通过。

## 目标

把 owner 提供的 A–K 功能清单和 87 款菜品候选，与仓库现有单机能力逐项比对；形成可跨会话读取的功能、架构和 Task 记录，后续缓慢推进，避免遗失产品决定或重复实现。

## 已确认授权

- 不改变当前架构；当前仍专注单机。
- 顺序为单机、网络、单机 Unity、网络 Unity。
- 可以列出真实 Task，但不执行；未讨论细节交由助手作规划决定并及时落盘。
- 更新 AGENTS.md 路由；在 D:/MyDownload 搜索并保存菜品资料。
- 87 款是完整候选而非首关/MVP，不以冷热糖度杯型重复计数；创新和复杂操作小游戏不进入基础。

## 交付范围

1. A01–K14 全部 124 项的状态、来源与后续归属；额外机制及餐厅扩建单列。
2. 两个原始附件及来源摘要，编号核对；87 菜映射、共享准备、工序和容器差异的导入规划。
3. 保持纯 C# 权威/固定 Tick/ET 宿主结构，记录新能力 owner、snapshot/checkpoint 边界和旧来源冲突。
4. 建立总 plan 与 20 个只规划 Task，含依赖、目标、验收、范围外、执行门和初始上下文；不把初稿宣称实施就绪。
5. AGENTS、spec index、reference index、路线/进度入口与 Todo 指向对应正文。

## 验收

- A–K 编号唯一且无漏项；记录已有领域、部分、待实现、后置，不把表现或真实网络标为已交付。
- 原始附件 SHA-256 与下载文件一致；Markdown 87 个独立菜品行和 Excel 87 个编号完整覆盖 F01–F44/S01–S12/D01–D31。
- 所有新 Task 保持 planning、planning_only=true、implementation_authorized=false；依赖无环、引用可解析；没有启动产品 Task。
- 四阶段顺序明确，Unity 仍 prohibited；当前没有生产代码/协议/schema/宿主变化。
- 产品细节在总 plan 和菜单整合中可追溯；架构记录不建立平行 roadmap。
- 文档链接与上下文清单检查通过；测试报告区分本轮静态检查与历史领域测试证据。

## 范围外与后续

不实现玩法、不导入 runtime 87 菜、不改生产配置、不重新选传输、不解除 Unity 禁令、不运行场景或产品测试。正式平衡值、采购经济、关卡菜单按独立内容 Task 逐批确定。具体新增 API/错误矩阵/迁移策略在对应 Task 执行前收敛。

## 产物

- [总 plan](../../spec/cooking/gameplay-menu-plan.md)
- [功能审计](research/feature-audit.md)
- [架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md)
- [Task 注册表](task-register.md)
- [菜单整合](../../../Docs/design/CookingGame/reference/menu-integration.md)
- [源数据核对](research/source-audit.json)

规划中的默认规则已由助手按 owner 授权补足；未将初始请求视为执行批准。当前无须再问同样的架构/阶段边界问题。
