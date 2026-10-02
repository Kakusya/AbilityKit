> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S13 初始设计

状态：draft；未实施。

研磨/萃取、加热/打泡模式、茶/珍珠共享，奶盖最后加、奶茶与冲调及鲜奶来源不混写。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Reviewed content-batch contract

Implement every source identity in this Task PRD through the existing authority. Use the source-to-stable-ID map, exact multiset counts, explicit intermediate stages and real processing/serving carriers from the reviewed S04 contract. The complete batch and special production cases are specified in the parent research/menu-review.md batch table. Validate each dish from physical supply through public commands to delivery; do not inject finished items, use direct mutation, or substitute catalog counts for executable recipes. Include wrong-order addition, vessel mismatch, handoff, portions and checkpoint recovery; preserve existing soup/toast regressions.
## Concrete API, errors and migration

Use CookingMenuCatalog.Requirements(selectedSourceIds) for finite production/delivery capability, supply and container closure, then ToContentDocument(baseline, selectedSourceIds) and CookingContentCatalog.Load for the actual canonical validated runtime snapshot. Stable runtime IDs come from reviewed source-ID mapping, not row order or names. Source367-node decisions,31 directory discrepancies and immutable MD/XLSX SHA hashes remain S04 source-audit evidence. Existing soup/toast IDs remain unchanged.

Recipes consume explicit counted material units; every stage outputs a separately named intermediate for the next real recipe. Shared preparation YieldPortions/ServePortion and exact RequiredProcessingContainerDefinition are core-owned contracts. Source-declared storage receives physical transfers including single yields. ContentProvenance, selected menus and source SHA participate in formal configuration identity. S05 fields RequiresBinding/DisposableOnSubmission must be mapped after coordinator validation, never emulated in a separate model.

Errors: missing supply blocks acquisition; missing capability/processing carrier/storage/serving container rejects closure/projection or runtime action; wrong carrier rejects StartProcess even with equal capacity; missing repeated unit and premature topping reject without item/process mutation. Drinks require the bound open order and correct single-serving cup; food uses ordinary plate delivery. Rebind/unbind and disposable settlement belong to S05 authority.

Positive examples: full S13 raw-to-prep-to-stage-to-final-to-correct-vessel paths through real spatial commands and actual ET TryEnqueue/Tick. Negative examples: wrong cake mold, early foam, missing second unit, dropped adapter carrier, incomplete selected dependencies. Checkpoint codec/dispose/restore resumes the same production with equal full final canonical state. Manual paused-worker swap and partial batch need explicit additional fixture cases rather than inferring them from recipe count.

No worker-owned schema bump: core chooses current content/checkpoint versions; old schema inputs fail explicitly under core migration policy. Raw8, serving2, batch2, score100 and ticks1 are explicit test fixture defaults, not balanced production values; progress tests use ticks4/reach1200. Batch-specific concern: Ground/extracted coffee, heat/foam modes, tea/pearls batch portions, must-last foam and brown-sugar milk without tea.

## Current implementation acceptance pointer

Coordinator-validated carrier/provenance/S05 hooks are now integrated into actual menu loading. All87 source-to-preparation-to-final-to-correct-vessel-to-delivery routes and121 actual ET recovery comparisons passed; current evidence and exact remaining S07/S14/Unity/balance boundaries: ../10-02-cooking-singleplayer-menu-schema/research/final-report.md and ../10-02-cooking-singleplayer-menu-schema/research/verification.md. Historical pending-hook/design-only statements above are superseded for worker menu acceptance; coordinator independent review/master integration still pending.
