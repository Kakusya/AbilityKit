> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# Cooking 玩法计划的架构记录

2026-10-02；保留既有架构，最新 owner 已授权审议后实施。应用技术路线唯一正文仍为 [technical-roadmap.md](technical-roadmap.md)，本文件记录玩法能力如何沿现有边界接入，不建立第二套路线。当前已验证增量见 [master 核心证据](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-core-integration-verification.md)，未完成的设计输入仍不表述为运行能力。

## 保留的结构

- 纯 C# `AbilityKit.Game.Cooking` 拥有物品/容器/加工/订单/前厅规则；`CookingLevelEtHost` 负责 Level 固定 Tick 与命令入口。先扩展既有 owner，不建立平行厨房运行时。
- `CookingRecipeCommand` 已有 Pickup、Drop、PutIn、TakeOut、Pour、StartProcess、SubmitOrder，以及本轮已合并的 Move、ContinueProcess、StopProcess、ServePortion、ClearContents、DiscardItem；AdvanceTicks 为 legacy 自动加工测试路径，不能推进 Manual。动作仍经过同一 owner 的验证、原子提交、幂等及 scope/version 校验；BindOrder、UnbindOrder、RebindOrder 已在本地 master 合并验证；它们复用 Item/Order/ExpectedItemVersion，fingerprint 覆盖既有字段。
- 运行态、快照与恢复 checkpoint 分开；新增状态必须评估 snapshot/canonical/SHA-256/checkpoint、同 Level 恢复、成功交接与失败重开的取舍，不能只加 UI 字段。
- 正式内容使用 `CookingContentDocument`、配置候选及加载校验。master 当前身份是 `cooking-definition-v3`，Recipe checkpoint schema4、Level envelope format5；不支持旧格式时显式拒绝。Execution/YieldPortions/RequiredProcessingContainerDefinition 和可选 ContentProvenance 经既有配置校验、冻结与 canonical identity，不另造菜单配置 authority。S05 新增必需 BoundOrder checkpoint 字段与可选 RequiresBinding/DisposableOnSubmission 内容字段已同步 canonical 和恢复校验；S06 分支 format6 尚未交付。
- Match、Connection、Participant、RestaurantRuntime、Kitchen、Level 语义见 [reference/product-lifetimes.md](reference/product-lifetimes.md)；ET 所有权见 [reference/et-entity-tree.md](reference/et-entity-tree.md)。这是设计参考，不宣称完整目标树已经落地。
- 通用 AbilityKit、Network 和 Server 不拥有菜品、顾客、票据或营业规则。现有应用层对框架的组合方式保持。

## 新能力归属（设计输入）

| 能力 | 应用层归属 | 主要读写边界 |
|---|---|---|
| 位置/朝向/障碍/目标解析 | Cooking 逻辑空间与交互服务 | 输出候选与动作预判；固定 Tick 执行时再次验证 |
| 份数/批量/分装 | 物品与容器内容、加工产物 | 物料守恒；失败不扣份数；实例身份与来源可追溯 |
| 手工参与/暂停/接手 | 加工 owner | 逻辑参与者和 elapsed ticks；设备推进不绑动画 |
| 饮品票据 | 订单 owner 引用成品实例 | 绑定/解绑/改绑幂等，已交付不可修改，不改变配方身份 |
| 有限库存/包装/到货 | Cooking 供应 owner | 库存变化与实例创建原子一致；无限取料配置显式区分 |
| 顾客路径/桌位/工作任务 | 前厅 owner + 逻辑空间 | 复用已有身份/队列/用餐/伙伴任务；路径不另造订单 |
| 设备布局/区域扩建 | 准备态布局与内容许可 | 先验证占位、交互面和通路，再一次提交；拒绝保持原布局 |
| 提示数据 | snapshot/event 的只读视图 | 操作原因、容器内容、进度、绑定、供应量；Unity 不回写 authority |

已合并核心公共 API 与纯 .NET checkpoint 格式以对应 spec/task 检查记录为准；供应、布局和前厅新增辅助尚未形成完整 ET 营业接线，网络 wire 尚待网络阶段处理。其余设计输入仍须具体签名、错误矩阵、正反例、恢复规则及实际测试，不能把表格存在当成跨层契约已经生效。

## 来源冲突登记

2026-10-02 本次审议已定位网络较晚 owner 决定：09-19 planning 纪要第 28/29 节确认 LiteNet reliable-UDP、复用现有通用 Listener/ServerChannel、仅固定 Tick 写世界、权威入队先到先得及完整快照；KCP 临时方向被 supersede。详见 N01 design。旧表保留审议起点，不再把已定位的 owner 取舍当作需要重复询问的未知；网络实施前仍需同步正式 ADR/spec 及实际测试。

| 冲突 | 来源 | 本轮处理 |
|---|---|---|
| KCP 后续方向 vs LiteNet reliable-UDP 唯一真实传输 | ADR/long-term-goals.md、ADR-0002 的 09-21 附注；09-19 planning task discussion-notes.md 第 26/28 节较晚决定 | 显式保留。网络 Task 开始前汇总完整决定并协调权威文档；本轮不改选型、不删除历史代码 |
| 稳定批次排序 vs 先到先得 | ADR-0002、交互 spec 和现有争抢测试；同 notes 第 29 节 | 单机继续现有稳定排序。网络阶段须明确权威入队序号与排序契约，不直接依赖线程/传输到达偶然顺序 |
| 配置容量 vs 全局一物规则 | 交互 spec 允许配置手/槽容量；新玩法提出普通单槽 | 普通设施单槽是新产品规划约束，特殊设施不一概收紧；未来显式修约 |
| 87 款目录 vs 现有汤/吐司基线 | 外部 v0.1；现有正式内容与回归 | 追加候选映射，不改现有 ID，不删除回归菜，不宣称汤是 F04 |
| 后续 Unity 顺序 vs 当前 Unity 禁令 | 本轮 owner 排序；future-scope.md | 允许记录条件性 planning Task；禁令保持，不能执行或领取实现 |

## 验收原则

单机先以命令序列、固定 Tick、状态守恒、canonical/hash、checkpoint 恢复验证。多逻辑玩家争抢仍在无网络测试中验证。网络再增加实际拓扑、延迟/乱序/重连及旧局拒绝；Unity 最后验证读得懂、拿得准、移动/合作体感。同机两实例不能代替两台物理 PC。
