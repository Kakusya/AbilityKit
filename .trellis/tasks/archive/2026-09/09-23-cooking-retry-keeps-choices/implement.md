# 失败重开带走当前进程的解锁与煮制加速：实施清单

设计见 [design.md](design.md)。先补领域入口，再接到宿主，最后用两个现有门禁验收。

## 1. 领域

- 在 `CookingRecipeSimulation` 增加一个准备态入口，供重开使用：按进度装修（为空则跳过）、按解锁摆放、挂上同一份进度。
- 这段入口在调用期间暂时拿掉生命周期门，结束时放回。任一拒绝则厨房保持调用前的现场，进度对象也不改。
- 不改 `CookTicks` 的缩短规则，不改 `PlaceUnlock` 的编号规则。

## 2. 宿主

- `CreateRetry` 增加可选 `CookingMajorProgress`。先建厨房并套用进度，成功后才 `InstallGeneration`。
- 套用失败：关闭新厨房，返回拒绝，不安装候选，不调用 `DropFailedScene`。失败现场仍是这次调用前的厨房。
- 套用成功：再走现有安装、收养和前厅丢弃。
- 不调用 `WriteMajorCheckpoint`，不调用 `Lock()`。

## 3. 测试

- 领域：一份含 `bread-slice` 解锁和煮制加速、没有装修的进度，套到标准供应厨房上。多出两份解锁面包片。随后开始番茄蛋花汤，所需 tick 是内容时长的一半。未知解锁或未知工位零变更。
- 宿主：失败现场先拿走番茄、开出订单。重开带同一份进度。新厨房没有那张订单，番茄回到供应位置，解锁面包片在，前厅空。再开始一锅汤，所需 tick 缩短。
- 宿主：不传进度的重开，canonical 仍等于标准供应。
- 宿主：进度里的装修指向不存在的工位，重开拒绝，厨房仍是失败现场。
- 重开前后，大关检查点目录不出现新文件。未锁定进度重开后还能改；已锁定进度重开后拒绝再改。

## 4. 文档

- `cooking-recipe-loop.md` 和 `spec/cooking/index.md` 记一条修约：失败重开会带走当前进程的解锁和煮制加速，不写检查点。
- `progress.md` 增加一节，并在第 18 节指向它。
- `Docs/Todo.md` 只在已有 P0-C1 条目上补已落地的这一小条。不勾选评分、失败条件或可见顾客。

## 5. 门禁

- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop`
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime`
- 不改 `tools/test-gates.json`。

## 不做

- 不新增内容工位。
- 不定义失败条件。
- 不改三个预存 DLL，不 push。
