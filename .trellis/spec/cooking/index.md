# 做菜经营游戏规划索引

> 这些文件由 2026-09-15 前的七份未实施规划迁移而来。它们记录待审阅的行为、边界、依赖与验收设想，**不证明功能已实现、任务已启动或测试已通过**。

## 使用方式

- 开始某项工作前，先读取对应 `.trellis/tasks/09-15-cooking-*/prd.md`、`design.md` 和 `implement.md`。
- 任务处于 `planning` 时只能澄清、研究和修订计划；启动实施前必须按 `.trellis/workflow.md` 完成复核。
- `blocked` 是完整能力出口的迁移元数据，不是 Trellis CLI 顶层状态；原因和解除条件见 task 的 PRD。
- `scoped-planning` / `partial-scope-authorized` 仅表示 owner 已允许细化一个受限的规划准备范围；它本身不是实施批准、测试通过、任务完成、归档或下游解锁。已实施范围必须由对应 task 的 checklist 与 `check.jsonl` 的实际命令证据单独证明。
- 遗留来源只读快照在 `.trellis/migration/legacy-cooking-changes/`；迁移清单保存路径、文件哈希和目标映射。

## 能力与任务

| 阶段 | 规范 | Trellis task | 迁移状态 |
|---|---|---|---|
| P0 交互基础 | [`cooking-interaction-foundation`](cooking-interaction-foundation.md) | `09-15-cooking-interaction-foundation` | `planned` |
| P1 LAN listen host/client | [`cooking-lan-session`](cooking-lan-session.md) | `09-15-cooking-lan-session` | 完整出口 `blocked`；L01-L09 transport-neutral .NET contract 已验证，D1 candidate spike 与完整 LAN 仍未运行 |
| P2 一条完整配方 | [`cooking-recipe-loop`](cooking-recipe-loop.md) | `09-15-cooking-recipe-loop` | 完整出口 `blocked`；R01-R06 pure .NET fixture loop 已验证，R07/正式内容/Unity/LAN 仍未运行 |
| P3 数据配置验证 | [`cooking-config-validation`](cooking-config-validation.md) | `09-15-cooking-config-validation` | `blocked` |
| P4 关卡/地图/Match 生命周期 | [`cooking-match-lifecycle`](cooking-match-lifecycle.md) | `09-15-cooking-match-lifecycle` | `blocked` |
| P5 持久化经营管理 | [`cooking-persistence-management`](cooking-persistence-management.md) | `09-15-cooking-persistence-management` | `blocked` |
| P6 响应性与网络测量 | [`cooking-network-measurement`](cooking-network-measurement.md) | `09-15-cooking-network-measurement` | `blocked` |
