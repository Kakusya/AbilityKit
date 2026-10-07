# Slice 16 compiler input and target attribution repair report

Date: 2026-10-07

## Authority and scope

- Dot request `AK-I6-COOK-FINAL-20261007-02` returned `changes-required` for exact candidate `2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc` and authorized this bounded repair.
- The checkout started at that exact candidate on branch `Kakusya/issue6-compiler-input-target-repair-ccswitch`.
- The concrete consumers are only the approved `cooking-et-level-runtime` and `cooking-kitchen-loop` gates. Their compiler-input closure and CoreCompile ownership checks require these shared tool changes.
- Implementation/control scope is exactly five files: `tools/test-gate-compiler-events.cs`, `tools/test-gate-result-contract.ps1`, `tools/tests/test-gate-result-contract.tests.ps1`, `tools/tests/fixtures/gate-result-model.ps1`, and the fixture-protocol update in `tools/tests/fixtures/gate-result-fake-dotnet.ps1`.
- No gate declarations/default gate, runner, Unity, SDK, dependency, ET version, gameplay, service, benchmark, Shooter, MOBA, Orleans, Issue/PR, workflow-record, or succession files were changed.

## Repair A: compiler input closure

The event reader previously discarded relative Csc file inputs and inputs absent when the binlog was replayed. It now accepts only the explicit Csc file parameter categories `Sources`, `References`, `Analyzers`, `AdditionalFiles`, `AnalyzerConfigFiles`, `EmbeddedFiles`, `Resources`, `LinkResources`; scalar parameters are not treated as files. Each relevant input retains its parameter name, original `ItemSpec`, project-directory-resolved absolute path, byte length, and SHA-256. An ItemSpec that is missing, cannot be resolved, is absent, or cannot be read makes the event manifest incomplete and fails closed.

Compiler evidence schema is now version 3. Each executed instance records `taskInputs` and binds every Csc-consumed input to exactly one captured compiler input with the same normalized path, length, and hash. Incremental-skip instances carry no task inputs.

## Repair B: CoreCompile target attribution

Project-instance correlation and target ownership are now distinct. Capture/project events use submission, node, project-instance, and project-context identity, while CoreCompile/Csc correlation additionally requires the exact `targetId`. Thus a Csc in a sibling target of the same project instance cannot satisfy the finished CoreCompile, while independent capture events remain correctly associated at project-instance scope.

## Pre-fix reproduction

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s16-prefx-red01 -CompilerTraceOnly
```

Native script exit was `1`: `45` executed, `37` Passed, `8` Failed. Expected rejection controls were falsely accepted for a generated relative input introduced after capture, a Csc-consumed input deleted before replay, relative source omission, relative source replacement, and a Csc-only `targetId` mismatch. The valid-relative and scalar-category positives also failed because the required identities were not preserved. The first real sibling-target fixture did not yet establish the intended native-positive setup and failed its control; this fixture was corrected before final verification without weakening the negative assertion.

Receipt: `local/Artifacts/issue6-cooking-slice1-s16-prefx-red01/controls.json`, length `157342`, SHA-256 `6bc92cbfb9c68476af1c64eb4a8a9ce67443f810e82e7331b9b86e34676fe266`. Source was SHA `2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc`, dirty `true`; before/after input fingerprint was `caff33c0fc3475bd45ab6b336b01e12b05fe1df95cb49076c3cf09071bf65155`.

## Intermediate failures retained

- `focused01`: native script exit `1`, `5/5` failed. An overly broad same-named task-parameter interpretation aborted the compiler fixture path. Receipt SHA-256 `911b322ab75b28dc218a95edb916506ec7bf9cc828034810f384bfead7c171c7`.
- `focused02`: native script exit `1`, `44/45` passed; `real-sibling-target-csc-cannot-satisfy-corecompile` failed because the fixture output/reference setup was incomplete. Receipt SHA-256 `4ad202cb315b102d316b75ba67dd2e2004937c2d4dff20d03cd490a4b2df213a`.
- `focused03`: native script exit `1`, `44/45` passed; the same sibling fixture still lacked a valid output arrangement. Receipt SHA-256 `de3f37b8caf6e8e9e47d667c9f9b2a82ca619cec70fd22a83519931c7a20277f`.
- `full01`: native script exit `1`, stopped after `57` executed with `54` passed and `3` failed because schema 3 evidence omitted `eventManifestArtifact`. The failing controls were `mixed-own-TFMs-and-hash-only-config-positive`, `relative-sdk-task-registration-positive`, and `recorded-generated-transition-is-not-authored-source-change`. Receipt SHA-256 `aa6172547b09f35a2c3b3361a5db9b6155d20c2184588255e3b673f414d11959`.

These results remain failures and are not relabeled by the final green runs.

## Named Slice 16 controls

1. `valid-relative-source-input-positive`
2. `scalar-compiler-parameters-not-classified-as-files`
3. `generated-relative-source-after-capture-before-csc-rejected`
4. `csc-consumed-input-missing-before-replay-rejected`
5. `relative-source-input-omission-rejected`
6. `relative-source-input-replacement-rejected`
7. `csc-only-target-id-mismatch-rejected`
8. `real-sibling-target-csc-cannot-satisfy-corecompile`
9. `corecompile-target-normal-parallel-positive`
10. `corecompile-target-multi-instance-positive`
11. `corecompile-target-incremental-skip-positive`

The sibling fixture performs a real restore and real build. Restore exits `0`; the build also exits `0`, produces one CoreCompile and one Csc under the same project instance with different target IDs, and the evidence validator rejects the attempted ownership substitution.

## Final verification

Focused compiler controls:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s16-focused04 -CompilerTraceOnly
```

Native script exit `0`; `45/45` Passed, `realDotNet=Executed`, `contractControlsAccepted=true`. Receipt: `local/Artifacts/issue6-cooking-slice1-s16-focused04/controls.json`, length `301300`, SHA-256 `bd41d92e85fdd2cf1c926cde6301e77ee6b9a5c7af66410fb31ebc75a84840de`.

Complete isolated contract suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-s16-full02
```

Native script exit `0`; `301/301` Passed, exactly `290 + 11`, with `301` unique names and no duplicates. Console-width coverage Passed, `realDotNet=Executed`, `contractControlsAccepted=true`, and `fullControlSuiteAccepted=true`. Receipt: `local/Artifacts/issue6-cooking-slice1-s16-full02/controls.json`, length `2916369`, SHA-256 `94f8435706c219c3480744626a187fb38e13d23f825767c750d5fba63d9622b4`.

Both final runs recorded source SHA `2bcb6d8165fd5abaa9469d9bc1462a7f3781d2dc` and dirty `true`. The complete run's before/after implementation-input fingerprint was identical: `6928cfd88c6b17be2e2f74277d466a073581530d344832e3af597cc0e18563e7`.

PowerShell parsing passed for all four modified `.ps1` files. `git diff --check` passed. The final pre-report working-file identities were:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `tools/test-gate-compiler-events.cs` | 15742 | `b51f764c4000876fca95a5e79d78a9f0dc13a954c92aa931976ca96494bce433` |
| `tools/test-gate-result-contract.ps1` | 110922 | `58c7b3771078c46b124c840744b17aa8fd57b12f91a1567114d84e25966ebdb6` |
| `tools/tests/test-gate-result-contract.tests.ps1` | 102356 | `5b8f33b6fc5e7a55f22c59df362adb6bf33730cd11f299d8bb15da3654eaa55e` |
| `tools/tests/fixtures/gate-result-model.ps1` | 13233 | `0f9507016caf79078d79e3323b99fd9f1080ed64c96574eb01e0110fe68f3c48` |
| `tools/tests/fixtures/gate-result-fake-dotnet.ps1` | 15553 | `4dc22ecf88e4ea392ca04b45677a340921f16e147b9435a7c1933194db71214b` |

The genuine `cooking-et-level-runtime` and `cooking-kitchen-loop` gates are `NotRun` in this source-worker dispatch by explicit stop boundary. Unity, physical LAN, services, benchmarks, and unrelated gates are also `NotRun` or N/A. A fresh independent verifier must run both genuine Cooking gates only after coordinator review and integration of the frozen implementation commit.
