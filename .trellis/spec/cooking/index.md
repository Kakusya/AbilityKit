# 做菜经营游戏规划索引

> 本目录保存 Cooking 的稳定行为契约与七个历史阶段的受限交付边界。2026-09-16 owner 已批准按现有 `check.jsonl` 将七个纯 .NET 增量重划为 `completed-limited-scope` 并归档；这不证明完整 P0–P6 产品出口、Unity、真实 LAN 或 durable storage 已完成。
>
> 2026-09-21 修约：按 spec index“必须重新审议当时契约”的程序，owner 批准 Trellis task `09-21-cooking-kitchen-loop-contracts` 后，对 P2、P3 与 P0 交互基础做了显式契约修约（完成形态与幂等延伸到倒出、位置枚举补 `ContainerSlot`、schema 升 v2）。每次修约都在被修订文件头部留带日期与来源的记录，并区分 owner 决定与经批准 design 生效的实现契约。修约不等于运行时规则已实现。
> 2026-09-21 二次修约：owner 批准 Trellis task `09-21-cooking-kitchen-loop-simulation` 后，容器即物品、七项动作、放入即拒绝、锁输入、端走继续、两种完成形态、闭环 fixture、订单要求、碗池与批次争抢仲裁已在单机纯 C# 范围实现并验证（证据见 task `check.jsonl` 与 `artifacts/cooking-kitchen-loop-domain/`）；前厅、小关时间结构、失败条件与 Unity 仍范围外。
> 2026-09-21 三次修约：owner 批准 Trellis task `09-21-cooking-formal-content-and-orders` 后，正式 Recipe/Process/Appliance/Container/Order 内容与 timing（数据驱动 `cooking-definition-v2` 内容目录）、order owner（订单簿、开单注入、提交/结算契约）已在单机纯 C# 范围实现并验证（证据见 task `check.jsonl` 与 `artifacts/cooking-formal-content/`）；评分/收益/评价、失败条件、前厅订单生成节奏、Level/Map schema 对内容的正式引用与 Unity 仍范围外。
> 2026-09-21 四次修约：owner 批准 Trellis task `09-21-cooking-et-closed-loop-acceptance` 后，ET fixed-tick Level 宿主（`CookingLevelEtHost`）已承载同一条番茄蛋花汤闭环验收——fixture 与标准初始供应来自正式内容目录，玩家动作只经宿主命令 ingress 与固定 Tick 驱动，开单/清洗完成经帧间注入缝隙，拒绝零变更以“命令级版本相等 + 对照臂 canonical 相等”两级证明，两遍重放 canonical/Sha256 一致（证据见 task `check.jsonl` 与 `artifacts/cooking-et-closed-loop/`）；`CookingContent` 增露加载时经 v2 校验的快照供 Level 生命周期消费。宿主与领域闭环的计数器差异（终态 `LogicalTick` 27 vs 8）是显式口径，不是回归。评分、失败条件、前厅、传输与 Unity 仍范围外。

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
| P2 配方循环 | [`cooking-recipe-loop`](cooking-recipe-loop.md) | `09-15-cooking-recipe-loop` | R01-R06 fixture loop，27/27 | 2026-09-21 修约：完成形态（`ConsumeInputs`/`RetainInputs`）与幂等延伸到倒出、多输入集合匹配契约已落地；容器即物品、七项动作、放入即拒绝、锁输入、端走继续、闭环 fixture、订单要求、碗池与批次争抢仲裁已由 task `09-21-cooking-kitchen-loop-simulation` 实现并验证（`Plate` 已退役，由放入/倒出取代）。2026-09-21 三次修约：正式内容目录（数据驱动、经 v2 校验）与 order owner（订单簿、开单注入、提交/结算契约）已由 task `09-21-cooking-formal-content-and-orders` 实现并验证；评分/收益/评价、失败处理、前厅订单生成节奏与 Level/Map schema 正式引用见 successor backlog；Unity 见 future scope。2026-09-21 四次修约：ET fixed-tick Level 宿主承载同一条番茄蛋花汤闭环验收（正式内容 fixture、宿主命令路径与固定 Tick、帧间注入、拒绝零变更两级证明、确定性重放）已由 task `09-21-cooking-et-closed-loop-acceptance` 实现并验证；宿主与领域闭环计数器差异为显式口径 |
| P3 配置验证 | [`cooking-config-validation`](cooking-config-validation.md) | `09-15-cooking-config-validation` | 当前 definition 范围，focused 6/6、当时完整 34/34 | 2026-09-21 修约：schema 已升 `cooking-definition-v2`，v1 配置与快照 blocked 拒绝、无迁移；多输入/默认供应/完成形态/物品容器能力的校验面已补齐；独立容器表退役（容器能力即物品定义字段），canonical recipe 段新增 `RequiresStation`，身份字符串保持 v2。2026-09-21 三次修约（随正式内容落地）：候选/canonical 增加订单模板与标准初始供应两段（身份字符串仍保持 v2）；校验面补齐订单模板外键（要求 recipe 存在、要求容器定义存在、带容器能力且接受该 recipe 产物）与供应项校验（定义存在、数量为正、位置语法、station 引用）；两处既有规则显式放宽——工位可声明零能力（台面）、免工位配方豁免 `CapabilityUnavailable`。正式 schema 推广、host/client compatibility、migration policy 见 successor backlog；Unity 见 future scope |
| P4 Match 生命周期 | [`cooking-match-lifecycle`](cooking-match-lifecycle.md) | `09-15-cooking-match-lifecycle` | fixture lifecycle/snapshot，focused 6/6、当时完整 40/40 | 正式 Room/Level/Map、产品退出语义、真实 LAN 见 successor backlog；Unity 见 future scope |
| P5 持久化技术合同 | [`cooking-persistence-management`](cooking-persistence-management.md) | `09-15-cooking-persistence-management` | in-memory contract，focused 12/12、当时完整 52/52 | durable store、process-crash、存档策略见 successor backlog；Unity 见 future scope |
| P6 网络测量基线 | [`cooking-network-measurement`](cooking-network-measurement.md) | `09-15-cooking-network-measurement` | InProcess baseline，focused 5/5、完整 57/57 | production measurement、真实 LAN、批准优化见 successor backlog；Unity 见 future scope |

后续如获 owner 授权，必须新建 Trellis task 并重新审议当时契约、环境与验证；不得恢复归档 task 或从本文推导实施许可。
