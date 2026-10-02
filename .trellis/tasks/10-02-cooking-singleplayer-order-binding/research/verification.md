# S05 production verification ? 2026-10-02

Worktree: `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-order-s05`; branch `cooking-order-s05`; reviewed dependency base `501bf383d`. Production/test commit: `c6bc46c5b`.

## Implemented and reviewed
Existing Recipe/Order authority now owns BindOrder/UnbindOrder/RebindOrder and observable physical-product BoundOrder. Commands append enum values and reuse Item/Order/ExpectedItemVersion; existing ET fingerprint already covers these fields, so no host byte-layout change was needed. New fixed rebind SHA-256 vector is `6F28E4F85048A27E3C6AB3E6058808B78C44479102629D83E9C6B57AA5A0F91D`; old vectors remain valid.

RequiresBinding and DisposableOnSubmission are optional false content/domain hooks, propagated/frozen and included in definition-v3 identity. Recipe schema4 and Level format5 are the single coordinated recovery bump. BoundOrder is JsonRequired including null; missing current-format authority fails codec read. Same-Level binding restore checks Open matching order, clean compatible serving vessel, unique order and container ownership and processing locks. Cancellation, disposal and handoff clear bindings. Bound vessels move/share; bound food must unbind before removal or further processing. Disposable Submit releases the hand/space without washing; cleanPool rejects disposable supply. Historical direct meal Submit, washable vessels and nonwashable defaults still pass regression.

Cross-review found and fixed processing-lock bypass, reverse container membership restore bypass, eligibility omission and post-settlement overflow. Final review additionally tightened vessel acceptance/capacity restore parity and nullable process-lock rejection. Submit versions/counters preflight before mutation. No unrelated refactor, menu/generated/FrontOfHouse/Unity/root-dashboard edits, merge or push.

## Actual checks

| Command/scope | Actual result | Evidence |
|---|---|---|
| Focused catalog/configuration suites | 16 passed, 0 failed/skip | `artifacts/s05-config/test.log`, `artifacts/s05-config/s05-config.trx` |
| Focused binding + command shape | 22 passed, 0 failed/skip | `artifacts/s05-domain-tests/s05-domain.trx` |
| ET focused binding (3 tests, also exercised by final gates) | 3 passed, 0 failed/skip | `local/Logs/s05-focused/s05-et-focused-final.trx`; final gate ET TRX includes latest missing-field/tamper assertions |
| `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop` final | exit0, focused221/221, Cooking343/343, ET76/76, no failures/skips | `local/Logs/test-gates/20261002-192317-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json`; per-step log/TRX paths in summary |
| `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime` final | exit0, Cooking343/343, ET76/76, no failures/skips | `local/Logs/test-gates/20261002-192356-cooking-et-level-runtime/cooking-et-level-runtime/gate-summary.json`; per-step log/TRX paths in summary |
| `git diff --check` | passed | actual command before commit |
| Trellis S05 manifests validate | passed12 entries each; warning55148-byte recipe spec exceeds32768 injection cap | S05 contract is prepended; source read directly when needed |

Actual ET proof produces raw food through public command ingress, plates it, rejects unbound drink, binds, exports codec checkpoint, destroys host, restores same Level and delivers to identical canonical terminal result. It also runs two-player binding arbitration and explicit unbind/rebind, verifies bound-order submission, missing authority rejection and a forged binding restore rejection. Final gates build both pure C# owner and analyzer-backed ET runtime; their SDK projects own these sources directly, no UPM/asmdef file is changed.

## Failures retained honestly
Initial focused tests caught no-spatial contained reach mismatch (fixed via ContainerIsReachable), deliberate old enum expectation drift (updated appended values), and a test using malformed version0 rather than positive stale version (corrected). Initial ET runs each had1pass/1fail because the test incorrectly expected replay admission rejection, then incorrectly expected another Tick disposition; actual host returns an immediate accepted TerminalDisposition marked duplicate. Assertions now verify that real contract and one settlement; failed outputs remain `local-s05-et-first.log`, `local-s05-et-fixed.log` and initial TRX in `local/Logs/s05-focused/`. Initial kitchen gate also passed216focused/338Cooking/76ET, but final counts above supersede it after review fixes and added matrix coverage.

## Remaining external boundaries
Coordinator must independently review/cherry-pick S05 and rerun combined integration before master merge. Menu owner opts in31 drinks and disposable cups; S07 supplies physical replenishment. This dispatch does not claim all87 production menus, networking/LAN, Unity or durable storage complete. Unity intentionally deferred, not recorded as a passing check. S05 task stays in_progress pending coordinator integration rather than being prematurely archived.
