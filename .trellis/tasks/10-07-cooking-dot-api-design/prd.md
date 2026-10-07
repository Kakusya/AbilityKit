# Cooking dot 回复中的 API 设计

## 来源与范围

Owner 本轮要求修改 `cooking-dot-worktree`，让询问 dot 的返回内容包含 API 设计。当前 Git/Orca 均只列出 AbilityKit 主工作树，仓库实际技能为 `cooking-dot-workflow`；未找到同名 worktree/技能。已提出可选名称澄清，按现有技能处理。仅修改流程文档，不实际调用 dot、不推送或改写远端、不修改 Cooking runtime 或其他示例。

消费者是 Cooking 流程主会话、dot 和后续执行者。现有缺口：请求模板只要求明确决定，回复完整性判断没有 API 设计要求。

## 允许文件与必要性

- `.agents/skills/cooking-dot-workflow/SKILL.md`：在规划与准备出口指向 API 设计合同。
- `assets/dot-request.md`：明确询问需要 dot 返回的 API 内容。
- `assets/dot-decision.md`：保存原始 API 设计及主会话完整性检查。
- `references/dot-dialogue.md`：唯一回复合同，定义缺项补问和不适用规则。
- `references/operator-example.md`：使未来请求示例与模板一致，保留历史证据。
- 本 task 记录与 context manifests：记录来源、范围和验证。

## 验收与实施

- 规划回复包含具体接口/类型/方法签名、参数和返回 DTO、关键行为及拒绝/错误语义、Cooking 消费者与边界；涉及可变状态、异步、消息或 schema 时按既有合同交代相应所有权/生命周期/幂等/兼容性，不扩大任务需求。
- 最终审阅核对候选 API 与已接受计划，列出差异及可接受性；纯文档或确实不涉及 API 时由 dot 明确写 N/A 和理由。
- 主会话保存原始回复及 API 设计引用，不替 dot 补出缺失设计；缺项沿用同一请求的一次补问及原截止时间，未满足时不进入 Prepare/Implement 或接受候选。
- 不新增等待预算、脚本 schema、自动 API 检查器、外部动作或授权。
- 轻量 PRD 已由主会话按 Owner 修改要求和现有 SOP 审议。顺序：更新唯一合同及模板/入口/未来示例，审阅 diff，再运行技能验证与 Markdown 链接/空白检查。

## 验证范围

文档检查可记 Passed；实时 dot 对话和宿主自动加载为 NotRun，.NET/Unity/物理 LAN 测试 N/A。不提交或归档用户的其他工作。

## 2026-10-07 追加授权：dot 审阅

Owner 后续要求“你可以给 dot 审阅一下”。由本轮原主会话将当前五处文档提交给已核实的 dot 会话，保存完整回复与精确来源。为供 dot 读取固定 GitHub commit，可创建并推送独立任务审阅提交；仅分支审阅，不含 master 合并、发布、其他 Issue 派发或清理。使用独立 Git index 保存本任务文件，不改变当前 master HEAD、真实 index 或其他工作区改动。此为外部技能审阅，不启动整个 supervised worker 流程或接管历史 flow。
