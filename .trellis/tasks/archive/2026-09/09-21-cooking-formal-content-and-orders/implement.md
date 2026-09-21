# Implement：Cooking 正式配方与订单内容

> 执行顺序遵循"红测先行"：每个阶段先写失败测试，再改产品代码，最后跑门禁。
> 所有命令在仓库根目录运行；Unity 不参与。门禁：`cooking-kitchen-loop`（既有 gate，本任务不新增）。

## 阶段 1：v2 候选扩展与内容目录（K01/K07）

1. 新测试 `src/AbilityKit.Game.Cooking.Tests/CookingContentCatalogTests.cs`（先红）：
   - 从 `Content/cooking-content-v2.json` 加载：通过校验、身份 hash 非空且两次加载一致；
   - 内容含四条正式配方（切番茄/打蛋/番茄蛋花汤/烤面包）、蛋花汤订单模板、标准初始供应；
   - 烤面包为"面包片→烤面包"，dough 不在正式物品中；
   - 坏内容（订单模板要求不存在的 recipe / 容器定义不接受产物 / 供应引用缺失物品 / cleanPool 项非容器）→ 结构化诊断、注册表零变更。
2. 产品代码：
   - 新文件 `src/AbilityKit.Game.Cooking/CookingContentCatalog.cs`：`OrderTemplateId`、`CookingOrderTemplateDefinition`、`CookingSupplyEntryDefinition`、`CookingContent`、`CookingContentCatalog`（JSON 加载 + 校验 + `BuildFixture`）；
   - `Content/cooking-content-v2.json`：正式内容全文（design §1.2 表）；
   - csproj：`Content` 项含 `CopyToOutputDirectory`；
   - `CookingConfigurationValidation.cs`：候选尾部追加 `OrderTemplates`/`StandardInitialSupply`；校验两张新表；snapshot/canonical 增加两段（schema 字符串不变）。

## 阶段 2：订单簿与提交/结算契约（K02–K05）

1. 新测试 `src/AbilityKit.Game.Cooking.Tests/CookingOrderBookTests.cs`（先红）：
   - 开单：模板不存在/身份重复拒绝；成功后 Open 可观察；
   - 提交：未开单、订单已完成、recipe 不匹配、容器定义不匹配、产物已消费分别结构化拒绝且 canonical 零变更；
   - 幂等：同 identity 重放 `IsDuplicate`；跨 identity 拒绝；
   - 结算记录：每次成功提交一条，字段完整，数量一致；
   - 碗脏/洗碗请求/干净数递减保持任务②行为。
2. 产品代码（`CookingRecipeLoop.cs`）：
   - 退役 `ICookingOrderPort`/`CookingOrderSubmission`/`CookingOrderAcceptance`；`CookingRecipeSimulation` 构造不再依赖端口；
   - 新增 `OrderState`/`OrderStatus`/`CookingOrderSettlement`/`CookingOrderResult`；
   - `OpenOrder` 公共注入方法；`SettlementHistory`；快照/canonical 增加 `Orders`/`Settlements`；
   - `SubmitOrder` 按 design §3.3 重写；尾插新 reason：`OrderNotFound`、`OrderAlreadyCompleted`、`OrderRequirementMismatch`、`OrderTemplateNotFound`、`OrderIdentityConflict`；
   - fixture 尾部追加 `OrderTemplates` 字典。

## 阶段 3：闭环 fixture 与存量测试迁移（K06/K07）

1. 迁移 `CookingKitchenLoopFixtureTests`：`CreateFixture()` 改从内容目录加载；L01 增加开单与结算断言；L03 改领域拒绝；新增烤面包闭环断言（面包片入烤箱→启动→完成→烤面包在烤箱生成）。
2. 迁移其余端口依赖测试（Cooking 9 个文件 + ET 2 个文件）：端口替为订单模板，按需 `OpenOrder`；`ThrowingOrderPort`/`ReenteringOrderPort` 相关用例改验证提交路径无外部端口依赖（重入防护由既有 fixed-tick 重入测试覆盖）。
3. 确认 ET 三个指纹金样断言不变（K08）：不改 `CookingLevelEtHostTests` 的金样期望值。

## 阶段 4：门禁与证据（K09/K10）

1. 运行门禁并记录真实命令与结果：
   - `dotnet test src/AbilityKit.Game.Cooking.Tests --filter "Gate=CookingKitchenLoop"`（focused）；
   - Cooking 全量、ET runtime 全量；
   - 回归 `cooking-et-level-runtime`（按 `tools/run_test_gate.ps1 -Gate` 实际执行范围）。
2. evidence 落 `artifacts/cooking-formal-content/`（内容加载、订单簿、闭环、门禁输出 JSONL/日志）；`check.jsonl` 记录真实命令。
3. 独立复验（子智能体，不调用被测 C# 代码）：按 K01–K10 复核证据与方法。
4. spec 修约：`.trellis/spec/cooking/cooking-recipe-loop.md` 追加"正式内容与 order owner"修约记录（订单簿入领域、端口退役、提交/结算契约、未覆盖边界）；`index.md` P2 行同步。
5. 更新 `Docs/design/CookingGame/progress.md` 与 `Docs/Todo.md`：不宣称评分/失败/前厅完成。

## 归档

- `python .trellis/scripts/task.py archive <dir> --skip-branch-validation`（分支即 base 分支 master，遵守"不新建分支"）；
- 归档 commit 使用 `--no-commit`，是否提交由 owner 决定（沿用阶段计划约定）。
