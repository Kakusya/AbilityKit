# Slice 15 exact-SHA verifier after compiler target compatibility repair

Target commit: `8497d3a673bd84be079d99a58df97b96676dea88`.

Use this fresh cc-switch Codex session with `model_provider=custom`. Do not communicate with, reuse, modify, stop, release, or clean any existing worker session or worktree. You are not alone in the repository; preserve every other branch, worktree, process, failure, and evidence record.

This is Cooking-only verification. Do not modify or revert implementation, gate configuration, Cooking business code, tests, dependencies, SDK settings, Unity, Shooter, MOBA, Orleans, or any example. Do not commit, push, merge, write GitHub, contact dot, create another worker, or clean resources. Ownership is limited to one new report:

- `.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/slice15-exact-sha-verification-report.md`

Before running anything, require `git rev-parse HEAD` to equal the target commit and require `git status --porcelain` to be empty. If either check fails, stop and report failure without changing source. Require the control artifact path below not to exist before launch.

Run these checks serially, never concurrently:

1. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s15-verifier-controls`
2. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI`
3. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI`

Preserve every original failure. Stop after any required check fails, leave later checks `NotRun`, and do not rerun a failed check to conceal it or repair product/tooling.

The controls must execute exactly 290 cases with 290 Passed, zero Failed, `fullControlSuiteAccepted=true`, `contractControlsAccepted=true`, `realDotNet=Executed`, console-width Passed, all original 249 names and all prior approved additions present, no duplicate names, and source-before/source-after SHA and fingerprints equal. The two genuine gates must each finish Passed/CLI 0 with actual nonzero TRX execution and complete required leaves; record actual counts rather than historical values.

For every command, preserve exact argv, start/end UTC, process exit, raw output, receipt or summary path and SHA-256, run/result IDs, source SHA/dirty state, SDK/MSBuild/PowerShell versions, declared and actual coverage, TRX files/counts, build/test binary identities, and limitations. Explicitly distinguish Passed, Failed, Blocked, Skipped, NotRun, and N/A. Verify the tested implementation bytes match the target commit.

After checks, write only the owned report, run `git diff --check`, and verify `git status --porcelain` contains only that report and generated ignored artifacts. Before completion, check the orchestration inbox and send exactly one `worker_done` for the assigned Task and Dispatch with explicit outcome, report path, and files modified. Then idle.
