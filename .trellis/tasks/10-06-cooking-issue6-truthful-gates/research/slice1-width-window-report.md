# Slice1 bounded followup: actual window widths

Task task_c9384f689b07 / Dispatch ctx_f42f62847f04; Owner Cooking-only scope, approved dot decision AK-I6-COOK-PLAN-20261006-01, implementation base b1644ec4e0418df99641a5db7e7e65010fc5f24a. This report is the only task write owned by this dispatch. The complete normative dot reply and current bounded worker spec were read; prior slice1 planning/source provenance and slice1-report remain intact.

Result: final frozen isolated suite Passed, 164/164 controls, zero assertion failures, suite native exit0, fullControlSuiteAccepted=true, contractControlsAccepted=true. Actual RawUI.WindowSize.Width coverage40/80/120 Passed. This is synthetic contract-control acceptance only: real Cooking dotnet gates, Unity, CI, Issue technical acceptance, merge and close remain NotRun; main performs a separate independent audit and genuine validation dispatch.

## Exact file scope and Cooking purpose

| Path | Bounded change and acceptance |
|---|---|
| tools/tests/fixtures/gate-result-native-probe.ps1 | Adds explicit Window mode for the two Cooking gates shared native-capture controls; retains default Buffer diagnostics, native output and timeout/cancel behavior. Records dimension, requested/setter/applied/observed widths, initial/final buffer/window dimensions and coordinates, preserved height and redirection. |
| tools/tests/test-gate-result-contract.tests.ps1 | Selects actual window40/80/120; strict per-case and aggregate checks bind exact streams, native7, successful setter/readback, redirection and preserved height. Retains every original160 control (three width cases use the newly approved dimension/values) and adds four mutations of actual subprocess receipts. |
| Docs/AbilityKit测试门禁与批量回归规范.md | Only its native-width paragraph changes: explicit window dimension/40/80/120, acceptance and old buffer evidence distinction; no other subsection changed. |
| .trellis/tasks/10-06-cooking-issue6-truthful-gates/research/slice1-width-window-report.md | This bounded dispatch evidence, commands, scope and limitations. |

Production runner/helper/config/model/fake executor and original slice1-report are read-only and byte-identical to b164; the complete test-gates.json file is unchanged, all30 nonCooking parsed objects and defaultGate match the base. No gameplay/C# project/package/SDK/ET/Unity/example producer or dedicated gate edits. Main-owned index.json, record-operation.py and task/flow evidence were present before work and preserved. No worker/native subagents, dependencies, real dotnet execution, visible windows, remote state writes, cleanup or global process termination.

## Dimension decision and observed evidence

The approved dot plan requires native capture at different console widths but does not prescribe BufferSize or40/80/180. This dispatch explicitly authorizes the narrower RawUI.WindowSize.Width mode with40/80/120; it is the formatter-window dimension and therefore serves that same requirement. No interactive rendering is claimed: stdout/stderr remain redirected byte streams, captured by the unchanged production Invoke-GateNative with CreateNoWindow=true and direct BaseStream copies.

Old f06/width diagnostics remain unchanged: buffer40/80 Blocked and buffer180 Passed. They are not promoted or counted as window evidence. The original warning+native0 red at local/Artifacts/issue6-cooking-slice1-8184fe62-25f7-45e6-90c8-ed49b06aea58, all earlier failed/superseded/Blocked artifacts and the original slice1-report remain preserved.

| Requested dimension/value | Setter/applied/readback | Initial/final window | Initial/final buffer | Streams/native | Raw case |
|---|---|---|---|---|---|
| RawUI.WindowSize.Width/40 | True/True/40 | 120x50 / 40x50 | 120x3000 / 120x3000 | exact O/E payload8192 each; stdout8201B stderr8251B; native7; exit confirmed | c148/width-evidence.json |
| RawUI.WindowSize.Width/80 | True/True/80 | 120x50 / 80x50 | 120x3000 / 120x3000 | exact O/E payload8192 each; stdout8201B stderr8251B; native7; exit confirmed | c149/width-evidence.json |
| RawUI.WindowSize.Width/120 | True/True/120 | 120x50 / 120x50 | 120x3000 / 120x3000 | exact O/E payload8192 each; stdout8201B stderr8251B; native7; exit confirmed | c150/width-evidence.json |

All three runs recorded ConsoleHost, outputRedirected=true, errorRedirected=true, coordinates/window position unchanged at0, and no limitation. Width120 still executes the setter; matching the initial value alone is insufficient. A unavailable setter/readback keeps widthCoverage Blocked/fullControlSuiteAccepted=false; real failures or stream corruption cannot be hidden by nominal width metadata. Four new negative cases change actual receipts: dimension, duplicate requested width, setter=false, streamsExact=false; each aggregate returns Blocked. The positive aggregate is the three actual successful subprocess records.

## Commands and preserved attempts

Working directory for all commands: C:/Users/Administrator/orca/workspaces/AbilityKit/issue6-cooking-truthful-gates.

Affected subprocess commands, executed twice (initial and final inputs), via unchanged Invoke-GateNative with directly captured stdout/stderr:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/tests/fixtures/gate-result-native-probe.ps1 -WidthMode Window -Width 40 -NativeExit 7 -WidthEvidencePath <fresh-case>/width-evidence.json
# Same command for -Width80 and -Width120; exact absolute argv and paths are in each native receipt.
```

| Attempt | Exact receipt/raw root under local/Artifacts/ | Native/counts/outcome |
|---|---|---|
| Initial affected | issue6-cooking-slice1-window-target-d838736c/receipt.json | native7 each,3/3 Passed, source before/after stable |
| Full01 frozen failed | issue6-cooking-slice1-window-full01/controls.json; issue6-cooking-slice1-window-full-log01/native.json + suite.stdout.txt/suite.stderr.txt | suite native1,158 recorded cases Passed, aborted before new mutations/final source check; whole run Failed, no acceptance |
| Binding fix isolated | issue6-cooking-slice1-window-binding-fix01/controls.json |5/5 expected results: actual-receipt positive Passed plus four mutation Blocked refusals; no producer launches |
| Final affected | issue6-cooking-slice1-window-target-final01/receipt.json | native7 each,3/3 Passed, final input fingerprint stable |
| Full02 final frozen | issue6-cooking-slice1-window-full02/controls.json; issue6-cooking-slice1-window-full-log02/native.json + suite.stdout.txt/suite.stderr.txt | suite native0,164/164 Passed; window coverage/full controls Passed |

Full01 failure: Windows PowerShell5 ConvertFrom-Json returned the JSON array as one pipeline object; wrapping that in @() nested the records, so the first mutation could not assign dimension. The one-line fix removes that extra wrapper. Full01 sourceBefore fingerprint73289870aa19c4ea20a582fa18a5da65ae2edf19ba139e40b9557a981ba4aae5; sourceAfter/freeze NotRun because the suite aborted. No edits occurred during either frozen run; the failed attempt is never retagged as Passed.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-window-full01
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-window-full02
```

Final outer native command: `{"executable":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","arguments":["-NoProfile","-ExecutionPolicy","Bypass","-File","C:\\Users\\Administrator\\orca\\workspaces\\AbilityKit\\issue6-cooking-truthful-gates\\tools\\tests\\test-gate-result-contract.tests.ps1","-ArtifactRoot","local/Artifacts/issue6-cooking-slice1-window-full02"],"workingDirectory":"C:\\Users\\Administrator\\orca\\workspaces\\AbilityKit\\issue6-cooking-truthful-gates"}`.
Final actual stage times UTC: 2026-10-05T19:18:27.7081371+00:00 -> 2026-10-05T19:24:56.8898348+00:00; owned PID73564, exitConfirmed=True.

Audit01 failed before receipt production because an ad-hoc Get-Content omitted -Encoding UTF8 and PowerShell5 decoded the UTF8 JSON as ANSI; no source bytes changed. Its explanation is preserved in issue6-cooking-slice1-window-audit01/failed-attempt.txt. Audit02 uses direct git show capture and explicit UTF8, proves config/defaultGate/nonCooking equality and all read-only/frozen hashes. An initial unscoped git diff --check found whitespace in preexisting main-owned index.json; those records were not modified. Scoped parse and whitespace checks pass.

## Source freeze, tools and hashes

Source before and after final frozen run: SHA b1644ec4e0418df99641a5db7e7e65010fc5f24a, dirty=True (owned edits plus preexisting main task records); fingerprint 5108ffb6130b775abdf1d419c5a86946150b5905b1d52f697a4d318538c5b300. The complete dirtyDetails, seven resolved input paths/lengths/hashes and exclusions are in both source objects in controls.json. SHA and input fingerprint match exactly.

Tools: Windows PowerShell 5.1.26100.9444; runtime host 4.0.30319.42000; Microsoft Windows NT 10.0.26100.0. No actual dotnet SDK/build/test was launched. Synthetic SDK/project/stage/TRX proofs belong only to explicitly marked TEMP fixtures.

| Final frozen input | SHA256 |
|---|---|
| tools/run_test_gate.ps1 | 575469c4238e29cf41b5826a43748d38c9c81578793261835b6e51dc3a886e8f |
| tools/test-gate-result-contract.ps1 | 647fd558fb7bb660e30a3749a8323b35961f6236bc5052eb2342c807407e167c |
| tools/test-gates.json | 85111d00c4a3a61c87f61da3e43d159da60e07d058406f7049e17495e7e520a1 |
| tools/tests/fixtures/gate-result-fake-dotnet.ps1 | d662821a04386382f3ac5c9bc981d3e973e96a38d5228b9e762d1325d70dbb76 |
| tools/tests/fixtures/gate-result-model.ps1 | fb7cf4c82af3c78c2f825317b9573a92abe28f0135d26d5e09f6cb8c1ffe5a35 |
| tools/tests/fixtures/gate-result-native-probe.ps1 | f3a1a95a96cb42f1c1c3dbc2cf77fb6df66d13855543a6332c576f56bd355bfe |
| tools/tests/test-gate-result-contract.tests.ps1 | 70d8724365b8b16cd00e6569287c4e94c769ef3204bb1f4cef584b887ec7c572 |

Final controls.json SHA256: cc0d452095be6828ffb15edb9e52814b3cc5ded3308771e54c0bc550cc2eaa39. Failed Full01 controls.json SHA256: b40f9d4c78a2e19b811dfa1b4d065255b6033f70b1cc8b54651f397d8b3a623d. Read-only input/report hashes and byte comparisons: local/Artifacts/issue6-cooking-slice1-window-audit02/scope.json. Exact implementation patch and git native receipt: same tree implementation-diff.stdout.txt and diff-native.json.

The tests ran before the scoped followup commit at base SHA with dirty=true. The introducing commit identity and final four-file hashes are recorded separately in the post-commit local audit and worker_done; matching byte hashes demonstrate unchanged tested inputs, not a claim that tests were executed at a later clean commit SHA. Main-owned dirty records remain outside the control input closure.

## Complete group receipt

| Group | Executed/Passed | Failed |
|---|---:|---:|
| valid | 2/2 | 0 |
| original-red | 1/1 | 0 |
| trx | 16/16 | 0 |
| environment | 1/1 | 0 |
| failure | 1/1 | 0 |
| source | 2/2 | 0 |
| timeout-cancel | 2/2 | 0 |
| coverage | 6/6 | 0 |
| selector | 2/2 | 0 |
| unsupported | 5/5 | 0 |
| config | 5/5 | 0 |
| identity | 1/1 | 0 |
| reuse | 29/29 | 0 |
| validator | 49/49 | 0 |
| production | 1/1 | 0 |
| compatibility | 32/32 | 0 |
| native-streams | 3/3 | 0 |
| native-width-bindings | 4/4 | 0 |
| paths | 1/1 | 0 |
| freeze | 1/1 | 0 |

All original cases remain: strict restore/build/test stage and native TFM/Configuration/filter identity, source/binary/assets/config/SDK proofs, zero/all-skipped/required-skipped/TRX definitions/filter counters, lowercase/forged/missing/duplicate descendants and summary bindings, nested/focus/selectors, same-second UUIDs, NoBuild/NoRestore prior proofs/no new credit, contained paths/reparse, production synthetic rejection and unsupported preflight. Compatibility controls list/diagnose32 gates and reject all30 unadapted legacy definitions before producer launch; they do not execute or change example workloads. Timeout/cancel controls stop only their owned fake subprocess and verify exit, retaining later NotRun children.

## Per-case receipt

Every row refers to issue6-cooking-slice1-window-full02/<case>/control.json and its immutable case raw output tree. Each control.json and the pinned controls.json contain the exact command/argv, native exit, expected/actual, evidence/counts and raw paths. Native=null means validator-only/no subprocess; counts=null means tests are not applicable to that control. The following table transcribes all164 expected/actual/native/count records; commands are linked through the exact raw case receipt, avoiding any reconstructed argv.

| Case/group/name | Expected | Actual | Native | Counts | Raw command/result |
|---|---|---|---:|---|---|
| c001/valid/current-build-test-JSON-roundtrip | {"exit":0,"status":"Passed","extra":true} | {"exit":0,"status":"Passed","extra":true} | 0 | [{"total":2,"resultEntries":2,"passed":2,"failed":0,"skipped":0,"notExecuted":0,"executed":2}] | c001/control.json |
| c002/valid/current-build-only-tests-null | {"exit":0,"status":"Passed","extra":true} | {"exit":0,"status":"Passed","extra":true} | 0 | [] | c002/control.json |
| c003/original-red/warning-native-zero-refused-for-no-build-evidence | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c003/control.json |
| c004/trx/test-missing | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c004/control.json |
| c005/trx/test-empty | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c005/control.json |
| c006/trx/test-malformed | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c006/control.json |
| c007/trx/test-zero | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c007/control.json |
| c008/trx/test-no-entries | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c008/control.json |
| c009/trx/test-failed-zero | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [{"total":2,"resultEntries":2,"passed":0,"failed":2,"skipped":0,"notExecuted":0,"executed":2}] | c009/control.json |
| c010/trx/test-counter | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c010/control.json |
| c011/trx/test-completed-counter | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c011/control.json |
| c012/trx/test-lowercase | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c012/control.json |
| c013/trx/test-wrong-assembly | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c013/control.json |
| c014/trx/test-stale | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c014/control.json |
| c015/trx/test-duplicate | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c015/control.json |
| c016/trx/test-no-definition | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c016/control.json |
| c017/trx/test-skipped | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [{"total":2,"resultEntries":2,"passed":0,"failed":0,"skipped":0,"notExecuted":2,"executed":0}] | c017/control.json |
| c018/trx/test-replaced-dll | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [{"total":2,"resultEntries":2,"passed":2,"failed":0,"skipped":0,"notExecuted":0,"executed":2}] | c018/control.json |
| c019/trx/test-native-fail | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c019/control.json |
| c020/environment/missing-tool-no-launch | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c020/control.json |
| c021/failure/required-native7-failure-later-NotRun | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c021/control.json |
| c022/source/source-change-fails | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c022/control.json |
| c023/source/evaluated-TFM-mismatch | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c023/control.json |
| c024/timeout-cancel/timeout-owned-child-exit-confirmed | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c024/control.json |
| c025/timeout-cancel/started-cancellation-owned-exit-confirmed | true | true | 1 | null | c025/control.json |
| c026/coverage/nested-complete | {"exit":0,"status":"Passed","extra":true} | {"exit":0,"status":"Passed","extra":true} | 0 | [] | c026/control.json |
| c027/coverage/repeated-nested-unique-node-directory-bindings | {"exit":0,"status":"Passed","extra":true} | {"exit":0,"status":"Passed","extra":true} | 0 | [] | c027/control.json |
| c028/coverage/StepName-positive-parent-partial-NotRun4 | {"exit":4,"status":"NotRun","extra":true} | {"exit":4,"status":"NotRun","extra":true} | 4 | [{"total":2,"resultEntries":2,"passed":2,"failed":0,"skipped":0,"notExecuted":0,"executed":2}] | c028/control.json |
| c029/coverage/nested-focus-partial | {"exit":4,"status":"NotRun","extra":true} | {"exit":4,"status":"NotRun","extra":true} | 4 | [] | c029/control.json |
| c030/selector/missing-selector-zero-launches | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c030/control.json |
| c031/selector/ambiguous-selector-zero-launches | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c031/control.json |
| c032/coverage/optional-declared-missing-tool-skip | {"exit":0,"status":"Passed","extra":true} | {"exit":0,"status":"Passed","extra":true} | 0 | [] | c032/control.json |
| c033/coverage/optional-real-failure-not-laundered | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [{"total":2,"resultEntries":2,"passed":0,"failed":2,"skipped":0,"notExecuted":0,"executed":2}] | c033/control.json |
| c034/unsupported/producer-powershell-script-blocked-before-launch | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c034/control.json |
| c035/unsupported/producer-unity-editmode-test-blocked-before-launch | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c035/control.json |
| c036/unsupported/producer-unity-playmode-test-blocked-before-launch | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c036/control.json |
| c037/unsupported/producer-unity-execute-method-blocked-before-launch | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c037/control.json |
| c038/unsupported/missing-explicit-coverage-before-launch | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c038/control.json |
| c039/config/duplicate-gate-fails-before-launch | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c039/control.json |
| c040/config/unknown-kind-fails-before-launch | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c040/control.json |
| c041/config/empty-tree-fails-before-launch | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c041/control.json |
| c042/config/throw-before-execution-complete-NotRun-tree | {"exit":1,"status":"Failed","extra":true} | {"exit":1,"status":"Failed","extra":true} | 1 | [] | c042/control.json |
| c043/config/unrelated-unadapted-entry-does-not-stop-selected-tree | {"exit":0,"status":"Passed","extra":true} | {"exit":0,"status":"Passed","extra":true} | 0 | [] | c043/control.json |
| c044/identity/same-second-same-directory-UUID-isolation | true | true | 2 | null | c044/control.json |
| c045/reuse/seed-current-original-receipts | 0 | 0 | 0 | null | c045/control.json |
| c046/reuse/-NoBuild-valid-proof-no-new-stage-credit | true | true | 0 | null | c046/control.json |
| c047/reuse/-NoRestore-valid-proof-no-new-stage-credit | true | true | 0 | null | c047/control.json |
| c048/reuse/-NoBuild-missing-proof-blocked | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c048/control.json |
| c049/reuse/-NoRestore-missing-proof-blocked | {"exit":2,"status":"Blocked","extra":true} | {"exit":2,"status":"Blocked","extra":true} | 2 | [] | c049/control.json |
| c050/reuse/explicit-build-seed | 0 | 0 | 0 | null | c050/control.json |
| c051/reuse/NoBuild-preserves-explicit-build-step | true | true | 0 | null | c051/control.json |
| c052/reuse/failed-build-retains-independent-restore | 1 | 1 | 1 | null | c052/control.json |
| c053/reuse/NoRestore-accepts-restore-alone-after-failed-build | 0 | 0 | 0 | null | c053/control.json |
| c054/reuse/corrupt-proof-configuration | 1 | 1 | 1 | null | c054/control.json |
| nr/reuse/NoRestore-proof-configuration | 1 | 1 | 1 | null | nr/control.json |
| c055/reuse/corrupt-proof-tfm | 1 | 1 | 1 | null | c055/control.json |
| nr/reuse/NoRestore-proof-tfm | 1 | 1 | 1 | null | nr/control.json |
| c056/reuse/corrupt-proof-sdk | 1 | 1 | 1 | null | c056/control.json |
| nr/reuse/NoRestore-proof-sdk | 1 | 1 | 1 | null | nr/control.json |
| c057/reuse/corrupt-proof-configSha256 | 1 | 1 | 1 | null | c057/control.json |
| nr/reuse/NoRestore-proof-configSha256 | 1 | 1 | 1 | null | nr/control.json |
| c058/reuse/corrupt-proof-sourceSha | 1 | 1 | 1 | null | c058/control.json |
| nr/reuse/NoRestore-proof-sourceSha | 1 | 1 | 1 | null | nr/control.json |
| c059/reuse/corrupt-proof-restoreNative | 1 | 1 | 1 | null | c059/control.json |
| nr/reuse/NoRestore-proof-restoreNative | 1 | 1 | 1 | null | nr/control.json |
| c060/reuse/corrupt-proof-buildNative | 1 | 1 | 1 | null | c060/control.json |
| nr/reuse/NoRestore-proof-buildNative | 0 | 0 | 0 | null | nr/control.json |
| c061/reuse/corrupt-proof-emptyOutputs | 1 | 1 | 1 | null | c061/control.json |
| nr/reuse/NoRestore-proof-emptyOutputs | 0 | 0 | 0 | null | nr/control.json |
| c062/reuse/changed-assets-proof-refused | 1 | 1 | 1 | null | c062/control.json |
| c063/reuse/changed-source-proof-refused | 1 | 1 | 1 | null | c063/control.json |
| c064/reuse/changed-DLL-proof-refused | 1 | 1 | 1 | null | c064/control.json |
| c065/reuse/changed-package-tool-proof-refused | 1 | 1 | 1 | null | c065/control.json |
| c066/validator/strict-lowercase-status | false | false | null | null | c066/control.json |
| c067/validator/strict-CLI-map | false | false | null | null | c067/control.json |
| c068/validator/native2-cannot-claim-Passed | false | false | null | null | c068/control.json |
| c069/validator/old-run-result | false | false | null | null | c069/control.json |
| c070/validator/duplicate-result-ID | false | false | null | null | c070/control.json |
| c071/validator/wrong-parent-ID | false | false | null | null | c071/control.json |
| c072/validator/missing-child | false | false | null | null | c072/control.json |
| c073/validator/empty-serialized-bindings | false | false | null | null | c073/control.json |
| c074/validator/forged-descendant | false | false | null | null | c074/control.json |
| c075/validator/duplicate-binding | false | false | null | null | c075/control.json |
| c076/validator/forged-completion-summary | false | false | null | null | c076/control.json |
| c077/validator/forged-missing-summary | false | false | null | null | c077/control.json |
| c078/validator/forged-full-acceptance | false | false | null | null | c078/control.json |
| c079/validator/leaf-claims-full-acceptance | false | false | null | null | c079/control.json |
| c080/validator/leaf-claims-children | false | false | null | null | c080/control.json |
| c081/validator/old-SHA-TRX-binding | false | false | null | null | c081/control.json |
| c082/validator/wrong-run-TRX-binding | false | false | null | null | c082/control.json |
| c083/validator/wrong-filter-binding | false | false | null | null | c083/control.json |
| c084/validator/wrong-filter-command | false | false | null | null | c084/control.json |
| c085/validator/duplicate-filter-stage-and-native-command | false | false | null | null | c085/control.json |
| c086/validator/forged-leaf-summary-command | false | false | null | null | c086/control.json |
| c087/validator/forged-leaf-summary-time | false | false | null | null | c087/control.json |
| c088/validator/native-tool-argv-contradiction | false | false | null | null | c088/control.json |
| c089/validator/native-executable-contradiction | false | false | null | null | c089/control.json |
| c090/validator/native-stream-other-node-path | false | false | null | null | c090/control.json |
| c091/validator/contradictory-test-counts | false | false | null | null | c091/control.json |
| c092/validator/wrong-test-definition | false | false | null | null | c092/control.json |
| c093/validator/source-after-mismatch | false | false | null | null | c093/control.json |
| c094/validator/SDK-provenance-mismatch | false | false | null | null | c094/control.json |
| c095/validator/native-stage-time-forgery | false | false | null | null | c095/control.json |
| c096/validator/stage-and-native-build-tfm-mismatch | false | false | null | null | c096/control.json |
| c097/validator/stage-and-native-test-tfm-mismatch | false | false | null | null | c097/control.json |
| c098/validator/stage-and-native-restore-tfm-mismatch | false | false | null | null | c098/control.json |
| c099/validator/stage-and-native-restore-configuration-mismatch | false | false | null | null | c099/control.json |
| c100/validator/stage-and-native-build-configuration-mismatch | false | false | null | null | c100/control.json |
| c101/validator/stage-and-native-test-configuration-mismatch | false | false | null | null | c101/control.json |
| c102/validator/forged-entire-source-SHA-rejected-by-parent | false | false | null | null | c102/control.json |
| c103/validator/compiler-input-closure-empty | false | false | null | null | c103/control.json |
| c104/validator/compiler-input-hash-forgery | false | false | null | null | c104/control.json |
| c105/validator/artifact-path-escape | false | false | null | null | c105/control.json |
| c106/validator/artifact-absolute-path | false | false | null | null | c106/control.json |
| c107/validator/artifact-backslash-path | false | false | null | null | c107/control.json |
| c108/validator/artifact-other-run-owner | false | false | null | null | c108/control.json |
| c109/validator/artifact-other-result-owner | false | false | null | null | c109/control.json |
| c110/validator/artifact-length-forgery | false | false | null | null | c110/control.json |
| c111/validator/artifact-hash-forgery | false | false | null | null | c111/control.json |
| c112/validator/duplicate-artifact | false | false | null | null | c112/control.json |
| c113/validator/undeclared-optional-skip | false | false | null | null | c113/control.json |
| c114/validator/synthetic-production-refusal | false | false | null | null | c114/control.json |
| c115/production/synthetic-config-refused-before-any-dotnet-launch | true | true | 1 | null | c115/control.json |
| c116/compatibility/repository-preflight-precheck | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c116/control.json |
| c117/compatibility/repository-preflight-moba-codegen | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c117/control.json |
| c118/compatibility/repository-preflight-moba-console-smoke | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c118/control.json |
| c119/compatibility/repository-preflight-runtime-contracts | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c119/control.json |
| c120/compatibility/repository-preflight-moba-network-options | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c120/control.json |
| c121/compatibility/repository-preflight-moba-acceptance-dotnet | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c121/control.json |
| c122/compatibility/repository-preflight-cooking-et-level-runtime | {"exit":1,"uninvoked":true} | {"exit":1,"uninvoked":true} | 1 | null | c122/control.json |
| c123/compatibility/repository-preflight-cooking-kitchen-loop | {"exit":1,"uninvoked":true} | {"exit":1,"uninvoked":true} | 1 | null | c123/control.json |
| c124/compatibility/repository-preflight-network-sdk | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c124/control.json |
| c125/compatibility/repository-preflight-core-stability | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c125/control.json |
| c126/compatibility/repository-preflight-foundation-units | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c126/control.json |
| c127/compatibility/repository-preflight-regression | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c127/control.json |
| c128/compatibility/repository-preflight-moba-content-contracts | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c128/control.json |
| c129/compatibility/repository-preflight-moba-xiaoqiao-unity | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c129/control.json |
| c130/compatibility/repository-preflight-moba-lianpo-unity | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c130/control.json |
| c131/compatibility/repository-preflight-moba-zhaoyun-unity | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c131/control.json |
| c132/compatibility/repository-preflight-moba-mozi-unity | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c132/control.json |
| c133/compatibility/repository-preflight-moba-daji-unity | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c133/control.json |
| c134/compatibility/repository-preflight-moba-yingzheng-unity | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c134/control.json |
| c135/compatibility/repository-preflight-moba-config-sync | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c135/control.json |
| c136/compatibility/repository-preflight-shooter-fast | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c136/control.json |
| c137/compatibility/repository-preflight-shooter-integration | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c137/control.json |
| c138/compatibility/repository-preflight-shooter-unity-playmode | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c138/control.json |
| c139/compatibility/repository-preflight-shooter-multiprocess | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c139/control.json |
| c140/compatibility/repository-preflight-shooter-multiprocess-compatibility | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c140/control.json |
| c141/compatibility/repository-preflight-shooter-multiprocess-soak | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c141/control.json |
| c142/compatibility/repository-preflight-shooter-multiprocess-ownership-cleanup | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c142/control.json |
| c143/compatibility/repository-preflight-runtime-performance-measurement | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c143/control.json |
| c144/compatibility/repository-preflight-shooter-performance | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c144/control.json |
| c145/compatibility/repository-preflight-moba-complete-battle-journey | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c145/control.json |
| c146/compatibility/repository-preflight-moba-smoke | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c146/control.json |
| c147/compatibility/repository-preflight-moba-multiprocess | {"exit":2,"uninvoked":true} | {"exit":2,"uninvoked":true} | 2 | null | c147/control.json |
| c148/native-streams/window-width-40-long-lines-direct-capture | true | true | 7 | null | c148/control.json |
| c149/native-streams/window-width-80-long-lines-direct-capture | true | true | 7 | null | c149/control.json |
| c150/native-streams/window-width-120-long-lines-direct-capture | true | true | 7 | null | c150/control.json |
| c151/native-width-bindings/reject-dimension | "Blocked" | "Blocked" | null | null | c151/control.json |
| c152/native-width-bindings/reject-duplicate | "Blocked" | "Blocked" | null | null | c152/control.json |
| c153/native-width-bindings/reject-setter | "Blocked" | "Blocked" | null | null | c153/control.json |
| c154/native-width-bindings/reject-streams | "Blocked" | "Blocked" | null | null | c154/control.json |
| c155/paths/reparse-artifact-refused | false | false | null | null | c155/control.json |
| c156/freeze/implementation-inputs-before-after-identical | true | true | null | null | c156/control.json |

## Remaining limits and handoff

No blocker remains for this bounded window-width implementation/control acceptance. Real .NET Cooking builds/tests and evaluated production inputs/assemblies, Unity/editor/platform coverage, actual CI required-check consumers, final Issue acceptance and merge/close remain NotRun. Old buffer-mode coverage stays Blocked for40/80. Interactive console rendering is not covered by redirected stream evidence. Existing production implementation limitations and full compatibility consumer inventory remain those of unchanged slice1-report/spec; this dispatch changes no production acceptance contract beyond documenting the isolated window-width dimension.

Commit scope is exactly the four owned paths above, using command-scoped Kakusya identity; no push/merge/close/archive or resource release. Main must independently audit this followup and dispatch genuine Cooking validation separately.
