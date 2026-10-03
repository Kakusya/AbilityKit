# Independent ACK cleanup / paused publication review

2026-10-03. Read-only review of main production `0d0beb2f1` and recovery test commit `3333c5311`. Loaded N03 PRD/design/implement and check manifests plus referenced context; no .NET command was run because the rich worker owns that window. Only this report is reviewer-owned.

## Decision

Import `3333c5311` is approved as a bounded regression increment. No production blocker was found in the reviewed diff. This is not full N03, rich four-cutpoint, physical LAN or performance acceptance. Appropriate master regression and rich recovery execution remain required.

Production file diff between main `0d0beb2f1` and recovery production `ea7567980` is empty. The test commit adds only the two regression tests and producer evidence report.

## Actual execution inspected

Recovery worktree `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-recovery-current`:

- `local/Logs/cooking-execution/network-paused-publication/paused-publication-original-red.trx`: total/executed2, passed0, failed2, notExecuted0. Both named cases failed on original source. Fixture compile errors are separate and are not behavioral red.
- `local/Logs/cooking-execution/network-paused-publication/paused-publication-final-duplicate.trx`: total/executed2, passed2, failed0, notExecuted0. Matching final stdout `local/paused-publication-final-duplicate.log` reports 2 passed, 0 failed/skipped and actual build outputs.
- Tests use actual CookingLevelEtHost, authority adapter, Session and framed InProcess raw controls. No fake capture or manually changed lifecycle string substitutes for actual Pause/Resume.

The first case retains an old Running baseline while Pause occurs, then exact ACK releases one complete Paused baseline. It compares observation and full Recipe canonical, frame sequence, null resumable checkpoint; ACK plus twelve paused owner frames does not flood unchanged state or advance clocks.

The second case closes participant A while paused, verifies actual CleanupPending, joins B, submits/repeats exact issued ACK, and asserts no Ready/rejection or clock/observation change. Resume plus owner frame clears A cleanup, releases B's exact prior identity as Ready and publishes newer complete state. Assertions cover duplicate coalescing and no early Ready.

## Source paths and boundaries

One nullable tuple per Connection retains identity and first correlation; repeated exact ACK uses `??=` rather than unbounded waiters. Invalid/stale ACK still follows BaselineRequired. It does not release BaselineAwaitingAck or set Ready during global pending cleanup. After accepted cleanup and successful capture, FlushDeferredAcks checks current live connection, physical connected flag, active participant binding, awaiting slot and exact issued identity before CompleteAck. Close/supersession, unavailable authority and scope retirement clear DeferredAck together with issued/Ready state. These reset statements are source evidence, not newly executed controls.

Paused ProcessOwnerFrame rejects business commands and returns before ConsumeFrame. It flushes only when no cleanup/unavailable state and publishes only after computing combined authority-plus-Session hash, when changed and no baseline awaiting ACK. Thus old issue is never overwritten, and Session projection changes can still propagate even with unchanged Recipe version. No schema, receipt bounds, domain authority or restoration permission changes.

## Required follow-up coverage (not fixed here)

- Dedicated deferred-slot close/supersession, scope retirement and authority Faulted/Disposed/Busy transitions were not executed by these two new tests. Add deterministic controls that create a real deferred slot before the transition and prove stale Ready cannot leak after it, rather than citing existing guards as test evidence.
- Malformed/different identity while an exact ACK is retained should be explicitly exercised to show it does not replace the first valid slot; source currently rejects it.
- Rich F01+D31 four-cutpoint composed continuation, latest master broad gates and physical independent-host evidence remain separate exits. Do not derive these from focused2/2.

## Worktree cleanup appendix (no deletion performed)

Actual `git worktree list --porcelain` and each tree's `git status --short` / `git merge-base --is-ancestor HEAD master` inspected. This only proves Git state, not absence of live processes or task references.

Immediate candidate inventory after root confirms no live ownership/process and migrates evidence:

| Worktree | Git state | Action prerequisite |
|---|---|---|
| cooking-core-verified-snapshot | clean, ancestor master | Preserve referenced local proof/logs and detached HEAD ref, then retire if no references need physical tree. |
| cooking-network-process-current | clean, ancestor master | Preserve original independently verified process logs/paired reports, runnable bin/metadata and source refs; confirm root no longer needs this execution tree. |
| cooking-core-s01-s03; cooking-menu-ready-s14; cooking-menu-s04; cooking-network-authority-current; cooking-network-et-s14-integration; cooking-network-transport-current; cooking-order-s05 | clean, NOT ancestor master | Cherry-import histories mean nonancestor is not proof of unsaved work. Keep refs, audit patch equivalence against accepted master and archive ignored evidence before retirement. |
| cooking-integration-s06-s14; cooking-natural-operating-s14; cooking-network-n01-n03; cooking-supply-s07; cooking-technical-recovery-s14 | dirty, some nonancestor | Save tracked diffs and untracked files with manifest/hashes plus branch refs and local evidence before any removal; no force-delete of unsaved state. |
| cooking-network-recovery-current | dirty and active rich/profile/process work | Keep. Its worker-owned new files and production-related diagnostics are current work. |

For every retired tree, archive ignored local Logs/TRX/runner artifacts outside that tree under a verified destination, record original absolute path, HEAD, archive file list/hashes, evidence paths and task references. A Git bundle/ref alone does not preserve ignored logs or dirty/untracked work. Update task routes to surviving archives; use native checked-path Orca/Git worktree removal only after root confirms ownership and process release. No tree was removed by this review.

## Verification

Lint/type-check: not rerun (read-only dispatch; .NET window reserved). Existing focused build/test outputs inspected, not claimed as independent rerun. Tests: inspected actual producer 2 red / 2 green; master regression pending. Review changed only this document; no source fix required.
