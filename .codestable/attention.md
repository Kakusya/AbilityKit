# 项目注意事项

供每次会话读取的少量指针；不复制规格、设计或任务正文。

- 所有工作流先遵守根 `AGENTS.md` 的统一知识入口；OpenSpec 与 CodeStable 都须按当前范围读取相关 lessons、specs 和活动 change。
- 游戏长期方向与尚未明确的范围：`ADR/long-term-goals.md`。不要把目标当成现有能力。
- 已确认的玩家主机模式：`ADR/decisions/0001-player-host.md`。
- 行为契约唯一归属 `openspec/specs/`；变更提议、差量和实施清单唯一归属 `openspec/changes/`。不要创建平行 requirements/features。
- 框架设计与源码边界：根 `AGENTS.md`、`Docs/design/`；协议工作先读 `Protocols/README.md`。
- 工程经验放 `lessons/`，需按任务关键词检索；已被测试或正式文档承接的结论改为指针或退役，不与规格竞争。
- `work/` 仅保存必要的跨会话恢复状态和证据指针，不复制活动 change 的任务清单。融合细则见 `ADR/reference/workflow-integration.md`。

- 维护/功能路由遵守根 `AGENTS.md` 的“工作分工”；文档入口为 `cs`，不恢复已移除的功能开发技能。
