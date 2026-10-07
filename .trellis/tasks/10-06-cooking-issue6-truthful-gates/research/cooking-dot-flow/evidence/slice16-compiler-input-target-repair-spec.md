# Slice 16: compiler input and target attribution repair

## Authority and base

Dot request `AK-I6-COOK-FINAL-20261007-02` returned `changes-required` for exact candidate `2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc` and explicitly authorized one bounded repair worker. Use a fresh cc-switch Codex session with `model_provider=custom` in a new Orca-managed child worktree based exactly on that candidate. At the migration prompt explicitly select `2. Use existing model`.

Do not communicate with, reuse, modify, stop, release, or clean any existing worker session or worktree. You are not alone in the repository; preserve every other branch, worktree, process, failure, evidence record, and user edit.

## Product boundary and Cooking consumer

This repair serves only the approved Cooking gates `cooking-et-level-runtime` and `cooking-kitchen-loop`. Their truthful compiler-input closure is the concrete consumer and necessity for every shared tool change.

Do not modify Shooter, MOBA, Orleans, Unity, SDK, dependencies, ET versions, gameplay, services, benchmarks, other gate definitions, defaultGate, or unrelated examples. Do not run Unity, physical LAN, services, or benchmarks.

## Exclusive file ownership

The worker may modify only:

- `tools/test-gate-compiler-events.cs`
- `tools/test-gate-result-contract.ps1`
- `tools/tests/test-gate-result-contract.tests.ps1`
- `tools/tests/fixtures/gate-result-model.ps1`
- `tools/tests/fixtures/gate-result-fake-dotnet.ps1` only when the fixture protocol must change
- this task's Slice 16 repair report and raw/control evidence

The worker must not modify `.agents/skills/cooking-dot-workflow/scripts/records.py`, `.agents/skills/cooking-dot-workflow/scripts/test_controls.py`, `.agents/skills/cooking-dot-workflow/references/recovery.md`, workflow records/index, Issue/PR data, or any other file. The coordinator will separately remove the reusable succession mechanism changes from this PR while preserving their history and evidence.

## Required repair A: compiler input closure

Current event reading silently skips relative Csc file inputs and file inputs that are missing at replay time. Preserve each relevant file parameter category and original `ItemSpec`; resolve file paths using explicit project/task context. A required compiler input that was actually consumed but cannot be located, read, or verified must fail closed. Do not classify every scalar compiler parameter as a file.

Reproduce and preserve the current false-accept behavior before fixing it. Add named controls for:

1. a valid relative source input positive;
2. a generated relative source added after capture but before Csc;
3. relative input omission;
4. relative input replacement;
5. an input consumed by Csc and then missing before replay.

## Required repair B: CoreCompile target attribution

`Get-GateEventContextKey` currently omits `targetId`, so a successful Csc from a sibling target in the same project instance can satisfy a different finished CoreCompile. Require each Csc to match the owning CoreCompile target ID. Independent capture events remain associated at project-instance scope; do not require their target ID to equal CoreCompile's target ID.

Reproduce and preserve the current false-accept behavior before fixing it. Add named controls for:

1. a Csc-only `targetId` mismatch negative;
2. a real fixture where a sibling target in the same project instance executes Csc;
3. retained normal parallel, multi-instance, and incremental-skip positives.

## Acceptance and stop boundary

- Preserve all existing 290 controls with their existing expectations.
- Run the affected controls and the complete `290 + N` isolated contract suite; record exact source identity, commands, native exits, actual/expected outcomes, named controls, counts, and raw paths.
- Inspect scope and report every modified file. Commit the bounded implementation and report in the worker branch.
- Do not run either genuine Cooking gate in this source-worker dispatch. A fresh independent verifier will run both gates only after coordinator review and integration of the frozen implementation commit.
- Do not push, merge, edit the Issue/PR, archive the task, or clean resources.
- Before completion, read the orchestration inbox, send exactly one matching `worker_done` for the active Task and Dispatch, then idle.
