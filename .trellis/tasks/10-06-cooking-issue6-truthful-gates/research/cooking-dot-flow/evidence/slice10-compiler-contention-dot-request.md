# Issue 6 compiler-trace contention decision request

Request ID: `AK-I6-COOK-COMPILER-20261007-01`

## Exact candidate and scope

- Gate implementation source remains `665068ae71100328cf6344b8d6c2a6fa74e968bd`.
- The request packet commit will contain only workflow recovery, raw verification evidence, and this request. The browser request must name its full pushed SHA; no gate implementation changed after `665068a`.
- Cooking is the only approved consumer. Shooter, MOBA, Orleans, Unity runtime/editor work, dependencies, SDKs, and ET versions remain outside scope.
- Issue #6 remains open and blocked. PR #12 remains draft. No merge, close, archive, or resource cleanup is authorized by this request.

## Verified observations

On committed source `665068ae71100328cf6344b8d6c2a6fa74e968bd`, the independent verifier ran the full isolated control suite first:

- `249/249 Passed`, native exit `0`.
- Source before and after remained exactly `665068ae71100328cf6344b8d6c2a6fa74e968bd`.

It then ran the original first Cooking gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI
```

Gate run ID: `0b3b3cac-0bee-4ac9-a9e0-ea843a97fbc9`.

- `ET relation analyzer build`: Passed.
- `Cooking ET runtime build with relation analyzer`: Passed.
- `Cooking domain and Level lifecycle tests`: Passed, `867/867`.
- `Cooking ET Level host and runtime tests`: Failed during build, native exit `1`; the ET tests did not execute.
- Overall gate: Failed, CLI exit `1`.
- Original second gate `cooking-kitchen-loop`: NotRun.

The generated `compiler.targets` installs the same target into every project in the build graph and appends each project's compiler inputs to one node-level path:

```xml
<WriteLinesToFile
  File="$(AbilityKitCompilerTracePath)"
  Lines="@(AbilityKitHashedCompilerInput->'%(FullPath)|%(FileHash)')"
  Overwrite="false"
  Encoding="UTF-8" />
```

All project instances receive the same `-p:AbilityKitCompilerTracePath=.../compiler.txt`. During the ET Runtime Tests build, a referenced `AbilityKit.ET.RelationAnalyzer.csproj` instance concurrently attempted that append and MSBuild failed with:

```text
MSB3491: compiler.txt cannot be written because another process is using the file
```

The verifier later exhausted its provider quota. Dispatch `ctx_1fcdf6ecf3ae` / task `task_29beea8dc224` was abandoned and capability-fenced while retaining the terminal and files. It sent no `worker_done`; no report was fabricated, and no process or resource cleanup occurred.

Primary evidence:

- `slice10-gate1-main-failure-observation.json`
- `slice10-gate1-et-build-compiler-targets.xml`
- `slice10-gate1-et-build-failed-stdout.txt`
- `slice10-main-compiler-parent-proof.json`
- `slice10-worker-stall-evidence.json`
- `slice10-worker-abandon-raw.json`

## Decision requested from dot

Choose the bounded implementation direction that completely removes concurrent writes while preserving truthful compiler-input membership for the full project graph. Please explicitly decide among, or replace, these directions:

1. Serialize the shared append operations without losing any project instance's compiler inputs.
2. Write collision-free per-project or per-project-instance trace files, then deterministically aggregate and validate them before evidence capture.
3. Use another complete-closure mechanism that proves every actual compiler input represented by the executed build and remains independently mutation-testable.

The decision must specify:

- the exact files the Orca worker may edit;
- how paths remain inside the owned run/result directory and avoid stale or cross-run traces;
- how duplicate project instances and parallel builds are handled deterministically;
- when aggregation completes relative to native build exit and evidence hashing;
- fail-closed behavior for missing, malformed, partial, locked, or extra traces;
- positive controls plus a deliberate contention/omission/substitution negative control;
- whether the original `249` controls need extension and the required final count expectation;
- the required rerun: full controls, then both original Cooking gates serially on one committed SHA.

Existing invariants remain mandatory: independent producer output membership, exact source/dirty qualification, immutable raw logs, native exit truth, test count truth, stage identity/reuse validation, no hand-edited generated result, and no successful claim when a declared test does not execute.

Requested outcome is one explicit technical direction for an Orca supervised worker, or `changes-required` with the missing evidence. This is not a final acceptance request.
