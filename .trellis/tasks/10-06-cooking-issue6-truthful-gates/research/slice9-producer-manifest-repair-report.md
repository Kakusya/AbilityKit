# Slice9 — independent producer membership repair

Task `task_9bfd5b74eaa6` / Dispatch `ctx_ccde18aea1d0`, sole Run `run_5ba664a5f016`. Dot request `AK-I6-COOK-REVIEW-20261006-03` reviewed source **3490ec80c207161aa3fd5967f6aa0ab71ba7bdc6** and required this bounded repair. The precise coherent SECONDARY omission was actually accepted on that original source; final isolated controls now reject it against a separately pinned producer receipt. Final affected **55/55** and full **249/249** completed Passed/native0/confirmed exit, retaining all **216 original group/name/expected controls**. These are synthetic isolated controls only; new-source genuine Cooking gates, main independent acceptance, dot final acceptance and delivery remain **NotRun**.

## Exact ownership and implementation

Only `tools/test-gate-result-contract.ps1`, `tools/tests/test-gate-result-contract.tests.ps1` and this report are changed/committed. Both existing Cooking gates consume the helper through the unchanged runner; the harness exercises the same runner and verifier boundaries. Runner, gate config, all fixtures, documentation, gameplay/projects/packages/SDK/ET/Unity/examples and other main Task/flow records are untouched by this dispatch. Main Task/progress/flow dirt is preserved separately, not included in the seven runtime control inputs. [scope-final.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/scope-final.json) compares all30 non-Cooking gate objects and `defaultGate` against619883e8a18027a1c9f7419f84023eb60c5f9466: unchanged.

The helper reuses the existing `stage-receipt.json` producer archive, `Add/Assert-GateArtifact`, `Assert-GateBinaryIdentitySet`, source fingerprints, stage/native verification and explicit reuse receipts. The producer now saves that archive once before `Assert-GateProvenance`, recording its assigned result identity, contained path, bytes/hash and source SHA/fingerprint in a trusted invocation-context map outside candidate JSON. Finally preserves partial valid restore receipts without rewriting an already pinned archive. The verifier requires that exact independent archive/reference, validates owner/hash/source/config/project/TFM/SDK and build mode/owner, then reads expected output membership from the immutable archived producer, never from the four candidate lists. Reused build origin is fixed to the independently saved manifest reference; no candidate-selected substitute is accepted. Existing build/loaded/test set comparisons then enforce the full consumer chain.

Windows canonical path+bytes+SHA comparisons remain insensitive to order, case/slashes and independently valid archive-copy paths/roles. The primary-assembly guard now uses BCL `Path.GetFullPath`, following two legitimate normalization positives that initially failed. Reused build output fingerprint comparison now uses the independently anchored complete canonical set; every stage still rederives its own declared fingerprint, and restore/output plus input reuse fingerprint guards remain. Restore/build/test argv, native/source/binary/config proofs, legal NoBuild/NoRestore stage-credit semantics and unsupported preflight refusal remain enforced. No new runner, producer registry, dependency or unrelated adaptation was introduced. Function provenance: existing verifier and archive facilities are those delivered at b8c53e8 and unchanged at3490ec8; slice1-report preserves their older selective reuse origins. No retained import/config/tool/trace/TRX dependency was removed.

## Reproductions, commands and native evidence

The original red removed `dependency.dll` beside the primary test assembly from build.outputs/loadedBefore/loadedAfter/test.inputs, retained the primary, recomputed build.outputFingerprint/test.inputFingerprint, serialized candidate.json and read it back into the actual verifier. Trusted plan hash, source/TRX/native receipts, actual binaries and every immutable archive stayed unchanged. Expected accepted=false, actual=true; standalone native1/confirmed exit. [red01/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red01/controls.json) preserves the complete before/after archive ledger and original actual result.

A separate actual read-only serialized probe found Reused build stage runId/resultId could change while the original pinned producer/archive stayed intact: two expected-false/actual-true controls, native1. [reused-stage-owner-red-controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/reused-stage-owner-red-controls.json), [reused-stage-owner-red02-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/reused-stage-owner-red02-native.json), [reused-stage-owner-red02.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/reused-stage-owner-red02.stdout.txt). Final membership now also compares candidate build IDs with the independent producer. Four added Executed/Reused stage-owner controls reject those mutations. No source was edited during any frozen affected/full run; the first full245 result remains valid for its own superseded inputs, not final-source acceptance. Frozen source copies remain in [full01-source-snapshot/source-pins.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full01-source-snapshot/source-pins.json) and full02-source-snapshot.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red01
powershell -NoProfile -ExecutionPolicy Bypass -File local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected01
powershell -NoProfile -ExecutionPolicy Bypass -File local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected02
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full01
powershell -NoProfile -ExecutionPolicy Bypass -File local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected03
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02
```

Each native receipt records actual executable/argv/working directory, PID, stage times, exit and confirmed process exit; direct stdout/stderr are separate, with no combined chronology or ErrorRecord formatting claim. Validator functions have nativeExit=null because they execute in the harness process; their actual disk candidates, unchanged assigned plan/context, reason and parent native receipt are retained. Runner controls include actual fake-subprocess launch logs and counts.

| Run | Actual controls | Native / exit confirmed | Complete receipts/raw |
|---|---|---|---|
| red01 | 1 counterexample accepted (expected refusal) | 1 / True | [red01-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red01-native.json); [red01/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red01/controls.json); [red01.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red01.stdout.txt); [red01.stderr.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/red01.stderr.txt) |
| affected01 | 49/51 Passed; two legitimate normalization positives refused | 1 / True | [affected01-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected01-native.json); [affected01/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected01/controls.json); [affected01.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected01.stdout.txt); [affected01.stderr.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected01.stderr.txt) |
| affected02 | 51/51 Passed; superseded by final build-ID guard | 0 / True | [affected02-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected02-native.json); [affected02/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected02/controls.json); [affected02.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected02.stdout.txt); [affected02.stderr.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected02.stderr.txt) |
| full01 | 245/245 Passed; superseded by final build-ID guard | 0 / True | [full01-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full01-native.json); [full01/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full01/controls.json); [full01.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full01.stdout.txt); [full01.stderr.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full01.stderr.txt) |
| affected03 | 55/55 Passed on final inputs | 0 / True | [affected03-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected03-native.json); [affected03/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected03/controls.json); [affected03.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected03.stdout.txt); [affected03.stderr.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/affected03.stderr.txt) |
| full02 | 249/249 Passed on final inputs | 0 / True | [full02-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02-native.json); [full02/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02/controls.json); [full02.stdout.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02.stdout.txt); [full02.stderr.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02.stderr.txt) |

Preserved failed scaffolds: reused-stage-owner-red01 native1 used the option token instead of its ContextPath value before any validator ran; corrected red02 produced the two genuine accepted counterexamples. full01-audit native1 initially selected legacy unsupported `producer-*` names as though they were new validator controls; its KeyError/raw/source snapshot remain, and full01-audit02 native0 verified245 controls and original216 retention. These are metadata/probe failures, not genuine .NET or gameplay failures. One rejected no-op apply_patch and read-only sharing-violation attempts against still-open captured streams changed nothing and are recorded in [live-read-limitation.txt](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/live-read-limitation.txt). Read-only git var GIT_AUTHOR_IDENT returned1 because identity was unset; command-scoped Kakusya identity is supplied for the owned commit, with no configuration changes; identity-probe-limitation.txt preserves this observation. Every earlier failure and superseded attempt remains retained; no cleanup/release occurred.

Final full native call: `2026-10-06T02:55:08.6912225+00:00` → `2026-10-06T03:10:06.7751587+00:00`, PID `83804`, native0/exit confirmed. Actual final command and all leaf counts/expected/actual/raw directories are in [full02-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02-native.json) and [full02/controls.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02/controls.json). Independent accounting: [full02-independent-accounting.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02-independent-accounting.json) / [full02-audit-native.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02-audit-native.json), original216 retained,33 added, zero accounting issues.

| Group | Executed / Passed |
|---|---:|
| restore-graph | 13 / 13 |
| restore-graph-json | 10 / 10 |
| restore-graph-reuse | 6 / 6 |
| imports | 2 / 2 |
| valid | 2 / 2 |
| original-red | 1 / 1 |
| trx | 16 / 16 |
| environment | 1 / 1 |
| failure | 1 / 1 |
| source | 2 / 2 |
| timeout-cancel | 2 / 2 |
| coverage | 6 / 6 |
| selector | 2 / 2 |
| unsupported | 5 / 5 |
| config | 5 / 5 |
| identity | 1 / 1 |
| reuse | 29 / 29 |
| validator | 103 / 103 |
| production | 1 / 1 |
| compatibility | 32 / 32 |
| native-streams | 3 / 3 |
| native-width-bindings | 4 / 4 |
| paths | 1 / 1 |
| freeze | 1 / 1 |

## Added producer association controls

All33 new controls operate on actual accepted runner Executed/Reused/NoRestore receipts, serialize/reload the candidate at the verifier boundary, and retain trusted plan/archive invariance per case. Negatives cover coherent secondary omission/replacement by another valid owned archive, missing proof/reference, wrong run/result/source/fingerprint/hash/length, swapped proof/reference and current build owner. Positives cover ordering/copy roles, coherent Windows normalization, existing actual NoBuild and NoRestore after a failed build. Context-corruption controls preserve the original trusted context snapshot and deliberately change only the supplied proof expectation; immutable producer archives are not rewritten.

| Name | Expected accepted / Actual | Plan+archives unchanged | Actual verifier outcome |
|---|---|---|---|
| producer-Executed-wrong-stage-runId | False / False | True | Stage stale owner. |
| producer-Executed-wrong-stage-resultId | False / False | True | Stage stale owner. |
| producer-Executed-coherent-secondary-omission | False / False | True | Binary identity membership mismatch: independent producer -> build.outputs |
| producer-Executed-coherent-secondary-replacement | False / False | True | Binary identity mismatch: independent producer -> build.outputs |
| producer-Executed-missing-proof | False / False | True | Independent producer receipt missing. |
| producer-Executed-wrong-run | False / False | True | Stale artifact owner. |
| producer-Executed-wrong-result | False / False | True | Stale artifact owner. |
| producer-Executed-wrong-source | False / False | True | Independent producer source mismatch. |
| producer-Executed-wrong-source-fingerprint | False / False | True | Independent producer source mismatch. |
| producer-Executed-wrong-hash | False / False | True | Missing/replaced artifact. |
| producer-Executed-wrong-length | False / False | True | Missing/replaced artifact. |
| producer-Executed-swapped-proof | False / False | True | Independent producer receipt reference mismatch. |
| producer-Executed-swapped-candidate-reference | False / False | True | Contract mismatch: trusted producer receipt reference |
| producer-Executed-missing-candidate-reference | False / False | True | Independent producer receipt reference missing/ambiguous. |
| producer-Executed-positive-ordered-copy-roles | True / True | True | Accepted valid runner proof |
| producer-Executed-positive-coherent-normalized-paths | True / True | True | Accepted valid runner proof |
| producer-Reused-wrong-stage-runId | False / False | True | Independent producer run/result/mode mismatch. |
| producer-Reused-wrong-stage-resultId | False / False | True | Independent producer run/result/mode mismatch. |
| producer-Reused-coherent-secondary-omission | False / False | True | Binary identity membership mismatch: independent producer -> build.outputs |
| producer-Reused-coherent-secondary-replacement | False / False | True | Binary identity mismatch: independent producer -> build.outputs |
| producer-Reused-missing-proof | False / False | True | Independent producer receipt missing. |
| producer-Reused-wrong-run | False / False | True | Stale artifact owner. |
| producer-Reused-wrong-result | False / False | True | Stale artifact owner. |
| producer-Reused-wrong-source | False / False | True | Independent producer source mismatch. |
| producer-Reused-wrong-source-fingerprint | False / False | True | Independent producer source mismatch. |
| producer-Reused-wrong-hash | False / False | True | Missing/replaced artifact. |
| producer-Reused-wrong-length | False / False | True | Missing/replaced artifact. |
| producer-Reused-swapped-proof | False / False | True | Independent producer receipt reference mismatch. |
| producer-Reused-swapped-candidate-reference | False / False | True | Contract mismatch: trusted producer receipt reference |
| producer-Reused-missing-candidate-reference | False / False | True | Independent producer receipt reference missing/ambiguous. |
| producer-Reused-positive-ordered-copy-roles | True / True | True | Accepted valid runner proof |
| producer-Reused-positive-coherent-normalized-paths | True / True | True | Accepted valid runner proof |
| producer-NoRestore-positive-original-build-failed | True / True | True | Accepted valid runner proof |

## Actual native window widths

Dimension remains `RawUI.WindowSize.Width`, requested40/80/120; setters succeeded, distinct applied/readback widths matched, original height50 and buffer120/3000 preserved. Each real owned fake probe exited7 with exact8192-character stdout and stderr payloads, streams redirected and exit confirmed. This is native stream capture/RawUI dimension proof, not interactive rendering. Old BufferSize40/80 Blocked evidence is unchanged and not promoted.

| Requested | Applied / Observed | Window height | Native / exact streams |
|---:|---:|---:|---:|
| 40 | 40 / 40 | 50 | 7 / True |
| 80 | 80 / 80 | 50 | 7 / True |
| 120 | 120 / 120 | 50 | 7 / True |

## Frozen source, scope and preservation

Initial implementation source was clean at3490ec8; main-owned task/flow/progress records were already dirty. Both final before/after control sources are explicitly **dirty3490ec8 plus the two exact owned source edits**; they are not a clean later commit. Source fingerprints are equal: `a007200710a9b6f854dd660fe2ef6f49863f69a3ac7a407d96e36d799140477c`. Main metadata dirt before/after is retained verbatim in the full receipt and [before-full02.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/before-full02.json) / [after-full02.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/after-full02.json); dirtyDetails counts 144/182. Exclusions: ignored local evidence, unique synthetic TEMP fixtures and coordinator task records, not arbitrary real compilation files. No real .NET writer existed at either launch or after completion; [full02-preflight-writers.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/full02-preflight-writers.json) / [final-checks.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/final-checks.json). No genuine .NET build/test/restore, Unity/example/service/benchmark, installation, remote write, subagent or process cleanup was performed; timeout/cancel controls stop only their owned fake process and verify exit.

Actual tool host: PowerShell `5.1.26100.9444`, CLR `4.0.30319.42000`, culture `zh-CN`, `git version 2.56.0.windows.1`; Python version/path are in scope-final.json. No SDK upgrade/discovery command was run against real dotnet; SDK10.0.300 labels inside control fixtures are explicitly synthetic.

| Frozen input | Working bytes | Working SHA256 | Git-normalized bytes / SHA256 |
|---|---:|---|---|
| `tools/run_test_gate.ps1` | 15192 | `575469c4238e29cf41b5826a43748d38c9c81578793261835b6e51dc3a886e8f` | 15192 / `575469c4238e29cf41b5826a43748d38c9c81578793261835b6e51dc3a886e8f` |
| `tools/test-gate-result-contract.ps1` | 81230 | `1ad2907cdac147ae1f90c81eba9635aff55b8b38353a85731c07fc7b2de6d954` | 80280 / `4aafa0d736e6293c6a227ad5f109e8ea49ddc92ccb2a8b29a5110d7aa00897f9` |
| `tools/test-gates.json` | 63749 | `85111d00c4a3a61c87f61da3e43d159da60e07d058406f7049e17495e7e520a1` | 63749 / `85111d00c4a3a61c87f61da3e43d159da60e07d058406f7049e17495e7e520a1` |
| `tools/tests/test-gate-result-contract.tests.ps1` | 61383 | `19582a4bdcfac2a44ab37c2978910108adf37b4ce5b2e8ea7b545f310078725f` | 60892 / `c9b2d172cf7fee281e814b8130da420b725bbf11e91305ac724baa0939e24c6b` |
| `tools/tests/fixtures/gate-result-model.ps1` | 6941 | `bcb89b5a2feb31b033fa3dea8eee095f9b07155dec87d82cf01706f5a5e76d64` | 6869 / `338d8cc9ed30b3ee5ff1f60119225b90735816f005b43e6a5615f43f094289c1` |
| `tools/tests/fixtures/gate-result-fake-dotnet.ps1` | 11372 | `aa83a5f7ee3ec4efc225bc3ef6fbdafb08900021b55e65e7dde98fdac070a86e` | 11256 / `b67f1ebb72e9d5542a5cff48238dd32be739340c0c23b517666fd9ca92cc5b85` |
| `tools/tests/fixtures/gate-result-native-probe.ps1` | 5534 | `f3a1a95a96cb42f1c1c3dbc2cf77fb6df66d13855543a6332c576f56bd355bfe` | 5534 / `f3a1a95a96cb42f1c1c3dbc2cf77fb6df66d13855543a6332c576f56bd355bfe` |
| `Docs/AbilityKit测试门禁与批量回归规范.md` | 38135 | `02888aea27bc2fb4e3a5c60f9973c9b8d40e23cb6651af83b61af8a0832ced64` | 37684 / `853bcbc2bc530492dcf433af909867521130ae5eb2d78841850cf460597025ee` |

[before.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/before.json) and [after-full02.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/after-full02.json) check 95816 historical files, zero changes, including all real archived runs and complete slice7/slice8 local evidence. [final-preservation.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/final-preservation.json) additionally checks original6241 failed-evidence manifest records, historical report pins and every prior owned red/affected/full01 file: zero changes. Parse/whitespace checks and no unintended writers are in [final-checks.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/final-checks.json); six other delivered source/doc inputs and all30 unrelated gates remain unchanged.

The final three-file commit's exact full SHA, parent, author, per-file raw/Git-normalized hashes, report hash and preserved main dirt are pinned after commit in [commit-receipt.json](../../../../local/Artifacts/issue6-cooking-slice1-producer-repair-09c42578/commit-receipt.json) and the exactly-once worker_done, avoiding a self-referential hash in this committed report. Tested working bytes must remain identical after commit; Git LF blobs are compared only after CRLF normalization, without claiming raw byte equality.

No unresolved blocker remains within this bounded repair. Main must independently audit this checkpoint, run both original genuine Cooking gates on the new committed implementation, obtain new exact-SHA dot final review and complete separate integration/delivery exits. All those exits remain NotRun; older b8 successful genuine gates do not verify this repair. No push, merge, Issue close/archive or resource release is authorized here.
