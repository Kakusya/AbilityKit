# Slice B2a handoff — bounded benchmark/common/Unity contracts

Authorized checkpoint: approved plan 5dc303a809, clean B1 base 56ebd215d496554d6a9dd7406b4c1f1d0897958a, dispatch task_45d4d15c6e4a / ctx_d02f9b96eea7. Issue 6 and this task remain in_progress. This slice changes only its owned tooling, mirror property forwarding, durable fixtures and current task records; no service adapter, gameplay, CI policy or dependency repair is included.

## Implemented contracts

The runtime and AOI wrappers now produce receipts from their actual bodies. Entry-level fake native executors exercise evaluation/restore/build/run before any workload can launch. Production gate execution captures explicit build/restore provenance and runs the bound binary with --no-build/--no-restore. Standalone wrappers preserve their original native exits and implicit dotnet run arguments. Runtime completes measurement coverage only; AOI retains the existing smoke/full matrix and thresholds, mapping native threshold rejection 2 to Failed/CLI 1 with native 2 preserved. The helper validates current report identity/schema/profile/module/scope, exact relevant case coverage, nonzero samples/operations, outcomes and sample/summary or threshold contradictions. Raw failed reports and native logs remain archived.

The sole test-gates.json registry declares concrete expected configuration/TFM/host/policy/scope, explicit evidenceType/provenance projects and benchmark report contracts. A deterministic resolver binds dimensions and configured argument hashes without environment probing or result-name inference. Receipt acceptance rejects unresolved tokens and mismatched evaluated project/configuration/TFM, invocation arguments/filter and Unity host/filter/policy. All seven supported kinds retain their own evidence rules, including gate aggregation and the supported, currently unconfigured execute-method kind. Partial/uninvoked nodes keep incomplete coverage and honest status. Existing gate kind/project/filter/required/optionalReason/ciPolicy declarations were compared to the B1 base with zero differences; source thresholds are unchanged.

Installed Unity mirror and Unity execution require explicit abilitykit.unity-build-proof.v1 provenance: current run/source/configuration, source before/after, exact editor version/identity, successful preceding editor compile command/log/times/exit and archived/current before/after assembly identities. Later existence/hash capture alone cannot pass. Missing installation remains Blocked or the exact optional MissingUnityInstallation conversion; missing project binary proof remains Blocked even for optional installations. Malformed/mismatched proof fails. The six mirror project references forward the same editor/platform properties. EditMode/PlayMode normalize actual raw NUnit counts and declared loaded assembly/filter identity; execute-method requires an instrumented method writing abilitykit.unity-execute-method.v1 via the explicit result-context/result-file CLI arguments. Occupied Unity project locks remain Blocked and untouched.

No production Unity proof exporter or actual validated Unity environment is produced here. To unblock production, a separately authorized instrumented current-source Unity compile must supply that full proof before mirror/test invocation; an execute-method producer must also write its declared structured completion. Synthetic controls are example=true and cannot grant production acceptance. No real .NET build/test/run, Unity, service/network, cleanup, soak, performance or online CI was executed.

## Three independent frozen-B1 audit fixes

Coordinator messages msg_37c967ac012d and msg_ff7589713e67 were read with retained D: originals, then reproduced through C:-owned fixtures. Serialized PSCustomObject/dictionary binding lookup now retains nonempty IDs through nested aggregation and rejects empty bindings. Canonical status/kind/root vocabulary is case exact; a lowercase producer status is rejected by the actual runner, whose normalized root is schema validated, as is the real three-body report chain. Native stdout/stderr capture now reads redirected streams directly instead of formatting PowerShell ErrorRecord objects; width 40/80/180 controls preserve the exact assertion text and native exit. Root-owned probes were never executed or mutated.

## Retained evidence and migration list

Keep complete raw artifact trees, controls.json files, invocation stdout/stderr, reports and these top-level logs together when evidence is migrated. Nothing below was deleted or overwritten:

- local/Artifacts/b2f-01 + local/b2f-01.log: earlier count/path binding failures.
- local/Artifacts/b2f-02 + local/b2f-02.log: fixture report scope and stale configured-kind count failures.
- local/Artifacts/b2f-03 + local/b2f-03.log: synthetic source identity failure.
- local/Artifacts/b2f-04 + local/b2f-04.log: fixture call overwrite, report aliasing and array-expression failures.
- local/Artifacts/b2f-05 + local/b2f-05.log: raw cases count 170 total, 169 Passed/executed, 1 NotRun, 0 failed; fullControlSuiteAccepted=false. This corrects the discarded 166/4 verbal count, per msg_5b1983e2cc50 and user correction.
- local/Artifacts/b2u-01 + local/b2u-01.log: repeated fixture directory index failures.
- local/Artifacts/b2u-02 + local/b2u-02.log: runner failure-path null log path failures.
- local/Artifacts/b2u-03 + local/b2u-03.log: Unity focused suite, 225 total, 40 Passed/executed, 185 NotRun, 0 failed; fullControlSuiteAccepted=false.
- local/Artifacts/b2a-focus1 + local/b2a-focus1.log: 232 total, 7 failed; normalized catch singleton arguments and standalone fixture variable scope failures.
- local/Artifacts/b2a-focus2 + local/b2a-focus2.log: 232 total, 99 Passed/executed, 133 NotRun, 0 failed; fullControlSuiteAccepted=false.
- local/Artifacts/b2a-freeze01 + local/b2a-freeze01.log: final frozen full receipt, measured below.

D:/MyWorkTree/AbilityKit/local/Artifacts/issue6-coordinator-audit remains coordinator-owned, including b2-preparation.md, canonical-status and b1-56ebd215d originals. B1 frozen full receipt local/Artifacts/i6-236e99fb-a287-48f3-94ee-337faa908ad6/controls.json remains retained with its historical 133/133/0 and fingerprint a5e3a20fdbf04f2d6cec027e5dc5550bf62502d6c7d25618819148a7b8cd0eb9; it is not substituted for B2a verification.

## Remaining B2b/C

B2b owns run_shooter_multiprocess_smoke, test_shooter_multiprocess_ownership_cleanup, run_moba_smoke and run_moba_multiprocess_smoke real-body adapters, their internal build/NoBuild/owned-process proof, final test-spec documentation and StepName consumer compatibility. They remain fail-closed and do not gain completed coverage from concrete declarations. Original UPM direct-dependency and hero scheduled-only ciPolicy failures remain Failed and untouched.

C follows code freeze and coordinator serial authorization: run the five-project real runtime-contracts gate on exact committed clean source with nonzero TRX, complete assets/build/loaded binary identity, source before/after, command/tool/native exit receipts and preserved failures. Coordinator independently audits this frozen B2a commit. This checkpoint neither accepts all of Issue 6 nor authorizes merge, push, archive or worktree deletion.
## Frozen full validation

Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1 -ArtifactRoot local/Artifacts/b2a-freeze01. Raw controls.json records status=Passed, total=232, executed=232, failed=0, observedExitCode=0, fullControlSuiteAccepted=True. Raw case status counts: Passed=232. Receipt SHA256: 0b48df7f1dcb929b13510fd3c7087300f0a408c5c4f435fa939a10021cb5d21d.

Actual sourceBefore SHA=56ebd215d496554d6a9dd7406b4c1f1d0897958a, dirty=True; source before/after input fingerprint=6bf4a2a10abee397fc8b652cb3285610015e3b2597c015a730d2f9a4db00097e / 6bf4a2a10abee397fc8b652cb3285610015e3b2597c015a730d2f9a4db00097e. Source stability and separately captured fixture/harness input stability passed. Tool versions: PowerShell 5.1.26100.9444, Python Python 3.14.8. The code and fixture inputs were frozen throughout this full run; subsequent changes are only excluded current task records. This is full isolated contract-control acceptance, not real workload or final Issue 6 acceptance. Real dotnet gates, Unity Editor, services/network smoke, soak, performance and online CI remain NotRun.

Focused b2a-focus2 and b2f-05 counts were re-read from original JSON rather than inherited verbal summaries. Owned PowerShell files parse without errors and git diff --check passes. An exploratory parser scan of unrelated tools encountered pre-existing encoding/parser failures outside this slice; those files were untouched and are not claimed validated. No implementation blockers remain in B2a; production Unity prerequisites and the exact B2b/C work remain as described above.
