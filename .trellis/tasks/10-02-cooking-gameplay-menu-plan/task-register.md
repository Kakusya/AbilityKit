> Current accepted status: S01-S14 pure C# singleplayer completed on master4dadd25c8, actual final gates644/772/299 and772/299. Read [exit proof](research/master-singleplayer-exit-verification.md). Parent stays active; N01-N03 next, Unity/S15 deferred. Earlier status tables are historical snapshots.

> Current incremental baseline: master125ffe906 reviewed generation staging, actual supply/preparation/layout, immutable Observe and pure Ready validator. Master kitchen605/733/254 and ET733/254 passed. S06/S07/S08/S14 remain in progress for scope permissions, trusted Host Ready/restart and natural service exit. Read [latest evidence](research/master-generation-menu-verification.md) and [complete remaining exits](research/completion-contract.md); older introductory statuses below are historical.

> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# Cooking Task 注册表

当前已验证状态：S01–S03 纯 C# 核心已本地合并并通过 master 门禁；S04/S05 核心与菜单生产恢复已合并，31 饮品绑定交付已独立验证，S04/S05/S09–S13 纯 C# 内容范围完成；S06 域组件已独立复核，ET 接入中，S07/S08 已有受限辅助而完整出口未完成。准确证据见 [master-complete-menu-verification.md](research/master-complete-menu-verification.md) 及 [核心证据](research/master-core-integration-verification.md)；下面初版注册表的“无执行许可”等表述仅保留历史含义。

> 最新授权：owner 已要求实施、验证并合并 master；S01–S14/N01–N03 获执行授权，按依赖与文件冲突分波，U01/U02 后置不执行、S15 为参考池。以下初版状态为登记时快照；当前状态和验证/合并证据见 [execution.md](execution.md) 及各 Task metadata/check.jsonl。

2026-10-02。以下均为真实 Trellis `planning` 初稿；无活动实施 Task，无执行许可，无时间表。单机优先；网络延期；Unity 条件性记录但仍禁止实施。PRD/design/implement/manifests 已建，具体 API、错误矩阵、版本与测试场景须在各 Task 执行前收敛，不冒充 ready。

| ID | Task | 范围 | 前置 | 关键验收 |
|---|---|---|---|---|
| S01 | [单机逻辑移动与交互目标](../10-02-cooking-singleplayer-spatial-interaction/prd.md) | A02–A07/A10，网格放置与连续移动分离 | 既有基线 | 朝向/距离/障碍改变候选，空手/持物解析稳定；拒绝零变更；普通手槽与台面单物件 |
| S02 | [单机手工作业与接续加工](../10-02-cooking-singleplayer-manual-process/prd.md) | C02–C04/D01–D09/I02–I06 | S01 | 松手与离开暂停保留，换人接续，不默认叠加加速；设备与端锅加工继续符合既有契约 |
| S03 | [单机批量分装与错误恢复](../10-02-cooking-singleplayer-portions-recovery/prd.md) | B08–B14/C13/G08/H07 | S01 | 一锅多份/最后一份争抢守恒；清空保留容器；重复命令不复制；非法投料拒绝 |
| S04 | [单机菜单拓扑与配置映射](../10-02-cooking-singleplayer-menu-schema/prd.md) | 87 款/72 供应/60 准备状态/19 工位的候选映射 | S02, S03 | 逐菜来源拓扑无环、重复份数及阶段顺序正确；本关菜单闭合；旧汤/吐司回归不变；明确版本迁移 |
| S05 | [单机饮品贴单与交付分离](../10-02-cooking-singleplayer-order-binding/prd.md) | E01–E12/F07–F08 | S03, S04 | 未绑定成品可暂存；饮品绑定/换绑/解绑明确；餐食不强制贴标；重复交付只结算一次 |
| S06 | [单机前厅与营业收尾补全](../10-02-cooking-singleplayer-front-house/prd.md) | F01–F09/G01/G11–G12/H01–H02 | S01, S05 | 复用询问/队列/用餐/离席与洗碗，补人工接手及通路；停止接单与结束分离；未满足不业务失败 |
| S07 | [单机有限供应与备料](../10-02-cooking-singleplayer-supply/prd.md) | G02–G10 | S03, S04 | 包装与游戏份区分、采购/到货/接收/仓储原子化；无限和有限供应不混写；库存不足可恢复 |
| S08 | [单机布局通路与扩建](../10-02-cooking-singleplayer-layout/prd.md) | I07–I11/餐厅扩建、摆放、顾客工作动线 | S01, S06, S07 | 准备态移动/旋转/区域扩建；占位/交互面/顾客与玩家通路验证后提交；营业中拒绝修改 |
| S09 | [候选正餐第一批](../10-02-cooking-singleplayer-menu-meals-basic/prd.md) | M1：F01–F16/F21–F27/F43–F44 | S04, S05 | 先 F01/F11/F21 验证再扩完整批，切配/煮/煎/组合/分装全部从供应可达；不等同首关菜单 |
| S10 | [候选正餐热加工扩展](../10-02-cooking-singleplayer-menu-meals-heat/prd.md) | M2：F17–F20/F28–F42 | S09 | 未熟披萨/蒸饺可交接，烤/蒸/炸/烤架能力独立，出餐小锅不混同后厨锅 |
| S11 | [候选甜品内容](../10-02-cooking-singleplayer-menu-desserts/prd.md) | M3：S01–S12 | S10 | 面糊共享分流、模具/脱模/裹粉/收尾可组合；不增加精准操作或冷却巡检 |
| S12 | [候选饮品基础内容](../10-02-cooking-singleplayer-menu-drinks-basic/prd.md) | M4：D01–D14 | S11, S05 | 榨汁/搅打/冲调区别、杯型与冷热状态明确，全部饮品独立贴单；共享果汁按份守恒 |
| S13 | [候选咖啡与茶饮内容](../10-02-cooking-singleplayer-menu-drinks-tea/prd.md) | M5：D15–D31 | S12 | 研磨/萃取、加热/打泡模式、茶/珍珠共享，奶盖最后加、奶茶与冲调及鲜奶来源不混写 |
| S14 | [单机关卡许可与可观察闭环](../10-02-cooking-singleplayer-level-observation/prd.md) | J01–J08 的数据面/K01–K02/K06/完整验收 | S05–S13 | 单机 ET 命令→Tick→提示数据→营业收尾可重放；checkpoint 重建等价；已解锁与本关允许取交集 |
| S15 | [单机成长与额外机制参考池](../10-02-cooking-singleplayer-growth-reference/prd.md) | K03–K14/H03–H06/I12/投掷接取冲刺/DLC | S14 | 先保留既有评分/伙伴 Level-local 成长，再逐项审议扩展；任何风险/失败/随机/经济规则不默认为基础 |
| N01 | [网络范围与历史冲突收敛](../10-02-cooking-network-contract-review/prd.md) | KCP/LiteNet、先到先得/稳定仲裁冲突 | S14 | 更新权威决定与唯一传输方向，确定身份、命令/快照契约和真实 LAN 验收；不以旧纪要自动执行 |
| N02 | [网络厨房与营业闭环](../10-02-cooking-network-gameplay-loop/prd.md) | 同一玩法的 listen host/remote 客户端 | N01 | 两台物理 PC 争抢/并行/绑定/提交/跨关状态一致；host 本地与远端共享验证；拒绝不丢料 |
| N03 | [网络恢复与测量](../10-02-cooking-network-reconnect-measurement/prd.md) | 断线、旧局输入、负载与延迟 | N02 | 完整基线恢复，旧 epoch/sequence 拒绝，断线队列明确；真实拓扑测量不冒充 InProcess 结果 |
| U01 | [单机 Unity 条件性可玩出口](../10-02-cooking-unity-singleplayer-conditional/prd.md) | A01/A08–A09/J01–J08/本地操作、场景与布局 | S14, N03 | 解除禁令后才能实施；看清物品/内容/进度/订单，连续走位与拿放顺畅；compile/EditMode/scene smoke 实跑 |
| U02 | [网络 Unity 条件性合作出口](../10-02-cooking-unity-network-conditional/prd.md) | 多人空间/交接/表现恢复 | U01, N03 | 解除禁令后两机多人可协作、看懂接手成果，延迟/断线恢复不复制物品；真实场景验收 |

S01–S08 先补共同规则，S09–S13 分批扩候选内容，S14 验证完整单机出口；S15 是后置参考审议，不强迫完成所有扩展再进入网络。N01–N03 只记录，U01 的 N03 前置表达 owner 的阶段顺序，不声称单机 Unity 技术上必须依赖网络。

已有任务 09-19-cooking-productization-network-slice-planning 继续保留为产品/网络历史讨论来源，本注册表不恢复归档任务、不替代其原始记录。后续重复需求应合并引用本表，避免再创建同义 Task。
