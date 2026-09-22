# 小关成功收口：实施清单

设计见 [design.md](design.md)。未 `task.py start` 前不改产品代码。

## 顺序

1. 读 `trellis-before-dev` 与 cooking spec index，确认交接规则写入哪份 spec，不另起平行文档。
2. 仿真：`ExportSuccessHandoff` 与 `AcceptSuccessHandoff`。先写失败用例（订单、结算、Tick、事件、LevelScope 非空，计数器回退）再写成功用例。
3. 关闭后的只读导出。核对 `CloseLifecycle` 今天是否拒绝 `ExportCheckpoint`；若拒绝，只放行导出，不放行 mutation。
4. 宿主 `CreateSuccessor` 改为安装 `Created` 代际并换入交接载荷。`Start` 对已交接代际绑定现有仿真，普通开局仍 factory 新建。
5. `CreateRetry`、同代际 `Restore`、指纹金样保持原路径。
6. 补 spec 一行：成功交接保留什么、清除什么、停在 `Created`。不把未实现的失败重开写成已实现。
7. 跑门禁并写 `check.jsonl`。

## 验收命令

沿用现有 P1，不新增 gate：

- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop`
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime`

领域新测试挂 `CookingKitchenLoop` trait。宿主新测试进 ET runtime 程序集。两条都要实跑；只跑 focused 不算宿主验收完成。

## 用例

- H01：番茄蛋花汤闭环走到含进行中加工、至少一单、一条结算、非零 Tick。成功 `Ended` 后交接。断言物品、加工、容器、干净池、消耗产物、三个计数器与收口前一致；订单、结算、去重、两类事件为空；Tick 与事件序号为 0。
- H02：新 `LevelId`、更高 `LevelEpoch`、同一 Match 与 Runtime；状态为 `Created`。tick 与 gameplay 命令拒绝且零变更。随后 `Prepare` + `Start`，保留现场仍在，订单不回来。
- H03：`Running`、`Paused`、`Failed`、`Aborted`、重复交接、epoch 不前进、LevelId 缺失或不变。现有原因码，源 canonical 不变。
- H04：同代际 checkpoint 恢复仍带订单、结算与原 Tick。既有 R01 不改断言。
- H05：宿主与领域各跑 H01/H02。宿主 `HostFrameSequence` 不回退。

## 风险

- `CompleteEnd` 已释放仿真所有权。交接若在释放后丢失对象，H01 会假绿（对新厨房断言空订单）。断言必须绑定收口前的物品 ID 与加工 ID，不能只断言订单为空。
- `Start` 分叉若用 null 仿真判断，普通开局会和交接代际混在一起。必须是 lifecycle 上的显式标记。
- 交接不小心调用 `ExportCheckpoint` 再 `Restore`，会把订单和 Tick 带进下一关。H01 专门钉空订单与 Tick 0。

## 回滚

改动集中在 `CookingRecipeCheckpoint.cs`、`CookingLevelLifecycle.cs`、`CookingLevelEtHost.cs` 与对应测试。未提交前可以整批撤回。不改内容 JSON，不改指纹金样，不改 `tools/test-gates.json` 的步骤，除非 trait 过滤实在挂不上。

## 完成前

- `check.jsonl` 记录实际命令、退出码与跳过原因。
- `progress.md` 第 8 节只把「成功进入下一小关」的本任务范围标成已落地；失败重开、工位升级、写盘仍保持未完成。
- 09-19 讨论任务保持 `planning`，不归档、不 `start`。
