# 本地产品目标与 ADR

- [长期目标](long-term-goals.md)：用户明确的方向；不是已批准的功能排期。
- `decisions/`：只存真正的架构决策，按 `NNNN-short-name.md` 编号，使用 Proposed / Accepted / Superseded 状态。
- [参考与安装](reference/README.md)：Trellis 的来源、版本、项目集成与遗留资料边界。
- [做菜经营游戏技术路线](../Docs/design/CookingGame/technical-roadmap.md)：已确认的应用层路线唯一正文；不替代行为草案或实施任务。
- [做菜规划 Trellis 索引](../.trellis/spec/cooking/index.md)：从旧规划迁移的待审阅阶段规范与任务入口；全部仍未实施。
- [ADR-0002：权威固定 Tick 与首阶段状态同步](decisions/0002-authoritative-fixed-tick-state-sync.md)：已接受的应用架构取舍。

本目录不替代 `Docs/design/` 的框架设计文档。涉及框架机制时引用既有设计，涉及本游戏产品方向时维护本目录，避免复制。

新 ADR 至少包含：状态、日期、上下文、决策、替代方案、影响、相关任务/测试链接。未确认的网络库、存档归属和主机迁移策略不能写成 Accepted。
