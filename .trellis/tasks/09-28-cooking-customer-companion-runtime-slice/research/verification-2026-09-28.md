# Verification — 2026-09-28

## Implemented scope

- `CookingFrontOfHouse` now assigns Level-local `CookingCustomerId` values and derives order identity from the customer rather than the reusable table.
- Immutable customer, companion and front-of-house snapshots expose phase, target and progress state with deterministic canonical text and SHA-256.
- Front-of-house checkpoint export/restore covers schedule, active customers, companion work, wash queue, unsatisfied records, service clocks and identity watermark.
- Restore validation rejects poisoned customer/table/order/companion/queue/counter/unsatisfied data before replacing the live front-of-house state.
- `CookingLevelCheckpoint` format is v2 and optionally carries front-of-house state; v1 is explicitly rejected.
- `CookingLevelEtHost` exports and restores kitchen plus front-of-house state, disposing the candidate host when front-of-house restore fails.

## Verification results

- Focused poisoned-checkpoint test: 1/1 passed after strengthening unsatisfied-order identity/watermark validation.
- Cooking full suite: 218/218 passed.
- ET Runtime full suite: 63/63 passed.
- `cooking-kitchen-loop`: focused 97/97, Cooking 218/218, ET Runtime 63/63, both builds zero warnings, exit 0.
- `cooking-et-level-runtime`: Cooking 218/218, ET Runtime 63/63, both builds zero warnings, exit 0.
- `git diff --check`: exit 0; only repository line-ending warnings were reported.

Gate summaries:

- `local/Logs/test-gates/20260928-184932-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json`
- `local/Logs/test-gates/20260928-184957-cooking-et-level-runtime/cooking-et-level-runtime/gate-summary.json`

## Review note

- The implementation worker was dispatched through Trellis, but its isolated sandbox rejected repository access and it made no changes.
- The first check worker could not start because the local Claude executable was unavailable.
- The Codex check worker received the task artifacts but its sandbox also rejected repository reads. It made no changes and independently highlighted the unsatisfied-order checkpoint poisoning risk.
- The main session completed the full diff/spec review, added structured rejection coverage for customer identity, table occupancy, order association, companion target, duplicate wash queue, counter watermark and unsatisfied-order poisoning, and reran both authoritative gates.

## Not run

- Unity compile/EditMode: outside the approved pure C# scope.
- Two-PC physical LAN and LAN wire changes: outside task scope.
- Durable store/process-crash recovery: outside task scope.
- Global regression gate: not required by the task-selected Cooking P1 gates.
