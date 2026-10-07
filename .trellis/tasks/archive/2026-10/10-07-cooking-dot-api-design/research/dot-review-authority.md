# 本轮外部技能审阅授权

Owner 原要求修改 Cooking dot 技能并增加 API 设计要求；后续明确要求交 dot 审阅。本轮原主会话 codex_01a115c0-9e42-7893-9f54-502ec4f0f82b 继续协调，真实终端 term_29fb1099-639c-4a9f-b517-c9e5c4a01279 位于 D:/MyWorkTree/AbilityKit，execution host local。Orca 当前只列出本仓库主工作树和该 Codex 终端，没有启动其他执行者。

dot 会话来自本轮 live tab 枚举，并与历史技能审阅的 conversation URL 一致： https://chatgpt.com/dots/01a10258-c084-74bb-8bcc-ec01958e5023 。page 77075ca3-7da4-4890-8bd5-002b9ef6a07e；后续每条浏览器命令显式绑定此 page。既有 Issue #13 对话只作为消息边界，不读取为本轮实施要求。

授权操作：冻结并发布本任务文档分支，向 dot 发送一次有明确 request ID/全 SHA 的只读审阅请求，按原 30 分钟预算及一次补问上限等待，保存原始回复并解释审阅决定。dot 只对话和读取 commit，禁止执行代码或修改 GitHub。API/runtime 和生产交付不在本轮范围。完整技能验证仍因本机缺少 PyYAML Blocked，必须向 dot 如实呈现。

这是 review-only external-skill-review，延续历史外部技能审阅记录形态；不是完整 cooking-dot-workflow 的显式启动，没有 Run/worker 派发、normal-Run 接管或旧 flow 续写。
