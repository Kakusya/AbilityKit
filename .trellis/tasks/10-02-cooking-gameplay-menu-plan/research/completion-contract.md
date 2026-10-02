> Current reviewed stage2026-10-03: S01-S14 pure C# singleplayer completed on source4dadd25c8; actual final master gates644/772/299 and772/299 passed, zero failures/skips. Detailed evidence: .trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-singleplayer-exit-verification.md. N01-N03 next; parent remains active, Unity/S15 deferred and physical two-PC LAN unverified. Earlier remaining-singleplayer notices below are historical.

# 完整出口审计表

本表以当前 owner“全部审议后执行直至完成”为目标；不是缩小到已经有测试的部分。当前允许实施的总范围为 S01–S14、N01–N03，阶段顺序不变。S15 是非基础参考池；U01/U02 仍由独立 Unity 执行门控制，不能从普通继续指令推导为解除长期禁令。

## 必须逐项证明

| 议题 | 完成证据必须覆盖 | 当前证据边界 |
|---|---|---|
| S01 逻辑空间 | 所有交互共享几何校验、连续逻辑移动、朝向、碰撞、只读预览、真实 ET 入口及恢复 | 本地 master 已合并并验证 S01–S03 核心；动态布局安装另由 S08 闭合 |
| S02 可接续加工 | 唯一 worker、手工停止/离开保留、换人继续、设备自动、同 Level 恢复 | 本地 master 已合并 S02 核心；F01/D31 实际 ET 换人恢复增量亦已合并并复跑 |
| S03 分装与恢复 | 多份产出/分装/最后份争抢守恒、清空保留容器、错误零变更、allocator 不泄漏 | 本地 master 已合并 S03 核心并验证守恒与恢复 |
| S04 内容闭合 | 87 菜来源、工位、容器、数量和阶段映射；可运行配置校验及现有菜回归 | 来源校验及 87 菜实际制作/盛装/ET 恢复已合并 cfd107c75；完整 87 菜已在 master 实际提交并验证 121 ET 恢复；见 master-complete-menu-verification.md |
| S05 制作与交付 | 未绑定暂存、饮品绑定/解绑/换绑、餐食无需贴票、重复交付一次、恢复 | 核心绑定/换绑/解绑/恢复与嵌套交付修复已合并 cfd107c75；31 饮品 binding/disposable 映射及实际提交/恢复已独立验证合并 |
| S06 前厅营业 | 到店/队列/入座/询问/用餐/离开/清理，人工与伙伴互斥，停止接单后自然收尾 | Master07bb27d54 gates635/763/289 and763/289 passed. Real finite F01+D31 two-player service, one unmet natural departure, cleaning, zero-star Success and four replay/restore branches now verified. Natural next service cold continuation remains separate S14 acceptance. |
| S07 供应 | 有限/无限显式区分、申请/到货/接收、物理包装/份数/仓储、补货和恢复守恒 | Master07bb27d54 includes actual finite/infinite supply, physical packages, allocator, runtime scoped procurement, Preparing/Running recovery and pending RemainingTicks carried through durable Created restart. Real natural finite procurement verified; narrowed-scope approved receipt remains final acceptance. |
| S08 布局 | 准备态安装真实空间配置、旋转占位、交互面、玩家与顾客通路、扩建、拒绝零变更及恢复 | Master07bb27d54 includes trusted layout installation, same-Level geometry recovery, next trusted seed projection with rollback and durable rehashed alternate-seed rejection. Real natural Front layout/continuous paths verified. Narrowed-scope successor acceptance remains open. |
| S09–S13 内容 | 44 正餐/12 甜品/31 饮品逐条供应→加工→容器→交付实跑，独立分支和最后收尾正确 | 87 菜实际生产与盛装、56 正餐甜品提交及 87 ET 恢复已验证；31 饮品绑定后提交与恢复已在 master 验证 |
| S14 单机完整出口 | ET 固定 Tick 营业闭环、许可交集、可观察数据、重放与 checkpoint、失败/成功跨关 | Master07bb27d54 includes frozen Observe, actual scoped Ready/runtime policy/Level8 recovery, durable Host typed baseline3 with monotonic frame clock and trusted reconstruction, and four-branch real natural operating. Actual635/763/289 and763/289 gates passed. Natural Ended -> durable next service cold continuation, narrowed carry and consolidated success/technical-Failed exit remain open. |
| N01 网络契约 | 较晚 owner 决定同步 ADR/spec；同一 Host 本地/远端入口、固定 Tick 队列、传输边界 | 研究结论已定位；正式修约及实现待单机后 |
| N02 网络闭环 | 全部新状态 wire round-trip，同机双进程真实 UDP，物理两机 LAN 争抢/并行/交付/跨关 | 第二物理主机当前不可用，不可标通过 |
| N03 恢复测量 | 身份重绑定、断线队列丢弃、暂停不消费、旧局拒绝、完整 baseline、真实拓扑指标 | 未重新验证新增功能 |

实际门禁至少按变更运行 cooking-kitchen-loop、cooking-et-level-runtime；通用网络包另加 runtime-contracts 及协议检查（若 Catalogs/WireSchemas 改变则通过生成器）。Unity 环境缺失或 Editor 占用只记 blocked/skip，不记 pass。

每行记录实际 commit、命令、exit、pass/fail/skip、日志与范围。未经审阅的分支、worker 自报、metadata、manifest 与 static verifier 均不能独自证明完成。不向远端推送；本地 master 合并前必须审阅差异并过相应门禁。
