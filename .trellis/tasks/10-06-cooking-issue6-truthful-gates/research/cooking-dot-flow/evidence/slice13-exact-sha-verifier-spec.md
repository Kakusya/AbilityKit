# Slice 13 corrected exact-SHA verifier

Target commit: `2b258b6ea2a40a9ac354b51847c749722564e8ba`.

This is a fresh Cooking-only verification attempt after the prior coordinator-specified artifact path was rejected before any control ran. Preserve that prior exit-1 result; do not overwrite or reinterpret it. You are not alone in the repository. Do not modify or revert implementation, gate configuration, Cooking business code, tests, dependencies, SDK settings, Unity, Shooter, MOBA, Orleans, or any other example. Do not commit, push, merge, write GitHub, contact dot, create another worker, or clean resources.

Ownership is limited to one new report:

- `.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/slice13-exact-sha-verification-report.md`

Before running anything, require `git rev-parse HEAD` to equal the target commit and require `git status --porcelain` to be empty. If either check fails, stop and report failure without changing source.

Run these checks serially, never concurrently:

1. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s13-verifier-controls`
2. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI`
3. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI`

The first artifact path is intentionally a new path under the harness-required `local/Artifacts/issue6-cooking-slice1-` prefix. Require it not to exist before launch. Preserve every original failure and stop after any required check fails. Do not rerun a failed check to conceal it and do not repair product or tooling.

The controls must execute exactly 289 cases with 289 Passed, zero Failed, `fullControlSuiteAccepted=true`, `contractControlsAccepted=true`, `realDotNet=Executed`, console-width Passed, all original 249 names present, no duplicate names, and source-before/source-after SHA and fingerprints equal. The two genuine gates must each finish Passed/CLI 0 with actual nonzero TRX execution and complete required leaves; record actual counts rather than historical values.

For each command, record exact argv, start/end UTC, process exit, receipt or summary path and SHA-256, run/result IDs, source SHA/dirty state, SDK/MSBuild/PowerShell versions, declared and actual coverage, TRX counts, build/test binary identities, and limitations. Explicitly distinguish Passed, Failed, Blocked, Skipped, NotRun, and N/A.

After checks, write the owned report, run `git diff --check`, and verify `git status --porcelain` contains only the owned report. Before completion, check the orchestration inbox and send exactly one `worker_done` for the assigned Task and Dispatch with explicit outcome, report path, and files modified.
