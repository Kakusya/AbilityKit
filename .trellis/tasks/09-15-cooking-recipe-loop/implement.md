# P2 一条完整配方：受限实施清单

> 顶层 task 为 `in_progress`。本期只完成已决定的 pure .NET fixture loop；完整 P2 LAN/content/Unity exit 继续 `blocked`，不归档。

## 1. 领域数据与命令边界

- [x] 1.1 已在应用层建立 Recipe/Process/Appliance/Container/Order port 及 runtime item/process state，定义/实例 ID 分离。
- [x] 1.2 已实现 held input → start process → logical Tick → atomic completion；R01-R02 验证 3 Tick 进度、缺 input、appliance capability/range/availability 与 mutation-safe rejects。
- [x] 1.3 已将 plate 与 order submit 作为独立原子 command；R01/R03 验证 plate 保留、reject 不回滚、accept 消费 product、submission reachability 与 container slot uniqueness。
- [x] 1.4 已实现 recipe command idempotency/fingerprint conflict 与稳定 events/snapshot；R04 验证 duplicate 和 consumed-product replay。

## 2. 数据驱动闭环验收

- [x] 2.1 建立单输入/单工序/3 Tick fixture 与第二 recipe/appliance fixture；R01-R05 已通过，第二 fixture 仅改变数据。
- [x] 2.2 R06 已验证相同 recipe commands 在两个 independent pure .NET simulations（host-local/remote-in-process fixture）得到同一 snapshot/event sequence。它不是 network/LAN evidence。
- [ ] 2.3 **Blocked / not run**：R07 two-PC LAN recipe loop。仍依赖 P1 production transport、LAN integration 及 D1-D4 decision gates；不得用 R06 替代。

## 3. 宿主与门禁证据

- [x] 3.1 已确认改动只在 pure .NET Cooking application/test projects；没有修改 Unity/generated `.csproj`、Server、Orleans 或 common AbilityKit。
- [x] 3.2 已运行 focused Cooking build/test 与 `git diff --check`。现有 `core-stability`/`runtime-contracts` 没有覆盖这个独立 Cooking project，未运行；详情见 `check.jsonl`。
- [x] 3.3 正式配方、order owner、评分/结算、失败产品 UX、Unity、production transport 与 LAN 仍明确 Draft/Blocked；fixture tests 不是上述决策或完整 P2 exit。
