> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S09 候选正餐第一批

> 最新执行授权：2026-10-02 owner 后续明确实施单机与联网、worktree并行、验证后合并master；Unity后置。 下面初稿未执行表述为登记时历史；具体执行设计及证据按总任务 execution.md 与本 Task 后续修订。

状态：planning / draft / 未执行。

## 目标与范围

M1：F01–F16/F21–F27/F43–F44。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：S04, S05。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

先 F01/F11/F21 验证再扩完整批，切配/煮/煎/组合/分装全部从供应可达；不等同首关菜单。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。

## Authorized implementation refinement, dependency closure pending

Owner subsequent approval and this Orca dispatch authorize implementation of S09; earlier planning-only wording above is historical. Scope is exactly 25 candidates: F01, F02, F03, F04, F05, F06, F07, F08, F09, F10, F11, F12, F13, F14, F15, F16, F21, F22, F23, F24, F25, F26, F27, F43, F44. F01/F11/F21 seed cases; chopping, boiling, frying, independent pasta/sauce branches and portioned staples. Catalog coverage and per-menu preflight evidence exist under S04; full batch acceptance waits for validated S05 import, actual delivery/bound recovery and preceding task closure. This candidate set does not become a first-level menu. Unity, economics, precise minigames, procurement and full S14 level exit remain outside this task.

## Current implementation acceptance pointer

Coordinator-validated carrier/provenance/S05 hooks are now integrated into actual menu loading. All87 source-to-preparation-to-final-to-correct-vessel-to-delivery routes and121 actual ET recovery comparisons passed; current evidence and exact remaining S07/S14/Unity/balance boundaries: ../10-02-cooking-singleplayer-menu-schema/research/final-report.md and ../10-02-cooking-singleplayer-menu-schema/research/verification.md. Historical pending-hook/design-only statements above are superseded for worker menu acceptance; coordinator independent review/master integration still pending.
