> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S11 候选甜品内容

> 最新执行授权：2026-10-02 owner 后续明确实施单机与联网、worktree并行、验证后合并master；Unity后置。 下面初稿未执行表述为登记时历史；具体执行设计及证据按总任务 execution.md 与本 Task 后续修订。

状态：planning / draft / 未执行。

## 目标与范围

M3：S01–S12。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：S10。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

面糊共享分流、模具/脱模/裹粉/收尾可组合；不增加精准操作或冷却巡检。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。

## Authorized implementation refinement, dependency closure pending

Owner subsequent approval and this Orca dispatch authorize implementation of S11; earlier planning-only wording above is historical. Scope is exactly 12 candidates: S01, S02, S03, S04, S05, S06, S07, S08, S09, S10, S11, S12. Shared batter, physical molds, demolding/coating, S11 biscuit times2, explicit final additions. Catalog coverage and per-menu preflight evidence exist under S04; full batch acceptance waits for validated S05 import, actual delivery/bound recovery and preceding task closure. This candidate set does not become a first-level menu. Unity, economics, precise minigames, procurement and full S14 level exit remain outside this task.
