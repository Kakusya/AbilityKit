# P2 一条完整配方：受限 pure .NET fixture 设计

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 完整 P2 产品出口未完成；本旧 task 仅按 R01-R06 pure .NET fixture limited delivery 收口。本文件记录已实现的 fixture contract，而非正式菜谱、订单、结算、Unity 或真实 LAN 实现。

## 已实施结构

`CookingRecipeSimulation` 位于 `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs`，独立于 P0 的 pickup/drop `CookingSimulation` 与 P1 session authority。它复用 `CookingScope`、Player/Item/Definition/Station ID、`ItemLocation`、canonical JSON/hash 与 JSONL evidence 的应用层风格，但不引入 Unity、transport 或通用框架依赖。

运行态由以下固定 fixture 规则驱动：

```text
held input
  -> StartProcess(appliance capability + reachable station)
  -> AdvanceTicks(1, 2: processing; 3: consume input/create product)
  -> Plate(container slot-N)
  -> ICookingOrderPort.Submit(accept or reject)
```

### 原子边界

- 每个 command 先验证，再一次性变更 state/event。
- process 未完成时没有 product；完成时 input 只消耗一次，product 只创建一次。
- container slot 在 commit 前从容器现有 product location 中选择第一个稳定 free `slot-N`。
- order port reject 只拒绝 submission，保留 plating/container state；accept 才消费 product 和写入 accepted order。
- product 保留 origin station，仅允许对该 station 有 reachability 的 player submit，避免任意 player 提交已装盘 product。
- command ledger key 为 session/player/recipe-command；相同 fingerprint 返回 duplicate result，不同 fingerprint 返回 conflict。

### 可观察状态

`CookingRecipeSnapshot` 的 canonical JSON/hash 包含 scope、state version、logical tick、items（definition/version/location/recipe/product/origin station）、process state、container slots 与 accepted orders。这样进度、产物、装盘和 order changes 都参与 mutation-free 与 determinism assertions。

## 订单边界

`ICookingOrderPort` 是本期唯一 order seam。test double 的 accept/reject 结果用于证明 atomicity，不拥有正式订单身份、评分、结算、奖励、存档或产品 failure UX。

## 验收矩阵

| ID | 实际状态 | 本期证据 |
|---|---|---|
| R01 | passed | 单输入/单工序 3 Tick、product、plate、accept order |
| R02 | passed | missing input、capability/range/availability、incomplete product mutation-safe rejects |
| R03 | passed | reject preserves plated state；submission reachability；multi-slot uniqueness |
| R04 | passed | duplicate command cached；consumed product cannot resubmit |
| R05 | passed | second recipe/appliance fixture changes definitions only |
| R06 | passed | equivalent host-local/remote-in-process fixture input produces same snapshot/events |
| R07 | blocked / not run | 真实 two-PC LAN；不得由 R06 替代 |

## Deliberate exclusions

不实施正式 recipe content、order/settlement owner、score、product timing balance、Unity projection/scene/UI、production transport, wire schema、socket/listen/connect、same-machine network integration、two-PC LAN、benchmark、persistence、match lifecycle、reconnect 或 migration。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**R01-R06 纯 .NET fixture recipe loop（27/27；5 个 JSONL、19 条记录）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- 正式内容、order/score/settlement、真实 LAN 移至 successor backlog；Unity 移至 future scope。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P2、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始检查事件保持不变；closure 只记录 2026-09-16 的 owner scope 决定。
