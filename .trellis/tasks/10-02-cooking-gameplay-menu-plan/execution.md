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
