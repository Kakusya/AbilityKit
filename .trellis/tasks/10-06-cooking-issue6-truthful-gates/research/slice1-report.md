# Issue6 Cooking truthful gates — slice1 implementation receipt

Dispatch: `task_c2b5e42b2e23` / `ctx_c1d5fbd7ef56`, worker terminal `term_a1a61c7c-aef6-4cd0-8c46-27a90632617b`, coordinator run `run_5ba664a5f016`. Date: 2026-10-06 (Asia/Shanghai). Authority: Owner invocation and dot `accept-plan`, request `AK-I6-COOK-PLAN-20261006-01`, reviewed plan SHA `cf41218ab468a40457f6a15bbe0fc2556158b983`; [complete normative reply](cooking-dot-flow/evidence/dot-plan-reply-raw.txt) takes precedence over this implementation report.

This receipt covers the shared runner/helper, the two existing Cooking declarations, isolated controls and their documentation. It does **not** accept Issue6 or either real Cooking gate. The source used by the controls is an explicitly recorded dirty implementation over `a6f00cf54579828456deccd7afbb961d8eab6752`, whose preceding implementation base is `619883e8a18027a1c9f7419f84023eb60c5f9466`. The eventual implementation checkpoint is the commit containing this report; the exact SHA is returned through the dispatch result and an ignored post-commit identity receipt. Dirty control evidence is not represented as verification of that later commit.

## File scope and necessity

| Owned file | Cooking consumer / necessary change | Slice1 acceptance |
| --- | --- | --- |
| `tools/run_test_gate.ps1` | Both Cooking gates: allocate complete trees before execution, preflight selected contracts, perform explicit stages, bind selection/coverage, serialize and validate, preserve truthful CLI | Actual runner positive/negative controls and all repository preflight entries; real Cooking work remains NotRun |
| `tools/test-gate-result-contract.ps1` | Runner and its controls: one normalized validator, derived coverage, artifact/provenance/TRX checks and native capture | JSON roundtrip, forged identity/summary/binding, source/binary/reuse/path/native controls; dot-sourcing does not launch work |
| `tools/test-gates.json` | Only `cooking-et-level-runtime` and `cooking-kitchen-loop`: declare concrete required coverage and TFM on their existing four/five steps | Parsed comparison against base preserves every other object and defaultGate; original order/projects/full tests/focused filter retained |
| `tools/tests/test-gate-result-contract.tests.ps1` | Shared contract controls for the two Cooking consumers | Actual child runner processes, sole validator mutations and preserved per-case receipts; no genuine dotnet workload |
| `tools/tests/fixtures/gate-result-model.ps1` | Creates unique TEMP synthetic project/reference/package/import/SDK data | Explicit example context, no real project writes, source closure includes extensionless/opaque generated/package inputs |
| `tools/tests/fixtures/gate-result-fake-dotnet.ps1` | Known fake executor for stage/evaluation/TRX fault injection | Real subprocess argv and outputs retained; marked synthetic; production refuses its evidence |
| `tools/tests/fixtures/gate-result-native-probe.ps1` | Direct stream/long-line/native-exit probe | Exact 8192-character payloads and requested/applied/observed RawUI buffer width sidecars; 40/80 coverage Blocked, 180 observed, independent stdout/stderr capture |
| `Docs/AbilityKit测试门禁与批量回归规范.md` | Shared result/Cooking evidence/reuse/compatibility contract | Documents current semantics and all legacy entries without adapting forbidden consumers |
| This report | Coordinator and independent reviewer | Scope, source freeze, raw receipt routes, original failures and remaining exits |

No gameplay, C# project, asmdef, package, SDK, ET implementation, Unity mirror/Editor, exporter, benchmark, protocol or dedicated example gate/tool/test was edited. Main-owned flow/task/remote records were preserved and excluded from staging. No worker or native subagent was spawned; no resources, old branches, artifacts, processes or worktrees were cleaned up.

## Implemented contract

Statuses are case-exact `Passed/0`, `Failed/1`, `Blocked/2`, `Skipped/3`, `NotRun/4`; native exits are separate. A UUID identifies each run and each node, with assigned parent/path and fresh owned output directories. All configured children exist, including later/unselected NotRun nodes with null execution metadata. Coverage is rederived from validated Passed leaves with unique resolvable nonempty bindings; aggregates cannot submit their own completion facts. Empty undeclared/uninvoked legacy nested gates cannot vacuously pass. Optional nonexecution is restricted to a declared MissingTool policy; actual failure/corruption propagates.

Production adapts only build/test/gate. A selected legacy definition missing a contract or containing an unsupported producer is Blocked before launching anything. Unknown/broken selected configuration is Failed. Unadapted unrelated entries do not block the two adapted Cooking trees. Missing/ambiguous selectors fail; successful focused execution retains parent NotRun/4 and `fullGateAccepted=false`.

Every test performs restore → build → test `--no-build --no-restore`; build steps perform restore/build. Project/Configuration/TFM/filter argv are bound to the assigned identity and the matching native stage receipt. NoBuild preserves explicit configured build steps, and only reuses test preparation. NoRestore needs restore proof alone, even after a failed original build. NoBuild needs restore and build. Reuse is indexed by explicit `-ReuseManifestPath`, never a latest-directory guess. Reused stage command/time/native exit are null; origin receipts and copied archives identify the prior producer. Missing proof is Blocked, corrupted or mismatched proof Failed.

Input identity uses actual evaluated project references/Compile/imports/items, package assets and resolved package files, SDK/MSBuild/Roslyn tools, and compiler-time traced inputs. The generated owned compiler target uses existing MSBuild GetFileHash before CoreCompile; it adds no repository project/package dependency. Source SHA/dirty details and hashes are captured before/after; compilation can include existing Unity/Packages sources without modifying or launching Unity. Stage inputs, assets, outputs, selected test assembly and dependencies are independently archived with run/result/path/length/hash. Before/after checks reject replacement. `loadedBefore/loadedAfter` describes the archived binary set supplied to the test host and verified against TRX assembly identities, not a general OS module-load trace.

TRX requires real entries, matching definitions/execution IDs/test names/assembly, current times, summary and nonoverlapping normalized counters. All skipped/zero/failed/required-unexecuted evidence fails, including native0 failures. VSTest's ordinary `completed=0` is checked as its own outcome counter, not equated with executed. Filter is bound through the actual test argv and receipt. Build leaves retain tests=null. Production rejects synthetic/example input and provenance.

Native output is copied directly from each redirected byte stream to separate files; no ErrorRecord formatting or claimed combined-stream chronology. Started cancellation/timeout kills only the owned Process instance and requires confirmed exit; subsequent nodes are NotRun. Width sidecars separately record requested RawUI.BufferSize.Width, setter/window-adjustment success, applied/observed buffer and window widths, redirection and limitations. The redirected host refuses requested buffer40/80 and reports120 even after its window was successfully shrunk; buffer180 is applied/observed180. Exact stream assertions can pass while actual width coverage remains Blocked, which keeps the whole control suite nonaccepted/CLI2. Interactive rendering is outside this evidence.

## Selective historical provenance

The old isolated tree is `Kakusya/issue6-test-gate-results` at `15e79fefd5ba1d9294491122c91df286ef94e2f8`. Its `tools/test-gate-result-contract.ps1` was read function by function (Get-GateHash line 2, fingerprint 9, Resolve-GateArtifact 45, New-GateResult 66, Set-GateTerminal 80, Invoke-GateNative 299, Get-GateProjectClosure 329 and Invoke-GateDotNet 797); no patch, commit or branch was restored/cherry-picked/merged. The current runner was reconstructed on the current approved base.

| Historical function/concept inspected | Retained or rewritten here | Removed dependency / new evidence |
| --- | --- | --- |
| New-GateResult / Set terminal / unique IDs / complete tree | Assigned UUID node model and five-state CLI; short directories reject collisions | No producer registry; repeated nested/same-second/empty/uninvoked controls |
| Resolve artifact / Get hash / fingerprint | Node-owned relative paths and immutable length/SHA-256 receipts | No extension whitelist; escape, junction, stale owner, length/hash and replaced-DLL controls |
| Native invocation/capture | ProcessStartInfo, direct BaseStream copies, owned process confirmation | No PowerShell formatted stderr; long-line widths, native7/native2, timeout/cancel controls |
| TRX counts/result validation | Actual entries+definitions and canonical summary/counters | Removed mechanical completed=executed assumption; completed0 positive/completed2 contradiction controls |
| Project closure / dotnet stages | Evaluated closure plus actual compiler trace, explicit restore/build/test and reuse proof | No historical whitelist, Unity/benchmark/exporter or example script adapters; generated opaque source/package/tool controls |
| Coverage/JSON validation | Single normalized validator for memory and JSON readback, rederived bindings/parents | Empty serialized bindings/lowercase/forged descendants/entire leaf SHA mutations |

Explicitly omitted the historical Get-GateProducerContract example-name registry, content-report/IR routing, six benchmark filename special cases, AOI/performance checks, Unity proof/execute adapters and eighteen producer migration fixtures. NoRestore's historical coupling to successful build was replaced by independent restore proof. Main's independently reported stage+native TFM/configuration mutations were reproduced red before correction, not waived by a previous aggregate pass.

## Worktree and compatibility audit

Git and Orca enumerations were reviewed, including actual AGENTS/task/research/diff in the stopped old tree. Relevant trees remained: `D:/MyWorkTree/AbilityKit` at `86d44e8f8a6ac2ca958f4c453555817da7b1f15c`, this managed tree at pre-checkpoint `a6f00cf54579828456deccd7afbb961d8eab6752`, and stopped `issue6-test-gate-results` at the SHA above. The old branch and evidence remain isolated; no edits or cleanup there. Enumerating unrelated Orca repositories did not authorize work there.

The complete 32-entry compatibility inventory is in the [shared specification](../../../../Docs/AbilityKit测试门禁与批量回归规范.md#只读兼容清单) and the full control receipt. Two Cooking entries have explicit contracts; all other 30 entries are Blocked/exit2 at preflight. defaultGate remains precheck, therefore its current execution is Blocked. The nested regression entry and its children remain uninvoked NotRun. All seven existing kinds remain listable; four unsupported kinds are refused before launch, not adapted.

Read-only production `.ps1/.yml/.yaml` searches found no other runner/summary caller in this checkout outside the runner/control suite. `tools/README.md`, the testing guide's retained historical commands, and task/design references still describe old commands/exit0 habits. External consumers are unknown, not asserted absent; old `gate-summary.steps` consumers must account for the new complete `children` and coverage contract. These consumers were not edited. Disk has no `.github/workflows`; main owns actual remote checks/workflow/protection readbacks. Configuration CI metadata or old workflow references cannot prove checks exist or passed.

## Preserved red and intermediate attempts

All paths below are relative to this checkout. They are ignored evidence, deliberately retained, and never substituted for final source verification.

| Evidence root under `local/Artifacts/issue6-cooking-slice1-` | Actual result / purpose |
| --- | --- |
| `8184fe62-25f7-45e6-90c8-ed49b06aea58` | Original base runner copied into an isolated tree; fake warning then native0 produced old Passed. `original-exit.json` contains the exact command/native0; original stdout/stderr/config/script/summary and initial-source.json retained. This is the original defect red, not an accepted gate |
| same root `/smoke1` | Failed path-length attempt before short directory allocation; retained raw/summary |
| `s02` | Failed fixture argument parsing / VoidTaskResult output attempt; retained |
| `s03` | Failed optional empty-array/null roundtrip attempt; retained |
| `s04`, `s05`, `s06`, `s07`, `s08` | Incremental isolated runner positives (s05 compiler trace/SDK; s06 reuse; s07 corrected restore argv; s08 final command/stream bindings), superseded by the full final receipt |
| `legacy1` | Actual unchanged default/precheck preflight Blocked2, no real workload |
| `f01` / `log01` | 101 isolated controls, native0; superseded by subsequently discovered defects |
| `f02` / `log02` | 146 isolated controls, native0; superseded, not final acceptance |
| `stage-red` | Four independent wrong build/test TFM and restore TFM/Configuration mutations also changed matching native receipts; wrongly accepted by f02 validator. Candidates/red-controls.json preserved; all fixed and six dimension mutations added |
| `trx-counter-red` | f02 helper archived; ordinary completed0 TRX wrongly refused. Red-control.json/raw TRX retained; corrected using official VSTest semantics |
| `aggregate-red` | f02 repository regression summary showed undeclared uninvoked nested gates vacuously Passed. Red-control.json retained; aggregate now NotRun |
| `f03` / `log03` | Failed full rerun: stricter binding exposed split restore TFM argv; 17 failed among 80 recorded controls before a failed baseline prevented later binding mutation. Incomplete receipt `fullControlSuiteAccepted=false` retained, no final source-after assertion |
| `f04` / `log04` | 154 frozen isolated controls, native0; superseded by two additional independent command-identity reds |
| `filter-command-red` | Duplicate --filter argv (matching native receipt also mutated) and a forged leaf summary command wrongly accepted by f04 validator. Both raw candidates/red-controls.json retained; unique filter, terminal summary/native bindings and six mutations added |
| `f05` / `log05` | 160 frozen controls, zero assertion failures/native0; superseded by main's width-observation requirement. Its width arguments alone are not accepted as distinct actual width coverage |
| `width1`, `width2` | Affected direct-capture probes with raw streams/native7 and explicit width sidecars. 40/80 buffer width remained120; width2 first shrinks the owned host window successfully but buffer constraint persists. Both attempts and limitations retained |
| `f06` / `log06` | Frozen full rerun: 160 assertions Passed, width coverage Blocked/actual CLI2; detailed receipt below. A later probe-only height hypothesis and affected controls are separately frozen, not retroactively attributed to f06 |
| `width3`, `width4` | Main requested preservation of actual buffer Height and dimension/position evidence. Height3000 and initial coordinates0 did not remove width40/80 rejection; width4 freezes final inputs before/after three affected controls. No speculative host adjustments followed |

The original red uses a copied original runner and a synthetic producer, without invoking the real old Unity/script producer. Corrected refusal is exercised at the new runner boundary by warning+native0 with missing build evidence (Failed1), plus unsupported legacy kind preflight Blocked2. No claim is made that standalone forbidden scripts were fixed.

## Validation and remaining exits

The frozen full command is `powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-f06`. Its parent stdout/stderr are directly captured under `local/Artifacts/issue6-cooking-slice1-log06/suite.stdout.txt` and `suite.stderr.txt`; `native.json` retains command, native exit, stage times and owned PID/exit confirmation. Every control owns `cNNN/control.json` and its raw outputs/candidate; `controls.json` includes every expected/actual/command/native exit/rawDirectory/test count and source closure. TEMP fixture paths are recorded and retained. `contractControlsAccepted` is separate from `consoleWidthCoverage`; unavailable required width coverage keeps `fullControlSuiteAccepted=false` and the suite Blocked/CLI2 instead of turning successful byte assertions into full acceptance.

Isolation and exclusions are explicit: control inputs are the seven implementation/config/control script files; ignored `local/` and unique TEMP outputs are evidence; main-owned task records/docs/report are not compilation inputs of the isolated contract suite. Actual per-leaf project evaluation and compiler inputs remain independently captured. Before/after source evidence records dirty details honestly even when coordinator records change. A subsequent evidence/report commit does not retroactively turn dirty tests into clean exact-SHA tests.

No dependencies were installed and no real dotnet build/test, Unity, services, benchmarks or physical LAN ran. Real gate test counts are unknown/NotRun; fake TRX entries are only two synthetic entries in relevant positive leaf controls. Actual distinct buffer width40/80 is an unresolved environment coverage Blocked, not a passed assertion or an implementation acceptance waiver. Slice2 must verify the exact checkpoint with both full Cooking commands, actual counts and original failures; independent main check and dot final-review remain NotRun. Whole Issue acceptance, final candidate validation, integration gates, merge, push, close and archive remain NotRun. Scope-excluded Unity/exporter/example/LAN work is N/A for this slice, while its actual execution remains NotRun.

## Frozen full receipt f06 (before final probe-only height hypothesis)

**160/160 contract/byte assertions Passed; zero assertion failures. Actual width coverage Blocked; full suite Blocked/CLI2 and fullControlSuiteAccepted=false.** This is implementation evidence with an explicit environment limitation, not whole-slice or Issue acceptance.

Receipt: `local/Artifacts/issue6-cooking-slice1-f06/controls.json`, SHA-256 `5c43bbeab510d60ae23a46ee9dc6b9db0233300bfb173ff3d2bb7a8687ddcbfe`. Parent native receipt: `local/Artifacts/issue6-cooking-slice1-log06/native.json`, SHA-256 `d46cf31695c08473551c3e5c1806feaeba980966862c623fabfd6af819688097`, actual exit `2`, started `2026-10-05T18:40:38.8893662+00:00`, ended `2026-10-05T18:47:07.2794837+00:00`, exitConfirmed=`True`.

Tools: PowerShell `5.1.26100.9444`, CLR `4.0.30319.42000`, OS `Microsoft Windows NT 10.0.26100.0`, git `2.56.0.windows.1`; actual PowerShell executable/hash is in `local/Artifacts/issue6-cooking-slice1-scope3/scope.json`. Fake SDK `10.0.300` is synthetic, not evidence of an installed real SDK.

Before/after: SHA `a6f00cf54579828456deccd7afbb961d8eab6752` / `a6f00cf54579828456deccd7afbb961d8eab6752`; dirty `True` / `True`; identical actual input fingerprint `8aeb1e611cfa8ced0551b32fc26703f51d4f316580f08c89666bc9e7842fa4bf` over 7 files. Full dirty details and absolute input manifests remain in the receipt; coordinator-only dirty changes are not hidden. The later commit is not retrospectively labeled this run source.

| Frozen input | Bytes | SHA-256 |
| --- | --- | --- |
| `tools/run_test_gate.ps1` | 15192 | `575469c4238e29cf41b5826a43748d38c9c81578793261835b6e51dc3a886e8f` |
| `tools/test-gate-result-contract.ps1` | 59990 | `647fd558fb7bb660e30a3749a8323b35961f6236bc5052eb2342c807407e167c` |
| `tools/test-gates.json` | 63749 | `85111d00c4a3a61c87f61da3e43d159da60e07d058406f7049e17495e7e520a1` |
| `tools/tests/fixtures/gate-result-fake-dotnet.ps1` | 7854 | `d662821a04386382f3ac5c9bc981d3e973e96a38d5228b9e762d1325d70dbb76` |
| `tools/tests/fixtures/gate-result-model.ps1` | 5255 | `fb7cf4c82af3c78c2f825317b9573a92abe28f0135d26d5e09f6cb8c1ffe5a35` |
| `tools/tests/fixtures/gate-result-native-probe.ps1` | 2751 | `f51dd1c05301cf57c03826a1cfe5d21bceee3230aa63f2adb63c7917d0407bdf` |
| `tools/tests/test-gate-result-contract.tests.ps1` | 33245 | `34c8d5197c94f90d5a4de551d1e8ff07e9e5f51ea31c74de5858de9ed8b4186f` |

| Automated group | Controls | Assertions |
| --- | --- | --- |
| valid | 2 | Passed 2, Failed 0 |
| original-red | 1 | Passed 1, Failed 0 |
| trx | 16 | Passed 16, Failed 0 |
| environment | 1 | Passed 1, Failed 0 |
| failure | 1 | Passed 1, Failed 0 |
| source | 2 | Passed 2, Failed 0 |
| timeout-cancel | 2 | Passed 2, Failed 0 |
| coverage | 6 | Passed 6, Failed 0 |
| selector | 2 | Passed 2, Failed 0 |
| unsupported | 5 | Passed 5, Failed 0 |
| config | 5 | Passed 5, Failed 0 |
| identity | 1 | Passed 1, Failed 0 |
| reuse | 29 | Passed 29, Failed 0 |
| validator | 49 | Passed 49, Failed 0 |
| production | 1 | Passed 1, Failed 0 |
| compatibility | 32 | Passed 32, Failed 0 |
| native-streams | 3 | Passed 3, Failed 0 |
| paths | 1 | Passed 1, Failed 0 |
| freeze | 1 | Passed 1, Failed 0 |

| Requested buffer width | Setter / applied | Observed buffer / window | Actual width coverage |
| --- | --- | --- | --- |
| 40 | False / False | 120 / 40 | Blocked |
| 80 | False / False | 120 / 80 | Blocked |
| 180 | True / True | 180 / 120 | Passed |

Each native probe retains its exact command, native7, stdout/stderr bytes, owned exit confirmation and width-evidence.json; the observed window adjustments are recorded separately and do not substitute for missing buffer width40/80. Suite stderr is empty. Scope3 parser/object checks passed (six scripts/30 unchanged non-Cooking objects/all metadata); final staged whitespace and exact commit scope receipts are recorded in the same ignored scope3 tree.

## Complete per-case receipt index

Every row is one executed control, not a real .NET test count. Exact argv, native exit, expected/actual, reason and raw paths are in the row directory/control.json and the full controls.json. control-index.json adds hashes of each case receipt and observational raw TRX entries/counters when a child produced a TRX; malformed/missing TRX is recorded as unavailable, never passed test coverage. Validator-only cases use the seed archives/candidate.json and launch no producer; their native exit is null.

| Case / group | Expected | Actual | Native exit | Raw directory |
| --- | --- | --- | --- | --- |
| current-build-test-JSON-roundtrip / valid | `{"exit":0,"status":"Passed","extra":true}` | `{"exit":0,"status":"Passed","extra":true}` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c001` |
| current-build-only-tests-null / valid | `{"exit":0,"status":"Passed","extra":true}` | `{"exit":0,"status":"Passed","extra":true}` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c002` |
| warning-native-zero-refused-for-no-build-evidence / original-red | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c003` |
| test-missing / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c004` |
| test-empty / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c005` |
| test-malformed / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c006` |
| test-zero / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c007` |
| test-no-entries / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c008` |
| test-failed-zero / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c009` |
| test-counter / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c010` |
| test-completed-counter / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c011` |
| test-lowercase / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c012` |
| test-wrong-assembly / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c013` |
| test-stale / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c014` |
| test-duplicate / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c015` |
| test-no-definition / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c016` |
| test-skipped / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c017` |
| test-replaced-dll / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c018` |
| test-native-fail / trx | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c019` |
| missing-tool-no-launch / environment | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c020` |
| required-native7-failure-later-NotRun / failure | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c021` |
| source-change-fails / source | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c022` |
| evaluated-TFM-mismatch / source | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c023` |
| timeout-owned-child-exit-confirmed / timeout-cancel | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c024` |
| started-cancellation-owned-exit-confirmed / timeout-cancel | `true` | `true` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c025` |
| nested-complete / coverage | `{"exit":0,"status":"Passed","extra":true}` | `{"exit":0,"status":"Passed","extra":true}` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c026` |
| repeated-nested-unique-node-directory-bindings / coverage | `{"exit":0,"status":"Passed","extra":true}` | `{"exit":0,"status":"Passed","extra":true}` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c027` |
| StepName-positive-parent-partial-NotRun4 / coverage | `{"exit":4,"status":"NotRun","extra":true}` | `{"exit":4,"status":"NotRun","extra":true}` | 4 | `local/Artifacts/issue6-cooking-slice1-f06/c028` |
| nested-focus-partial / coverage | `{"exit":4,"status":"NotRun","extra":true}` | `{"exit":4,"status":"NotRun","extra":true}` | 4 | `local/Artifacts/issue6-cooking-slice1-f06/c029` |
| missing-selector-zero-launches / selector | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c030` |
| ambiguous-selector-zero-launches / selector | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c031` |
| optional-declared-missing-tool-skip / coverage | `{"exit":0,"status":"Passed","extra":true}` | `{"exit":0,"status":"Passed","extra":true}` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c032` |
| optional-real-failure-not-laundered / coverage | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c033` |
| producer-powershell-script-blocked-before-launch / unsupported | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c034` |
| producer-unity-editmode-test-blocked-before-launch / unsupported | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c035` |
| producer-unity-playmode-test-blocked-before-launch / unsupported | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c036` |
| producer-unity-execute-method-blocked-before-launch / unsupported | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c037` |
| missing-explicit-coverage-before-launch / unsupported | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c038` |
| duplicate-gate-fails-before-launch / config | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c039` |
| unknown-kind-fails-before-launch / config | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c040` |
| empty-tree-fails-before-launch / config | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c041` |
| throw-before-execution-complete-NotRun-tree / config | `{"exit":1,"status":"Failed","extra":true}` | `{"exit":1,"status":"Failed","extra":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c042` |
| unrelated-unadapted-entry-does-not-stop-selected-tree / config | `{"exit":0,"status":"Passed","extra":true}` | `{"exit":0,"status":"Passed","extra":true}` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c043` |
| same-second-same-directory-UUID-isolation / identity | `true` | `true` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c044` |
| seed-current-original-receipts / reuse | `0` | `0` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c045` |
| -NoBuild-valid-proof-no-new-stage-credit / reuse | `true` | `true` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c046` |
| -NoRestore-valid-proof-no-new-stage-credit / reuse | `true` | `true` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c047` |
| -NoBuild-missing-proof-blocked / reuse | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c048` |
| -NoRestore-missing-proof-blocked / reuse | `{"exit":2,"status":"Blocked","extra":true}` | `{"exit":2,"status":"Blocked","extra":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c049` |
| explicit-build-seed / reuse | `0` | `0` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c050` |
| NoBuild-preserves-explicit-build-step / reuse | `true` | `true` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c051` |
| failed-build-retains-independent-restore / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c052` |
| NoRestore-accepts-restore-alone-after-failed-build / reuse | `0` | `0` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c053` |
| corrupt-proof-configuration / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c054` |
| NoRestore-proof-configuration / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c054/nr` |
| corrupt-proof-tfm / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c055` |
| NoRestore-proof-tfm / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c055/nr` |
| corrupt-proof-sdk / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c056` |
| NoRestore-proof-sdk / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c056/nr` |
| corrupt-proof-configSha256 / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c057` |
| NoRestore-proof-configSha256 / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c057/nr` |
| corrupt-proof-sourceSha / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c058` |
| NoRestore-proof-sourceSha / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c058/nr` |
| corrupt-proof-restoreNative / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c059` |
| NoRestore-proof-restoreNative / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c059/nr` |
| corrupt-proof-buildNative / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c060` |
| NoRestore-proof-buildNative / reuse | `0` | `0` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c060/nr` |
| corrupt-proof-emptyOutputs / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c061` |
| NoRestore-proof-emptyOutputs / reuse | `0` | `0` | 0 | `local/Artifacts/issue6-cooking-slice1-f06/c061/nr` |
| changed-assets-proof-refused / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c062` |
| changed-source-proof-refused / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c063` |
| changed-DLL-proof-refused / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c064` |
| changed-package-tool-proof-refused / reuse | `1` | `1` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c065` |
| strict-lowercase-status / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c066` |
| strict-CLI-map / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c067` |
| native2-cannot-claim-Passed / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c068` |
| old-run-result / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c069` |
| duplicate-result-ID / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c070` |
| wrong-parent-ID / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c071` |
| missing-child / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c072` |
| empty-serialized-bindings / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c073` |
| forged-descendant / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c074` |
| duplicate-binding / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c075` |
| forged-completion-summary / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c076` |
| forged-missing-summary / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c077` |
| forged-full-acceptance / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c078` |
| leaf-claims-full-acceptance / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c079` |
| leaf-claims-children / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c080` |
| old-SHA-TRX-binding / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c081` |
| wrong-run-TRX-binding / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c082` |
| wrong-filter-binding / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c083` |
| wrong-filter-command / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c084` |
| duplicate-filter-stage-and-native-command / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c085` |
| forged-leaf-summary-command / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c086` |
| forged-leaf-summary-time / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c087` |
| native-tool-argv-contradiction / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c088` |
| native-executable-contradiction / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c089` |
| native-stream-other-node-path / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c090` |
| contradictory-test-counts / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c091` |
| wrong-test-definition / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c092` |
| source-after-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c093` |
| SDK-provenance-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c094` |
| native-stage-time-forgery / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c095` |
| stage-and-native-build-tfm-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c096` |
| stage-and-native-test-tfm-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c097` |
| stage-and-native-restore-tfm-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c098` |
| stage-and-native-restore-configuration-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c099` |
| stage-and-native-build-configuration-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c100` |
| stage-and-native-test-configuration-mismatch / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c101` |
| forged-entire-source-SHA-rejected-by-parent / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c102` |
| compiler-input-closure-empty / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c103` |
| compiler-input-hash-forgery / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c104` |
| artifact-path-escape / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c105` |
| artifact-absolute-path / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c106` |
| artifact-backslash-path / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c107` |
| artifact-other-run-owner / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c108` |
| artifact-other-result-owner / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c109` |
| artifact-length-forgery / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c110` |
| artifact-hash-forgery / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c111` |
| duplicate-artifact / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c112` |
| undeclared-optional-skip / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c113` |
| synthetic-production-refusal / validator | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c114` |
| synthetic-config-refused-before-any-dotnet-launch / production | `true` | `true` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c115` |
| repository-preflight-precheck / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c116` |
| repository-preflight-moba-codegen / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c117` |
| repository-preflight-moba-console-smoke / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c118` |
| repository-preflight-runtime-contracts / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c119` |
| repository-preflight-moba-network-options / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c120` |
| repository-preflight-moba-acceptance-dotnet / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c121` |
| repository-preflight-cooking-et-level-runtime / compatibility | `{"exit":1,"uninvoked":true}` | `{"exit":1,"uninvoked":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c122` |
| repository-preflight-cooking-kitchen-loop / compatibility | `{"exit":1,"uninvoked":true}` | `{"exit":1,"uninvoked":true}` | 1 | `local/Artifacts/issue6-cooking-slice1-f06/c123` |
| repository-preflight-network-sdk / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c124` |
| repository-preflight-core-stability / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c125` |
| repository-preflight-foundation-units / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c126` |
| repository-preflight-regression / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c127` |
| repository-preflight-moba-content-contracts / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c128` |
| repository-preflight-moba-xiaoqiao-unity / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c129` |
| repository-preflight-moba-lianpo-unity / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c130` |
| repository-preflight-moba-zhaoyun-unity / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c131` |
| repository-preflight-moba-mozi-unity / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c132` |
| repository-preflight-moba-daji-unity / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c133` |
| repository-preflight-moba-yingzheng-unity / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c134` |
| repository-preflight-moba-config-sync / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c135` |
| repository-preflight-shooter-fast / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c136` |
| repository-preflight-shooter-integration / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c137` |
| repository-preflight-shooter-unity-playmode / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c138` |
| repository-preflight-shooter-multiprocess / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c139` |
| repository-preflight-shooter-multiprocess-compatibility / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c140` |
| repository-preflight-shooter-multiprocess-soak / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c141` |
| repository-preflight-shooter-multiprocess-ownership-cleanup / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c142` |
| repository-preflight-runtime-performance-measurement / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c143` |
| repository-preflight-shooter-performance / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c144` |
| repository-preflight-moba-complete-battle-journey / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c145` |
| repository-preflight-moba-smoke / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c146` |
| repository-preflight-moba-multiprocess / compatibility | `{"exit":2,"uninvoked":true}` | `{"exit":2,"uninvoked":true}` | 2 | `local/Artifacts/issue6-cooking-slice1-f06/c147` |
| width-40-long-lines-direct-capture / native-streams | `true` | `true` | 7 | `local/Artifacts/issue6-cooking-slice1-f06/c148` |
| width-80-long-lines-direct-capture / native-streams | `true` | `true` | 7 | `local/Artifacts/issue6-cooking-slice1-f06/c149` |
| width-180-long-lines-direct-capture / native-streams | `true` | `true` | 7 | `local/Artifacts/issue6-cooking-slice1-f06/c150` |
| reparse-artifact-refused / paths | `false` | `false` | null | `local/Artifacts/issue6-cooking-slice1-f06/c151` |
| implementation-inputs-before-after-identical / freeze | `true` | `true` | null | `local/Artifacts/issue6-cooking-slice1-f06/c152` |

Full index: `local/Artifacts/issue6-cooking-slice1-f06/control-index.json`, SHA-256 `5391f1f6aa3ce0a2be0444acdd86e7a7c1445accdef496bafaaa533abdd263a6`. Original runner copied for the red has Git blob `04c9aab708ad6fef10d1070842de963f0d1d3d1b`, identical to base619883 tools/run_test_gate.ps1 (verified by git rev-parse and git hash-object).

## Final affected receipt and checkpoint source

After f06 settled, main explicitly requested one existing-BCL hypothesis: preserve actual buffer Height and record buffer/window positions and heights; if no genuine width proof results, retain Blocked and deliver without more speculative changes. The probe-only change records initial/final buffer/window dimensions and window/cursor coordinates and uses the existing height instead of 300. Actual height was 3000, initial coordinates all 0; buffer40/80 still failed and remained120, while window40/80 had applied and buffer180 applied/observed180. The height hypothesis was disproved. Original f06 and width1/2/3 evidence were preserved.

The final affected receipt is `local/Artifacts/issue6-cooking-slice1-width4/controls.json`: three direct subprocess controls Passed, zero assertion failures, width coverage Blocked, fullControlSuiteAccepted=false; native exits are each the deliberately requested 7, with exact long stdout/stderr bytes and confirmed exits. Source before/after remains dirty SHA a6f00cf54579828456deccd7afbb961d8eab6752; identical final seven-file input fingerprint is b0040e686636e2bfd2499212641ff33fe264a8dc058c5d59350cba02d8e5aef3. Production runner/helper/config and the harness/model/fake executor hashes match f06; only the native probe changed. This affected receipt does not claim a fresh full 160-control execution or clean checkpoint-SHA verification.
Affected receipt SHA-256: `1ca2e7ceab4634a01d4c9c18facd66d5365e8e425eb8d8b77e3f53f5a279a712`. Exact single input delta: `[{"path":"tools/tests/fixtures/gate-result-native-probe.ps1","beforeSha256":"f51dd1c05301cf57c03826a1cfe5d21bceee3230aa63f2adb63c7917d0407bdf","afterSha256":"b06801b0fd4e17835edc81b3ba30eb19f194ee56b271fcf3836ab89d80474240"}]`.

Final scope audit: local/Artifacts/issue6-cooking-slice1-scope4/scope.json records six zero-error PowerShell parses, whitespace checks on all nine owned files, all 30 unchanged non-Cooking objects and unchanged shared metadata/defaultGate. The final scoped staged-diff and exact commit identity receipts are saved under local/Artifacts/issue6-cooking-slice1-scope3; coordinator-owned changes remain outside the commit. No real workload, integration, merge, push, close, archive or cleanup was performed.

Scoped staged diff check native0 and exact nine-file staging passed; no other files were staged. A first manual wrapper invocation failed parameter binding before any git subprocess launch (argv concatenation lacked parentheses); local/Artifacts/issue6-cooking-slice1-scope3/final-staged-attempt1.json preserves its command/reason/native-null classification. The corrected native command, separate stdout/stderr and exit0 are in final-staged-scope.json and final-staged-diff-check stream files. This manual invocation failure is separate from the 160 assertion results and is not erased.
