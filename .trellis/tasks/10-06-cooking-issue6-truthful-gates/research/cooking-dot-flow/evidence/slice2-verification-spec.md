# Separate authoritative Cooking verification dispatch

Continue only after main has checked and settled slice1 Task/Dispatch and filled the full implementation SHA. The accepted dot plan is normative: read the complete `dot-plan-reply-raw.txt`, task PRD/design/manifests and AGENTS. This dispatch owns only `research/slice2-verification-report.md` and fresh ignored `local/Artifacts/` evidence. It owns no implementation source. You are not alone; preserve main-owned flow/evidence and all other changes. No subworkers, remote writes, push, merge, close, archive, cleanup, business fixes, dependency/toolchain upgrades or Unity/example/service/benchmark execution.

Before running: verify exact supplied implementation SHA, full delivered-file hashes and no dirty implementation source; main-owned task evidence dirt is disclosed separately and must not become compilation input. Record tool versions, actual argv, source SHA/dirty/exclusions and actual evaluated project closure. Coordinate exclusive .NET execution: this dispatch is the sole authorized .NET check writer; do not run checks in other trees. Stop if another writer is discovered rather than killing it.

Execute serially from this managed worktree, using the committed runner without NoBuild/NoRestore/StepName or synthetic context:

1. powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI
2. powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI

Both commands must be actually attempted unless an unsafe active writer/environment prevents the next launch; a failure is preserved and does not authorize repair. Record complete stdout/stderr, native/CLI exit, unique run/result identity, summary and all stage/artifact paths/hashes. Independently inspect every required build/test leaf, canonical status, coverage bindings, genuine nonzero TRX counts, focused filter and full projects, source/binary/tool identities and pre/post stability. No skipped/missing/zero tests count as Passed. Build leaves have tests=null. Produce a compact report with per-command/leaf Passed/Failed/Blocked/Skipped/NotRun, counts and linked raw evidence; explicitly distinguish actual gameplay failure from runner/validator failure without editing either. No historical counts or synthetic acceptance.

Upon completion or first unresolved blocker, report exact SHA, dirty disclosure, all actual commands/exits/counts/evidence and limitation. Emit exactly one worker_done with both current Task and Dispatch IDs, explicit outcome and report path, then idle. No source commit or followup work without a new authoritative dispatch. Main may independently read evidence while you run but will not mutate source or run concurrent tests.


Main freeze: implementation SHA **a0824b6dc4dbea3c4fc0722c60cf7d257c8fa261**. All seven tools/source hashes are pinned in `window-main-scope-audit.json`; compare them before and after. Matching previous worker_done msg_e57b5975b93e and dispatch ctx_f42f62847f04 is settled; this terminal is explicitly approved for reuse for this new bounded dispatch. Latest Issue6 body read back at issue6-before-verification.json remains OPEN and scope unchanged.

Before the two genuine gates, rerun the entire isolated harness on this committed source with a fresh ignored ArtifactRoot using `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot <fresh local/Artifacts path>`. Record native exit, all164 controls, actual window width observations, source SHA/dirty/input fingerprint before/after. Preserve failure and still attempt both genuine commands unless unsafe. No source edits or commit is authorized.

During genuine verification, independently compare each actual project.assets.json project.restore.configFilePaths with restore/build input ledgers (existence/path/hash; do not copy or disclose credential-bearing config contents). This is read-only verification of the dot-required evaluated config closure, not authority to repair or upgrade. Note any evidence gap and retain native outputs.
