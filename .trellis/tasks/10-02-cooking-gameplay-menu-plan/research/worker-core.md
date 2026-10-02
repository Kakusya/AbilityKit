# Orca worker：厨房核心 S01–S03

用户已在最终 plan 后明确授权实施、worktree 并行、验证后合并 master；未提细节此前授权助手决定。阅读 AGENTS.md 的最新执行授权与总任务 execution.md，不要把旧 planning-only 文本作为拒绝执行理由。不再询问重复授权。

你负责独立 Orca worktree 的 **Task S01、S02、S03 核心完整实现**，按顺序推进并分别记录设计/证据。你不是唯一开发者，不撤销别人改动；其他 worktree 正在做菜单映射、网络，禁止修改 master 或他人工作树。不要使用子 agent 再扩大并发；如需协调用当前 Orca dispatch preamble 的 ask/mail。

## Ownership

- src/AbilityKit.Game.Cooking/CookingDomain.cs、CookingRecipeLoop.cs、CookingRecipeCheckpoint.cs、CookingLevelCheckpoint.cs、CookingContentCatalog.cs、CookingConfigurationValidation.cs、Content/cooking-content-v2.json（必要时改名版本并更新 csproj 与引用）。可新增同一 authority partial 文件，不能另造未接入系统。
- src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs 的命令 payload 指纹与既有 ingress 配套；新增 ET 测试。
- 以上行为的 domain tests、src/AbilityKit.ET.Runtime.Tests 新测试；S01/S02/S03 自己的 task artifacts；生效 spec 仅对对应段落显式修约。
- 不修改 Session/网络实现、菜单新目录、根执行 dashboard/AGENTS、Unity。共享核心变更由你单一 owner 合并 schema/checkpoint 一次，不让三 Task 独立升版本。

## Before coding

读取相关 task PRD/design/implement/manifests、interaction/recipe/config/lifecycle spec、ADR-0002、reference README、et-entity-tree、product-lifetimes、验证规则。细化三 Task 的具体 API/错误矩阵/测试/版本策略和实现边界；引用本 brief 与 execution.md 的已收敛规则。owner 已批准总 plan 和委托细节；这是实施设计细化，不扩大产品范围。初始化本机 developer（如缺则 Kakusya，来自 Git identity）并启动你的 Task，不提交本机缓存。

## 必须实现

1. S01：**现有 CookingRecipeSimulation** 持有位置/朝向，固定 Tick lane 中的移动命令（确定性整数细分单位/连续逻辑坐标，不是棋盘步进）、边界、swept 障碍与玩家碰撞，普通手/台面单物件。World/Station IDs 映射锚点；容器内容递归继承空间位置。共享距离/朝向/遮挡验证进入 Pickup、Drop、StartProcess、PutIn、TakeOut、Pour 双端、SubmitOrder；WorldPosition 不能不校验。PreviewInteraction 只读，候选按距离/朝向/ordinal ID 稳定排序，上下文映射实际操作，执行再次校验。无 spatial config 的历史 fixture 兼容旧语义；有配置缺锚点配置拒绝。pose 在 snapshot/canonical/hash/checkpoint、同 Level恢复、失败初始重开、成功承接完整覆盖；地图切换非法 pose 不静默保留。
2. S02：Recipe execution kind 默认 Automatic、可 Manual。Manual StartProcess 认领唯一 worker；ContinueProcess/StopProcess 或同等明确命令支持停止保留/换人继续，fixed tick 每进程每 tick 最多+1，不加速。离开几何范围自动释放 worker/暂停；一个 player 不同时操作两个 manual process；暂停保持输入锁。Automatic 和端锅继续不变。Legacy AdvanceTicks 不绕 manual 固定时钟。success handoff/准备布局迁移保留进度清 worker，同 Level restore 保留且校验 worker。新增字段进入内容校验/config hash/snapshot/checkpoint。
3. S03：Recipe YieldPortions 默认1、完成容器 RemainingPortions；批量来源/余额守恒。ServePortion 每次生成独立一份，最后份消费输入并复用容器；完成批次拒绝追加/取料/再启动。Pour 保留整体搬全部内容和默认一份语义；批量整体倒出必须预验足够目标槽位后一次提交。ClearContents 保留容器、删内容、清 recipe/completion/balance，不生成分数/退料；DiscardItem 普通物件不销毁容器设备。加工锁定拒绝清空。allocator、overflow 在 commit 前 staged 预验，失败 checkpoint counter 不改变。所有动作沿同 Submit/IsWellFormed/SubmitCore/去重/仲裁路径。批量首版可明确定为 RetainInputs 生成可分装批次，ConsumeInputs 仍单份，校验禁止无实现含义的多份 ConsumeInputs，不绕过物料守恒。
4. Payload 新字段全部加入 ET CookingCommandFingerprint.CanonicalBytes（手写，极易漏）。schema/checkpoint 明确一次升版，拒绝未支持旧版本，不默认恢复缺失 pose/worker/份数。保留默认一份/自动 recipe 回归。

## Acceptance

新增有意义测试覆盖：几何改变可达性/背向/墙、玩家碰撞稳定、preview后争抢重验、每个动作不可绕reach；甲操作2tick暂停5tick乙接续、无叠加、设备继续；3份分装、最后份两个逻辑玩家一成功、重复与 payload冲突、clear重用、不兼容零变更、allocator失败零counter变更；篡改checkpoint拒绝/完整重建与不中断canonical相等/成功承接余额不回满。必须用 **CookingLevelEtHost ingress→fixed Tick→checkpoint→恢复→继续** 的真实路径证明，不只测试未接入新 helper。

实际运行 focused tests 与 cooking-kitchen-loop/cooking-et-level-runtime 适用门禁，记录 exit/pass/fail/skip、日志位置和数量；大量既有 CS1591 警告写日志，别回传整个输出。提交你 branch 的改动，不 merge master，不 push，不勾选无证据完成。给协调者报告 branch/path/commit/测试/真实剩余缺口。全部完成时按 Orca preamble worker_done，然后停止编辑。
