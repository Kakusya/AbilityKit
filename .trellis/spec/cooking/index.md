> Current pure C# singleplayer completion: [master-singleplayer-exit-verification.md](../../tasks/10-02-cooking-gameplay-menu-plan/research/master-singleplayer-exit-verification.md), source4dadd25c8 final644/772/299 and772/299 gates. N01-N03 are next under owner authorization; [current network contract](../../tasks/10-02-cooking-network-contract-review/design.md). Historical baselines below retain their original scope; Unity and optional S15 remain deferred.

> Current verified source07bb27d54 uses definition3 /Recipe5 /Level8 and typed major baseline3. Required HostFrameSequence preserves durable restart clock; typed2/legacy1 reject via ReadBaseline. Historical format2 proof below does not establish current Host loading. [Actual master proof](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-durable-natural-verification.md). Natural successor cold continuation and narrowed carry remain open.

> Scoped menu/current generation continuation: sourcefa2f60e17, [menu authorization contract](cooking-menu-catalog.md#scoped-menu-ready-and-runtime-authorization-2026-10-03). Read current progress and parent task evidence before interpreting historical planning or format notices. Complete natural S14 and durable Host restart remain open.

> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# 做菜经营游戏规划索引

## 最高优先级范围（2026-10-05）

Owner 重申：只做 Cooking，Shooter、MOBA、Orleans 等其他示例不得修改。范围规则以 [AGENTS](../../../AGENTS.md) 为准，先于下方历史通知、Issue 计划和调度；共享框架／工具仅允许为已批准的 Cooking 需求做必要改动。示例的缺陷或门禁失败不能成为扩张工程范围的理由。

## 2026-10-02 功能与菜单规划入口

Owner 确认不改架构，按“单机 → 网络 → 单机 Unity → 网络 Unity”推进，当前专注单机，只登记 Task 不执行。见 [功能与菜单总 plan](gameplay-menu-plan.md)：124 项功能对照、87 款候选菜单整合、20 个 planning Task 与架构记录。Unity Task 为条件性禁止执行记录，未解除禁令；新规划不改下列生效历史契约，也不代表内容已导入。现有 planning 与 reference 路由见总 plan。

> 本目录保存 Cooking 的稳定行为契约与七个历史阶段的受限交付边界。2026-09-16 owner 已批准按现有 `check.jsonl` 将七个纯 .NET 增量重划为 `completed-limited-scope` 并归档；这不证明完整 P0–P6 产品出口、Unity、真实 LAN 或 durable storage 已完成。
>
> 2026-09-21 修约：按 spec index“必须重新审议当时契约”的程序，owner 批准 Trellis task `09-21-cooking-kitchen-loop-contracts` 后，对 P2、P3 与 P0 交互基础做了显式契约修约（完成形态与幂等延伸到倒出、位置枚举补 `ContainerSlot`、schema 升 v2）。每次修约都在被修订文件头部留带日期与来源的记录，并区分 owner 决定与经批准 design 生效的实现契约。修约不等于运行时规则已实现。
> 2026-09-21 二次修约：owner 批准 Trellis task `09-21-cooking-kitchen-loop-simulation` 后，容器即物品、七项动作、放入即拒绝、锁输入、端走继续、两种完成形态、闭环 fixture、订单要求、碗池与批次争抢仲裁已在单机纯 C# 范围实现并验证（证据见 task `check.jsonl` 与 `artifacts/cooking-kitchen-loop-domain/`）；前厅、小关时间结构、失败条件与 Unity 仍范围外。
> 2026-09-21 三次修约：owner 批准 Trellis task `09-21-cooking-formal-content-and-orders` 后，正式 Recipe/Process/Appliance/Container/Order 内容与 timing（数据驱动 `cooking-definition-v2` 内容目录）、order owner（订单簿、开单注入、提交/结算契约）已在单机纯 C# 范围实现并验证（证据见 task `check.jsonl` 与 `artifacts/cooking-formal-content/`）；评分/收益/评价、失败条件、前厅订单生成节奏、Level/Map schema 对内容的正式引用与 Unity 仍范围外。
> 2026-09-21 四次修约：owner 批准 Trellis task `09-21-cooking-et-closed-loop-acceptance` 后，ET fixed-tick Level 宿主（`CookingLevelEtHost`）已承载同一条番茄蛋花汤闭环验收——fixture 与标准初始供应来自正式内容目录，玩家动作只经宿主命令 ingress 与固定 Tick 驱动，开单/清洗完成经帧间注入缝隙，拒绝零变更以“命令级版本相等 + 对照臂 canonical 相等”两级证明，两遍重放 canonical/Sha256 一致（证据见 task `check.jsonl` 与 `artifacts/cooking-et-closed-loop/`）；`CookingContent` 增露加载时经 v2 校验的快照供 Level 生命周期消费。宿主与领域闭环的计数器差异（终态 `LogicalTick` 27 vs 8）是显式口径，不是回归。评分、失败条件、前厅、传输与 Unity 仍范围外。
> 2026-09-24 评分增量：归档 task `09-24-cooking-scoring-network-slice` 已实现订单模板固定基础分、结算总分、配置阈值 0–3 星和 Host/Client 快照 SHA-256 共识；未满足订单 0 分且不倒扣，0 星仍自然完成。收益、评价、长期进度奖励与复杂评分平衡仍范围外。
> 2026-09-29 二十次修约：owner 批准 Trellis task `09-29-cooking-singleplayer-multi-order-menu` 后，正式内容增加盘子与烤面包订单；单机前厅按 Level-local 到店序号临时固定轮换汤/面包，菜单和顾客实际模板进入 snapshot/canonical/SHA-256 与 checkpoint v3，同 Level ET 恢复继续同一序列。汤 100、面包 50、星级阈值 100/200/300、干净盘 2 个及固定轮换全部是临时方案；LAN/session、Unity、正式平衡和 durable recovery 仍范围外。
> 2026-09-29 十九次修约：owner 批准 Trellis task `09-28-cooking-companion-level-growth` 后，固定伙伴在当前 Level 成功完成 3 个询问或洗碗任务即解锁洗碗加速；新任务耗时为基础值的一半、向上取整且最低 1 Tick，进行中任务按认领时耗时继续。完成计数是唯一状态源，snapshot/canonical/SHA-256 与前厅 checkpoint 覆盖计数、派生解锁和冻结耗时；失败重开与下一 Level 清零，同 Level ET checkpoint 恢复保留。纯 C# 与 ET runtime 已验证；LAN、Unity、更多能力、长期成长和经济仍范围外。
> 2026-09-28 十八次修约：owner 批准 Trellis task `09-28-cooking-customer-companion-runtime-slice` 后，既有前厅节奏升级为可观察、可哈希、可恢复的权威运行态——顾客使用 Level-local 单调身份，订单身份跟随顾客而非桌位；固定伙伴暴露稳定身份、工作目标与进度；前厅 snapshot/canonical/SHA-256 与 checkpoint 覆盖询问、洗碗、用餐、队列、未满足记录和身份水位；`CookingLevelCheckpoint` 升 v2，ET host 同 Level 恢复时厨房与前厅共同成功或共同失败。单机纯 C# 已验证；Unity 表现、伙伴成长、收益评价、LAN wire 与 durable/process-crash 恢复仍范围外。
> 2026-09-28 十七次修约：owner 批准 Trellis task `09-28-cooking-level-transition-choices-lan` 后，既有成功交接、装修/解锁/Buff 与大关检查点接入生产 `CookingSessionHost`/`CookingSessionClient`——会话快照以 Level scope、generation、sequence、跨关进度和厨房 canonical 共同计算 SHA-256；Host 只在检查点写入成功后发布下一 Level，写入失败回滚且不广播；旧 Level 命令/快照拒绝，Match-scoped 重连凭证在下一 Level 继续恢复完整基线。同机 loopback UDP 已验证；两台物理 PC LAN、产品传输选型、主机迁移、Profile/SaveSlot 和 Unity 仍范围外。
> 2026-09-23 十六次修约：owner 批准 Trellis task `09-23-cooking-retry-keeps-choices` 后，失败重开在安装下一代之前套用当前进程的解锁和煮制加速。未知选择拒绝换代，失败现场留在原代际。不写检查点。失败条件和可见顾客仍未实现。
> 2026-09-23 十五次修约：owner 批准 Trellis task `09-23-cooking-handoff-inquiry` 后，成功交接前先给本关来过但还没开单的桌子开单，订单随后被本关交接清掉。收益和可见顾客仍未实现。
> 2026-09-23 十四次修约：owner 批准 Trellis task `09-23-cooking-failed-front-house` 后，失败重开丢掉前厅座位、未满足、营业时钟和洗碗队列，不把上一关的询问或洗碗写进新厨房。失败条件、收益和可见顾客仍未实现。
> 2026-09-22 十三次修约：owner 批准 Trellis task `09-22-cooking-front-house-reset` 后，成功交接会清空前厅座位、未满足和营业时钟。正在洗的碗先洗完，其余脏碗留在厨房。收益和可见顾客仍未实现。
> 2026-09-22 十二次修约：owner 批准 Trellis task `09-22-cooking-dining-seat` 后，订单完成后顾客按配置占桌用餐，座位空出前不能由前厅结束服务。未满足仍马上离席。收益和评价仍未实现。
> 2026-09-22 十一次修约：owner 批准 Trellis task `09-22-cooking-front-of-house` 后，一位固定伙伴问完才开单，没有询问才洗碗，营业结束且座位空了才允许前厅驱动的成功。收益、评价、伙伴成长和失败条件仍未实现。
> 2026-09-22 十次修约：owner 批准 Trellis task `09-22-cooking-level-progression` 后，准备态可以换工位、把解锁定义摆进延续厨房、缩短之后新开始的煮制，并且只有锁定后的成功收口写入大关检查点。前厅、评分和真实断电恢复仍未实现。
> 2026-09-22 九次修约：owner 批准 Trellis task `09-22-cooking-level-settlement-store` 后，已确认的结算列表按代际写入调用方给的目录，新读取器可以原样读回。评分、收益、长期进度入账和真实断电恢复仍未实现。
> 2026-09-22 八次修约：owner 批准 Trellis task `09-22-cooking-level-settlement-confirmation` 后，小关成功收口可按代际确认一份结算列表。重复确认无效，失败与准备态不确认。评分、收益与写盘仍未实现。
> 2026-09-22 七次修约：owner 批准 Trellis task `09-22-cooking-level-fail-retry` 后，小关失败换代丢掉失败现场，按标准初始供应重建厨房，新代际停在 `Created`。不结束 Match，不换 MatchId。失败条件、工位升级与写盘仍未实现。
> 2026-09-22 六次修约：owner 批准 Trellis task `09-22-cooking-level-success-handoff` 后，小关成功换代保留厨房现场并清除本关订单、结算、去重与事件历史，新代际停在 `Created`。失败重开已由七次修约落地；工位升级与写盘仍未实现。
> 2026-09-22 五次修约：owner 批准 Trellis task `09-22-cooking-checkpoint-recovery`（Todo P0-C1 前两条未勾项）后，同步 snapshot 与恢复 checkpoint 已显式区分——`CookingRecipeCheckpoint`（物品/tombstone、加工、容器、订单、逻辑 Tick、命令水位、去重账本、事件序列、ID 计数器、Match/epoch/config identity、lifecycle 状态）与 `CookingLevelCheckpoint` 信封（格式版本 + 完整性）已在单机纯 C# 范围实现并验证，“导出 → 销毁 host → 重建 → 继续运行”与不中断基线终态不可区分（证据见 task `check.jsonl` 与 `artifacts/cooking-checkpoint-recovery/`）。durable store、跨小关 checkpoint 产品语义、失败条件、前厅、传输与 Unity 仍范围外。

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
| P1 会话权威 | [`cooking-lan-session`](cooking-lan-session.md) | `09-15-cooking-lan-session` | L01-L09 transport-neutral contract，权威摘要 20/20 | 2026-09-28：`09-28-cooking-level-transition-choices-lan` 已验证生产 Cooking session 的同机 loopback 跨 Level 完整快照、旧水位拒绝、检查点失败阻断和重连共识；两 PC LAN、产品传输选型、D1-D4 与 Unity 仍见 successor/future scope |
| P2 配方循环 | [`cooking-recipe-loop`](cooking-recipe-loop.md) | `09-15-cooking-recipe-loop` | R01-R06 fixture loop，27/27 | 厨房闭环、正式内容/order owner、ET fixed-tick 闭环均已验证。2026-09-24 评分增量已实现订单固定基础分、总分、0–3 星和网络快照共识。2026-09-28 可见顾客纵切已实现稳定顾客/伙伴身份、前厅 snapshot/canonical/SHA-256 与 ET 同 Level 恢复。2026-09-29 固定伙伴首个 Level-local 成长已实现；同日单机多订单纵切增加盘子与烤面包订单、临时汤/面包轮换、双订单 150 分闭环和 checkpoint v3 菜单恢复。全部菜单规则与数值仍为临时方案。正常产品流程不设置业务失败；收益/评价、正式菜单生成、更多或长期伙伴成长、LAN 投影、Unity 表现和 durable/process-crash 恢复仍见 successor/future scope | 厨房与前厅恢复 checkpoint 已实现；正式菜单/平衡、后续伙伴能力、长期成长与 LAN/Unity 表现需另立 task |
| P3 配置验证 | [`cooking-config-validation`](cooking-config-validation.md) | `09-15-cooking-config-validation` | 当前 definition 范围，focused 6/6、当时完整 34/34 | 2026-09-21 修约：schema 已升 `cooking-definition-v2`，v1 配置与快照 blocked 拒绝、无迁移；多输入/默认供应/完成形态/物品容器能力的校验面已补齐；独立容器表退役（容器能力即物品定义字段），canonical recipe 段新增 `RequiresStation`，身份字符串保持 v2。2026-09-21 三次修约（随正式内容落地）：候选/canonical 增加订单模板与标准初始供应两段（身份字符串仍保持 v2）；校验面补齐订单模板外键（要求 recipe 存在、要求容器定义存在、带容器能力且接受该 recipe 产物）与供应项校验（定义存在、数量为正、位置语法、station 引用）；两处既有规则显式放宽——工位可声明零能力（台面）、免工位配方豁免 `CapabilityUnavailable`。正式 schema 推广、host/client compatibility、migration policy 见 successor backlog；Unity 见 future scope |
| P4 Match 生命周期 | [`cooking-match-lifecycle`](cooking-match-lifecycle.md) | `09-15-cooking-match-lifecycle` | fixture lifecycle/snapshot，focused 6/6、当时完整 40/40 | 正式 Room/Level/Map、产品退出语义、真实 LAN 见 successor backlog；Unity 见 future scope |
| P5 持久化技术合同 | [`cooking-persistence-management`](cooking-persistence-management.md) | `09-15-cooking-persistence-management` | in-memory contract，focused 12/12、当时完整 52/52 | durable store、process-crash、存档策略见 successor backlog；Unity 见 future scope |
| P6 网络测量基线 | [`cooking-network-measurement`](cooking-network-measurement.md) | `09-15-cooking-network-measurement` | InProcess baseline，focused 5/5、完整 57/57 | production measurement、真实 LAN、批准优化见 successor backlog；Unity 见 future scope |

后续如获 owner 授权，必须新建 Trellis task 并重新审议当时契约、环境与验证；不得恢复归档 task 或从本文推导实施许可。
