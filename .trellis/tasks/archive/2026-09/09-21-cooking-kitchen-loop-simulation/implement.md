# Implement：Cooking 厨房闭环仿真规则与闭环 fixture

> 执行顺序遵循"红测先行"：每个阶段先写失败测试，再改产品代码，最后跑门禁。
> 所有命令在仓库根目录运行；Unity 不参与。

## 阶段 1：容器即物品（K01）

1. 新建测试文件 `src/AbilityKit.Game.Cooking.Tests/CookingContainerAsItemTests.cs`（先红）：
   - 容器物品可被拾取/放下/放入/取出/倒出；
   - fixture 只声明物品定义即可拥有容器（无独立容器表）；
   - 快照/canonical 可观察容器内容物。
2. 产品代码：
   - `CookingRecipeLoop.cs`：删 `CookingContainerDefinition`；`CookingRecipeFixture.Containers` 删除；`_containerItems` 改以 `ItemId` 为键；容量/可接受集合读物品定义；
   - `CookingRecipeSnapshotContainer.Id` 改 `ItemId`；canonical 增加内容物；
   - `CookingConfigurationValidation.cs`：候选/canonical/snapshot 的 containers 段退役；
   - `CookingLevelLifecycle.cs`：`CookingLogicalLayout.Containers` 改 `IReadOnlyList<DefinitionId>`，校验改为"定义存在且带容器能力"，`ContainerNotFound` 保留；
   - 连带迁移 `CookingMatchLifecycle`、ET 宿主测试、EtBridge 检查。
3. 既有测试迁移：`CookingRecipeLoopTests`、`CookingConfigurationSchemaV2Tests`、`CookingConfigurationValidationTests`、`CookingLevelLifecycleTests`、`CookingMatchLifecycleTests`、`CookingVerticalSliceTests`、`CookingEtProjectionTests` 中容器表相关断言。

## 阶段 2：七项动作（K02）+ 命令形状

1. 新测试 `CookingKitchenActionsTests.cs`（先红）：七项动作各一条 happy path + 一条 mutation-free 拒绝；`Plate` 消失；`IsWellFormed` 新分支。
2. 产品代码：
   - `CookingRecipeOperation` 尾插 `Drop, PutIn, TakeOut, Pour`，删 `Plate`；
   - `CookingRecipeCommandValidation.IsWellFormed` 同步；
   - `CookingRecipeSimulation` 实现四个动作（§2.2/§2.3/§4.2）；
   - 锁输入反查索引 + 清理 + fixed-tick 索引校验。

## 阶段 3：启动加工重构（K03/K04/K05）

1. 新测试 `CookingProcessAnchorTests.cs`（先红）：
   - 容器锚定匹配（多输入集合、默认供应、歧义拒绝）；
   - 免工位启动（打蛋）与工位绑定（切/煮）；
   - 放入即拒绝；锁输入四动作拒绝；端走继续；
   - 白名单腐败检测器可达（testing 后门注入）。
2. 产品代码：
   - `StartProcess` 重写（锚点、匹配器、`RequiresStation`、双索引）；
   - `ProcessState.Station` 可空；`_processesByAnchorItem`；
   - `ValidateProcessForFixedTick` 白名单化；
   - `CookingRecipeDefinition` 增加 `RequiresStation`。

## 阶段 4：完成形态与 allocator（K06/K07）

1. 扩展/新测试（先红）：
   - `ConsumeInputs` 两条路径（工位生成、容器内生成）；
   - `RetainInputs` 到点只切 completed、不生成；`Pour` 生成一次、重复倒出拒绝；
   - 普通容器间 Pour 转移与原子拒绝；
   - allocator 故障/碰撞/空白在命令路径与 fixed-tick 路径都 mutation-safe。
2. 产品代码：
   - `BuildFixedTickPlan`/`CommitFixedTick` 两种完成形态；
   - `AdvanceTicks` 命令路径改用 allocator（删硬编码）；
   - 产物位置规则（工位/容器空缺槽）。

## 阶段 5：闭环 fixture（K08/K09/K10）

1. 新测试 `CookingKitchenLoopFixtureTests.cs`（先红）：番茄蛋花汤端到端 + 快照哈希确定可重复；订单要求匹配/拒绝/不回滚；碗池上限与注入清洗。
2. 产品代码：
   - `IsDirty`/`ContainerCompleted` 物品状态 + 快照/canonical；
   - `ICookingBowlWashingPort` + `CompleteWash` + fixture 供应声明；
   - `SubmitOrder` 可达性改按容器当前位置；`CookingOrderAcceptance.OrderCompleted`；
   - 订单要求由 fixture 端口实现（测试侧 recording port 带要求表）。

## 阶段 6：争抢仲裁（K11）

1. 新测试 `CookingCommandArbitrationTests.cs`（先红）：同批次两玩家争抢同物/同工位/同容器；乱序与重复一致。
2. 产品代码：`SubmitBatch`/`ExecuteBatch` + 稳定排序。

## 阶段 7：门禁与重锚（K12）

1. `tools/test-gates.json` 增加 `cooking-kitchen-loop`（P1，构建 Cooking 域 + ET runtime，跑 Cooking.Tests 与 ET.Runtime.Tests，filter `Gate=CookingKitchenLoop` 的 focused 步骤 + 全量回归步骤）；
2. `Docs/AbilityKit测试门禁与批量回归规范.md` §9 同步；
3. ET 指纹金样重锚（`CookingLevelEtHostTests`）：先占位 → 跑出真实值 → 锚定；cross-surface 一致性循环保持。

## 阶段 8：验证与收口

1. 跑 `cooking-kitchen-loop` gate（ focused + 全量）+ `cooking-et-level-runtime` 回归；
2. evidence 落 `artifacts/cooking-kitchen-loop-domain/`（`COOKING_RECIPE_EVIDENCE_DIRECTORY` 等既有环境变量）；check.jsonl 记录真实命令与结果；
3. spec 修约：`cooking-recipe-loop.md`、`cooking-config-validation.md` 追加 2026-09-21 修约记录（含旧→新→来源表）；
4. 独立复验（不调用被测 C#）：解析 TRX、重算快照哈希、手工验证 evidence 自洽；
5. `task.py finish` + `archive --no-commit`；git commit 由 owner 决定。

## 每阶段完成定义

- 新测试先红后绿；拒绝路径断言"前后 canonical 相等"；
- 无 `Skip`/`Ignore`；无双断言凑数；
- evidence JSONL 行数、TestId、前后哈希非空由测试自身断言。
