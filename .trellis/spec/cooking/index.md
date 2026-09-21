# 做菜经营游戏规划索引

> 本目录保存 Cooking 的稳定行为契约与七个历史阶段的受限交付边界。2026-09-16 owner 已批准按现有 `check.jsonl` 将七个纯 .NET 增量重划为 `completed-limited-scope` 并归档；这不证明完整 P0–P6 产品出口、Unity、真实 LAN 或 durable storage 已完成。
>
> 2026-09-21 修约：按 spec index“必须重新审议当时契约”的程序，owner 批准 Trellis task `09-21-cooking-kitchen-loop-contracts` 后，对 P2、P3 与 P0 交互基础做了显式契约修约（完成形态与幂等延伸到倒出、位置枚举补 `ContainerSlot`、schema 升 v2）。每次修约都在被修订文件头部留带日期与来源的记录，并区分 owner 决定与经批准 design 生效的实现契约。修约不等于运行时规则已实现。

## 当前状态入口

- [Cooking Game 当前工程进度](../../../Docs/design/CookingGame/progress.md)：archived verified delivery、non-Unity successor backlog 与 prohibited Unity scope 的跨阶段汇总。
- [Cooking Unity future scope](../../../Docs/design/CookingGame/future-scope.md)：长期禁止、不可领取、非 blocker 的 Unity 历史范围及重新授权条件。
- [Cooking non-Unity successor backlog](../../../Docs/design/CookingGame/successor-backlog.md)：P1–P6 尚未启动、未批准、无时间表的后续；P0 无 successor。

## 状态语义

- `archived verified delivery` / `completed-limited-scope`：只表示对应 task 中已有 check evidence 支持的纯 .NET 受限交付完成并可归档。
- `successor backlog`：仍可能有价值但未建立 active task、未批准实施、没有日期的非 Unity 工作；不是旧 task blocker。
- `prohibited Unity scope`：长期禁止实施，不是 backlog、successor 前置或阶段 blocker；旧 projection/authoring 场景只保留宿主无关不变量。
- 原始命令、pass/blocked/not-run 证据仍以各 task 的 `check.jsonl` 为准；closure 只记录 owner 对最终范围的重划。
- `.trellis/migration/legacy-cooking-changes/` 是只读来源快照，不随收口决定改写。

## 能力与交付边界

| 阶段 | 规范 | 历史 task | archived verified delivery | 未完成范围入口 |
|---|---|---|---|---|
| P0 交互基础 | [`cooking-interaction-foundation`](cooking-interaction-foundation.md) | `09-15-cooking-interaction-foundation` | T01-T08 纯 .NET authority/interaction，10/10 | P0 无 non-Unity successor；Unity 见 future scope。2026-09-21 修约：位置枚举补 `ContainerSlot` 与“容器是带容器能力的物品”，不变量不变 |
| P1 会话权威 | [`cooking-lan-session`](cooking-lan-session.md) | `09-15-cooking-lan-session` | L01-L09 transport-neutral contract，权威摘要 20/20 | production transport、真实 LAN、D1-D4 见 successor backlog；Unity 见 future scope |
| P2 配方循环 | [`cooking-recipe-loop`](cooking-recipe-loop.md) | `09-15-cooking-recipe-loop` | R01-R06 fixture loop，27/27 | 2026-09-21 修约：完成形态（`ConsumeInputs`/`RetainInputs`）与幂等延伸到倒出、多输入集合匹配契约已落地；容器即物品、七项动作、闭环 fixture 与运行时规则属后续 task，未启动/未实现。正式内容/order/settlement/真实 LAN 见 successor backlog；Unity 见 future scope |
| P3 配置验证 | [`cooking-config-validation`](cooking-config-validation.md) | `09-15-cooking-config-validation` | 当前 definition 范围，focused 6/6、当时完整 34/34 | 2026-09-21 修约：schema 已升 `cooking-definition-v2`，v1 配置与快照 blocked 拒绝、无迁移；多输入/默认供应/完成形态/物品容器能力的校验面已补齐。正式 schema 推广、host/client compatibility、migration policy 见 successor backlog；Unity 见 future scope |
| P4 Match 生命周期 | [`cooking-match-lifecycle`](cooking-match-lifecycle.md) | `09-15-cooking-match-lifecycle` | fixture lifecycle/snapshot，focused 6/6、当时完整 40/40 | 正式 Room/Level/Map、产品退出语义、真实 LAN 见 successor backlog；Unity 见 future scope |
| P5 持久化技术合同 | [`cooking-persistence-management`](cooking-persistence-management.md) | `09-15-cooking-persistence-management` | in-memory contract，focused 12/12、当时完整 52/52 | durable store、process-crash、存档策略见 successor backlog；Unity 见 future scope |
| P6 网络测量基线 | [`cooking-network-measurement`](cooking-network-measurement.md) | `09-15-cooking-network-measurement` | InProcess baseline，focused 5/5、完整 57/57 | production measurement、真实 LAN、批准优化见 successor backlog；Unity 见 future scope |

后续如获 owner 授权，必须新建 Trellis task 并重新审议当时契约、环境与验证；不得恢复归档 task 或从本文推导实施许可。
