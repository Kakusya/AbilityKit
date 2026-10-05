# 2026-10-05 Owner scope correction: original implementation stopped

Do not execute the old slices below. Only Cooking is authorized; no Shooter/MOBA/Orleans example code, scripts, tests or dedicated gates. B2b Shooter, MOBA and the old five-project runtime verification are stopped. Preserve prior commits and evidence, do not merge the broad branch or claim Issue completion. Shared follow-up needs a reviewed Cooking-specific scope. See [stop record](research/cooking-only-scope-stop.md) and AGENTS.

# Execution slices and required controls

Status: in_progress. Parent approved production implementation at clean 5dc303a809; exact source is in research/implementation-progress.md. Slice A has a full 86-control passing checkpoint; B/C and final acceptance remain pending. Parent msg_eb5f3bb83337 narrows this dispatch delivery to Slice A only; the next supervised dispatch owns B/C. No further dot gate.

## Slice A: shared contract and isolated runner controls

Implement thin helper plus existing runner normalization/aggregation, preserving TRX/Unity XML checks. Config remains sole coverage authority. Capture original skip→Passed red under unique fixture root before fixing, then same fixture demonstrates correct non-Passed. Validate identities, raw/derived exits, source/dirty pre/post fingerprints, paths/ownership, counts/times, set/binding equations and manifests. Summary/console/exit have one normalized source. Fake child execution proves fixture behavior only.

## Slice B: seven kinds and eighteen producer adapters

Adapt all six configured kinds plus unity-execute-method and all [eighteen inventoried paths](research/source-inventory.json), per [R4 compatibility contract](research/revision-contract.md). Add optional context/result parameters, preserve standalone CLI/native behavior, business and performance criteria. Capture internal and implicit builds/restores, including cleanup→Shooter NoBuild and MOBA host/client --no-build. Route ready polling/stdout/stderr/client/cleanup output to node UUID roots. Check Unity properties through six references.

For cleanup/MOBA and other service scripts, inject fake executor/probe/IO at script entry BEFORE loading process helpers, Stop-AbilityKitServices, PID/port discovery, Start/Stop-Process or dotnet. Assert zero real network/process/cleanup calls. Outer-runner stub alone is insufficient. Expected timeout is an assertion input with raw failure retained, never ignored Failed gate child. Use synthetic reports for measurement/native2 tests. Additional validate_shooter_test_gates consumer edits require explicit parent scope approval; document partial exit4 without adding CI.

## Slice C: committed-source real .NET and independent audit

Commit completed implementation after isolated checks, freeze exact SHA/clean status, then execute appropriate real .NET gate in parent-controlled serial window. Candidate runtime-contracts requires actual tool/scope review. One unique evidence root records SDK/runtime, command/exit, source/dirty pre/post fingerprints, actual entries/tests, raw TRX, binary/MVID/runtimeconfig/deps identities and normalized tree. Dirty concurrent inputs invalidate acceptance even if HEAD stays fixed.

Verification commands (full isolated suite executed; real .NET still NotRun):

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate runtime-contracts
~~~

Unity mirror only with actual prerequisites and approved coverage, else truthful Blocked/Skipped. Do not run Editor, services, soak or performance. Parent independently audits final committed diff, original red, controls and real evidence; merge/archive/cleanup are separate parent decisions.

## Required future automated controls

Each row requires named cases/raw evidence/actual assertions. This historical matrix defines required behavior; actual measured Slice A controls and pending producer coverage are separated in research/slice-a-handoff.md.

| Control | Expected normalized result |
| --- | --- |
| current valid receipt/TRX/binary and complete tree | Passed0; binding IDs resolve to valid Passed leaves |
| missing Unity required / optional | Blocked2 / raw Blocked2 retained under Skipped3 edge; .NET parent Passed0 |
| optional actual failure / malformed / source or manifest absent | Failed1 / Failed1 / Blocked2 or Failed1; no generic skip |
| missing tool / cannot launch | Blocked2 invocation time, no process time; later leaves NotRun |
| compile failure / timeout / cancel after launch | Failed1 with observed native exit and real times |
| cancel before invocation | leaf NotRun4 all times/command/process exit null |
| native0 missing/invalid result / missing or unknown field | Failed1 with raw diagnostic preserved |
| child Passed+native2 / Blocked+native0 / wrong canonical exit | Failed1 protocol contradiction; AOI native business2 explicitly maps Failed1 |
| empty artifact / malformed XML/TRX / zero declared test / failed TRX+native0 | Failed1; pure build/audit N/A unaffected |
| counters/outcomes/data-row IDs inconsistent / required skip | Failed1, no suite double-count |
| nonzero NUnit skipped/ignored | total3/passed1/skipped2/executed1; required coverage Failed1, raw skipped counters reconciled |
| old-run TRX / wrong SHA/run/result / overwritten DLL | Failed1, no arbitrary log recovery |
| dirty mid-run / same SHA different fingerprint | Failed1, source and binary pre/post hashes |
| nested required Failed / Blocked / partial | Failed1 / Blocked2 / NotRun4 |
| focus successful leaf+unselected / failure / required blocked | aggregate NotRun4 with actual invocation / Failed1 / Blocked2 |
| NoBuild/NoRestore missing / matching / corrupt/mismatched | Blocked2 / proceed with current build/restore NotRun / Failed1 |
| fresh restore creates or changes assets / immutable source changes | restore output change accepted / Failed1; immutable input fingerprint excludes generated outputs |
| reuse old restore assets / changed post-restore build inputs or binaries | Failed1; compare current reuse capture to the prior valid stage outputs and build inputs |
| cleanup internal build→NoBuild good/missing/host or binary mismatch | proceed / Blocked2 / Failed1, fake registry before script helpers |
| MOBA internal build→host+client --no-build | both manifest IDs/hash match; all logs/ready/cleanup run-owned; zero real launches |
| cleanup expected timeout / unexpected success / wrong stage / build failure | harness pass only exact failure+all cleanup assertions; otherwise Failed1 |
| measurement valid / absent / corrupt / wrong-run report | measurement-only token / Failed1 / Failed1 / Failed1; no benchmark launch |
| AOI valid / threshold native2 / wrong profile | threshold token / Failed1(raw2) / Failed1; unchanged criteria |
| backslash/mixed/drive/UNC/traversal/ADS/device/trailing-dot | rejected; slash-only positive accepted |
| sibling old root / symlink/reparse root or component | rejected; actual link control Blocked if unavailable, policy simulation distinct |
| same-name concurrent runs / repeated nested gates | exclusive UUID roots, no cross-binding/count duplication |
| all optional unexecuted / unselected-only optional / empty config | Skipped3 / Skipped3 / config Failed1 |
| example=true result or manifest in production | Failed1 before coverage acceptance |
| smoke/full StepName consumer | leaf may pass, aggregate4 partial only; Failed1/Blocked2 still fail; no CI claim |
| each of 18 scripts | parameters/results/early-exit/throw and unchanged standalone assertions tested |
| each of 7 kinds | current valid evidence + missing/malformed evidence controls |

Current task-local planning checks, counts and limitations are in [audit correction verification](research/audit-correction-verification.md); the [prior revision receipt](research/revision-verification.md) remains historical evidence. validate-planning.py checks its exact schema keyword subset, complete synthetic tree/artifact semantics and isolated policy invalid copies. It does not prove PowerShell runner controls, production report validators or general Draft2020-12 conformance. Task remains planning.
