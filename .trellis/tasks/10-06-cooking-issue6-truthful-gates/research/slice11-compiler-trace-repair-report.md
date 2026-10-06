# Slice 11 compiler trace repair report

Date: 2026-10-07

## Authority and scope

- Dot request: `AK-I6-COOK-COMPILER-20261007-01`.
- Reviewed packet: `c84cc3687351a975c060335f907de46231dfced5`.
- Gate implementation source: `665068ae71100328cf6344b8d6c2a6fa74e968bd`.
- Worker checkout HEAD: `df9d83a4923ebbb996d636d37ab8f6ff5f48e07a`.
- The checkout was dirty for this Slice 11 implementation. The final scope audit is limited to the five assigned tool/test files, the new SDK event reader, and this report. Ignored `local/Artifacts` evidence is not a source input.
- The bidirectional-completeness follow-up changed only the helper, harness, and this report. It read but did not modify the two fixtures or SDK event reader retained from the first repair.
- Cooking only. No Shooter, MOBA, Orleans, Unity runtime/editor, business/test project, dependency, SDK, package, gate declaration, or test-scope source was changed.

## Feasibility checkpoint

The installed .NET SDK exposes the required native build evidence without a package or tool install. A temporary `net10.0` reader referenced only SDK-shipped `Microsoft.Build.dll` and `Microsoft.Build.Framework.dll`, then replayed a native binary log with `Microsoft.Build.Logging.BinaryLogReplayEventSource`.

Observed environment and structured events:

- .NET SDK: `10.0.300`.
- MSBuild: `18.6.3.23102`.
- Initial replay: 1,256 structured events.
- `ProjectStarted` / `ProjectFinished`: `15 / 15`.
- CoreCompile `TargetStarted` / `TargetFinished`: `2 / 2`.
- Csc `TaskStarted` / `TaskFinished`: `2 / 2`.
- Correlation fields: SubmissionId, NodeId, ProjectInstanceId, ProjectContextId, TargetId, TaskId, and EvaluationId.
- Csc inputs: `TaskParameterEventArgs` with `Kind=TaskInput`.
- No package download, tool install, SDK upgrade, dependency change, or project change was required.

The generated production reader project sets empty restore sources and directly references the two assemblies under the evaluated SDK root. Product binlogs use `ProjectImports=None`, so imported project contents are not embedded.

## Implementation result

- Each build receives a GUID invocation ID and a new `<result>/bi/<invocation>` directory.
- Each target execution creates a GUID capture ID, writes one private pending record, hashes and closes it, then atomically moves it to one completion record. There is no shared append.
- Completion records bind run, result, invocation, capture, normalized project path, TFM, configuration, platform, RID, target, and normalized input path/length/SHA-256 identities.
- Raw binlog events independently enumerate project instances, CoreCompile outcomes, Csc executions, task inputs, and capture-message contexts. Aggregation is bidirectional: every CoreCompile requires exactly one matching capture message/completion, every successful finished CoreCompile requires exactly one successful Csc, every skipped CoreCompile requires zero Csc events, and every Csc requires exactly one CoreCompile and capture. Project and project-instance context mismatches, missing, extra, and ambiguous relationships fail closed.
- Separate instance manifests are retained for repeated projects and inputs. Deduplication occurs only after instance coverage succeeds.
- A skipped CoreCompile event plus preserved output identity is recorded as `IncrementalSkip`; the reader coalesces the SDK's skipped-plus-finished event pair into one skip fact.
- Executed and Reused producer receipts pin the complete compiler evidence source. Reuse copies and rebinds the raw binlog, reader, target, captures, event manifest, aggregate receipt, and input archives.
- Product build command, exit, and times remain the terminal native identity even if the reader or later evidence validation fails. Unknown writer ownership is Blocked; evidence rejection after native exit 0 is Failed with native exit 0.
- Evidence path containment rejects reparse traversal, partial or locked files, stale timestamps, duplicate IDs, filename/ID substitution, malformed records, extra files/directories, identity replacement, and conflicting normalized input identities.

## Commands and receipts

Focused real/raw controls:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11focused04 -CompilerTraceOnly
```

Exit `0`; `28` executed, `28` passed, `0` failed. This focused run preceded the final protocol-only native-zero control and terminal-binding audit fix; the complete run below covers the final source.

Complete isolated suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11full02
```

Exit `0`; `284` executed, `284` passed, `0` failed; console-width coverage Passed; `fullControlSuiteAccepted=true`; `realDotNet=Executed`. Receipt:

- Path: `local/Artifacts/issue6-cooking-slice1-s11full02/controls.json`
- SHA-256: `375eddf6876ad9af519424122e8ab80f0781be265ce54288ed40ec835c2f1440`
- Accounting: original `249` plus `35` added controls.
- Original-name comparison: all `249` original names present, no missing names, no duplicate names.
- Receipt source before/after hashes and fingerprints are identical.

### Bidirectional completeness review follow-up

The review first added the real incremental bypass control without changing the aggregator:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11review-red01 -CompilerTraceOnly
```

Exit `1`; `29` executed, `28` passed, `1` failed. The one failure was `incremental-real-corecompile-bypassing-capture-is-rejected`: a valid seed build produced four outputs, then the real incremental MSBuild invocation exited `0` with four skipped CoreCompile instances, zero Csc executions, and only two capture messages/completions. The old aggregator returned success (`reason=null`). Receipt SHA-256: `31f5aaf09e9e530ed7b2083476d1b98ef155ec4099989ea3673649f83140a52f`.

After the bidirectional fix, the focused suite passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11review-focused01 -CompilerTraceOnly
```

Exit `0`; `33` executed, `33` passed, `0` failed. The same native incremental invocation shape remained exit `0`, four skipped CoreCompile, zero Csc, and two captures; aggregation rejected it with `CoreCompile event missing exact capture coverage.` Receipt SHA-256: `bddb6f2703520119d9d4d7294979aad1cbae9bb057e13be0f1361322971ea271`; source-before/source-after fingerprints were identical.

The final complete isolated suite passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11review-full02
```

Exit `0`; `289` executed, `289` passed, `0` failed; `fullControlSuiteAccepted=true`, `contractControlsAccepted=true`, `realDotNet=Executed`, and console-width coverage Passed. Receipt SHA-256: `53056b6c0861f81f2bc21d4b33253ca55de23728d0bfda91066a90a1d7343690`. Exact accounting is original `249` plus `40` added controls. Comparison against preserved `s11compat04` found all `249` original names, and comparison against the earlier `284`-control pass found every earlier name; neither comparison found duplicates. Source SHA stayed `df9d83a4923ebbb996d636d37ab8f6ff5f48e07a`, the checkout remained truthfully dirty, and source-before/source-after fingerprints were identical at `e519a1cd75a9d99e6d0fbf182d9525cd016096877ac4dfe3aa22e2cb8edb8a5e`.

The unchanged `current-build-test-JSON-roundtrip` control now requires the literal cardinality condition `$uniqueInstanceOpaque.Count -ne $instances.Count` to be false: the nonzero unique `Generated.opaque` identity count derived from `compilerEvidence.instances` must equal `compilerEvidence.instances.Count`, and must also equal the aggregate `compilerInputs` `Generated.opaque` count. Its original name and successful expectation are unchanged; the existing path/length/SHA-256 bijection still requires exactly one matching aggregate identity per instance identity. The observed fixture counts are Tests leaf `2 instances / 2 instance opaque / 2 aggregate opaque` and Build leaf `1 / 1 / 1`.

### Exact cardinality final-review receipts

The prior red and green evidence above is retained. After adding the literal instance-cardinality condition, the focused compiler-trace run passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11final-focused01 -CompilerTraceOnly
```

Exit `0`; `33` executed, `33` passed, `0` failed. Receipt SHA-256: `f00f6927609b236a517cdc69b921101cff22625a475358a5dd7c3ebdbcc1c5f4`. `realDotNet=Executed`; source-before/source-after SHA and fingerprints were identical.

The complete isolated suite for the same source passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s11final-full01
```

Exit `0`; `289` executed, `289` passed, `0` failed; `fullControlSuiteAccepted=true`, `contractControlsAccepted=true`, `realDotNet=Executed`, and console-width coverage `Passed`. Receipt SHA-256: `e1e2ebf937b834cf1c451575c5546a2d59efdc51d9d938810d727b937e27908b`. Source SHA before/after was `df9d83a4923ebbb996d636d37ab8f6ff5f48e07a`; source fingerprints before/after were both `6272e859747a967bb406dbd47caca868be027fb16ccf3491d2264226adcd2537`. Comparison with preserved `s11compat04` found all `249` original names, `0` missing, `40` added, and `0` duplicate names. The genuine Cooking gates `cooking-et-level-runtime` and `cooking-kitchen-loop` remained `NotRun` (preflight only, no workload launch).

The complete suite inventory-preflighted repository gate declarations but did not launch repository workloads. Both genuine Cooking gates were deliberately selected with a nonexistent step and remained uninvoked.

## Added controls

| Control | Expected | Actual |
| --- | --- | --- |
| `real-parallel-diamond-global-properties-generated-inputs` | `true` | `true` |
| `real-parallel-barrier-overlap` | `true` | `true` |
| `raw-binlog-reader-project-corecompile-csc-correlation` | `true` | `true` |
| `legitimate-repeat-project-instances-preserved` | `true` | `true` |
| `legitimate-repeat-inputs-deduplicated-after-instance-coverage` | `true` | `true` |
| `deterministic-order-and-normalized-full-paths` | `true` | `true` |
| `historical-shared-append-contention-reproduced` | `true` | `true` |
| `second-real-csc-bypassing-capture-is-rejected` | `true` | `true` |
| `incremental-corecompile-skip-with-valid-output-identity` | `true` | `true` |
| `unique-owned-build-invocation-directories` | `true` | `true` |
| `stale-completion-record-rejected` | `true` | `true` |
| `duplicate-capture-id-record-rejected` | `true` | `true` |
| `extra-completion-file-rejected` | `true` | `true` |
| `extra-completion-directory-rejected` | `true` | `true` |
| `partial-pending-record-rejected` | `true` | `true` |
| `malformed-completion-record-rejected` | `true` | `true` |
| `held-completion-file-fails-bounded` | `true` | `true` |
| `project-identity-replacement-rejected` | `true` | `true` |
| `tfm-identity-replacement-rejected` | `true` | `true` |
| `run-identity-replacement-rejected` | `true` | `true` |
| `result-identity-replacement-rejected` | `true` | `true` |
| `invocation-identity-replacement-rejected` | `true` | `true` |
| `one-compiler-input-removed-rejected` | `true` | `true` |
| `one-compiler-input-substituted-rejected` | `true` | `true` |
| `conflicting-normalized-input-content-rejected` | `true` | `true` |
| `input-reparse-escape-rejected` | `true` | `true` |
| `malformed-raw-event-log-rejected` | `true` | `true` |
| `missing-build-end-event-rejected` | `true` | `true` |
| `native-zero-compiler-evidence-failure-remains-failed-zero` | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` |
| `producer-Executed-compiler-source-omission` | `false` | `false` |
| `producer-Reused-compiler-source-omission` | `false` | `false` |
| `producer-Reused-compiler-source-substitution` | `false` | `false` |
| `producer-Executed-compiler-source-substitution` | `false` | `false` |
| `compiler-entire-instance-and-candidate-entry-removed-rehashed` | `false` | `false` |
| `compiler-capture-trace-identity-replacement-rejected` | `false` | `false` |
| `incremental-real-corecompile-bypassing-capture-is-rejected` | `true` | `true` |
| `finished-corecompile-without-csc-rejected` | `true` | `true` |
| `skipped-corecompile-with-csc-rejected` | `true` | `true` |
| `corecompile-project-mismatch-rejected` | `true` | `true` |
| `csc-context-mismatch-rejected` | `true` | `true` |

## Real concurrency evidence

The positive real fixture built Left and Right concurrently, with a Shared diamond dependency evaluated under two different global-property sets. Native exit was `0`; the event manifest and completion set contained four distinct instances: Left, Right, Shared(Left), and Shared(Right). Shared retained two distinct property fingerprints, all four instances retained generated inputs, and the synchronization barrier observed overlapping target execution.

The historical control forced the prior shared-append design to contend on one locked file. Its native exit was `1` with exit ownership confirmed, and the failure remains an expected Passed control rather than being laundered into a successful build. The replacement per-capture publication completed successfully under the same parallel barrier.

The bypass control ran a real native build with four Csc executions but only two capture messages/completions. Native exit remained `0`; aggregation rejected it with `Csc execution missing independent completion coverage.`

The second unchanged real build produced four CoreCompile `Skipped` events, zero Csc executions, four `IncrementalSkip` instance records, and unchanged output path/length/SHA-256 identities. A held completion file failed in 568 ms, within the bounded interval.

The review's second real incremental build deliberately disabled the capture hook for the Right branch after the valid seed outputs existed. Native MSBuild still exited `0`; its four skipped CoreCompile events exceeded the two capture messages/completions, and aggregation rejected the missing reverse coverage. Separate relationship controls also rejected finished-without-Csc, skipped-with-Csc, CoreCompile project substitution, and Csc project-instance context substitution.

## Status and limitations

- Full isolated contract controls: Passed (`289 / 289`, exact `249 + 40`; no missing or duplicate original names).
- Real parallel/concurrency fixture: Passed.
- Historical shared-append failure reproduction: Passed as an expected failure control (native exit `1`).
- Genuine `cooking-et-level-runtime`: NotRun.
- Genuine `cooking-kitchen-loop`: NotRun.
- Unity: N/A for this source-worker assignment.
- Package/network restore: no new package or tool was requested or downloaded; the reader uses installed SDK assemblies.
- Platform coverage in this worker is Windows PowerShell 5.1, .NET SDK 10.0.300, and MSBuild 18.6.3.23102. The coordinator's distinct verifier owns serial execution of the final controls and both genuine Cooking gates.
