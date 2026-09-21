# Design：Cooking checkpoint 与恢复契约

> 上游：`prd.md`；契约基线见 `.trellis/spec/cooking/`（`cooking-recipe-loop.md` 闭环契约、`cooking-persistence-management.md` envelope 形态）。本设计只新增领域/宿主侧的 checkpoint 出口与测试，不改既有命令 wire、指纹金样与门禁配置。

## 1. 分层与新增面

```text
CookingRecipeCheckpoint          仿真权威状态载荷（domain，新文件 CookingRecipeCheckpoint.cs）
  ├── ExportCheckpoint()         CookingRecipeSimulation 导出（实例方法）
  └── RestoreCheckpoint(...)     CookingRecipeSimulation 整册恢复（实例方法，结构化拒绝）
CookingRecipeCheckpointCodec     序列化信封：format version + integrity + 结构化读回（对照 CookingProgressCodec）
CookingLevelCheckpoint           宿主级信封内容：level scope + config identity + preparation + lifecycle 状态/version
                                + HostFrameSequence + LastCommittedSimulationBatch + Recipe 载荷
CookingLevelCheckpointCodec/Applier  宿主级信封序列化与应用校验
CookingLevelEtHost.ExportCheckpoint()        导出（Running + pending 空）
CookingLevelEtHost Restore(checkpoint, configuration, factory)  销毁后重建并继续（静态工厂）
```

分层原则：仿真侧载荷不认识宿主；宿主侧信封不认识序列化细节由 codec 承担；恢复入口同时服务测试宿主与未来生产宿主（同一条恢复路径）。

## 2. 仿真侧载荷（`CookingRecipeCheckpoint`）

字段与来源（`_` 前缀私有字段名的映射即覆盖表）：

| 记录字段 | 来源 | 说明 |
|---|---|---|
| Scope / StateVersion / LogicalTick | `_fixture.Scope`、`_stateVersion`、`LogicalTick` | 身份与水位 |
| Items | `_items` 全量（含墓碑） | `CookingRecipeCheckpointItem` 复刻 `ItemState` 全字段 |
| Processes | `AllProcesses()`（两个索引的并集） | 含 Completion/Container/LockedInputs |
| Containers | `_containerItems`，**保序** | slot 分配由内容物在列表中的存在性与物品自身 SlotId 决定，顺序改变会改变后续槽位 |
| Orders | `_orders.Values` | 模板/要求/状态/完成 tick |
| Settlements | `_settlements` | 订单提交计数与结算事实 |
| ConsumedProducts | `_consumedProducts` | 不可派生（ poured/submitted 产物记账） |
| CleanContainerCounts | `_cleanContainerCount` | 干净碗池计数与在册干净容器不是同一集合（台面上的干净碗仍计数），必须显式 |
| Deduplication | `_processedCommands` | 键 (session, player, command) + fingerprint + 缓存结果（Outcome/Reason/StateVersion；Events 重放时本就丢弃，不入账） |
| Events / EventSequence / TickEvents | `_events`、`_eventSequence`、`_tickEvents` | 事件序列历史与游标（每个 fixed tick 也消耗一个 event sequence） |
| NextProcessId / NextProductId / NextSettlementSequence | `_nextProcessId`、`_nextProductId`、`_nextSettlementSequence` | ID 计数器 |
| Hands | 由 Items 派生（`Location.Kind == PlayerHand`），**不入账** | 恢复时重建 |

可派生索引（`_processesByStation`/`_stationsByProcess`/`_processesByAnchorItem`/`_anchorItemsByProcess`/`_inputsByProcessItem`/`_lockedInputsByProcess`/`_hands`）恢复时从 Processes/Items 重建。重建结果在随后第一次 `AdvanceFixedTick` 会被既有 `ValidateProcessIndexesForFixedTick` 与 `ValidateProcessForFixedTick` 全套校验——恢复正确性有既有腐败检测器兜底，不为恢复新造第二套校验语义。

`CookingRecipeCheckpoint.CanonicalText()/Sha256()`：稳定 canonical（字段全有序、camelCase、无缩进），用于完整性校验与两臂终态比较。

## 3. 信封（codec）

对照 `CookingProgressCodec`：

```csharp
public sealed record CookingCheckpointEnvelope(int FormatVersion, CookingLevelCheckpoint Checkpoint, string IntegritySha256);
public enum CookingCheckpointReadReason { None, RecordTruncated, RecordTooLarge, UnknownFormatVersion, IntegrityFailure, IdentityMismatch, ScopeMismatch, ... }
public sealed record CookingCheckpointReadResult(bool Accepted, CookingCheckpointReadReason Reason, CookingLevelCheckpoint? Checkpoint);
public static class CookingLevelCheckpointCodec { CurrentFormatVersion = 1; Serialize; Deserialize; }
```

- 载荷内不用“ID 作字典键”（列表 + 记录对），避免为每个 ID 类型补 `ReadAsPropertyName` 转换器；既有 `SettlementId` 转换器是字典键的特例，不复用其结论。
- `MaximumRecordCharacters` 与 P5 同数量级；超出即 `RecordTooLarge`。
- 完整性 = payload canonical 的 SHA-256（十六进制），与 P5 同款计算口径。

## 4. 宿主级导出（`CookingLevelEtHost.ExportCheckpoint`）

- 前置：`_lifecycle.State == Running && _ownedSimulation is not null && _pending.Count == 0`；否则返回 `CookingLevelCheckpointExportResult(false, reason)`（结构化，reason 取值 `LevelNotRunning`/`LevelPaused`/`PendingCommands`）。
- 内容：`CookingLevelCheckpoint(Scope, ConfigIdentity, Preparation(Level/Map/Layout 复刻), State=Running, Outcome=null, LifecycleVersion=Version, HostFrameSequence, LastCommittedSimulationBatch, Recipe=仿真导出)`。
- Preparation 来自 `_lifecycle.Preparation`（internal 可见）；随信封携带使“按 checkpoint 重建该代际”自描述；调用方仍须提供同一份 configuration 与工厂（内容不随信封走，由 config identity 校验同一性）。

## 5. 宿主级恢复（`CookingLevelEtHost.Restore`）

```csharp
public static CookingLevelCheckpointRestoreResult Restore(
    CookingLevelCheckpoint checkpoint,
    CookingConfigurationSnapshot configuration,
    ICookingLevelGameplayFactory factory)
```

步骤（全部在调用线程，宿主构造即获得 ET 运行时单例所有权，成功后交还调用方）：

1. 校验信封层（格式版本/完整性在 codec 已完成）+ 载荷层：`configuration.Identity == checkpoint.ConfigIdentity`、`checkpoint.Scope` 合法（构造期）、`checkpoint.Preparation.Level == checkpoint.Scope.Level` 等准备态不变量；失败返回结构化 reason，不创建任何宿主。
2. `var lifecycle = new CookingLevelLifecycle(checkpoint.Scope, configuration, factory)`；`var host = new CookingLevelEtHost(lifecycle)`。
3. `host.Prepare(checkpoint.Preparation)` → Ready（version 2）；`host.Start()` → 工厂创建 fresh 仿真、绑定门、Running（version 3）。
4. `lifecycle.TryGetGameplay(out var simulation)` → `simulation.RestoreCheckpoint(checkpoint.Recipe)`：整册换入载荷（fresh 构造期状态被完全替换），校验失败返回结构化 reason 且零变更（替换是原子的：先构建全部新字典/列表，最后整体赋值，参考 `CommitFixedTick` 的替换提交模式）。
5. 宿主水位：`HostFrameSequence = checkpoint.HostFrameSequence`、`LastCommittedSimulationBatch = checkpoint.LastCommittedSimulationBatch`（既有私有 setter，恢复路径经 internal 接缝赋值；恢复后第一帧 candidateFrame = checkpoint.HostFrameSequence + 1，单调不重置）。
6. `lifecycle.AdoptRecoveredVersion(checkpoint.LifecycleVersion)`（internal 接缝：仅允许 `Running` 且 `version >= Version`，只推进版本计数，不改状态机语义）。
7. 返回 `RestoreResult(true, None, host, ...)`。

任一步失败（工厂抛、Prepare/Start 拒、恢复校验拒）→ 释放已创建宿主（进程级单例，泄漏会污染同进程后续测试）→ 返回结构化失败。

## 6. 恢复校验清单（`RestoreCheckpoint`，全部结构化、零变更）

- `payload.Scope == _fixture.Scope`（session/world/match 逐一）；
- 每个 item 的 definition 存在于 `_fixture.Items`；`Removed=true` 的墓碑保留原 definition/version/location；
- 每个 process：recipe 存在且 `RequiredTicks` 一致、station 在 `_fixture.Appliances`（免工位进程 station 为 null）、anchor 与 lockedInputs 均为未移除物品、`0 <= ElapsedTicks < RequiredTicks`、`Completion == recipe.Completion`、container 引用存在且带容器能力；
- 容器内容物均为未移除物品且其 `Location.OwnerId == 容器`（槽位一致）；锁输入反查可重建且一一对应；
- 订单：template 在 fixture、RequiredContainerDefinition 存在且带容器能力；
- 消耗产物账与物品集合不矛盾（被消耗产物可以是在册非移除成品——倒出/提交后成品仍在册；该账只用于拒绝重复倒出/提交语义，校验其引用的物品在册）；
- 计数器单调：`NextProcessId >= 0`、`NextProductId >= 0`、`NextSettlementSequence == Settlements.Count`（settlement sequence 由提交追加，二者一致）；`EventSequence` 与历史长度不要求相等（tick 也占号），只要求非负；
- 干净池计数：definition 在 `CleanContainerSupply` 中且 `0 <= count <= supply`。

## 7. 口径决定（显式记录，防误判）

1. **宿主终态账本不进 checkpoint**：`_terminal` 是单宿主的投递簿记（含 connection/correlation 元数据）。恢复臂以**原批量**重投已执行命令时：命令水位已随 checkpoint 恢复，admission 即判 `BatchStale`；基线臂同键同指纹重投走 `_terminal` 给 `Duplicate` disposition。**两臂都不产生第二次执行**（状态版本与结算次数不变），标签不同是宿主簿记口径——验收断言“不二次推进”这一语义，不比较 disposition 标签。另注意：去重指纹覆盖整条命令（含 `SimulationBatch`），因此同 identity **换新批量**重投在仿真侧判 `CommandIdentityConflict`（既有契约，非本任务改变）；域级同命令同批量重放返回带 `IsDuplicate` 的缓存结果，由 C04 在域内证明。
2. **lifecycle 事件历史不恢复**：新宿主对象的事件历史记录它自身的 `BeginPreparation/CompletePreparation/Start`；`LifecycleVersion` 经接缝续上（R2 的“lifecycle 状态”= 状态/outcome/version）。恢复臂与基线臂的最终 lifecycle version 一致。
3. **只支持 Running 导出**：Paused 携带 live queue 语义（§4.1），跨宿主恢复 paused 代际属后续范围；本任务导出前置 `Running`，pending 非空即拒（宿主 Dispose 本就取消未决命令，权威态记录在半途点不成立）。
4. **终态 checkpoint canonical 相等是复合证明**：它同时锁定 hash、ID、版本、去重账本、事件与 tick 历史、订单与结算；单项断言（canonical、state version、logical tick、下一产物 ID、去重重放、settlement 计数）保留可诊断性。

## 8. 测试结构

域内（`src/AbilityKit.Game.Cooking.Tests/CookingCheckpointRecoveryTests.cs`，`[Trait("Gate", "CookingKitchenLoop")]`）：

- C01 `export_covers_every_checkpoint_field_after_a_partial_loop`：跑到“蛋液入锅、碗回台面”后导出，逐项断言 R2 覆盖表（墓碑 tomato-1/egg-1、容器 pot 有序内容、干净池=1、去重账本条数、事件/tick 历史长度、三个计数器、scope）。
- C02 `restore_into_a_fresh_simulation_reproduces_the_same_trajectory`：导出→序列化→反序列化→恢复到 fresh 仿真；两边各跑同一段续跑命令，canonical/hash/version 一致；下一产物 ID 一致（烤面包探针）。
- C03 `restore_rejects_foreign_or_corrupt_payloads_without_mutation`：scope 不匹配、tamper（改一个字符）、truncate、格式版本篡改分别结构化拒绝，且目标仿真 canonical 不变。
- C04 `restored_simulation_deduplicates_replayed_commands`：恢复后以新批量重放已执行命令 → `IsDuplicate=true`、Outcome/Reason 与首次一致、state version 不变；再以旧批量重放（域级无 admission，直接 Submit）→ 同样重复结果。

宿主级（`src/AbilityKit.ET.Runtime.Tests/CookingLevelCheckpointTests.cs`，`[Trait("Gate", "CookingLevelRuntime")]`，形态对照 `CookingLevelClosedLoopTests`）：

- R01 `recovered_host_continues_the_loop_like_the_uninterrupted_baseline`：基线臂完整 17 命令闭环；恢复臂同样序列，在**煮制进行中**（汤进程 elapsed=3/6）导出→`Dispose`→`Restore`→继续。比较：终态 canonical/Sha256、state version、logical tick、烤面包探针的下一产物 ID、新批量重放去重结果、settlement 计数、终态 checkpoint canonical。宿主为进程级单例，两臂顺序执行。
- R02 `export_requires_a_running_quiescent_generation`：pending 非空（入队不 Tick）导出被拒（`PendingCommands`）；Paused 导出被拒；拒绝不改变可观察状态。
- R03 `restore_rejects_checkpoints_from_another_generation`：改 epoch 的 checkpoint 应用被拒且零变更；配置身份不匹配被拒。
- R04 `restored_host_keeps_host_frame_sequence_monotonic`：恢复后第一帧 `HostFrameSequence == checkpoint.HostFrameSequence + 1` 且仿真 `LogicalTick` 连续。

测试卫生：恢复臂中途失败必须释放宿主（沿用 ClosedLoop 测试的 try/catch 释放纪律），否则进程级单例泄漏会串联污染后续用例。

## 9. 门禁与证据

- 不新增 gate、不改 `tools/test-gates.json` 结构与步骤；两个 P1 gate（`cooking-et-level-runtime`、`cooking-kitchen-loop`）都已全量运行 Cooking.Tests 与 ET.Runtime.Tests，本任务测试分别挂 `CookingKitchenLoop`/`CookingLevelRuntime` trait 即被双边覆盖。`cooking-kitchen-loop` 的 description 增加“恢复 checkpoint 契约”措辞并与 `Docs/AbilityKit测试门禁与批量回归规范.md` §3 同步（文档级，无步骤变化）。
- evidence 落 `artifacts/cooking-checkpoint-recovery/`（R01 两臂终态记录 + C02 域内等价记录），字段沿用 `CookingRecipeAcceptanceEvidence` 既有形态或 task 内新增最小字段；check.jsonl 记录真实命令与退出码。
- 变异测试候选：去掉 tombstone 入账、去掉干净池计数入账、去掉去重账本入账、HostFrameSequence 不续接、恢复跳过 ProcessState.LockedInputs。

## 10. 未覆盖边界（显式范围外）

durable store 与进程崩溃、跨小关 checkpoint 产品语义（保存/清除/加载流程）、Paused 导出、lifecycle 事件历史恢复、前厅、评分、失败条件、传输、Unity、ET Phase B。
