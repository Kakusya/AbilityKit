> Current stage reconciliation2026-10-03: authorized S01-S14 pure C# singleplayer scope completed on source4dadd25c8; N01-N03 are authorized next. Owner2026-09-24 decisions select generic LiteNet reliable-UDP as the sole real transport, InProcess only local/test, superseding temporary KCP notes below. Formal decision: [ADR-0002](decisions/0002-authoritative-fixed-tick-state-sync.md). [Actual singleplayer proof](../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-singleplayer-exit-verification.md). Network implementation and physical two-PC LAN remain unverified; Unity/S15 stay deferred. Earlier planning-only/singleplayer-current notices are historical.

> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# 做菜经营游戏：长期目标

更新：2026-09-21。来源：用户 /init 目标及后续纠正。状态：P0–P6 各有已验证并已按受限范围归档的纯 .NET 增量；完整游戏产品出口仍未完成。Cooking Unity 实施在可预见的长期内禁止，非 Unity 后续未启动且须另行批准。

> 2026-09-21 修订：owner 决定当前范围收敛为**单机**，原同机 UDP 经营闭环方向放弃（任务与 gate 已退役，源码与证据保留），传输计划改用 KCP 组件，属未启动、未批准、无时间表的后续工作。本次修订只更新方向表述，不删除历史记录。

## 已确认的方向

2026-10-02 owner 确认：不改变当前架构，按“单机 → 网络 → 单机 Unity → 网络 Unity”顺序缓慢推进，当前仍专注单机；允许列 Task 但不执行，未提细节由助手补足规划并记录。详见 [功能与菜单 plan](../.trellis/spec/cooking/gameplay-menu-plan.md)。此顺序不解除 Unity 禁令，不代表网络已批准；旧 KCP/LiteNet 来源差异见 [规划架构记录](../Docs/design/CookingGame/gameplay-plan-architecture.md)，重新立项前必须协调权威文档。

1. 制作本地运行的做菜经营游戏。
2. **当前范围收敛为单机内容**；局域网合作降为远期方向（传输计划改用 KCP，未启动、未批准、无时间表），公网连接为更远期目标，均不纳入近期验收。
3. 创建房间的电脑同时运行服务器并允许本机玩家参与；其他电脑作为客户端加入。角色决策的唯一归属为 [ADR-0001](decisions/0001-player-host.md)。该决策在联机范围重新立项前继续有效，不因当前单机范围而作废。
4. 本地持续维护长期目标、架构决策和参考资料；Trellis 任务在开始工作时读取相关 ADR、设计文档和项目规范，未确认产品选择必须由 owner 明确。
5. 已确认的做菜经营游戏应用技术路线与优先级见[技术路线图](../Docs/design/CookingGame/technical-roadmap.md)；当前受限交付、非 Unity 后续与禁止的 Unity 范围见[当前进度](../Docs/design/CookingGame/progress.md)。路线不等于完整产品已实现，successor backlog 也不是实施批准。

## 未确认的范围

经营与动作烹饪的比重、首发平台、人数规模、完整离线玩法、存档归属、房主退出语义、断线恢复、无界面独立服务器需求仍未确认。没有将“长期经营为主”视为用户选择。

公网 NAT 穿透、中继、账号与平台大厅均未选型；联机范围本身已推迟，更不代表已承诺自动发现或特定连接方式。

> 2026-09-21 补充：原 LiteNetLib 同机 UDP 开发基线已放弃；KCP 传输方向未启动、未批准、无时间表，不得从本文件推导任何传输实现授权。

## 工程建议（不是批准决策）

- 纯 .NET 领域规则继续保持与表现宿主分离。Cooking Unity package、场景、authoring、projection、UI 与 EditMode 当前长期禁止；重新授权条件见 [`future-scope.md`](../Docs/design/CookingGame/future-scope.md)。
- 先验证最小做菜经营闭环，再验证主机玩家与局域网客户端共同参与；具体玩法、验收场景和实施证据在对应 Trellis task 中审阅、执行和记录。
- Orleans 只是仓库现有宿主示例，不能替代网络技术选型。

## 事实毕业规则

本文件保存方向和范围空白，不充当实施任务清单、行为规范或已实现能力说明。稳定的工程/领域规则进入 `.trellis/spec/`，当前任务的目标、设计、实施清单与检查证据进入 `.trellis/tasks/`；本文件只保留概述或链接，不复制正文。
