# Design：Cooking ET Level 闭环验收

> 上游：`prd.md`；契约基线与修约记录见 `.trellis/spec/cooking/`（`cooking-recipe-loop.md` 2026-09-21 三次修约、`index.md`）。
> 本设计只新增测试与一处加法式产品暴露，不改领域规则、命令 wire、指纹与门禁配置。

## 1. 总体接法

闭环验收落在 `src/AbilityKit.ET.Runtime.Tests/CookingLevelClosedLoopTests.cs`（`[Trait("Gate", "CookingLevelRuntime")]`），与既有宿主测试同 trait：两个 Cooking P1 gate（`cooking-et-level-runtime`、`cooking-kitchen-loop`）都已全量运行 ET.Runtime.Tests 工程，不新增 gate、不改 `tools/test-gates.json`。

驱动模型（全部经 `CookingLevelEtHost`）：

| 步骤 | 通道 |
|---|---|
| 加载正式内容 | `CookingContentCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)))` |
| 构造 fixture | `CookingContentCatalog.BuildFixture(content, scope, players, "clean-pool")`（可洗碗定义与干净池上限由内容 cleanPool 供应项派生） |
| 配置身份 | `content.Snapshot`（本次新增暴露）→ `CookingLevelLifecycle(scope, snapshot, factory)`；`CookingLevelPreparation(level, map, layout, content.Identity)` |
| 标准初始供应 | `CreateStartedHost(simulation => CookingContentCatalog.ApplyStandardInitialSupply(simulation, content))`（`Start` 前应用到预建仿真，沿用既有接缝） |
| 玩家动作 | `host.TryEnqueue(envelope)` → `host.Tick()`；每帧一个最小命令批次 + 一次 fixed tick |
| 加工等待 | 纯时钟帧：无 pending 命令时 `host.Tick()` 只推进一个 logical tick |
| 开单 / 清洗完成 | 帧间直接 `simulation.OpenOrder(...)` / `simulation.CompleteWash(...)`（注入缝隙，非玩家命令） |

帧与时钟的对应关系由测试断言固定：命令帧 `HostFrameSequence == simulation.LogicalTick`、恰一个 `Executed` disposition；纯时钟帧零 disposition、`LogicalTick +1`。加工完成只由宿主帧驱动——`AdvanceTicks` 在 admission 被 `ReservedClockOperation` 拒绝的既有行为不变，测试用"发送 AdvanceTicks 被拒"反向钉住该边界（E01 内一条显式断言）。

## 2. 产品侧改动：`CookingContent.Snapshot`

`CookingContentCatalog.Load` 内部已经把候选提交注册表并得到校验后快照（`registry.Current`），但只回放了 `OrderTemplates`/`StandardInitialSupply`/`Identity`。Level 生命周期需要完整 `CookingConfigurationSnapshot`（校验 + 身份 + 供工厂消费）。选择暴露而非测试侧重放：

- 重放候选会二次校验并允许两份快照漂移，违背"单一加载路径、单一身份"；
- 生产宿主把正式内容接入 Level 生命周期时同样需要该快照，不是测试专用便利。

改定为加法：`CookingContent` 增加 `public CookingConfigurationSnapshot Snapshot { get; init; }`，`Load` 在构造返回值时赋值。无行为变化，不影响 `CookingContentCatalogTests` 既有断言（记录相等性按值比较，新增 init 属性默认值不改变既有加载结果——既有测试只断言身份/计数/供应，不断言整个记录相等）。

## 3. 测试结构

```
CookingLevelClosedLoopTests
├── E01_full_tomato_egg_soup_loop_through_the_et_level_host   // 全链路 + 帧结构 + AdvanceTicks 保留拒绝
├── E02_order_requirement_rejects_a_foreign_product_at_the_host_boundary  // 领域 L03 的宿主侧对应
└── E03_the_same_host_loop_reaches_the_same_canonical_state_when_replayed // 两遍 canonical/sha 一致
```

Fixture（文件内 private，形态对照 `CookingLevelEtHostTests.Fixture` 但内容驱动）：

- ` players`：单玩家 `chef-a`，能力 `cook`，工位集合 = 内容四个 station（board-a/stove-a/oven-a/counter-a）；
- `Factory : ICookingLevelGameplayFactory`：`NewSimulation()` = `new CookingRecipeSimulation(contentFixture, null, recordingWashPort)`；不追加任何手写物品（锅/碗/番茄/鸡蛋/面包片全部来自标准初始供应与干净池）；
- `RecordingWashPort`：记录 `RequestWash` 请求；
- preparation 布局：`ApplianceStations` = 四个 station，`Containers` = bowl/pot（v2 校验面要求容器定义存在且带容器能力，布局校验 `ContainerNotFound` 同源）。

命令助手（对照领域 L01 的 `Command`/`Submit`，改为宿主通道）：

```
Execute(host, evidence, testId, batch, commandId, operation, item/station/container/order/expectedVersion, summary):
  before = simulation.Snapshot()
  admission = host.TryEnqueue(new CookingLevelCommandEnvelope(levelScope, command(batch, commandId, operation, ...), "conn", commandId))
  assert admission.Accepted
  frame = host.Tick()
  assert frame.Accepted && frame.SimulationBatch == batch
  disposition = Assert.Single(frame.Dispositions) → Kind == Executed
  assert disposition.Result!.Outcome == Accepted
  append evidence(before, after, ...)
```

批量编号从 1 起严格递增（宿主对旧批量判 `BatchStale`）；`expectedVersion` 逐命令从当帧前快照读取（与领域 L01 同一纪律）。

## 4. E01 动作序列（与领域 L01 同构）

1. `StageBowlOnCounter`：从干净池拾取 `pool-bowl-1` → 放到 `counter-a`（供应与池物品 ID 由内容目录确定性生成：`pool-bowl-1/2`、`pot-1@stove-a`、`tomato-1/2@world:pantry`、`egg-1/2`、`bread-slice-1/2`）。
2. 取 `tomato-1` → 放 `board-a` → 启动 `chop-tomato`（`RequiresStation`，cut）→ 2 个纯时钟帧 → 产物 `chopped-tomato` 在砧板（ConsumeInputs）。
3. 拾取番茄块 → 放入 `pot-1`（锅接受 chopped-tomato/beaten-egg）。
4. 取 `egg-1` → 放入碗 → 拾取碗 → 免工位启动 `beat-egg`（能力 beat，`RequiresStation=false`）→ 2 个纯时钟帧 → `beaten-egg` 落在碗 slot-0。
5. 倒出：碗 → 锅（蛋液入锅，碗腾空）→ 碗放回 `counter-a`。
6. 锅上启动 `tomato-egg-soup`（heat，6 tick，RetainInputs，水为默认供应）→ 6 个纯时钟帧 → 锅 `ContainerCompleted`、无活动加工。
7. 端走：拾取已完成的锅（进度保留在"已完成"）。
8. 倒出：锅 → 碗，生成 `tomato-egg-soup` 于碗 slot-0，锅内容清空。
9. 帧间 `OpenOrder(order-soup-1, tomato-egg-soup-order)`（前厅注入）。
10. 提交（`SubmitOrder`，item=汤，order=order-soup-1）→ 订单 `Completed`、结算 1 条、洗碗请求 1 次、碗离册、干净碗计数 1。
11. 帧间 `CompleteWash(pool-bowl-1)` → 碗回 `world:clean-pool`、非脏、计数恢复 2。
12. 断言终态快照哈希非空、evidence 记录数等于命令数；另发一条 `AdvanceTicks` 断言 admission 拒绝 `ReservedClockOperation`（宿主拥有时钟的边界）。

## 5. E02 拒绝路径（领域 L03 同构）

面包片 `bread-slice-1` → 烤箱 `bake-bread`（2 帧）→ 烤面包入碗 → `OpenOrder(蛋花汤订单)` → 提交烤面包：disposition 为 `Executed` 但 `Result.Outcome == Rejected`、`Reason == OrderRequirementMismatch`、`Events` 空。

"零变更"在宿主层不能直接比较整段 canonical：每帧固有的 fixed tick 必然推进 `logicalTick` 与 `stateVersion`，拒绝帧也不例外。实现采用两级证明：

1. **命令级**：`Execute` 拒绝分支断言 `result.StateVersion == before.Version`（命令执行本身零变更）且无事件；
2. **帧级差分**：`RunBreadArm(submit: true)` 与 `RunBreadArm(submit: false)`（同序列、最后一帧只推进时钟）两臂顺序执行，比较终态 canonical 文本相等——被拒绝的命令帧与纯时钟帧到达同一状态，时钟推进被对照臂抵消。

另断言 `SettlementHistory` 空、碗中烤面包不回滚、订单仍 `Open`、无洗碗请求。钉住"拒绝经宿主命令路径可观察且零变更"。

## 6. 与领域闭环的计数器差异（显式决策）

领域 L01：`AdvanceTicks` 一次完成切番茄（不推进 `LogicalTick`）+ 显式帧 1–8 → 终态 `LogicalTick=8`。宿主路径：17 个命令帧 + 10 个时钟帧（切 2、打蛋 2、煮 6）→ 终态 `LogicalTick=27`；`stateVersion` 与结算记录中的 `LogicalTick` 字段随之不同。因此：

- E01/E03 只断言**结果契约**（产物定义与位置、订单状态、结算条数与字段、碗池计数、脏标记）与 **ET 侧确定性**（两遍相等）；
- 不把领域运行的 canonical 文本/哈希当作 ET 运行的期望值；反之亦然。该口径同时写入 spec 修约，防止后续把计数器差异当回归。

## 7. ET 宿主单例与失败隔离（实现中发现并修复）

`EtRuntimeHost` 是进程级单例（`Interlocked.CompareExchange` 守卫），同进程同时只能有一个存活宿主；xUnit 侧 `[assembly: CollectionBehavior(DisableTestParallelization = true)]` 已禁并行，但**失败路径的宿主泄漏**会污染后续测试（表现为后续测试报 "Only one ET runtime host may be active per process" 的假红）。实现为此加了两道保证：

- `RunLoop`：循环中途断言失败时先释放宿主再抛（宿主正常交调用方 `using` 释放）；
- `Fixture.CreateStartedHost`：`Prepare`/初始化回调/`Start` 任一失败时释放宿主再抛。

E03 两遍重放与 E02 两臂因此顺序执行、各自释放后再比较。另：`AdvanceTicks` 探针必须带 `process` 与 `tickCount` 才过 `IsWellFormed`（形状校验先于 `ReservedClockOperation` 保留检查）。

## 8. 回归与不变量

- 三个二进制指纹金样（`CookingLevelEtHostTests`：pickup `6B91D5…`、recipe-less start `10F9BA…`、put-in `70181A…`）字节不变——本任务不改命令记录与 `CanonicalBytes`，由既有测试直接守护；
- 既有 `CookingLevelEtHostTests`（24 个宿主机制测试）与 `CookingVerticalSliceTests`（legacy 宿主纵切）不改写法；
- 领域侧 `CookingKitchenLoopFixtureTests` 不动；
- evidence：`COOKING_RECIPE_EVIDENCE_DIRECTORY=artifacts/cooking-et-closed-loop` 下每个测试一个 JSONL，字段与领域证据一致（runner 标注 ET 测试工程）。

## 9. 实现实测值（原推定项回填）

- 纯时钟帧 10 个（切 2、打蛋 2、煮 6），命令帧 17 个，终态 `LogicalTick=27`；证据中相邻命令的 tick 间隔为 1/1/1/1/3/1/1/1/1/1/3/1/1/7/1/1，即三处加工等待（3=命令帧+2 时钟帧、3=同、7=命令帧+6 时钟帧）。
- evidence：E01 17 条（全部 Accepted）、E02 8 条（7 Accepted + 1 Rejected/OrderRequirementMismatch）；E03 不落证据（纯确定性比较），与领域 L02 同一取舍。
- 变异测试 4 项全部杀死：M1 翻转订单完成断言（E01/E03 红）；M2 供应跳过工位项（3/3 红，`ArgumentException`）；M3 宿主每帧双 fixed tick（3/3 红，时钟帧纪律断言首杀）；M4 去掉 `Snapshot = snapshot` 赋值（3/3 红，生命周期 `ArgumentNullException`）。
