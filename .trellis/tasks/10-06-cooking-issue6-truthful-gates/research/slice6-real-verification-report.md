# Slice6 genuine Cooking verification — Passed within this dispatch

Task `task_31fd1c2368df` / Dispatch `ctx_4c94250ae711`; exact implementation **68a880f2a31c7269da634b2f152362bccb5857ab**. Both exact genuine commands completed serially with canonical CLI0 and parent native0; every required leaf is Passed, no missing coverage or skipped/unexecuted tests. This records actual positive verification only; dot final acceptance, serialized association counterexamples, integration checkout verification, merge/close/archive remain **NotRun**.

## Commands and actual outcomes

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI
```

No NoBuild/NoRestore/StepName/synthetic context or new timeout argument was supplied. All work executed from this managed checkout. stdout/stderr were drained directly into separate files; no combined chronology or ErrorRecord formatting is claimed.

| Gate | Actual UTC start → end | CLI / parent native | Required/completed/missing | Actual summary |
|---|---|---:|---:|---|
| cooking-et-level-runtime | 2026-10-05T22:12:27.5409680+00:00 → 2026-10-05T22:44:09.4136978+00:00 | 0 / 0 | 4/4/0 | [86324cd6-22d9-45ea-9762-e2c301e59734](../../../../local/Logs/test-gates/86324cd6-22d9-45ea-9762-e2c301e59734/gate-summary.json) |
| cooking-kitchen-loop | 2026-10-05T22:45:26.6855912+00:00 → 2026-10-05T23:26:32.9710429+00:00 | 0 / 0 | 5/5/0 | [e1f37682-a128-4be4-9acb-586e5cc4ce8d](../../../../local/Logs/test-gates/e1f37682-a128-4be4-9acb-586e5cc4ce8d/gate-summary.json) |

| Gate / required leaf | Canonical / native | Actual tests (executed/passed/failed/skipped/notExecuted) |
|---|---|---:|
| cooking-et-level-runtime / 1 ET relation analyzer build | Passed / 0 | N/A; tests=null |
| cooking-et-level-runtime / 2 Cooking ET runtime build with relation analyzer | Passed / 0 | N/A; tests=null |
| cooking-et-level-runtime / 3 Cooking domain and Level lifecycle tests | Passed / 0 | 867/867/0/0/0 |
| cooking-et-level-runtime / 4 Cooking ET Level host and runtime tests | Passed / 0 | 328/328/0/0/0 |
| cooking-kitchen-loop / 1 Cooking domain build | Passed / 0 | N/A; tests=null |
| cooking-kitchen-loop / 2 Cooking ET runtime build with relation analyzer | Passed / 0 | N/A; tests=null |
| cooking-kitchen-loop / 3 Kitchen-loop focused contract tests | Passed / 0 | 644/644/0/0/0 |
| cooking-kitchen-loop / 4 Cooking domain and Level lifecycle regression | Passed / 0 | 867/867/0/0/0 |
| cooking-kitchen-loop / 5 Cooking ET Level host and runtime regression | Passed / 0 | 328/328/0/0/0 |

All 11 result IDs, both run UUIDs and all five TRX run UUIDs are unique. The focused filter is exactly `Gate=CookingKitchenLoop`; full-project commands have no filter. Builds have tests=null. Independent checks rederived bindings from the declared Passed leaves, parsed real UnitTestResult/UnitTest/Execution/TestMethod identities and nonoverlapping counters, matched current assembly/filter/run/SHA/times and confirmed genuine nonzero execution. Every actual test entry is Passed. No gameplay assertion or runner/validator failure occurred in these two runs; this does not rewrite historical Failed runs.

## Source, actual inputs and tools

HEAD remained68a880f2; implementation source is clean. The overall worktree is dirty with main-owned task/evidence records: 345 entries before, 457 after gates; this new owned report is additional. Full UTF-8 details/exclusions and all eight working/Git-blob hashes are in [before freeze](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/preflight.json), [between gates](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/before-second.json) and [after freeze](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/after-gates.json). Actual evaluated/compiler ledgers contain no .trellis task input. No source was edited or committed.

All eight pinned files match before/after. Five working files use CRLF while committed blobs use LF; raw hashes differ as pinned, and only CRLF→LF normalization gives byte equality. Both raw identities/lengths are retained; no raw-equality claim. The unchanged195 suite is not repeated: its historical proof belongs to dirty79 parent+owned edits, never relabeled clean68. Main independent15 controls are separate evidence. [seven/eight working fingerprints](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/working-fingerprints.json) records `88a9bee7851b98d9a533f91c8b05da14f7666c283b8854777071d44ed17806fa` / `08482c7e776cc5bd02effc6db3f74bd402563a1647d9539d65c9d21d67378cec`.

Tools: Windows PowerShell5.1.26100.9444 / CLR4.0.30319.42000, SDK10.0.300 / MSBuild18.6.3+caa81fa49 / host10.0.8 / win-x64, Python3.14 existing standard library. Actual executable hashes, --version/--info/--list-sdks output and all native argv/times/exits are retained in leaf ledgers and audit receipts. No dependency/toolchain upgrade, Unity/example/service/benchmark execution or remote write occurred.

There are 13 distinct actual project/TFM pairs. SDK evaluation/preprocess and CoreCompile trace inputs retain referenced projects, existing Compile Include files (including Unity/Packages), actual imports, packages/generators/analyzers and SDK/NuGet task dependency inputs; no historical extension whitelist was used. Restore argv binds Configuration only; every referenced project preserves its declared TFM, including RelationAnalyzer netstandard2.0. Current and immutable archived assets/settings/project/config identities match. Compiler traces, assemblies/dependency/resource outputs and loaded binary proofs have current path/length/hash identity and owned immutable archives.

- cooking-et-level-runtime: 328 owned .NET invocations; 14453 hashed run files; root fingerprint `1bd0317a0b2c086a0b2cb1c4db22e8d807f0d484af334f102dff426925c9b7f9` equals after. [full independent audit](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cooking-et-level-runtime-independent-audit.json) and [all raw/stage/artifact file hashes](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cooking-et-level-runtime-run-files-manifest.json).
- cooking-kitchen-loop: 496 owned .NET invocations; 19344 hashed run files; root fingerprint `1bd0317a0b2c086a0b2cb1c4db22e8d807f0d484af334f102dff426925c9b7f9` equals after. [full independent audit](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cooking-kitchen-loop-independent-audit.json) and [all raw/stage/artifact file hashes](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cooking-kitchen-loop-run-files-manifest.json).

Both runs have 824 owned .NET invocations total, all native0; actual producer stages are 9 restore / 9 build / 5 test. Stage argv/SDK/host/times/native exits, source pre/post, full actual project closure and binary/artifact identities are linked above, including separate parent [gate1 native](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cooking-et-level-runtime-native.json) / [gate2 native](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cooking-kitchen-loop-native.json) receipts and raw streams.

Effective config: `C:\Users\Administrator\AppData\Roaming\NuGet\NuGet.Config`, 210 bytes, SHA256 `3bdb050540ecdd6fe717cbb9028c0ff9e2af5e1544afa71b6bade80140c7a236`, unchanged before/after. Every actual assets.project.restore.configFilePaths matches SDK settings and typed EffectiveNuGetConfigHashOnly restore/build input proofs; configuration contents were never displayed or archived (artifact=null; run manifests independently show no matching content archive).

## Dot clarification and finite observation

Complete [dot clarification](../../../../.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/cooking-dot-flow/evidence/dot-clarification-reply-raw.txt) binds request AK-I6-COOK-CLARIFY-20261006-02 / SHA68 and supersedes only main-added direct test.inputs duplication. The original [strict extra-check observation](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/test-config-ledger-gap-first.json) remains unchanged: test inputs contain loaded binaries, no duplicate config entry. Under A, actual sourceBeforeRestore/before/after config checks, typed restore/build proof, test --no-build --no-restore, build.outputs→loadedBefore→test.inputs path/length/hash correspondence, unchanged loaded binaries, current TRX and completed post-test source/config checks all independently hold for every test leaf. This is actual-data association proof; serialized replacement/deletion negatives required by dot are deferred to main after settlement and remain NotRun here.

Gate1 ET native producer77060 completed naturally at22:33:35.8573901Z before clarification T0=22:36:54.586390Z and deadline22:41:54.586390Z. Its native0/exit confirmation/TRX328/328 were already recorded; no test writer was live when the amendment was read. Later CPU belonged to PowerShell aggregate/JSON validation, not ET nontermination. [natural-completion accounting](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/gate1-natural-completion-amendment-accounting.json) preserves the distinction and stale premise; no cancellation or fake timeout/terminal/TRX was made.

Gate2 whole observation clock was fixed to actual runner creation22:45:26.690298Z, deadline23:30:26.690298Z; native ET creation23:00:50.334774Z, deadline23:15:50.334774Z. [context](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/gate2-observation-context.json) and every numbered 30-second snapshot preserve PID/creation/executable/command/parent scope, CPU/file state and fixed clocks. Short focused/full Cooking invocations completed between samples; their actual native receipts supply exact starts/ends rather than invented live observations. [actual native test clocks](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/gate2-completed-native-test-clock-proof.json) records 7.355557 / 17.810871 / 686.048881 seconds, all below15min. Whole parent elapsed 2466.285 seconds is below45min. OS creation is the external clock basis; invocation receipts separately retain the pre-launch timestamps. No observation boundary or cancellation occurred; [natural observer completion](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/gate2-observation-final.json) retains final state.

Before both launches there were zero .NET writers; post-gate process ownership/exit evidence is retained. No process was killed or cleaned up. Independent audits use existing Python standard library and Windows NLS ordering calibrated to PowerShell zh-CN; the original lexical calibration mismatch and pre-calibration script are retained [calibration receipt](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/fingerprint-sort-calibration-native.json), not presented as a source failure.

## Preservation, scope and remaining acceptance

All6241 original slice2/slice4 Failed-run/report files remain byte-identical before/between/after; no historical failure is promoted. The source freeze receipts pin this preservation. Both provisional summaries are retained separately and were never accepted before parent exit. Both current actual commands and audit native receipts were preserved without retry or source repair.

One final assigned-root scaffold assertion/native1 assumed null instead of the current typed PowerShell empty-string parent. The failed script, original attempt accounting and direct native failure replay remain in [failure accounting](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/assigned-scope-scaffold-initial-failure-accounting.json); the corrected read-only inspection matches the actual assigned plan. No genuine command was rerun and no source was changed.

A first evidence-manifest finalizer exited native1 when it tried to hash its own still-open stderr capture. [Failed finalizer receipt/raw](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/finalize-receipt-native.json) and original script are preserved; the second finalizer excludes its current capture files and pins all completed evidence. This metadata-scaffold failure does not change either genuine gate result.

Cross-gate final inspection found zero mismatches across 71,438 current input/binary entries in 91 ledger groups; gate2 did not invalidate gate1 identities. [Cross-gate proof](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cross-gate-current-input-binary-stability.json) and [native receipt](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/cross-gate-stability-native.json) preserve actual checks. [Assigned-node inspection](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/assigned-node-path-and-coverage-inspection.json) verifies both complete roots and all nine paths/declarations/bindings. [Final receipt](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880/final-receipt.json) pins this report, source freeze and owned evidence hashes.

The sole nonignored worker write is this new report; all other worker writes are fresh ignored evidence under [owned evidence root](../../../../local/Artifacts/issue6-cooking-slice6-verification-c66e1880). Main task/flow changes are preserved and excluded from compilation; no source commit, push, merge, close, archive, resource release or unrelated work. Actual native/gameplay results within this dispatch are Passed. Dot final technical acceptance, serialized binary-association negatives, actual integration checkout bytes/gates and Issue delivery remain NotRun; main owns those exits.
