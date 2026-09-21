# Implement：Cooking ET Level 闭环验收

> 执行清单与阶段门。每完成一项更新对应勾选；证据落 `artifacts/cooking-et-closed-loop/`，命令与结果同步 `check.jsonl`。
> 状态：已完成（2026-09-21）。实测值以 [research/verification-2026-09-21.md](research/verification-2026-09-21.md) 与 design.md §9 为准。

## 阶段 1：产品侧快照暴露

- [x] 1.1 `CookingContent`（`src/AbilityKit.Game.Cooking/CookingContentCatalog.cs`）增加 `CookingConfigurationSnapshot Snapshot { get; init; }`，`Load` 赋值 `registry.Current` 快照。
- [x] 1.2 构建 `AbilityKit.Game.Cooking` 与 `AbilityKit.Game.Cooking.EtRuntime`，确认零警告/零错误；既有 `CookingContentCatalogTests` 不改断言全绿。

## 阶段 2：ET 闭环验收测试

- [x] 2.1 新建 `src/AbilityKit.ET.Runtime.Tests/CookingLevelClosedLoopTests.cs`（`[Trait("Gate", "CookingLevelRuntime")]`）：内容加载、`BuildFixture`、`content.Snapshot` 接 Level 生命周期与 preparation、`Factory` + `RecordingWashPort`、批量递增命令助手、evidence 助手（`COOKING_RECIPE_EVIDENCE_DIRECTORY`）。
- [x] 2.2 E01 全链路：标准初始供应 → 碗上台面 → 切番茄（2 时钟帧）→ 入锅 → 打蛋（2 时钟帧）→ 倒蛋液 → 煮制（6 时钟帧）→ 端走 → 倒汤 → 开单 → 提交 → 洗碗回池；断言产物定义/位置、订单 Completed、结算 1 条、洗碗 1 次、碗池 1→2、帧结构（命令帧单 disposition、时钟帧零 disposition、`HostFrameSequence == LogicalTick`）、`AdvanceTicks` 保留拒绝（`ReservedClockOperation`）。
- [x] 2.3 E02 拒绝零变更：烤面包入碗 + 蛋花汤订单 → 宿主提交拒绝 `OrderRequirementMismatch`；命令级 `result.StateVersion == before.Version` + 帧级对照臂 canonical 相等（design.md §5）。
- [x] 2.4 E03 确定性：两遍完整闭环 canonical 文本与 Sha256 相等（顺序执行，ET 宿主是进程级单例）。
- [x] 2.5 focused 运行 `AbilityKit.ET.Runtime.Tests`（`Gate=CookingLevelRuntime`）与全量，修复红项；实现中发现并修复宿主失败路径泄漏（design.md §7）。

## 阶段 3：门禁与回归

- [x] 3.1 `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime` 全步骤 exit 0（Cooking 175/175、ET 43/43）。
- [x] 3.2 `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop` 全步骤 exit 0（focused 54/54、Cooking 175/175、ET 43/43）。
- [x] 3.3 独立复验：不调用被测代码，直接核验 evidence JSONL 字段完整（before/after hash、outcome、reason、事件、runner、fixtureId）、批量严格递增、tick 递增、操作序列与时钟间隔（3/3/7）、拒绝记录零事件。
- [x] 3.4 变异测试 4 项全部杀死（改后红、原样还原）：
  - M1 翻转订单完成断言 → E01/E03 红；
  - M2 供应跳过工位项（锅）→ 3/3 红（`ArgumentException`）；
  - M3 宿主每帧推进两个 fixed tick → 3/3 红（时钟帧纪律断言首杀）；
  - M4 去掉 `Snapshot = snapshot` → 3/3 红（生命周期 `ArgumentNullException`）。
  - （变异一律用精确字符串swap还原；未对含未提交改动的文件使用 `git checkout --`。）
- [x] 3.5 确认三个二进制指纹金样字节不变（既有测试通过即证据；`git diff` 确认 `CookingLevelEtHostTests.cs` 零改动）。

## 阶段 4：证据与文档

- [x] 4.1 `COOKING_RECIPE_EVIDENCE_DIRECTORY=artifacts/cooking-et-closed-loop` 重跑 E01–E03，确认 JSONL 落地且无重复（先清空再生成：2 个文件、17+8 条）。
- [x] 4.2 `.trellis/spec/cooking/cooking-recipe-loop.md` 追加"2026-09-21 ET Level 宿主闭环验收"修约段（宿主时钟归属、注入缝隙口径、计数器差异口径、实现状态声明）。
- [x] 4.3 `.trellis/spec/cooking/index.md` 头部追加四次修约说明；P2 行补 ET 侧闭环验收状态。
- [x] 4.4 `Docs/design/CookingGame/progress.md` 增加"2026-09-21 ET Level 闭环验收增量"一节（门禁实测数字 + 非宣称清单），后续节号顺延；successor 段落更新。
- [x] 4.5 `Docs/Todo.md`：P0-C1"让 ET fixed-tick host 承载同一条番茄蛋花汤闭环验收"勾选并引用证据；总体判断补一句状态。
- [x] 4.6 `check.jsonl` 记录每个被检文件的理由；`research/verification-2026-09-21.md` 写方法、门禁输出、独立复验、变异结果与未覆盖边界。
- [x] 4.7 `implement.jsonl` 补全上下文清单（spec/研究文档）。

## 阶段 5：收口

- [x] 5.1 全量复跑两个 Cooking gate，确认绿；`git status --porcelain` 只含本任务预期文件（1 个产品文件修改 + 1 个新测试文件 + task 目录）。
- [x] 5.2 `python .trellis/scripts/task.py validate` → `start` → 文档回填实测值 → `archive --no-commit --skip-branch-validation`（branch == base_branch == master）。
- [x] 5.3 汇报：两个 gate 实测数字、证据位置、是否提交由 owner 决定（归档一律 `--no-commit`）。
