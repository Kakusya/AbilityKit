# Journal - Li He (Part 1)

> AI development session journal
> Started: 2026-09-15

---


## Session 1: Archive Cooking limited-scope tasks
<!-- trellis-session: v=2 fp=6f3743c4ec1a2b92 -->

**Date**: 2026-09-16
**Task**: Archive Cooking limited-scope tasks
**Branch**: `chore/archive-cooking-limited-scope`

### Summary

Archived the seven verified Cooking pure-.NET limited-scope tasks and the closure task, moved prohibited Unity work to a non-active future scope, recorded non-Unity successor backlog, and synchronized Cooking specs, roadmap, progress, delivery plan, and ADR status.

### Git Commits

| Hash | Message |
|------|---------|
| `a76c05686` | chore: archive cooking limited-scope tasks |

### Status

[OK] **Completed**


## Session 2: Cooking UDP LAN minimum slice
<!-- trellis-session: v=2 fp=d7175e56e436e247 -->

**Date**: 2026-09-16
**Task**: Cooking UDP LAN minimum slice
**Branch**: `master`

### Summary

Delivered and verified a pure .NET LiteNetLib reliable-UDP Cooking listen-host/client slice: protocol codec, serialized authority dispatcher, loopback contracts, same-machine host/client harness, manual cooking-udp P1 gate, and two-PC LAN procedure. Verified 7 UDP tests, cooking-udp gate, 57 existing Cooking tests, and same-machine artifacts. Physical two-PC LAN remains explicitly not-run.

### Git Commits

| Hash | Message |
|------|---------|
| `0a788e65a` | feat(cooking): add reliable UDP LAN harness |

### Status

[OK] **Completed**


## Session 3: Cooking ET runtime extraction and tick dispatch
<!-- trellis-session: v=2 fp=cde0444e831d4c03 -->

**Date**: 2026-09-17
**Task**: Cooking ET runtime extraction and tick dispatch
**Branch**: `feat/et-share-bootstrap`

### Summary

Extracted and verified the internal ET runtime, added a minimal Cooking recipe command queue dispatched by a real ET UpdateSystem, recorded red-green and regression evidence, preserved incomplete phase-three/four boundaries, and archived the roadmap task.

### Git Commits

| Hash | Message |
|------|---------|
| `38d822271` | feat(cooking): dispatch recipe commands through ET tick |
| `43eafce25` | docs(cooking): record ET runtime delivery boundary |

### Status

[OK] **Completed**


## Session 4: Cooking ET Level 固定时钟 Phase A
<!-- trellis-session: v=2 fp=f44c60ad335d6388 -->

**Date**: 2026-09-19
**Task**: Cooking ET Level 固定时钟 Phase A
**Branch**: `feat/et-share-bootstrap`

### Summary

完成并验证 canonical Level 生命周期、ET Level host、单一固定加工时钟、关系分析器与专用门禁；纠正 MOBA precheck 范围判断并归档任务。

### Git Commits

| Hash | Message |
|------|---------|
| `29bc685cc777751b246cd5d5ade23c3021e3aecc` | feat(cooking): add ET Level fixed-tick runtime |

### Status

[OK] **Completed**

## Session 5: Cooking 正式配方与订单内容（单机）
<!-- trellis-session: v=2 fp=formal-content-orders -->

**Date**: 2026-09-21
**Task**: 09-21-cooking-formal-content-and-orders（已归档，未提交）
**Branch**: `master`

### Summary

successor backlog P2 首条落地：数据驱动正式内容目录（`cooking-definition-v2` JSON + 加载器 + v2 校验扩展）、order owner 移入领域（订单簿、`OpenOrder` 前厅注入、`ICookingOrderPort` 退役）、提交/结算契约（五个结构化拒绝分支全 mutation-free、结算记录无评分字段）、闭环 fixture 改从正式内容运行、烤面包修正为 owner 采纳版本。门禁 `cooking-kitchen-loop`（focused 54/54、Cooking 175/175、ET 40/40）与回归 `cooking-et-level-runtime` 通过；四类变异测试全部被杀死（M4 初版存活后补 O06 闭合）；独立子智能体复验发现问题已全部处理。spec（recipe-loop/index）与 progress.md/Todo.md 同步，未宣称评分/失败/前厅完成。

### Notes

- 变异测试回滚误用 `git checkout --` 重置过 `CookingRecipeLoop.cs`，已完整重放实现并复跑双套件确认；后续回滚一律用同脚本精确还原。
- 任务会话内三个被跟踪二进制 DLL（analyzer 插件、moba codegen、ET source generator）被构建改写，判定副产物并还原，未纳入改动。
- 归档 commit 遵循 `--no-commit`；是否提交由 owner 决定。

### Status

[OK] **Completed (archived, uncommitted)**

## Session 6: Cooking ET Level 闭环验收（单机）
<!-- trellis-session: v=2 fp=et-level-closed-loop -->

**Date**: 2026-09-21
**Task**: 09-21-cooking-et-closed-loop-acceptance（已归档，未提交）
**Branch**: `master`

### Summary

Todo P0-C1 未勾项落地：ET fixed-tick Level 宿主（`CookingLevelEtHost`）承载同一条番茄蛋花汤闭环验收。产品侧仅一处加法——`CookingContent` 增露加载时经 v2 校验的快照，Level 生命周期与 preparation 身份同一来源。新增 `CookingLevelClosedLoopTests`：E01 正式内容 fixture + 标准初始供应，17 条玩家命令全部经宿主命令 ingress 与固定 Tick（10 个纯时钟帧），帧结构、`AdvanceTicks` 保留拒绝、订单/结算/洗碗回池全断言；E02 拒绝零变更用"命令级版本相等 + 对照臂 canonical 相等"两级证明（宿主每帧固有 tick 推进由对照臂抵消）；E03 两遍 canonical/Sha256 一致。门禁 `cooking-et-level-runtime`（Cooking 175/175、ET 43/43）与 `cooking-kitchen-loop`（focused 54/54）通过；四个二进制指纹金样字节不变（宿主测试文件零改动）；四项变异测试全被杀；evidence 经独立脚本（不加载被测程序集）复验。spec recipe-loop/index 四次修约、progress.md 新增第 6 节、Todo.md 勾选该条。

### Notes

- ET 宿主是进程级单例：E02/E03 的多臂必须顺序执行；实现中发现并修复两处失败路径宿主泄漏（`RunLoop` 中途失败、`CreateStartedHost` 初始化失败），修复前变异测试会出现"Only one ET runtime host"串联假象。
- 宿主闭环与领域闭环 canonical 不要求字节一致（终态 LogicalTick 27 vs 8）：宿主拥有时钟、`AdvanceTicks` 被保留拒绝是既有契约，该差异已在 spec 显式记录为口径，防后续误判回归。
- evidence 环境变量需用绝对路径：testhost 的工作目录不是仓库根，相对路径会静默回落到临时目录。
- 归档 commit 遵循 `--no-commit`；是否提交由 owner 决定。

### Status

[OK] **Completed (archived, uncommitted)**


## Session 7: Cooking 恢复 checkpoint 契约（单机）
<!-- trellis-session: v=2 fp=3533212033b79ede -->

**Date**: 2026-09-22
**Task**: Cooking 恢复 checkpoint 契约（单机）
**Branch**: `master`

### Summary

Todo P0-C1 前两条未勾项落地：同步 snapshot 与恢复 checkpoint 显式区分，宿主导出/销毁/重建/继续与不中断基线不可区分。

### Main Changes

- 域侧 `CookingRecipeCheckpoint`：覆盖物品/tombstone、活动加工、容器有序内容、订单、结算、消耗产物账、干净池计数、去重账本、事件/tick 历史与 event sequence、三个 ID 计数器、state version/logical tick 与 scope；可派生索引恢复时重建，fixed-tick 腐败检测器下一 tick 兜底复核。`RestoreCheckpoint` 整册换入、结构化拒绝、零变更。
- 宿主侧 `CookingLevelCheckpoint` + codec（格式版本 + 完整性 + 结构化读回）；`CookingLevelEtHost.ExportCheckpoint` 前置 Running 且 pending 为空；静态 `Restore` 按同一代际重建（HostFrameSequence 单调不 reset、命令水位恢复、失败释放宿主）。仿真类改 partial 是 `CookingRecipeLoop.cs` 唯一改动。
- R01 两臂顺序执行（ET 宿主进程级单例）：基线臂 vs 恢复臂（煮制进行中导出→销毁→重建→继续）终态 canonical/Sha256、版本、tick、下一产物 ID、结算次数与终态 checkpoint canonical 全部相等，evidence 逐位一致；R02 导出前置、R03 跨代际/跨配置/载荷投毒/载荷缺失拒绝、R04 帧序列连续；域内 C01–C04。
- 实现期发现并修复：仅改 epoch 的 checkpoint 会绕过校验被静默恢复（补 payload-scope 前置）；去重指纹含批量，同 identity 换批量重投判 CommandIdentityConflict，宿主级“不二次推进”证明改为 基线 Duplicate vs 恢复 BatchStale（design §7.1 按实测改写）。
- 门禁 `cooking-et-level-runtime`（Cooking 179/179、ET 47/47）与 `cooking-kitchen-loop`（focused 58/58）全步骤 exit 0；五个变异全部杀死；evidence 经独立脚本（不加载被测程序集）复验；三个二进制指纹金样字节不变。spec 五次修约、progress.md 第 7 节、Todo.md 勾选前两条。
- durable storage、跨小关 checkpoint 产品语义、失败条件、评分、前厅、传输与 Unity 仍范围外。归档遵循 --no-commit；本会话提交由我决定（bd6a2f10c）。


### Git Commits

| Hash | Message |
|------|---------|
| `bd6a2f10c` | feat(cooking): land recovery checkpoint contract and rebuild equivalence |

### Status

[OK] **Completed**


## Session 8: 小关失败重开按标准供应重建厨房
<!-- trellis-session: v=2 fp=ec29940795152414 -->

**Date**: 2026-09-22
**Task**: 小关失败重开按标准供应重建厨房
**Branch**: `master`

### Summary

失败重开丢掉失败现场，按标准初始供应重建厨房并停在 Created。同一 LevelId、更高 epoch、同一 Match。不写盘，不改成功交接和同代恢复。

### Main Changes

- CreateRetry 安装下一代后用工厂新建仿真并 ApplyStandardInitialSupply，停在 Created。
- Start 绑定这份标准供应厨房，不接回失败现场。

### Git Commits

| Hash | Message |
|------|---------|
| `a55848f27` | feat(cooking): rebuild the kitchen from standard supply on a failed retry |

### Testing

- [OK] cooking-kitchen-loop：focused 61/61、Cooking 182/182、ET runtime 51/51。
- [OK] cooking-et-level-runtime：Cooking 182/182、ET runtime 51/51。

### Status

[OK] **Completed**

### Next Steps

- 工位升级、确认结算写盘、失败条件仍未做。


## Session 9: 前厅询问、洗碗与小关时间
<!-- trellis-session: v=2 fp=f68ec376d923c201 -->

**Date**: 2026-09-22
**Task**: 前厅询问、洗碗与小关时间
**Branch**: `master`

### Summary

一位固定伙伴问完才开单、空闲才洗碗；营业结束且座位空了才允许成功。三项已完成关卡任务已归档。

### Main Changes

- 新增 CookingFrontOfHouse：占桌、询问后开单、空闲洗碗、超时未满足、前厅成功条件。
- 宿主只在运行帧之后推进一步；暂停帧不推进。
- 归档成功交接、结算落盘、跨关进度和前厅四项任务。

### Git Commits

| Hash | Message |
|------|---------|
| `460f2233a` | feat(cooking): ask before an order and wash only when idle |
| `c368dfc39` | chore(task): archive 09-22-cooking-front-of-house |
| `f96986f6a` | chore(task): archive three finished Cooking level tasks |

### Testing

- [OK] cooking-kitchen-loop：focused 75/75，Cooking 196/196，ET 55/55。
- [OK] cooking-et-level-runtime：Cooking 196/196，ET 55/55。

### Status

[OK] **Completed**

### Next Steps

- 可见顾客、收益评价、伙伴成长、失败条件、Profile/SaveSlot、connection 到 PlayerId 仍未做。


## Session 10: Cooking customer and companion runtime slice
<!-- trellis-session: v=2 fp=d44c1bead6227390 -->

**Date**: 2026-09-28
**Task**: Cooking customer and companion runtime slice
**Branch**: `feat/cooking-customer-companion-runtime-slice`

### Summary

Implemented stable customer and companion runtime projections, deterministic front-of-house snapshots and v2 checkpoint recovery; verified both Cooking P1 gates and archived the task.

### Git Commits

| Hash | Message |
|------|---------|
| `f66cb5a5a` | feat(cooking): add customer companion runtime state |

### Status

[OK] **Completed**
