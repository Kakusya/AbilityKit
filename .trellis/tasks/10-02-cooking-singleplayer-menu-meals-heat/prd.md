> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S10 候选正餐热加工扩展

> 最新执行授权：2026-10-02 owner 后续明确实施单机与联网、worktree并行、验证后合并master；Unity后置。 下面初稿未执行表述为登记时历史；具体执行设计及证据按总任务 execution.md 与本 Task 后续修订。

状态：planning / draft / 未执行。

## 目标与范围

M2：F17–F20/F28–F42。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：S09。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

未熟披萨/蒸饺可交接，烤/蒸/炸/烤架能力独立，出餐小锅不混同后厨锅。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。

## Authorized implementation refinement, dependency closure pending

Owner subsequent approval and this Orca dispatch authorize implementation of S10; earlier planning-only wording above is historical. Scope is exactly 19 candidates: F17, F18, F19, F20, F28, F29, F30, F31, F32, F33, F34, F35, F36, F37, F38, F39, F40, F41, F42. Uncooked pizza/dumpling stages, real bake/steam/fry/grill vessels, separate serving saucepan. Catalog coverage and per-menu preflight evidence exist under S04; full batch acceptance waits for validated S05 import, actual delivery/bound recovery and preceding task closure. This candidate set does not become a first-level menu. Unity, economics, precise minigames, procurement and full S14 level exit remain outside this task.
