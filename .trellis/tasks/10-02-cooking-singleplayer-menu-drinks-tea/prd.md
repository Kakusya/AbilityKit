> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S13 候选咖啡与茶饮内容

> 最新执行授权：2026-10-02 owner 后续明确实施单机与联网、worktree并行、验证后合并master；Unity后置。 下面初稿未执行表述为登记时历史；具体执行设计及证据按总任务 execution.md 与本 Task 后续修订。

状态：planning / draft / 未执行。

## 目标与范围

M5：D15–D31。以单机→网络→单机 Unity→网络 Unity 顺序推进。

## 前置

依赖：S12。必须读取 [总 plan](../../spec/cooking/gameplay-menu-plan.md)、[注册表](../10-02-cooking-gameplay-menu-plan/task-register.md)、相关生效 spec 与历史 check。

## 验收

研磨/萃取、加热/打泡模式、茶/珍珠共享，奶盖最后加、奶茶与冲调及鲜奶来源不混写。拒绝零变更、幂等、canonical/hash、scope 与恢复覆盖随实际新增状态一起验证。

## 范围外

本轮不改产品代码，不执行该 Task；单机 Task 不做传输与 Unity，网络 Task 不做 Unity；额外机制只审议不默认纳入基础。

## 执行门

这是可审议的初始记录，不是最终实施批准。开始前细化具体 API/错误矩阵/测试与版本策略，重新核查依赖及环境，审阅最终 planning summary 后由 owner 明确批准。

## Authorized implementation refinement, dependency closure pending

Owner subsequent approval and this Orca dispatch authorize implementation of S13; earlier planning-only wording above is historical. Scope is exactly 17 candidates: D15, D16, D17, D18, D19, D20, D21, D22, D23, D24, D25, D26, D27, D28, D29, D30, D31. Ground/extracted coffee, heat/foam modes, tea/pearls batch portions, must-last foam and brown-sugar milk without tea. Catalog coverage and per-menu preflight evidence exist under S04; full batch acceptance waits for validated S05 import, actual delivery/bound recovery and preceding task closure. This candidate set does not become a first-level menu. Unity, economics, precise minigames, procurement and full S14 level exit remain outside this task.

## Current implementation acceptance pointer

Coordinator-validated carrier/provenance/S05 hooks are now integrated into actual menu loading. All87 source-to-preparation-to-final-to-correct-vessel-to-delivery routes and121 actual ET recovery comparisons passed; current evidence and exact remaining S07/S14/Unity/balance boundaries: ../10-02-cooking-singleplayer-menu-schema/research/final-report.md and ../10-02-cooking-singleplayer-menu-schema/research/verification.md. Historical pending-hook/design-only statements above are superseded for worker menu acceptance; coordinator independent review/master integration still pending.
