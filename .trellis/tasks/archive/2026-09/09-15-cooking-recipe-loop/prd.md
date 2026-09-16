# P2 一条完整配方

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

## 当前状态与受限实施范围

- 本 task 已于 2026-09-16 归档，Trellis status 为 `completed`；最终语义为 `completed-limited-scope`，只覆盖 R01-R06 pure .NET fixture。
- 已实现并验证的范围仅为 **pure .NET fixture recipe loop**：单输入、单工序、3 个逻辑 Tick、一个完成产物、container slot 与注入式 accept/reject order port。
- R01-R06 已实际运行；R07、Unity、真实 LAN、正式内容与完整 P2 exit 尚未执行。
- P0 的 authority/location/idempotency evidence 是当前领域前置。P1 的纯 .NET contract 可作为后续 integration seam，但完整 P1 LAN 仍 blocked，不能由本期 in-process test 替代。

## 来源与可追溯性

来源快照：`.trellis/migration/legacy-cooking-changes/add-cooking-recipe-loop/`。完整文件哈希与目标映射见 `.trellis/migration/legacy-cooking-changes/manifest.json`。

## Why

本期验证不依赖 Unity 或 transport 的数据驱动结构：合法 input 经 appliance process 进度完成为 product，product 被独立装盘后再由外部 order port 接受或拒绝。这样能验证原子边界、逻辑 Tick、ownership、容量、snapshot 与幂等，而不将路线图示例升级为正式菜谱、订单或结算。

## 已实现的 fixture contract

- Recipe/Process/Appliance/Container 与 runtime Item/Process state 均在 Cooking application 的纯 .NET 层，定义与实例 ID 分离。
- `StartProcess` 验证 scope、player、input hand location/version、item/player capability、appliance availability/capability/reachability 与 station process occupancy；通过后才锁定 input。
- `AdvanceTicks` 只以明确 logical Tick 推进。Tick 1/2 保持 process；到达 fixture 的 Tick 3 后，一次性消耗 input、创建一份 product、清除 process。
- `Plate` 是独立原子步骤，为容量允许的 container 分配稳定且唯一的 `slot-N`；拒绝不产生部分 mutation。
- `SubmitOrder` 是独立原子步骤：它验证 product plated state、来源 station reachability、version 与 submission lifecycle，再调用 injected order port。port reject 不回滚 plating；accept 才消费 product 并记录 order。
- 同一 command identity 返回 cached result，无二次 mutation/event；不同 identity 重交已消费 product 被拒绝。
- Canonical snapshot/hash 包含 logical tick、items、recipe/product identity、process progress、origin station、container slots 和 accepted order IDs。

## Fixture decisions, not product decisions

- 单输入/单工序、3 Tick、fixture definitions 和 test order port 仅用于结构验收；它们不是正式菜谱、真实秒数或平衡参数。
- `ICookingOrderPort` 只定义可观察的 accept/reject 边界。正式 order owner、评分、结算、失败产品 UX、时限、奖励与持久化继续由 owner 决定。
- 本期不创建 recipe editor、Unity projection/UI、Catalog/Wire Schema、transport adapter、真实 socket 或 save/migration 行为。

## 完整 P2 仍 blocked/deferred

- 正式配方内容、order/settlement owner、评分、失败处理与产品 timing。
- P0 Unity T09-T10 与 P2 Unity scene/projection/EditMode。
- P1 的 D1-D4、production adapter、L10-L12 和真实两 PC LAN。
- R07 two-PC LAN recipe-loop 验收、benchmark、P2 archive 与 P3/P4/P5/P6 完整解锁。

## Impact

- 实现仅位于 `src/AbilityKit.Game.Cooking/` 和对应 xUnit tests；不修改通用 AbilityKit、Unity 自动生成项目、Server 或 Orleans。
- 后续正式 protocol/config source 仍须遵守 `Protocols/README.md`，本期没有新增 Catalog/Wire Schema。
- 纯 .NET fixture 通过不能声明正式内容、LAN、Unity、生产订单系统或 P2 完整完成。
## 2026-09-16 受限范围收口

- Owner 已批准将本 task 的最终完成边界重划为：**R01-R06 纯 .NET fixture recipe loop（27/27；5 个 JSONL、19 条记录）**。该范围由现有 `check.jsonl` 支持，已于 2026-09-16 按 `completed-limited-scope` 归档，status 为 `completed`。
- 正式内容、order/score/settlement、真实 LAN 移至 successor backlog；Unity 移至 future scope。
- 旧 checklist 中未运行的 Unity、真实 LAN、production transport、正式内容、durable storage 或完整产品出口不再是本旧 task 的归档 blocker；它们也没有因此变成已完成。
- 本 task 的 `completed`/归档语义只修饰上述纯 .NET 受限交付，不表示完整 P2、Unity 可玩版本、真实 LAN 或完整 P0-P6 产品出口完成。
- 原始检查事件保持不变；closure 只记录 2026-09-16 的 owner scope 决定。
