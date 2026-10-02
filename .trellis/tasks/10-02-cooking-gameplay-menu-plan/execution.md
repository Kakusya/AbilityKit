> Current reviewed stage2026-10-03: S01-S14 pure C# singleplayer completed on source4dadd25c8; actual final master gates644/772/299 and772/299 passed, zero failures/skips. Detailed evidence: .trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-singleplayer-exit-verification.md. N01-N03 next; parent remains active, Unity/S15 deferred and physical two-PC LAN unverified. Earlier remaining-singleplayer notices below are historical.

> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# Cooking 单机与联网执行记录

## 最新恢复指令

### 审议完成后的实际恢复回执

规划审议提交为 `8eb7203b6`；全部21个Task context manifests实际validate通过，S01大源文件超过32KiB注入上限的警告已明确通知核心worker以直接读源作为fallback。规划static verifier通过21Task/20children/124功能ID/87菜单ID/依赖无环/附件byte-identical；该检查不证明产品实现。

Orca已按停止事实重试现有worktree：核心 `ctx_4e56ba63e0df`（原task_3539e6eaa06f），菜单 `ctx_cf4084e88097`（原task_0b550b23177b）；启动均ready、input_accepted、turnStart observed。已发送主分支审议文档和修正项，fleet实际显示两者working/live；没有重复使用旧dispatch凭证。旧网络dispatch仍exited，保留其worktree等待S14后恢复。

整合分支 `cooking-integration-s06-s14` 已合入最新规划，并提交 `83a514e83`：静态布局净空、可信设备占地与对应测试，实际focused13/13、0skip，exit0；日志在该worktree `local/Logs/cooking-execution/layout-clearance.log`，既有CS1591警告未抑制。此仍非S08完整ET出口，未回并master，不标任务完成。

Owner 明确要求“继续审计，直到各个议题都审议清楚，然后执行直至完成，中间不要停下来”。本次不再只登记 Task；先完成各 Task 的具体设计、实施清单及 manifests 审阅，再按既有依赖执行和验证。单机优先，网络随后；Unity 继续作为后置独立阶段，不提前启动。架构不变，未提细节由助手按已确认产品边界补足。

旧三个 Orca dispatch 已被主动停止，不能视为存活或重用其凭证。保留 worktree 的改动需要审阅、恢复研究和实际复跑；历史 worker 自报测试不算协调者验证。当前三个只读审计分工分别负责核心、菜单、经营整合，研究成果写入本 task 的 research，不写产品代码。

## 最新实际检查点（2026-10-02 18:45）

当前核心 dispatch 为 `ctx_4e56ba63e0df`（reviewing），菜单 dispatch 为 `ctx_cf4084e88097`（waiting，等待核心接口并补充来源容器闭合）；旧网络 dispatch 保持停止。上述为当前消息证据，下面首次启动和停止记录属于历史。不得用历史 dispatch 替代当前监督对象。

协调整合分支 `cooking-integration-s06-s14` 已接入菜单来源增量 `6eadb3396`、公开命令验收设计 `c2a98405f` 和 F31 兼容路线测试 `30be8312f`；此前含布局边界修复 `3684320d5`、供应恢复校验修复 `860aa9c54`。菜单独立审阅见 [menu-increment-review.md](research/menu-increment-review.md)。这些增量尚未合入 master，不表示整个 S04/S06/S07/S08 完成。

协调者在组合版本 `30be8312f` 实际运行 `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop`：两项构建 exit 0、focused 155/155、Cooking 276/276、ET 67/67，失败与跳过均为 0。日志与 TRX 位于整合 worktree 的 `local/Logs/test-gates/20261002-184417-cooking-kitchen-loop/cooking-kitchen-loop/`，摘要为其中 `gate-summary.json`。该证据不覆盖仍在核心分支中的空间/手工/份数接口，也不覆盖全部 87 菜或新增 ET 接入。

批量半成品输出暂存容器闭合正在补审；来源目录需要保留米饭桶、酱汁碗等输出选择，运行时必须真实分装和搬运，不能清空重置锅或注入成品绕过剩余份数。前厅增量继续扩展现有 `CookingFrontOfHouse` owner；其 ET payload、跨 owner 互斥和几何接入由协调者统一整合，未完成前不标 S06 出口通过。

## 当前目标

### 19:25 已审阅前厅与菜单域增量

前厅域增量 `4aadf7588` 已在 integration 提交；协调者实际聚焦 37/37、组合 kitchen focused223/Cooking344/ET73 全过，证据在 S06 `research/domain-increment-verification.md`。未合入 master、S06 未完成；真实 ET 工作入口、跨厨房互斥和布局路径安装仍待接入。

菜单 driver `973cb3319` 经独立只读审阅接入 integration 为 `bddad6c9e`；87 条实际制作/盛装迹线、56 餐食/甜品提交与 31 饮品待绑定状态已核对。未注入中间成品或免费重置；39 个单份准备件尚未实跑其声明的可选存储路径，菜单 ContinueProcess 接手与 ET 流程尚未证明。该提交也早于 catalog 的 carrier/provenance 映射接线，不能把其结果移作后续契约的证明。

协调者在组合版本实际运行 kitchen：focused319/319、Cooking440/440、ET73/73，两构建 exit0，失败/跳过0。摘要 [menu-front-integration-gate-summary.json](research/menu-front-integration-gate-summary.json)，实际日志/TRX 在 integration `local/Logs/test-gates/20261002-192434-cooking-kitchen-loop/`。这仍不是 S04–S14 完整出口，也不是 master 的 440 项验收。

S05 与菜单监督 worker 当前分别 reviewing；暂无 worker_done。继续使用原 dispatch，不能因等待接口或历史 heartbeat 创建重复 worker。网络和 Unity 阶段未开始。

### 19:13 本地 master 核心增量已验证

`501bf383d` 在独立干净检出通过 kitchen 门禁（207 focused / 328 Cooking / 73 ET），排除前厅未提交修改；协调者随后合并为 master `79d93c923`，实际复跑 kitchen 和 ET Level 两门禁，Cooking 328/328、ET 73/73，无失败/跳过。准确结果及两份摘要见 [master-core-integration-verification.md](research/master-core-integration-verification.md)。S01–S03 仅按已审议纯 C# 核心范围置 completed；其目录保留以维护现有依赖和证据路由，整体目标继续 active。

S05 已实际监督启动：`ctx_20fd4aef295e` / `task_fb06a0f6f8d2` / `term_d61992ca-ba80-4a2e-8f6d-c70f532286e5`，worktree `cooking-order-s05` 从 `501bf383d` 建立，ready/input_accepted/turn_started observed。负责绑定/一次性杯及对应指纹/恢复格式；主协调者暂不并发修改该 Host/Recipe 入口。菜单 worker 继续独占目录、graph driver 与新 ET 菜单验收文件。

前厅独立审阅发现同一脏碗可恢复多个未完工作，已交实现者修复，新增轮次毒化反例自报聚焦 37/37；尚待协调者复跑和接入 ET，未合并该增量。菜单 worker 自报 87 制作盛装及 56 餐食交付，尚待提交审阅，不记为 master 全量菜单验收。31 饮品仍待 S05 正式绑定。完整出口表继续约束 S04–S14/N01–N03。

### 18:58 核心审阅后的修复工作

核心 worker 提交 `8111472f2`，已接入整合分支为 `be82b22ae`；完成消息已处理，dispatch `ctx_4e56ba63e0df` 的监督终端实际 release，输出归档保留。其自报 domain 28 / ET 新增 6 及两门禁均通过，独立审阅记录见 [core-increment-review.md](research/core-increment-review.md)，不得把 worker 自报当组合版本已通过。

独立审阅发现锁定输入恢复只校验 membership，缺完整多重集及跨工序唯一归属；普通成品身份也缺配方/产物定义对应校验。整合分支正在补完整恢复和 fixed Tick 不变量，以及篡改 checkpoint 反例。这些问题阻止当前核心回并 master。

菜单容器补充 `bb167ddf8` 已接入为 `21b11eff4`，Execution/Yield 真实映射 `68a7f6934` 已接入为 `d8744d043`。来源存放容器仅为目录元数据，非运行分装证据。核心未实现此前请求的 RequiredProcessingContainerDefinition/ContentProvenance，协调者正在现有配置/匹配/启动加工中补齐，未增加平行配置 authority。

一次组合门禁在前厅生产编辑中间态编译失败（缺尚未写完的新类型），日志在整合分支 `local/Logs/test-gates/20261002-184859-cooking-kitchen-loop/`，不能标通过。随后冻结编辑窗口实际聚焦运行菜单接口与 checkpoint 新例：14 例中 13 通过、1 失败；失败为新增丢弃墓碑 fixture 未先真实取出物品，已交审阅者修正并待复跑。原始输出保存为 [hooks-checkpoint-first-run.log](research/hooks-checkpoint-first-run.log)。后续新增用例尚未运行，不计入该次结果。现有前厅 21 例通过属实现者自报，新增排队/路径/人工接手仍待验证。

2026-10-02 owner 在最终规划汇总后明确要求：列 Task，按实际情况用 worktree 并行实施，检测验证，通过后回并 master；先单机和联网，Unity 后做，活用 Orca。该回复是对上一轮 plan 的实施授权；此前助手获授权补足未提细节。当前不再受初版 planning-only 限制。

完整范围为 S01–S14 的基础单机玩法、87 款候选内容分批导入与闭环、N01–N03 的共享网络链路/恢复测量；S15 只是后置扩展参考审议。U01/U02 未授权。不能只做容易通过的小片段便标整个目标完成。

## Task 与并行波次

| 线 | Task | 工作边界与顺序 | 状态 |
|---|---|---|---|
| 厨房核心 | S01→S02→S03，之后 S05 | 同一 worktree 单 owner；所有命令/DTO/canonical/checkpoint 和 ET 指纹协调修改 | 研究完成，待启动 Orca worker |
| 菜单内容 | S04，再 S09–S13 | 独立 worktree；先完整源映射与依赖审计，核心契约合入后导入内容与校验 | 待启动 Orca worker |
| 网络 | N01，再 N02→N03 | 独立 worktree；先对齐已确认 LiteNet/通用 Transport 边界，再接入新增玩法及恢复测量 | 待启动 Orca worker |
| 单机经营整合 | S06/S07/S08/S14 | 协调会话 worktree；依赖核心和菜单，逐批整合，不建立平行 authority | 待前置 |

## Orca 实际启动回执

Run：`run_ca505f084a8c`。2026-10-02 已启动以下三个 supervised Codex worker，均有 input_accepted + turn_started/observed；不是仅登记。

| 线 | Dispatch | Orca 分支/worktree | Task |
|---|---|---|---|
| 厨房 | ctx_ab9d7e55d402 | cooking-core-s01-s03；C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-core-s01-s03 | task_3539e6eaa06f |
| 菜单 | ctx_cb37026cb88a | cooking-menu-s04；C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-menu-s04 | task_0b550b23177b |
| 网络 | ctx_50c9cd456cac | cooking-network-n01-n03；C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-n01-n03 | task_f91244e49d20 |

协调经营 worktree：`cooking-integration-s06-s14`，同一 Orca workspace 根目录下；主会话持有，不增加第四个 worker。

规划及执行基线已提交 master：`b4f1a2f53`、`ce3110b53`。产品 feature 尚未回并；三个 worker 的具体生产改动正在独立分支中。启动前领域基线 `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --nologo --verbosity minimal` 226/226 pass、0 skip；大量既有共享包 CS1591 警告未当作零警告构建。

网络调查发现通用 LiteNet 缺服务端 Listener/ServerChannel，Cooking Session 仍直接持 NetManager。协调者已按现有已批准通用 Transport 架构授权网络 worker独占最小 adapter、包/asmdef/SDK引用与专属测试；移出回调中模拟写入，不新建平行网络抽象。菜单 ×2 与现有 DuplicateRecipeInputs 校验冲突已交核心 owner 修正为多重集合，尚未声称通过。

S04/N01 的只读研究及相互独立设计工作可以前置，生产整合仍遵守依赖。后续 Task 的 ready 不以文档存在判断，必须确认前置实际验证。

## 核心设计收敛

- S01：既有 RecipeSimulation 拥有逻辑 pose；地图锚点把 World/Station ID 映射连续位置，固定 Tick 命令移动/朝向，障碍和角色碰撞；preview 只读，所有实际拿放/容器/加工/交单再次使用同一几何验证。无空间配置的历史 fixture 保持旧语义，配置存在时缺锚点拒绝，不能任意可达。
- S02：配方分 Automatic/Manual；Manual 进程最多一个 active worker，停止/离开暂停保留，另一玩家继续；设备仍自动，端锅继续。legacy 多 tick 不能绕过 manual。success handoff 保留进度但清 worker；同 Level 恢复保留工作状态。
- S03：默认产量1，批量输出显式份数；新增 ServePortion、ClearContents、DiscardItem 走同一提交 lane。保留 Pour 的整体转移语义；按份取用守恒、重复不生成第二份、容器清空不销毁。完成批次拒绝改料/重启；目标空间和 ID allocator 在提交前完整验证。
- 核心 owner 统一协调 schema/checkpoint 新字段及版本，只升一次，显式拒绝不支持旧格式；既有默认一份/自动工序测试继续成立。ET 手写 CookingCommandFingerprint 必须覆盖新增 payload，网络 DTO 必须 round-trip。
- 核心三个任务改现有 owner，允许拆为同一 partial class 的文件，不另造未接入新系统。菜单映射不是生产实现，网络研究不是实际 LAN 通过。

## 已读取环境证据

- 根工作树 master，开始时仅保留上一轮规划变更；初始 HEAD 9bfdebc6e5ed423fa96c5bf48bb79e1001e08efd。
- `orca status` app/runtime/graph ready；AbilityKit 已注册。优先 Orca 受管工作树、监督 worker、消息与清理生命周期。
- `orca host list` 只有 local，`orca environment list` 为空。当前无法证明两台物理 PC LAN；本机实际 UDP、独立进程、恢复/测量可执行，但不能替代双机验收。
- .NET SDK 实际 10.0.300，未有 global.json 固定 SDK 的主张。

## 合并门

各 worker 独立提交 feature 分支；主会话审核 diff 与范围，执行相应 cooking-kitchen-loop/cooking-et-level-runtime，网络/通用包变化附加 runtime-contracts 和必要协议检查。只合并测试已通过的增量；合并后再跑覆盖新增整合行为的验证。master 不直接接收未审查或混入用户改动的代码。不推远端。

Orca worker/dispatch、worktree 路径、提交 SHA、实际测试结果及合并 SHA 后续按真实回执追加。超时不等于 worker 退出；同一活跃 dispatch 持续观察，不重复启动。

## Implementation stopped for planning-only scope

### 2026-10-02 编排消息核对

已读取 `delivery_607bd3144158` 的全部 4 条消息：3 条历史 heartbeat，以及核心 worker 的状态消息 `msg_3ba951486b77`。核心 worker 报告 S01–S03 新增 19 例通过，并报告几何、手工接续、份数、多重集合、schema-v3/checkpoint-v4 等分支改动；此为 worker 自报，协调会话尚未独立审阅或复跑，不作为已实现、已验证或已合并证据。该消息不是 `worker_done`，不表示任务完成。

本次 `worker-list` 确认三个 dispatch 均为 `failed`、terminal 为 `retained`，与主动停止一致；未重启。历史 heartbeat 不推翻当前停止状态。保留既有 worktree，当前继续仅规划。

All three dispatches in run_ca505f084a8c returned stopped with ptyKilled=true. Existing worktrees and unmerged changes are preserved. No feature branches were merged into master. The prior implementation goal is paused.

## 2026-10-02 19:36 S05/menu integration verification

Reviewed S05 imports and nested serving ownership fix `901f465b5` passed actual composite kitchen gate 340/462/166. Evidence: [s05-menu-composite-verification.md](research/s05-menu-composite-verification.md). Order worker settled and released; menu dispatch ctx_cf4084e88097 continues actual 31-drink binding delivery. Native S06 owner continues ET/Level integration in retained integration worktree. Master remains the reviewed S01–S03 baseline; no full singleplayer exit or network/Unity completion claimed.


## 2026-10-02 19:47 Supply and preparation coordination

S07 supervised worker ready/input_accepted/turn_started: dispatch ctx_aa1cb5f5926a, task task_65e3da632b1e, terminal term_3fe4de66-df2f-4c8e-a4bb-894e11367e55, Orca worktree cooking-supply-s07 based on 901f465b5. Domain supply owns Recipe/config/checkpoint sections; S06 retains Level/host/manual sections. Exact brief: research/s07-worker-brief.md; research: research/supply-runtime-integration-research.md. Enum numbers 17–19 reserved S06, explicit20–22 supply. Level format6 S06 and Recipe schema5 supply remain candidates until reviewed integration; master actual Recipe4/Level5. S08 design now records effective geometry installation, bounded floor complement, trusted policy, one preparing kitchen, item/pose/front reference atomicity and existing decoration/unlock failure defects; implementation follows S06 stable ownership.

## 2026-10-02 Reviewed menu closure and operating increments

Master production commits 7dd149b75 / 4b78e8467 and scoped closure record 95f19cca4 contain all 87 menu deliveries, including actual binding/unbinding/rebinding of the 31 drinks. Master gates 20261002-200244-cooking-kitchen-loop (372/494/197) and 20261002-200449-cooking-et-level-runtime (494/197) passed with zero failures/skips. S04/S05/S09/S10/S11/S12/S13 are complete only for recorded pure-C# content-and-delivery scope. Root, S06/S07/S08/S14 and network remain incomplete.

Menu dispatch ctx_cf4084e88097 and supply dispatch ctx_aa1cb5f5926a settled and were released; their worktrees are preserved. Subsequent review fixes are owned by the authorized native agents, with one .NET window per worktree. Orca check currently has no undelivered messages.

Integration checkpoint 83be569cd adds the reviewed geometry projection helper only. Actual composite gate 20261002-200652-cooking-kitchen-loop passed 386/511/206 before the newer F08 policy and general allocation-watermark increments; it does not validate those subsequent edits or a full S08 installer. S06 F08 adds trusted serving-point/table policy and current-pose delivery checks; focused evidence and independent review are pending final consolidation. S07 review reproduced poisoned ordinary allocation watermarks and now generalizes required per-item allocation sequence validation; supply-worktree full gate remains in progress. Existing failed test evidence is retained alongside fixes.

S06/F08 was independently reviewed and committed in integration as 318483c1d. Coordinator gates 20261002-202643-cooking-kitchen-loop (386/511/213) and 20261002-202730-cooking-et-level-runtime (511/213) actually passed with zero failures/skips. Evidence: research/front-delivery-composite-verification.md and research/front-house-delivery-independent-review.md. Integration then imported S07 source as 3eedefb54 / 7f1ef8320 / c61cce1f5, preserving both front and supply enum/shape additions. These imported supply increments have not yet passed the combined integration gate.

Independent supply review found a further zeroed-item-sequence plus zeroed-counter recovery gap, recorded in research/supply-allocation-independent-review.md; correction is assigned to the supply owner before accepting the domain exit. ET supply fingerprint/envelope, Closing request gate and Level7/Recipe5 recovery integration are assigned separately. Full S07/preparation and S08 remain incomplete.

Reviewed front/helper commits merged to master as 484547d73 and 7a043b6cb. Actual post-merge master gates: 20261002-203658 kitchen386/511/213 and 20261002-203752 ET511/213, all pass/zero skip. Evidence research/master-front-delivery-verification.md. Current master Recipe4/Level6; integrated supply is not yet merged. S08 preparation decisions are recorded in its design; transient layout versus explicit major-decoration preferences remain distinct.

Integration now imports allocation-zero fix a0bfd5b59 as abc1d653f. Supply ET fingerprint/closing/Level7, Created-choice atomicity and effective geometry primitive are stable WIP with producer focused evidence; coordinator combined gate is running. Read research/supply-et-increment-review.md, research/supply-et-independent-review.md and research/prepared-geometry-increment-review.md. Effective geometry primitive is not a public preparing-layout flow or dynamic-layout recovery.

Stable supply/geometry primitive checkpoint committed as integration4e32fbe61. Actual coordinator kitchen438/563/218 and ET563/218 passed, no failures/skips; first C03 format-literal test failure retained. See research/supply-composite-verification.md. Master remains reviewed front7a043b6cb; preparing/layout installation is next, with root owning Lifecycle/host/Level checkpoint and supply owner only new trusted preparation configuration/helper/tests.

S08 actual preparation integration is now in progress after reviewed4e32fbe61. Trusted per-Level preparation configuration/footprint permissions/front-route helper and stable identity producer focused24/24 passed, with source in two new uncommitted files; evidence in the S08 research/preparation-configuration-increment.md. Root added internal first preparation kitchen initialization and Start reuse, and actual Lifecycle focused22/22 passed before the additional preparation-admission flag/DTO edits. The new Preparing gate and required Level7 candidate service-start tick/preparation identity fields are being integrated with host production by one host owner; no combined pass is claimed for that later WIP. Root owns Lifecycle/Level DTO, host owner owns host/new ET tests, helper owner owns only new configuration/test files. Master remains Recipe4/Level6.

Integration585e15982 first Preparing runtime is committed and independently coordinator-verified: 20261002-213444 kitchen462/589/223 and 20261002-213554 ET589/223, all passed/zero skips. See research/preparing-composite-verification.md. Master production remains7a043b6cb Recipe4/Level6. Host producer now owns next Preparing export/recovery and uninterrupted canonical control in host/tests only; root owns Lifecycle recovered-version Preparing admission. InstalledLayout is still null, no full S08 or S14 closure claimed.

Preparing recovery committed integrationd9b0e5603 after independent forged-zero-clock-front blocker reproduction and correction. Final214615 kitchen462/590/227 and214713ET590/227 passed/zero skips; earlier214151/214247 are preserved pre-fix evidence. See research/preparing-checkpoint-composite-verification.md. Next same-Level trusted initial/dynamic geometry installation: root core gate/reference-state staging and EffectiveSpatial seed, host producer ONLY host/new PreparedLayout ET tests. Root core geometry focused5/5 passed; that later WIP has no full gate yet. Cross-Level new-geometry permission/carry remains a subsequent explicit transaction, not covered by the seed-only correction.

S14 remaining exit audit captured in research/s14-operating-exit-audit.md. Existing authorized supply producer owns ONLY new CookingLevelObservation.cs and matching new tests, pure frozen projection; host owner retains host/layout. Observation source may proceed independently, but host owns integration .NET window until explicit release. Root integrates Observe later; no parallel business ledger or S14 completion claim. Core layout independent static review passed bounded preflight, without host WIP review or separate .NET.


## 2026-10-02 22:20 master preparation/layout verification

Reviewed source merged to d0eeb4b42. Actual post-merge kitchen gate 468/596/236 and ET gate596/236 passed, zero failures/skips. Definition3/Recipe5/Level7. See [master proof](research/master-prepared-layout-verification.md). Same-Level layout/preparation recovery is verified; cross-Level transaction, menu Ready availability and full S14 operating exit remain open.


## 2026-10-02 22:44 readonly observation master verification

Source16fdc55a2 reviewed and imported ase3a84d765. Post-merge kitchen473/601/238 and ET601/238 passed zero failures/skips; [proof](research/observation-composite-verification.md). S14 stays in_progress. Next authorized exact increment: [Ready design](../10-02-cooking-singleplayer-level-observation/design.md), [availability data mapping](research/s14-manufacturing-availability-mapping.md).


## 2026-10-02 23:06 baseline/staging core

Reviewed source11a49537a withactual final kitchen480/608/238 zero failures/skips after Location-null red/green. [Proof](research/baseline-generation-core-verification.md); S14 in_progress. Host producer owns successor/retry transaction and root will later connect menu policy and typed restart.

## Master125ffe906 verified continuation

Generation transaction and pure menu helper merged; actual master kitchen605/733/254 and ET733/254, zero failures/skips. See research/master-generation-menu-verification.md. Continue Host Ready and runtime permissions, then durable Host load and natural operating acceptance. Preserve one Recipe/Front/Level authority; no Unity execution.

## Scoped menu productionfa2f60e17

Host/runtime/checkpoint8 integration independently reviewed and master gates630/758/266 and758/266 passed, zero failures/skips. Actual red enum-count test preserved and corrected with stable tail-value assertions. See research/master-menu-policy-verification.md. Continue actual natural Front service/replay/restore, scope-narrowed carry and durable Host restart before completing S14.

Current reviewed source `07bb27d54`: durable Host typed baseline3 and four-branch natural operating accepted by actual master635/763/289 and763/289 gates. Read [master-durable-natural-verification.md](research/master-durable-natural-verification.md) for precise evidence. Natural successor cold continuation and narrower-scope carry remain in progress; S14/network/Unity completion is not inferred.


## 2026-10-03 reviewed recovery acceptance

Source86c3eb41d passed actual master kitchen644/772/298 and ET772/298, zero failures/skips. Natural durable successor, scoped carry, trusted global choices, atomic retry standard stock and technical quarantine/cold recovery are accepted within pure C# singleplayer. See parent task research/master-s14-recovery-verification.md and s14-final-exit-review.md. Explicit ET Preparing floor expansion is the remaining singleplayer proof. Network remains next; network-resume-audit.md records formal/source conflicts and unavailable physical second LAN host. Unity and S15 remain deferred.
