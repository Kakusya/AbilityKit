# Cooking Game 当前工程进度

## Issue #6 Cooking-only 续作（2026-10-06）

Owner 显式调用 cooking-dot-workflow，当前 dot 已 accept-plan `cf41218ab468a40457f6a15bbe0fc2556158b983`，规划与剩余出口见 [当前 task](../../../.trellis/tasks/10-06-cooking-issue6-truthful-gates/prd.md) 和 [完整决定](../../../.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/cooking-dot-flow/evidence/dot-plan-reply-raw.txt)。新分支 `Kakusya/issue6-cooking-truthful-gates` 从 master `c78ed3de83f69bb53ff1a09bbc23638115928e1e` 创建；仅适配两个 Cooking .NET gates 的结果/覆盖/provenance，未适配入口启动前 Blocked。旧十八 producer、Unity mirror/Editor/exporter、示例五项目验收退出条件被收窄，本轮 N/A/实际 NotRun；旧越界分支保持停止隔离。当前只有规划接受，实施/真实测试/最终接受/合并/关单均未完成。下方旧 #6 调度是历史，不恢复旧 Run 或授权。

Current checkpoint (2026-10-06): implementation `b8c53e8153b9c6f015e75040b9561b3078f05e5f` is pushed. Both genuine Cooking gates Passed/native0 (867+328 and644+867+328 tests); full216 isolated controls and main29 independent controls Passed with original source/dirty qualifications preserved. Main independently checked all owned archive bytes, binary linkage and TRXs. The distinct verifier is settled/revoked/idle. [Current review envelope](../../../.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/cooking-dot-flow/evidence/final-main-review-envelope.json) supersedes the earlier planning-only checkpoint. Exact-SHA final dot review, merge and actual merged-source integration remain NotRun; Issue6 is OPEN. Old out-of-scope tree stays isolated; Unity/physical LAN boundaries are unchanged.

## Owner 最高优先级范围与停止越界实施（2026-10-05）

- Owner 重申只做 Cooking（Cook／做菜项目）；Shooter、MOBA、Orleans 等其他项目仅为示例，不得修改。最高优先级规则已写入 [AGENTS](../../../AGENTS.md)，并加入 [SOP](../../../.trellis/spec/abilitykit/supervised-issue-delivery.md) 的派发和审计前置检查。
- Issue #6 原全示例适配计划偏离该边界：立即停止 Shooter／MOBA 等示例的后续切片，不再按原计划启动 MOBA 或五项目运行验收；共享改动必须重新证明 Cooking 的具体必要性，不能整项沿用旧授权。
- worker 的范围纠偏前源码 checkpoint 为 `4dd4f3fff0d7d08cf59dfdbbadc73fe35836705f`，已存在的越界提交与证据保留，未合入 master；本轮仅追加约束／任务文档。2026-10-05 本次核对：该 worktree 无 Orca 终端，未发现匹配的活动测试／执行进程，旧 Shooter dispatch 已 failed/abandoned；不重启。master 仍为 `7aa3e8b67c13a469192edaa761cb3dfc1fa9294b`。
- 下方“#6 实施中”与原整项合并安排保留为历史，由本节覆盖；整个 Issue 未验收，worktree 未删除。具体范围纠偏见 [记录](../../../.trellis/tasks/10-04-supervised-issue-delivery-sop/research/cooking-only-scope-correction.md)。

## 协调者接管与 Issue #6 实施中（2026-10-04）

- Owner 明确由当前协调者接替 dot 的方案审议、Issue 组织、派发、审计和收尾职责，采用 [协调审计与执行者 SOP](../../../.trellis/spec/abilitykit/supervised-issue-delivery.md)。下方等待 Dot 的调度已被本节覆盖，历史审阅证据保留。
- [Issue #6](https://github.com/Kakusya/AbilityKit/issues/6) 的修订计划 `5dc303a809` 已经协调者审阅批准，在 `issue6-test-gate-results` 由 GPT-6.1-Sol / medium 分段实施。2026-10-05 更新：B1 提交 `56ebd215d` 已修复步骤回执伪装；协调者独立复跑 133 项有 1 项输出捕获失败，并发现覆盖引用为空和非规范状态仍被接受。修复与剩余适配进行中，见 [SOP 试行审计记录](../../../.trellis/tasks/10-04-supervised-issue-delivery-sop/research/issue6-trial-audit.md)。整个 Issue 尚未验收。
- Owner 已授权本次审计通过后的本地 master 合并与该 worktree 清理；尚未执行。真实 .NET 门禁、集成检查和证据迁移仍待完成，不能用隔离测试结果替代。其余 Issue 的实施、合并或发布不由本次授权解锁。
- 既有 ET 固定版本、玩法/网络剩余出口、性能延期、Unity 后置和物理 LAN 未验证边界保留。

## Issue #5 收尾与 #6 接续（2026-10-04）

- #5 的最终文档版本 `530609fd0f0a264ea03a1e94a00647825f27cc67` 已经 Dot 复审接受，[PR #10](https://github.com/Kakusya/AbilityKit/pull/10) 已合并，merge SHA `f4aeff2fc1cae9efbbdf9a36484f0320d46a5100`。主仓库已快进到该版本；[任务归档与收尾证据](../../../.trellis/tasks/archive/2026-10/10-04-current-et-foundation-contract/research/closeout.md)。
- Owner 已要求收尾后直接开始 [Issue #6](https://github.com/Kakusya/AbilityKit/issues/6)。本轮先提交实施计划和结果 schema，等待 Dot 审议；该明确计划门仍适用，尚未授权跳过它修改运行器。其余 blocked Issue 未解锁。
- 以下 Issue #5 启动基线及更早进度保留历史上下文，由本节覆盖当前调度；既有玩法、ET、网络、性能、Unity 和物理 LAN 的验证边界不变。

## 当前基线与调度（2026-10-04，Issue #5）

- 已核验源码 HEAD：`5312c6e4bf2b612260297e2d8623aa362a9051e6`；静态审计基线 `a2cd7284e12d50a10bbb7abee6fc265577b9aa7c`。差异仅新增 48 行 [重建交接](reclone-handoff-2026-10-04.md)，没有新增构建或运行验收。
- [Issue #5](https://github.com/Kakusya/AbilityKit/issues/5) 是唯一 orca-ready：当前 ET 合同、源码核验、AGENTS 与依赖规划的文档工作。#1–#4/#6–#9 最新读取均 blocked；本地 [PRD](../../../.trellis/tasks/archive/2026-10/10-04-current-et-foundation-contract/prd.md) 与 [最小计划/冲突](../../../.trellis/tasks/archive/2026-10/10-04-current-et-foundation-contract/design.md)。标签不授权运行时迁移、升级、合并或发布。
- [当前 ET 合同](current-et-foundation-contract.md) 分开事实、目标和待批准施工。当前 ET 固定，ADR-0003 仍 Proposed；单一 writer、单一 Tick、现有分阶段提交保留。文档交付不表示全面迁移完成。
- S01–S14/N01 按原 SHA/范围已接受；N02/N03 整体未完成。command366 无终态、rich 四断点和 P6 原失败/出口保留，暂停不表示修复。可读来源为 [重建交接](reclone-handoff-2026-10-04.md) 与 [完整网络出口](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/current-network-exit-refresh.md)；[会话收尾](session-closeout-2026-10-04.md) 有字面问号损坏，不据其猜造事实。
- 功能/合作/恢复/故障正确性优先，当前施工以 Issue #5 文档范围为准。延迟/吞吐/native timer/scheduling/O04 仍 OWNER_DEFERRED，普通性能 NOT_ACCEPTED；Unity/S15 后置且未授权，物理两 PC LAN NOT_VERIFIED/environment-unavailable，不重复硬件问题。
- 本次文档工作 .NET/Unity/游戏/网络验收 NotRun。旧结果仅按原 task/check、原件与范围解读；备份不随 clone 分发，缺失原件不能重建“已通过”。下方全部是历史正文，不产生当前授权；AGENTS 原文见 [历史索引](history/agents-notices-2026-10-04.md)。

## 历史正文（原序保留，含字面问号损坏）

以下 Current/LIVE 与阶段通知仅描述当时源/范围，已由上方当前入口覆盖。保留原链接，不猜测损坏文本、不改写旧验收。

> 2026-10-04?????????[?????????????](session-closeout-2026-10-04.md)?????N02/N03???????????????????????????????

> Owner2026-10-03: latency/throughput and O04 native/timer candidate OWNER_DEFERRED, not completed. Current priority: existing-framework functional gameplay/cooperation/recovery/fault correctness. Read [current disposition](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/functional-first-owner-disposition.md). Preserve previous evidence/thresholds, physicalNOT_VERIFIED and Unity/S15deferred. Older optimization-first notices historical.

> Current2026-10-03: O03 boundedProfile81315 terminal1 as required (12initial/midpassed,6finalNOT_MEASURED,36endpointnative0), originals independently reviewed. Matchedmid no stable1ms gain; no adoption/import. Actual primitiveOFF/ON/OFF calibration79616 independently passed1800samples/native0 and exactleasecleanup, WaitOne1 medians15.5102/1.0145/15.5099ms. Isolated matched12pair timer-matrix SOURCE ONLY approved; build/runtime pending review. No networkperformance or productionadoption claim. Owner confirms secondPC unavailable; no repeated hardware question. N02/N03 remain incomplete; fullrich4/P6/ordinaryperformance/physicalLAN open, Unitydeferred. Read latest master-assembled-relay-verification.md/native-wait-calibration.md/current-network-exit-refresh.md; older notices historical.

> Current2026-10-03: isolated O03 corrected initialControls21873 passed both15/1 (four native0), independently checked complete typedState+Session/exactACKReady/currentclose/drain and loaded assemblies. Bounded18requested-row Profile granted as diagnosis; unreachable final5000-command targets remain NOT_MEASURED, no ordinary performance/default adoption/main import. Main concurrency21/common seven negatives accepted; N02/N03 and full rich/P6/ordinaryperformance/physical exits incomplete, Unity deferred. Read master-assembled-relay-verification.md and current-network-exit-refresh.md; earlier notices historical.

> Current2026-10-03: current concurrency21 two real pairs and offline verifier positive+seven targeted negative copies accepted within scope; O03 initial15/1 controls failed pre-client at genuine pending ACK boundary (original31939 retained). Reviewed correction9e10fd4d0 receives fresh BuildOnly only, not runtime accepted. S01-S14/N01 accepted; N02/N03 incomplete, full rich/P6/ordinary performance/physical LAN remain open, Unity deferred. Read current master-assembled-relay-verification.md; earlier notices are historical.

> Current2026-10-03: current-source efe06ff8a fresh forced compile and two independent-process concurrency21 pairs accepted (root47261terminal0; four endpoints native0; exact full inventory and original hashes checked). O03 driver2197e9849 source reviewed; BuildOnly granted, runtime NOT_RUN. S01-S14/N01 accepted, N02/N03 incomplete; P6/rich/performance remain open, physical LAN NOT_VERIFIED, Unity deferred. Sixteen unused worktrees retired; main+activeO03 retained. Earlier notices below are historical.

> Current2026-10-03: actual main13ecae215 SDK311/kitchen644-867-328/ET867-328 gates passed, zero failures/skips; root49812terminal0. S01-S14/N01 accepted, N02/N03 incomplete. O03 generic7/all25/unchangedoldconsumer pass scoped only; driver source review9a88 found blockers being fixed, not built/run. P6/rich failures and ordinary Release performance remain open. Sixteen unused trees archived/retired; physical LAN NOT_VERIFIED, Unity deferred. Current evidence: [master verification](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-assembled-relay-verification.md). Older notices are historical.
> Latest actual 2026-10-03: S01-S14/N01 accepted; N02/N03 in_progress. P0-P5 accepted; P6 failed repeat2 full-projection30s deadline. Rich43003 terminated exit1 in first case due runner default4MiB decoder; production Cooking has its bounded codec. Runner correction pending. O02 focused7 passed only; performance not accepted, physical two-PC unverified, Unity deferred. Read [actual evidence](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-assembled-relay-verification.md). Older running notices below are historical.

> Current checked 2026-10-03: P0-P5 each accepted three fresh actual pairs; P6 FAILED repeat2 unhealthy sample (30s full committed projection deadline), recovery does not pass profile. Actual rich audited BuildOnly88331 passed on54072313a with original manifest085517-9859398; four-case NoBuild43003 is RUNNING (first manual-paused), not accepted. Isolated O02 source90ecbfece focused7tests independently passed, only local microprobe, no end-to-end/production import. S01-S14/N01 accepted; N02/N03 in_progress; ordinary performance NOT_ACCEPTED; physical two-PC NOT_VERIFIED; Unity deferred. Main source/scripts remain stable during rich run. Timely unused-worktree cleanup remains required, nine actual retirements. [Actual evidence](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-assembled-relay-verification.md). Older notices below are historical.

> Current checked 2026-10-03: three fresh P0 repetitions accepted on final-close handshake source272c336bb; actual wrapper40142exit0 and independently equal full Host/client states with exact5s close windows. P1 is RUNNING under root38708; P1-P6 not accepted. O01b twelve Release samples are fully eligible but all four configurations NOT_ACCEPTED; no performance candidate imported. Rich companion/verifier source01800828e remains NOT_BUILT/NOT_RUN. Singleplayer S01-S14 and N01 accepted; N02/N03 in_progress; physical two-PC NOT_VERIFIED and Unity deferred. Read [actual relay verification](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-assembled-relay-verification.md). Earlier notices below are historical.

> Current checked 2026-10-03: S01-S14 singleplayer accepted; N01 contract accepted; N02/N03 in_progress. Latest assembled kitchen644/859/328 and ET859/328 gates passed (zero failures/skips). All four Release reference configurations completed three fresh samples each, are eligible, but NOT_ACCEPTED against unchanged performance criteria; see [actual Release reference](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/reference-release-plan.md). Corrected real relay controls passed; corrected numerical P0 session9642 terminated exit1 at cached-retry business conservation before FINAL_READY; diagnostics and source correction remain pending, P1-P6 unaccepted. Read [retained actual failure and current work](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-assembled-relay-verification.md). Rich companion source hold released; O01 byte-preserving list serialization candidate approved source-only in isolated worktree. Physical two-PC NOT_VERIFIED, Unity deferred. All older live-window and pending-evaluator notices below are historical.

> Current 2026-10-03: assembled kitchen644/859/328 and ET859/328 gates passed with zero failures/skips; audited relay build and original real socket/queue controls passed. First numerical P0 failed after real client close with UDP10054; bounded close correction remains source-only pending rebuild/control/P0. Release2x5 InProcess/UDP samples completed with target shortfalls, inventory-encoding eligibility correction pending;4x5 runs continue under sole owner9093. Rich four-cutpoint companion and offline verifier implementation approved, not verified. Read [current actual evidence](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-assembled-relay-verification.md). N02/N03 remain in_progress; physical LAN NOT_VERIFIED, Unity deferred. Earlier running-gate/not-built notices below are historical.

> Current2026-10-03: full bounded load matrix12configurations/36samples source ea182bc65 independently accepted by [load review](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/load-matrix-independent-review.md). Isolated concurrency tool actual two UDP process pairs/21phases each independently accepted by [concurrency review](../../../.trellis/tasks/10-02-cooking-network-gameplay-loop/research/concurrency-tool-independent-review.md). Root assembled kitchen/ET gates are RUNNING, not passed yet; relay source2d671ebe9 is NOT_BUILT/NOT_VERIFIED. Performance goals unmet, physical LAN NOT_VERIFIED, N02/N03 in_progress, Unity deferred. Earlier partial-matrix/window notices below are historical.

> Current follow-up 2026-10-03: master df0d18636 independently accepts three separate-process fault/load pairs (six zero exits, complete captured-state equality) and boundary44; actual master Cooking859 passed. Previous ET328/network-sdk311 remain earlier evidence; assembled broad gates await active load source freeze. Load matrix has six of twelve configurations accepted so far, performance NOT_ACCEPTED. Isolated raw P0-P6 relay and concurrency-tool implementation is approved, source-only while the load owner holds the exclusive .NET window. Physical two-PC NOT_VERIFIED; Unity deferred. Read [current master evidence](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-local-recovery-verification.md), [process review](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/process-fault-load-independent-review.md) and [boundary review](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/session-boundary-independent-review.md). Earlier notices below are historical.

> Current 2026-10-03 master verification: reviewed ACK/paused-publication correction and full-menu four-cutpoint recovery are integrated. Actual corrected gates passed: Cooking 815, ET 328, network SDK 311; zero failures/skips. N02/N03 remain in_progress; independent-process fault/load follow-up, performance analysis and physical two-PC verification remain open. Unity remains deferred. Evidence: [master-local-recovery-verification.md](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/master-local-recovery-verification.md). Earlier notices below are historical.

> Latest diagnostic follow-up: runner-only profile013845e26 imported9f18dcb41, independently reviewed6e3bb5c09. Both capacity controls and six fresh producer samples passed; UDP complete-projection p95=3019.5009/3126.8889/3140.605ms, no performance acceptance. Root master smoke for the unchanged import awaits the exclusive .NET window. [Profile review](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/measurement-profile-independent-review.md); [remaining exit audit](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/research/remaining-network-exit-audit.md). Current independent-process diagnostic runner retained spatial-fixture and final-close-handshake failures; these are not whole-run passes. New Session boundary test source is approved under its concrete plan; actual checks remain pending.

> Later2026-10-03: Created cleanup32/32 and first-Join identity36/36 are independently accepted. Corrected rich run has both endpoint PASS reports and equal gameplay hash, but wrapper ExitCode handling failed; whole command rerun is ongoing. N03 only starts bounded local deterministic recovery engineering under an explicit partial dependency exception ([PRD](../../../.trellis/tasks/10-02-cooking-network-reconnect-measurement/prd.md)); neither N02 nor physical LAN is inferred complete. Exact current outcomes remain in the linked N02 evidence.

> 2026-10-03: N01 contract accepted; N02 bounded production in_progress. Read [root acceptance](../../../.trellis/tasks/10-02-cooking-network-contract-review/research/root-contract-acceptance.md). Actual current network gates and physical two-PC LAN remain unverified; S01-S14 accepted source4dadd25c8 unchanged.

> Current pure C# singleplayer exit: S01-S14 completed on source4dadd25c8 with actual final gates644/772/299 and772/299, zero failures/skips. [Exact scope, evidence and next network exits](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-singleplayer-exit-verification.md). Unity/S15 remain deferred; two-PC LAN unverified. Earlier singleplayer-in-progress entries below are historical.

> Latest accepted source86c3eb41d: master kitchen644/772/298 and ET772/298 passed, zero failures/skips. [Actual recovery acceptance and remaining expansion proof](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-s14-recovery-verification.md). Earlier work-in-progress notices below are historical.

> Current uncommitted S14 recovery corrections: root focus48/48 and technical ET producer5/5 green; actual catalog retry and assembled gates pending. [Evidence and remaining acceptance](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/recovery-fixes-red-green.md). Master accepted proof remains below.

> Latest verified source `07bb27d54`: durable Host publication/load and four-branch natural operating acceptance are merged. Master kitchen635/763/289 and ET763/289 passed, zero failures/skips. Current definition3 /Recipe5 /Level8 and typed major baseline3 (required Host clock). Natural successor cold continuation and narrower-scope carry remain open. [Actual proof](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-durable-natural-verification.md). Earlier checkpoint entries below are historical.

> Latest verified sourcefa2f60e17: trusted scoped menu Host Ready, runtime permission and same-Level recovery are merged. Master kitchen630/758/266 and ET758/266 passed with zero failures/skips. Current definition3 / Recipe5 / Level8, typed major baseline2 separate. Natural service, narrowed successor carry and durable Host restart remain open. [Actual proof](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-menu-policy-verification.md).

> Latest verified source `125ffe906`: generation transaction and pure menu validator merged. Master kitchen605/733/254 and ET733/254 passed, zero failures/skips. Definition3/Recipe5/Level7 remain current; typed major baseline2 is separate. Host Ready/runtime permissions, durable Host restart and full natural S14 service remain open. [Evidence](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-generation-menu-verification.md).

> 2026-10-02 23:06: source master11a49537a adds reviewed typed major baseline2 and private generation staging core. Final root kitchen480/608/238 passed zero failures/skips after real null-location red/green fix. Host transaction/restart and Ready menu wiring remain work in progress. Definition3/Recipe5/Level7 remain unchanged. [Evidence](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/baseline-generation-core-verification.md).

> 2026-10-02 22:44: master e3a84d765 now includes reviewed frozen scoped Observe. Actual post-merge kitchen473/601/238 and ET601/238 passed, zero failures/skips. Formats definition3/Recipe5/Level7. Ready availability, cross-Level carry/permission/new geometry, failed baseline/durable load and full S14 operating exit remain open. [Evidence](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/observation-composite-verification.md).

> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# Cooking Game 当前工程进度

> 2026-10-02 22:20 latest master production `d0eeb4b42`: reviewed supply, shared Preparing kitchen, actual trusted layout installation and same-Level geometry recovery merged and verified. Master kitchen468/596/236 and ET596/236 passed, zero failures/skips. Current definition3/Recipe5/Level7; [master evidence and remaining exits](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-prepared-layout-verification.md). Cross-Level layout/permission transactions and full S14 operating exit remain incomplete; network/Unity stay later stages.

> 2026-10-02 21:48 integration `d9b0e5603`: Preparing export/recovery now verified with a real uninterrupted-versus-restored final canonical control and strict initial front-state checks. Final kitchen462/590/227 and ET590/227 passed, zero failures/skips; [evidence and preserved blocker](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/preparing-checkpoint-composite-verification.md). Trusted layout installation and full S14 exit remain outstanding. Master production remains7a043b6cb Recipe4/Level6.

> 2026-10-02 21:36 integration checkpoint `585e15982`: first Preparing runtime independently reviewed and coordinator gates passed (kitchen 462/589/223; ET Level 589/223; zero failures/skips). Preparation uses the same kitchen and ET tick; service starts at a recorded clock offset. Preparing recovery and actual layout installation remain incomplete; S06/S07/S08/S14 stay in progress. See [preparation verification](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/preparing-composite-verification.md). Master production remains `7a043b6cb`, Recipe4/Level6; integration Recipe5/Level7 is not yet merged.

> 2026-10-02 20:38 latest master: reviewed front ET/manual/recovery and trusted delivery destinations merged as `7a043b6cb`, with bounded layout projection helper `484547d73`. Actual master kitchen gate 386/511/213 and ET Level 511/213 passed with zero failures/skips. See [master front verification](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-front-delivery-verification.md). Recipe4/Level6 are current; supply Recipe5/Level7 remains an integration candidate. Preparing supply, actual layout installation and full S14 operating exit remain incomplete. Older checkpoints below are historical.

> 2026-10-02 20:04 最新本地 master：完整 87 菜实际生产交付与恢复已审阅合并 `7dd149b75`，kitchen 实际 372/494/197、ET Level 实际 494/197，全通过且0跳过。S04/S05/S09–S13 按纯 C# 内容与交付范围完成，见 [准确范围与门禁](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-complete-menu-verification.md)。S06 ET 接线和 S08 几何投影在集成分支待审阅；S07 供应、S08 原子安装与 S14 完整营业出口仍未完成。下面检查点为历史序列。

> 2026-10-02 19:41 最新本地 master：`cfd107c75` 已合并审阅后的 87 菜制作/盛装/恢复、S05 绑定核心与嵌套容器交付修复、受限前厅域层；合并后 kitchen gate 实际 340/462/166，全通过且无跳过。证据见 [master-menu-binding-gate-summary.json](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-menu-binding-gate-summary.json)。31 饮品绑定后交付、S06 ET 接线、S07/S08 实际供应布局和 S14 完整营业出口仍待完成；网络/Unity 后置。

> 2026-10-02 19:36 集成分支检查点：S05 绑定及嵌套容器归属修复、87 菜 ET 恢复、真实手工交接已组合验证，kitchen gate 实际 340 focused / 462 Cooking / 166 ET，0 失败/跳过。尚未回并 master；31 款饮品绑定交付与 S06–S14 完整经营出口仍待完成。见 [组合验证记录](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/s05-menu-composite-verification.md)。

> 2026-10-02 19:13 最新已合并增量：本地 master `79d93c923` 已接收审阅后的 S01–S03 纯 C# 核心及恢复修复；合并后 kitchen 与 ET Level 两门禁实际通过，Cooking 328/328、ET 73/73，无失败/跳过。证据与准确范围见 [master-core-integration-verification.md](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/master-core-integration-verification.md)。S04 菜单全量运行、S05 绑定及 S06–S14 经营整合仍待完成；菜单/布局/供应辅助已合并不等于其完整产品出口。网络尚未接续执行，Unity 仍后置。

> 2026-10-02 18:45 当前执行检查点：单机整合分支已有菜单来源、F31 兼容路线、布局校验和供应组件受限增量，组合门禁实际 focused 155/155、Cooking 276/276、ET 67/67。尚未回并 master；核心空间/手工/份数接口及完整 87 菜、前厅/供应/布局 ET 闭环仍未证明完成。当前监督对象、提交和证据目录见 [execution.md](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/execution.md)，完整剩余出口见 [completion-contract.md](../../../.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/completion-contract.md)。下列“只列 Task 不执行”是本日较早的历史范围，已被最新实施授权更新。

> 2026-10-02 规划补充（不改变下列实际交付）：owner 确认不改架构、当前专注单机，按“单机 → 网络 → 单机 Unity → 网络 Unity”缓慢推进，只列 Task 不执行。功能/菜单/架构及既有 plan 接续见 [总 plan](../../../.trellis/spec/cooking/gameplay-menu-plan.md)，已登记 20 个 planning 初稿。网络延期，Unity 条件性记录仍禁止执行。87 款菜单尚未导入 runtime；本轮只有资料/规划静态检查，没有新增产品验证。

> 文档类型：跨阶段状态入口，不是行为规格或新的完成证明。行为契约以 [`.trellis/spec/cooking/`](../../../.trellis/spec/cooking/index.md) 为准；历史命令与结果以对应归档 task 的 `check.jsonl` 为准。

## 1. 2026-09-16 治理结论

Owner 已批准把过去混在同一阶段 task 中的状态拆为三类：

1. **Archived verified delivery**：P0–P6 各自已有 `check.jsonl` 支持的纯 .NET 受限增量，按 `completed-limited-scope` 语义归档。
2. **Non-Unity successor backlog**：P1–P6 仍可能有价值但未启动、未批准、无时间表的产品化工作，统一见 [successor backlog](successor-backlog.md)；P0 无 successor。
3. **Prohibited Unity future scope**：Cooking Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 scene smoke 长期禁止、不可领取、非 blocker，统一见 [future scope](future-scope.md)。

`archived`/`completed` 只修饰重划后的有限纯 .NET 交付，不表示完整 P0–P6 产品出口、Unity 可玩版本、production transport、真实两 PC LAN 或 durable storage 已完成。

## 2. Archived verified delivery

| 阶段 | 已验证并收口的受限交付 | 历史 evidence |
|---|---|---|
| **P0 交互基础** | 纯 .NET 权威拾取/放下、唯一位置、稳定排序、原子校验提交、命令幂等与 canonical snapshot/hash | `10/10`；8 个 JSONL、21 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-interaction-foundation/check.jsonl) |
| **P1 会话权威** | transport-neutral binding、handshake、bounded ingress、baseline/delta、epoch/sequence、dedup 与 transport-loss diagnostics | build 0 warning/error；权威摘要 `20/20`；6 个 JSONL、50 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-lan-session/check.jsonl) |
| **P2 配方循环** | 单输入/单工序/3 tick fixture、product、plate、注入式 order accept/reject、幂等与 in-process equivalence | 当时完整回归 `27/27`；5 个 JSONL、19 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-recipe-loop/check.jsonl) |
| **P3 配置校验** | definition batch 全错误诊断、原子替换、不可变 snapshot、canonical identity/hash、schema migration blocked result | focused `6/6`，当时完整回归 `34/34`；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-config-validation/check.jsonl) |
| **P4 Match 生命周期** | fixture `Preparing → Ready → Started → Ended`、restart 新 Match/epoch、隔离、snapshot watermark | focused `6/6`，当时完整回归 `40/40`；10 条 evidence；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-match-lifecycle/check.jsonl) |
| **P5 持久化技术合同** | 长期 progress、confirmed settlement、幂等 ledger、integrity envelope、in-memory `prepare → commit → read` 与 validated restart | focused `12/12`，当时完整回归 `52/52`；7 条 evidence；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-persistence-management/check.jsonl) |
| **P6 网络测量基线** | 显式 `InProcess` workload/report、ingress-to-commit、queue/throughput/p50/p95/p99/allocation、fault trace 与 optimization blocker | focused `5/5`，完整回归 `57/57`；JSON/CSV/JSONL；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-network-measurement/check.jsonl) |

P1 旧 metadata 曾写 `19/19`，属于摘要漂移；原始权威 check event 为 `20/20`，本次不改写该历史事件，只修正摘要。P0 的运行时 artifact 目录仍可在 check event/evidence 文字中引用，但不再作为 manifest `file` context。

## 3. 当前可运行纵向链路

纯 .NET fixture 已能无界面地创建 scope 与实例、完成 in-process handshake、执行权威交互、运行最小配方、推进 Match、应用 settlement/progress，并生成 InProcess 测量诊断。此外曾有 pure .NET Cooking UDP 链路（LiteNetLib reliable-UDP loopback 与同机双进程 listen-host/client），其 task 已于 2026-09-21 因 owner 决定放弃并归档；三个 Cooking UDP 项目退出当前构建范围，源码与 artifact 证据保留，不得据此宣称能力被删除或从未存在。真实 artifact 仍将其标为 same-machine，且两台物理 PC LAN 未运行。以上证明有限领域合同、测试 transport 边界和同机真实 socket 曾经可以运行；不证明 Unity 表现、物理两机 LAN 或 durable storage，也不证明当前范围包含网络内容。2026-09-21 起，单机纯 .NET 范围另有一条可运行的番茄蛋花汤厨房闭环，其契约层、仿真规则、正式内容与 ET 宿主闭环验收分三个 task 交付，见第 4、5、6 节。 2026-09-22 起，该范围另有一条运行态恢复契约（checkpoint 与销毁重建等价验收），见第 7 节。

## 4. 2026-09-21 单机厨房闭环增量

Owner 于 2026-09-21 把范围收敛为单机纯 C#/.NET，并按两个 Trellis task 交付厨房闭环的契约层与仿真规则；两个 task 均已归档，均不含传输、UDP/KCP 与 Unity 内容。

| Task | 已验证并收口的受限交付 | Evidence |
|---|---|---|
| `09-21-cooking-kitchen-loop-contracts` | schema 升 `cooking-definition-v2`（物品容器能力、多输入集合、默认供应、完成形态；v1 配置与快照以结构化 blocked 诊断拒绝，不写迁移）、纯函数配方匹配（集合相等，歧义时返回确定候选表）、命令形状放松与仿真/ET 两套指纹跨面对齐 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-contracts/check.jsonl)；`artifacts/cooking-kitchen-loop-contracts/` |
| `09-21-cooking-kitchen-loop-simulation` | 容器即物品（`CookingContainerDefinition` 退役，内容物按容器物品 ID 归属，快照与 canonical 可观察）、七项权威原子动作（拾取/放下/放入/取出/启动加工/倒出/提交，`Plate` 退役）、放入即拒绝、锁输入与 item→process 反查、端走继续的位置白名单（腐败检测器保持可达）、两种完成形态进入 fixed-tick 原子交换、命令与 fixed-tick 共享产物 ID allocator、按容器当前位置判定提交可达性、番茄蛋花汤闭环 fixture（免工位打蛋、工位按加工类型绑定、烤箱配方、订单要求与完成回报、碗脏/净与带上限的干净碗池加 NPC 清洗注入端口）、批次争抢按 Tick/玩家/命令序号稳定排序 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-simulation/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-simulation/research/verification-2026-09-21.md)；`artifacts/cooking-kitchen-loop-domain/`（21 个目录） |

门禁：新增 P1 `cooking-kitchen-loop`（域构建、ET runtime 构建、focused `Gate=CookingKitchenLoop` 42/42、Cooking 163/163、ET runtime 40/40），回归门禁 `cooking-et-level-runtime` 同时通过；[`tools/test-gates.json`](../../../tools/test-gates.json) 与 [测试门禁规范](../../AbilityKit测试门禁与批量回归规范.md) §3 已同步。

以上只证明单机纯 .NET 厨房闭环规则与闭环 fixture 可运行、可确定性重放、可按门禁验证，且 ET 命令指纹金样已随 `Plate` 退役处理（pickup 与 StartProcess 两个既有金样字节不变，新增 PutIn 金样）。不证明失败条件与小关成功条件（owner 明确推迟）、订单生成节奏与前厅、固定伙伴最终人数、生产传输（KCP，未启动未批准）、durable storage、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。计划中任务③的单机验收动作（指纹金样重锚、验收 evidence 落盘、回归门禁实际运行与记录）已由任务①②连带完成，本文与 [Todo.md](../../Todo.md) 的状态表述即其收口，未为此新建独立 task。

2026-09-21 另有一条单机增量把正式内容与 order owner 落地，见第 5 节。

## 5. 2026-09-21 单机正式内容与 order owner 增量

Owner 于 2026-09-21 批准 Trellis task `09-21-cooking-formal-content-and-orders`（successor backlog P2 首条），把“单输入/单工序/3 Tick fixture + 测试端口判定订单要求”的临时形态替换为正式内容与领域订单归属；task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 正式内容目录：数据文档 `cooking-definition-v2`（`src/AbilityKit.Game.Cooking/Content/cooking-content-v2.json`）经 `CookingContentCatalog` 加载并过 v2 校验；10 物品、4 工位、4 配方（切番茄/打蛋/番茄蛋花汤/烤面包）、1 订单模板、5 项标准初始供应；加工时长等数值只存在于内容文档；烤面包按 owner 采纳版本“面包片→烤面包”，占位 dough 退役 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/research/verification-2026-09-21.md)；`artifacts/cooking-formal-content/` |
| v2 校验扩展：候选/canonical 增加订单模板与标准初始供应两段（身份字符串保持 v2）；订单模板外键（要求 recipe 存在、要求容器定义存在、带容器能力且接受该 recipe 产物）与供应项校验（定义存在、数量为正、位置语法、station 引用）；两处既有规则显式放宽（工位可声明零能力、免工位配方豁免 `CapabilityUnavailable`） | 同上；spec [cooking-config-validation](../../../.trellis/spec/cooking/cooking-config-validation.md) P3 行 |
| order owner 移入领域：订单簿（模板、要求、Open/Completed）与快照/canonical 可观察；开单是前厅注入入口（`OpenOrder`，与 `CompleteWash` 同模式，不产生命令事件）；`ICookingOrderPort`/`CookingOrderSubmission`/`CookingOrderAcceptance` 退役 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/check.jsonl) |
| 提交/结算契约：五个结构化拒绝分支（未开单、订单已完成、recipe 不匹配、容器定义不匹配、产物已消费）全部 mutation-free；成功提交原子消耗产物、订单 Completed 一次、追加结算记录（sequence/order/template/recipe/product/player/container/logicalTick，无评分字段）；闭环 fixture 改从正式内容运行并保持确定性重放 | 同上 |

门禁：沿用 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 54/54、Cooking 175/175、ET runtime 40/40，含三个二进制指纹金样字节不变），回归 `cooking-et-level-runtime` 同时通过；未新增 gate。

以上只证明正式内容可数据驱动加载与校验、订单归属与提交/结算契约在领域内确定成立、闭环可从正式内容确定性重放。不证明评分/收益/评价与小关结算（owner 推迟，结算记录刻意不含这些字段）、失败条件与失败重试（owner 推迟）、订单生成节奏与前厅顾客/NPC 过程（订单只能由前厅经 `OpenOrder` 注入）、过度加工与烧焦、跨小关装修/道具/Buff、检查点、Level/Map schema 对内容的正式引用（P3）、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。

## 6. 2026-09-21 ET Level 闭环验收增量

Owner 于 2026-09-21 批准 Trellis task `09-21-cooking-et-closed-loop-acceptance`（Todo P0-C1 未勾项"让 ET fixed-tick host 承载同一条番茄蛋花汤闭环验收"），把第 4/5 节只在领域仿真层运行的闭环搬到 ET fixed-tick Level 宿主；task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 正式内容直达 ET 宿主：`CookingContentCatalog.Load`（测试输出目录的 `cooking-content-v2.json`）→ `BuildFixture` → `ApplyStandardInitialSupply`，ET 侧不再手写第二套 items/appliances/recipes 字典；`CookingContent` 增露加载时经 v2 校验的快照，Level 生命周期与 preparation 配置身份同一来源 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-et-closed-loop-acceptance/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-et-closed-loop-acceptance/research/verification-2026-09-21.md)；`artifacts/cooking-et-closed-loop/` |
| 全链路走宿主命令路径：E01 从标准初始供应出发，17 条玩家命令全部经 `TryEnqueue` + `Tick()`（每帧一个最小命令批次 + 一次 fixed tick），切番茄/打蛋/煮制的 10 个纯时钟帧只由宿主帧驱动，走完取料、切、入锅、打蛋、倒蛋液、煮制、端走、倒汤、开单（帧间注入）、提交、洗碗回池；帧结构断言钉住命令帧单 disposition、时钟帧零 disposition、`HostFrameSequence == LogicalTick`；`AdvanceTicks` 在 admission 被 `ReservedClockOperation` 拒绝（宿主拥有时钟） | 同上；`artifacts/cooking-et-closed-loop/E01/`（17 条证据，全部 Accepted，终态 `LogicalTick=27`） |
| 拒绝零变更两级证明：E02 以领域 L03 同构场景（烤面包入碗 + 蛋花汤订单）经宿主提交，命令级 `result.StateVersion == before.Version` 且无事件，帧级与"同序列纯时钟帧"对照臂 canonical 相等（每帧固有的 tick 推进由对照臂抵消）；无结算、订单仍 Open、碗中菜品不回滚 | 同上；`artifacts/cooking-et-closed-loop/E02/`（8 条证据，末条 Rejected/OrderRequirementMismatch、0 事件） |
| 确定性与回归：E03 两遍完整宿主闭环 canonical 文本与 Sha256 一致；既有 24 个宿主机制测试与 legacy 纵切不改写法；三个二进制指纹金样（pickup/start-process/put-in）字节不变（`CookingLevelEtHostTests` 零改动）；命令 wire 形状不变；未新增 gate | 同上 |

门禁：沿用 P1 `cooking-et-level-runtime`（Cooking 175/175、ET runtime 43/43，本任务 +3）与 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 54/54、Cooking 175/175、ET runtime 43/43），均全步骤 exit 0；[`tools/test-gates.json`](../../../tools/test-gates.json) 未改动。变异测试 4 项全部杀死（订单完成断言、标准供应工位项、宿主每帧 fixed tick 数、内容快照赋值）；evidence 经不加载被测程序集的独立脚本复验（字段完整、批量严格递增、tick 递增、操作序列与时钟间隔、拒绝记录零事件）。

以上只证明同一条番茄蛋花汤闭环可在 ET fixed-tick Level 宿主上以正式内容运行、拒绝在宿主命令路径零变更、可确定性重放，且宿主机制与指纹金样无回归。不证明评分/收益/评价与小关结算（owner 推迟）、失败条件与失败重试（owner 推迟）、前厅与订单生成节奏（订单只能由前厅经 `OpenOrder` 注入）、过度加工与烧焦、跨小关规则、检查点与 snapshot/checkpoint 分离、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。宿主与领域闭环的 canonical 不要求字节一致：宿主每帧推进一个 tick（终态 `LogicalTick=27`），领域 L01 用 `AdvanceTicks` 与显式帧 1–8（终态 `8`），该差异是宿主拥有时钟的必然结果，已在 spec 显式记录为口径而非回归。

## 7. 2026-09-22 恢复 checkpoint 契约增量

Owner 于 2026-09-22 批准 Trellis task `09-22-cooking-checkpoint-recovery`（Todo P0-C1 前两条未勾项：区分同步 snapshot 与恢复 checkpoint；“导出 checkpoint -> 销毁 host -> 重建 -> 继续运行”等价验收），task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 同步快照与恢复 checkpoint 显式区分：`CookingRecipeCheckpoint` 覆盖物品/tombstone、活动加工（elapsed/required/completion/container/lockedInputs）、容器**有序**内容、订单、结算、消耗产物账、干净碗池计数、去重账本、事件/tick 历史与 event sequence、三个 ID 计数器、state version/logical tick 与 scope；可派生索引（持物、进程索引、锁输入反查）由载荷重建；`CookingRecipeSimulation.RestoreCheckpoint` 整册换入（原子替换、结构化拒绝、零变更），恢复后既有 fixed-tick 腐败检测器在下一 tick 兜底复核 | [check](../../../.trellis/tasks/archive/2026-09/09-22-cooking-checkpoint-recovery/check.jsonl)；[research/verification-2026-09-22.md](../../../.trellis/tasks/archive/2026-09/09-22-cooking-checkpoint-recovery/research/verification-2026-09-22.md)；`artifacts/cooking-checkpoint-recovery/` |
| 宿主级信封与恢复入口：`CookingLevelCheckpoint`（level scope/epoch、config identity、preparation、lifecycle 状态/version、HostFrameSequence、命令水位 + 整册仿真载荷）经 `CookingLevelCheckpointCodec`（格式版本 + 完整性 + 结构化读回）序列化；`CookingLevelEtHost.ExportCheckpoint` 前置 Running 且 pending 为空；静态 `Restore` 按同一代际重建（同 scope/epoch/config identity、仿真由工厂创建后整册换入、HostFrameSequence 单调不 reset、失败即释放宿主编修） | 同上；spec [`cooking-recipe-loop.md`](../../../.trellis/spec/cooking/cooking-recipe-loop.md) 2026-09-22 修约节 |
| 等价验收：R01 基线臂与恢复臂（煮制进行中导出 → 销毁 → 重建 → 继续）终态 canonical/Sha256、state version、logical tick、下一产物 ID（烤面包探针 `product-4`）、结算次数与两份终态 checkpoint canonical 全部相等，两臂 evidence 逐位一致；R02 导出前置（pending 非空/Paused 拒绝）；R03 跨代际/跨配置/载荷投毒结构化拒绝；R04 HostFrameSequence 连续；域内 C01 覆盖表逐项、C02 序列化往返后续跑等价、C03 篡改/截断/外键拒绝零变更、C04 恢复后去重账本仍生效 | 同上；`artifacts/cooking-checkpoint-recovery/`（R01 两臂各 21 条、C02 双臂 21/7 条证据，独立脚本复验通过） |

门禁：沿用 P1 `cooking-et-level-runtime`（Cooking 179/179、ET runtime 47/47，本任务 +8）与 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 58/58、Cooking 179/179、ET runtime 47/47），均全步骤 exit 0；[`tools/test-gates.json`](../../../tools/test-gates.json) 未新增 gate，仅 `cooking-kitchen-loop` description 补“恢复 checkpoint 契约”措辞（域内新测试挂 `CookingKitchenLoop` trait）。变异测试 5 项全部杀死（tombstone 不入账、干净池计数不入账、去重账本不入账、锁输入不入账、HostFrameSequence 不续接）；既有测试零回归，三个二进制指纹金样字节不变（`CookingLevelEtHostTests` 零改动）。

以上只证明单机纯 C# 的运行态恢复契约成立：checkpoint 自包含、可结构化拒绝、销毁重建后与不中断基线不可区分。不证明 durable storage 或进程崩溃恢复（store 未实现）、跨小关 checkpoint 产品语义（保存/清除/加载流程，见 09-19 讨论 PRD）、失败条件与失败重试、评分/收益/评价、前厅与订单生成节奏、Paused 代际导出、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。宿主与领域闭环计数器口径不变：宿主每帧推进一个 fixed tick，checkpoint 的 LogicalTick 与 HostFrameSequence 在导出点相等。

## 8. 2026-09-22 小关成功交接增量

成功换代不再新建空厨房。`ExportSuccessHandoff` 保留物品、加工、容器、脏/净碗、消耗产物账、下一 Process 与下一产物 ID；清除订单、结算、去重和两类事件；逻辑 Tick、事件序号、状态版本、下一结算序号归零。宿主把这份厨房换入下一代并停在 `Created`。准备态不能改厨房。工位升级和写盘仍未做。失败重开见第 9 节。

门禁：`cooking-kitchen-loop`（focused 61/61、Cooking 182/182、ET runtime 49/49）与 `cooking-et-level-runtime`（Cooking 182/182、ET runtime 49/49）均 exit 0。日志在 `local/Logs/test-gates/20260922-112553-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-112611-cooking-et-level-runtime`。

## 9. 2026-09-22 小关失败重开增量

失败换代不再把空厨房留到下一次 `Start`。`CreateRetry` 在同一 `LevelId`、更高 `LevelEpoch`、同一 Match 上安装下一代，丢掉失败现场，用工厂新建仿真并按 `ApplyStandardInitialSupply` 摆厨房，然后停在 `Created`。准备态不能改厨房。不写盘，也不改同代际恢复或成功交接。失败条件仍未定义。

门禁：`cooking-kitchen-loop`（focused 61/61、Cooking 182/182、ET runtime 51/51）与 `cooking-et-level-runtime`（Cooking 182/182、ET runtime 51/51）均 exit 0。日志在 `local/Logs/test-gates/20260922-154801-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-154818-cooking-et-level-runtime`。

## 10. 2026-09-22 小关成功结算确认增量

成功收口后、交接前，可以按这一代的 Match、`LevelId`、`LevelEpoch` 记住本关结算条。同一份列表再确认一次不再生效。失败、未结束和准备态不能确认。交接后的空账不能盖掉已经记住的列表。不打分，不加钱，不写盘。

门禁：`cooking-kitchen-loop`（focused 63/63、Cooking 184/184、ET runtime 52/52）与 `cooking-et-level-runtime`（Cooking 184/184、ET runtime 52/52）均 exit 0。日志在 `local/Logs/test-gates/20260922-170119-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-170138-cooking-et-level-runtime`。

## 11. 2026-09-22 小关结算落盘增量

成功确认后的结算列表可以按这一代的 Match、`LevelId`、`LevelEpoch` 写到调用方给的目录。关掉读取器再用同一目录新建一个，读回的仍是同一份列表。同一份再写一次是重复。换一份列表被拒绝，不覆盖第一次。空列表和「没有这条记录」不是同一种结果。截断或篡改后的文件读取失败，不当成空确认。文件没写上时，这一次新的内存确认会撤回。失败关卡和交接之后的空账不能写。不打分，不加钱。

门禁：`cooking-kitchen-loop`（focused 66/66、Cooking 187/187、ET runtime 53/53）与 `cooking-et-level-runtime`（Cooking 187/187、ET runtime 53/53）均 exit 0。日志在 `local/Logs/test-gates/20260922-174507-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-174525-cooking-et-level-runtime`。验收只用同一测试进程里的新读取器，不证明断电或另一个操作系统进程被杀死后的恢复。

## 12. 2026-09-22 跨小关装修、道具、Buff 与成功检查点增量

准备态可以把旧工位换成新工位。没做完的工序跟着走，已经走过的进度和所需时长都不变。工位不存在，或者新工位上已经有工序，整次拒绝。解锁的定义按标准供应再摆进延续厨房，不把厨房推倒重来。煮制加速只缩短这之后新开始的番茄蛋花汤，正在煮的那一锅不改，也不能叠两层。大关检查点只在这些选择锁定、并且这一关已经成功交接之后写入。失败不写，坏文件不能当成一份新餐厅。

门禁：`cooking-kitchen-loop`（focused 70/70、Cooking 191/191、ET runtime 54/54）与 `cooking-et-level-runtime`（Cooking 191/191、ET runtime 54/54）均 exit 0。日志在 `local/Logs/test-gates/20260922-185452-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-185531-cooking-et-level-runtime`。评分和真实断电恢复仍未做。

## 13. 2026-09-22 前厅询问、洗碗与小关时间增量

一位固定伙伴在营业时间按到店顺序询问，问完才开单。没有空桌不入座，收尾不再来人。没有待询问的桌子时，伙伴才去洗脏碗。顾客等到上限仍没吃上就离席；已经开出的订单标成未满足，不写结算，也不算失败。营业结束、座位空、伙伴空闲之后，才允许由前厅驱动成功。旧的直接成功收口仍可调用。宿主只在运行帧之后推进一步，暂停不推进。

门禁：`cooking-kitchen-loop`（focused 75/75、Cooking 196/196、ET runtime 55/55）与 `cooking-et-level-runtime`（Cooking 196/196、ET runtime 55/55）均 exit 0。日志在 `local/Logs/test-gates/20260922-224049-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-224111-cooking-et-level-runtime`。可见顾客、收益、评价、伙伴成长和失败条件仍未做。

## 14. 2026-09-22 用餐占桌增量

订单完成后，顾客按配置的用餐 tick 继续占桌，到点才离席。这段时间不能由前厅结束服务。未满足仍然马上离席，不写结算。没挂前厅的关卡仍可直接结束。

门禁：`cooking-kitchen-loop`（focused 76/76、Cooking 197/197、ET runtime 56/56）与 `cooking-et-level-runtime`（Cooking 197/197、ET runtime 56/56）均 exit 0。日志在 `local/Logs/test-gates/20260922-232144-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-232204-cooking-et-level-runtime`。收益、评价和可见顾客仍未做。

## 15. 2026-09-22 下一小关清空前厅增量

成功交接后，前厅的座位、未满足和营业时钟清空。正在洗的碗会先洗完。还没洗的脏碗留在厨房，下一关伙伴空闲时继续洗。失败重开不走这次清空。

门禁：`cooking-kitchen-loop`（focused 77/77、Cooking 198/198、ET runtime 57/57）与 `cooking-et-level-runtime`（Cooking 198/198、ET runtime 57/57）均 exit 0。日志在 `local/Logs/test-gates/20260922-235333-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-235404-cooking-et-level-runtime`。收益、评价和可见顾客仍未做。

## 16. 2026-09-23 失败重开丢掉前厅增量

失败重开接受新厨房后，前厅的座位、未满足、营业时钟和洗碗队列都丢掉。正在询问的顾客不会在新厨房开出订单，正在洗的旧碗也不会被洗进新厨房。成功交接仍先洗完正在洗的碗。

门禁：`cooking-kitchen-loop`（focused 78/78、Cooking 199/199、ET runtime 58/58）与 `cooking-et-level-runtime`（Cooking 199/199、ET runtime 58/58）均 exit 0。日志在 `local/Logs/test-gates/20260923-005749-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260923-005810-cooking-et-level-runtime`。失败条件、收益和可见顾客仍未做。

## 17. 2026-09-23 交接前收完询问增量

成功交接导出前，本关来过但还没开出订单的桌子会先开单。这张订单计入本关被清除的数量，下一关订单簿为空。伙伴已经开始洗的碗仍先洗完。

门禁：`cooking-kitchen-loop`（focused 78/78、Cooking 199/199、ET runtime 59/59）与 `cooking-et-level-runtime`（Cooking 199/199、ET runtime 59/59）均 exit 0。日志在 `local/Logs/test-gates/20260923-090136-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260923-090154-cooking-et-level-runtime`。收益、评价和可见顾客仍未做。

## 18. 2026-09-23 失败重开带走当前进程选择增量

失败重开传入当前进程的进度时，新厨房仍先按标准供应重建，再额外摆上解锁定义，并挂上同一份煮制加速。套用发生在安装下一代之前。未知工位或未知解锁拒绝这次重开，失败现场留在原代际。不写大关检查点。不传进度时和原来一样。

缩短后的时长仍只记在进度对象上。新开始的煮制如果把所需 tick 改短，固定时钟会拒绝这份工序。正式内容没有第二口灶，所以这次重开还不换新工位。

门禁：`cooking-kitchen-loop`（focused 80/80、Cooking 201/201、ET runtime 61/61）与 `cooking-et-level-runtime`（Cooking 201/201、ET runtime 61/61）均 exit 0。日志在 `local/Logs/test-gates/20260923-093600-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260923-093617-cooking-et-level-runtime`。失败条件、评分和可见顾客仍未做。

## 19. 2026-09-24 基础积分与星级网络共识

归档 task `09-24-cooking-scoring-network-slice` 已实现第一版积分/星级合同：订单模板配置固定基础分，成功结算只计分一次，未满足订单 0 分且不倒扣；当前小关总分按配置阈值映射为 0–3 星，0 星仍自然完成。`CookingRecipeSnapshot` canonical/SHA-256 包含总分和星级，同机 Loopback UDP Host/Client 已验证相同投影。`cooking-kitchen-loop` 当时通过（Cooking 208、ET Runtime 61）。

以上不包含收益、货币、小费、长期进度奖励、速度/连击/品质倍率或复杂评价平衡。

## 20. 2026-09-28 跨关选择与状态承接 LAN 纵切

Owner 批准 Trellis task `09-28-cooking-level-transition-choices-lan` 后，既有单机跨关领域能力接入生产 `CookingSessionHost`/`CookingSessionClient`。完整会话投影现在包含 Level scope、generation、sequence、装修/解锁/Buff 和厨房快照，并以稳定 canonical 计算 SHA-256；Recipe command 携带 Level identity，旧 Level 命令、旧 generation 和乱序/重复快照不能覆盖下一 Level。

Host 的跨关提交复用 `ExportSuccessHandoff`/`AcceptSuccessHandoff`、`CookingMajorProgress` 与 `CookingMajorCheckpointStore`：检查点写入成功后才安装并广播下一 Level；写入失败恢复源厨房、Level scope 和客户端投影。成功 handoff 同时在领域根部清除 `IsClosing`/`IsCompleted`，新 Level 从非营业、非完成状态开始。Match-scoped 重连凭证跨 Level 保持有效，重连只接收当前完整基线。

验证：新增 4 个跨关 LAN 测试；Cooking 完整测试 213/213。`cooking-kitchen-loop`（focused 92/92、Cooking 213/213、ET runtime 61/61）与 `cooking-et-level-runtime`（Cooking 213/213、ET runtime 61/61）均 exit 0。日志在 `local/Logs/test-gates/20260928-175010-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260928-175045-cooking-et-level-runtime`。

以上只证明同一台电脑上的生产 Cooking session + LiteNet loopback 跨 Level 技术纵切成立，不改变当前产品传输选型，不证明两台物理 PC LAN、主机迁移、Profile/SaveSlot、真实断电恢复或 Unity 可玩版本。

## 21. 2026-09-28 可见顾客与固定伙伴运行时纵切

Owner 批准 Trellis task `09-28-cooking-customer-companion-runtime-slice` 后，既有 `CookingFrontOfHouse` 从匿名桌位状态升级为稳定的 Level-local 顾客实体和固定伙伴工作投影。每次入座分配新的 `customer-N`，订单身份跟随顾客；同一桌连续接待不会复用订单。snapshot 可观察等待询问、询问中、已开单、用餐四阶段，以及伙伴的询问/洗碗目标和 elapsed/required tick，并以稳定 canonical 计算 SHA-256。

前厅 checkpoint 覆盖日程、活动顾客、伙伴工作、洗碗队列、未满足记录、营业时钟和身份水位；恢复先完整校验再整册换入，顾客/桌位/订单关联、伙伴目标、重复队列、时钟和未满足订单投毒均结构化拒绝且原对象零变更。`CookingLevelCheckpoint` 格式升至 v2，ET host 导出/恢复时厨房和前厅共同成功或共同失败；v1 不迁移。

验证：Cooking 完整测试 218/218，ET Runtime 63/63。`cooking-kitchen-loop`（focused 97/97、Cooking 218/218、ET Runtime 63/63）与 `cooking-et-level-runtime`（两个 build 零警告、Cooking 218/218、ET Runtime 63/63）均 exit 0。日志见 task `check.jsonl` 和 `research/verification-2026-09-28.md`。

以上只证明单机纯 C# 的运行态可观察性与同 Level 销毁重建恢复成立。不证明 Unity 表现、坐标/寻路/动画、伙伴成长与关系、收益/小费/评价、LAN wire、两台物理 PC LAN、durable store 或 process-crash 恢复。

## 22. 2026-09-29 固定伙伴小关内成长增量

Owner 批准 Trellis task `09-28-cooking-companion-level-growth` 后，固定伙伴获得第一项 Level-local 成长。询问成功开单和洗碗成功完成各累计一次；累计完成 3 个任务后派生解锁洗碗加速，之后新认领洗碗所需 Tick 为基础值的一半、向上取整且最低 1 Tick。任务认领时冻结 `RequiredTicks`，已经开始的任务不会因之后解锁而改速；第三次完成后同一固定 Tick 新认领的洗碗立即使用加速值。

成长完成计数是 `CookingFrontOfHouse` 内唯一可变来源，解锁状态只从计数和 schedule 阈值派生。前厅 snapshot/canonical/SHA-256 包含阈值、完成计数、解锁状态和冻结耗时；同 Level checkpoint 保留这些状态并原子拒绝负计数、派生不一致和非法耗时。失败重开和下一 Level 清零成长；ET host 无需新增生产状态，继续透传领域前厅 snapshot/checkpoint。

验证：领域聚焦 `CookingFrontOfHouseTests` 20/20，ET checkpoint 聚焦 7/7。`cooking-kitchen-loop`（focused 104/104、Cooking 225/225、ET Runtime 64/64）与 `cooking-et-level-runtime`（Cooking 225/225、ET Runtime 64/64）均构建 0 警告、0 错误并 exit 0。日志见 task `check.jsonl` 与 `local/Logs/test-gates/20260929-094117-cooking-kitchen-loop`、`local/Logs/test-gates/20260929-094410-cooking-et-level-runtime`。

以上不包含 LAN/session 前厅投影、Unity 表现、更多伙伴能力、跨 Level 或长期成长、收益/评价、Profile/SaveSlot、durable store 或 process-crash 恢复。

## 23. 2026-09-29 单机多订单菜单纵切

Owner 批准 Trellis task `09-29-cooking-singleplayer-multi-order-menu` 后，既有烤面包配方被提升为第二种可直接点单菜品。正式内容增加容量 1、只接受烤面包的 `plate`，碗不再接受烤面包；标准供应增加 2 个干净盘。订单模板增加 `toasted-bread-order`，临时基础分 50；番茄蛋花汤保持临时 100 分，星级阈值仍为 100/200/300。

前厅现在显式绑定有序菜单，按顾客 Level-local `ArrivalOrder` 派生模板，临时序列为汤、面包、汤、面包。菜单顺序和每位已开单顾客的实际模板进入 snapshot/canonical/SHA-256；ET host 复用同一领域菜单，不维护第二模板游标。`CookingLevelCheckpoint` 格式升至 v3，同 Level 恢复校验菜单、顾客模板和厨房订单一致性，并证明恢复后下一顾客模板与不中断基线一致。

验收覆盖同一 Level 完成一份汤和一份烤面包：两笔 settlement 分别保存正确模板/recipe，总分累计 150；盘子提交后进入既有清洗流并只恢复 plate clean-pool。聚焦验证为 Cooking 33/33、ET Runtime 24/24。`cooking-kitchen-loop`（focused 105/105、Cooking 226/226、ET Runtime 67/67）与 `cooking-et-level-runtime`（Cooking 226/226、ET Runtime 67/67）均构建 0 警告、0 错误并 exit 0；日志在 `local/Logs/test-gates/20260929-165608-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260929-165643-cooking-et-level-runtime`。

固定轮换、汤 100、面包 50、星级阈值、干净盘 2 个以及既有 recipe/前厅 Tick 全部是临时测试基线，不代表正式菜单、平衡或经济设计。LAN/session wire、UDP/KCP、Unity、第三种菜品、顾客偏好、收益/经济、Profile/SaveSlot 与 durable/process-crash 恢复均未修改。

### Non-Unity successor backlog

P1–P6 尚可另行审议的工作包括两台物理 PC LAN 验收、Room/Match 产品语义、durable store、process-crash 恢复、批准 workload/threshold 和非 Unity 优化验证。原 UDP task 曾提供 LiteNetLib minimal wire/adapter、loopback 与 same-machine harness，但未替代两 PC 证据，也未解决完整 production transport 的认证、安全、重连或产品生命周期语义；该方向已于 2026-09-21 放弃，传输计划改用 KCP，属未启动、未批准、无时间表的后续工作。基础积分/星级、固定伙伴首个 Level-local 洗碗加速和单机两菜品订单纵切已完成，正常产品流程不设置业务失败。正式菜单生成与平衡、更多伙伴能力、长期成长、LAN 投影、收益/评价、更多顾客内容、Profile/SaveSlot、真实断电恢复、connection→PlayerId 绑定与 ECS 清退仍需分别审议。

### Prohibited Unity scope

Cooking Unity 应用层、场景、authoring/export、projection、UI、动画、EditMode 与 scene smoke 长期禁止实施，不再作为旧 task、successor 或完整出口的当前 blocker。其历史来源、跨宿主 authority/identity/stale-input 不变量和重新授权条件见 [future scope](future-scope.md)。

## 24. 后续读取顺序

1. 读取本文确认三类状态。
2. 读取 [技术路线](technical-roadmap.md)、[交付计划](delivery-plan.md) 与 [Cooking spec index](../../../.trellis/spec/cooking/index.md)。
3. 需要历史证据时读取归档后的 task PRD/design/implement/check；不得因归档推导完整产品完成。
4. 只有 owner 明确批准新范围后才新建 Trellis task；不要恢复已归档 task。
5. Unity 重新授权必须满足 [future scope](future-scope.md) 的独立条件；non-Unity 后续从 [successor backlog](successor-backlog.md) 选择并重新审议。

## 25. 维护规则

- 本文只汇总状态与链接，不复制行为契约、测试矩阵或未来 checklist。
- 未实际运行的 Unity、两 PC LAN、durability 或 global gate 继续是 not-run/未完成；本次同机 loopback protocol 证据不能替代这些出口。
- `.trellis/migration/legacy-cooking-changes/` 保持只读。
