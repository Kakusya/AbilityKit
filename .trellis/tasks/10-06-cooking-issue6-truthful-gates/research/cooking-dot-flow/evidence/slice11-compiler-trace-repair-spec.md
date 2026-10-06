# Slice 11 compiler trace repair assignment

## Authority and exact source

- Dot request `AK-I6-COOK-COMPILER-20261007-01` approved this direction for reviewed packet `c84cc3687351a975c060335f907de46231dfced5`; gate implementation source is `665068ae71100328cf6344b8d6c2a6fa74e968bd`.
- Full decision: `dot-compiler-contention-reply-raw.txt`.
- Preflight isolation: `slice11-preflight-writer-isolation.json` Passed. Old resources remain retained; do not touch them or the old run directory.
- Cooking only. No Shooter, MOBA, Orleans, Unity runtime/editor, business/test projects, dependencies, SDK upgrades, package downloads, test scope changes, or ET version changes.
- You are not alone in the worktree. Preserve coordinator evidence and all unrelated dirty files. Never reset, clean, revert, amend, rebase, push, merge, close resources, or change Git configuration.

## Owned files

You may modify only:

1. `tools/test-gate-result-contract.ps1`
2. `tools/tests/test-gate-result-contract.tests.ps1`
3. `tools/tests/fixtures/gate-result-fake-dotnet.ps1`
4. `tools/tests/fixtures/gate-result-model.ps1`
5. Optionally add `tools/test-gate-compiler-events.cs` only if the current SDK's built-in structured event API requires it; keep it a narrow event reader.
6. Add `.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/slice11-compiler-trace-repair-report.md`

Do not edit the workflow skill, runner entry, `tools/test-gates.json`, any csproj, or any other task/evidence file. Coordinator owns manifests and flow records.

## First checkpoint: bounded feasibility

Before implementation, prove whether the current installed SDK can expose the native build's structured MSBuild events and project-instance/CoreCompile/Csc identity using only SDK-shipped assemblies/capabilities. Prefer binlog/structured events. A generated temporary tool project/target may live only under an owned temporary directory. Do not download packages or install tools.

Send the coordinator one checkpoint with the exact approach and evidence. If the SDK cannot provide this without a new dependency, stop `blocked` with the precise missing capability. Do not replace it with directory scanning, text-log guesses, project-path closure counts, or trace self-reporting.

## Required implementation contract

- Every native build gets a unique invocation ID and a new owned run/result/build-invocation directory that must not preexist.
- Every actually executed capture gets a collision-free capture ID created at target execution. Each project instance writes only its own temporary record, closes and verifies it, then atomically publishes one completion record. No shared append and no overwrite.
- Bind each record to run/result/invocation/capture, normalized full project path, actual TFM/configuration and distinguishing instance properties; record every input's normalized path, byte length, and SHA-256.
- Validate evidence paths remain in the owned root with link/reparse escape rejection. Reject stale run material, collisions, duplicate capture IDs, partial/malformed/locked files, identity substitution, and conflicting hashes for one normalized path.
- Use the native build's structured MSBuild events as the independent list of actual project instances and CoreCompile/Csc executions. Starting from those events, require one matching completion record per required execution, then reject every extra/unowned record. Capture messages may correlate IDs but cannot establish completeness alone.
- Preserve separate instance records even when project paths or inputs repeat; final input members may be deterministically deduplicated only after instance coverage is proven. Treat incremental skips explicitly using current-engine event plus valid output identity.
- Aggregate only after native exit is confirmed and all event/log/capture streams are closed. Native0 plus evidence failure remains Failed with native0 truthful; native nonzero remains its real exit; unknown ownership is Blocked; unrun tests remain NotRun.
- Bind raw event evidence, generated target, all captures, full instance manifest and aggregate producer receipt to the trusted invocation before candidate JSON. Executed and Reused verification must keep this independent basis.
- Raw path/property logs remain local evidence. Published report contains bounded summaries and hashes, no config contents. Do not embed imported file contents in binlogs.

## Required controls

Preserve all original `249` control names, expectations and historical failures. Report `249 + N` with every added control named and its expected/actual result.

Add real MSBuild fixtures for two parallel projects, diamond references, the same project under different global properties, legitimate repeat instances/inputs, generated inputs and incremental skips. Use a synchronization barrier to force overlapping target execution: preserve an old shared-append failure reproduction, prove the new design succeeds while still parallel, and prove a held completion file fails within a bounded interval.

Negative controls must reject:

- an entire instance trace and corresponding candidate entry removed with fingerprints recomputed;
- a second real CoreCompile instance deliberately bypassing the capture hook while all remaining traces/messages are self-consistent;
- one input removed or substituted;
- trace/project/TFM/run/result/invocation identity replacement;
- stale records, duplicate IDs, extra files, partial writes, malformed event logs, missing end events, path/reparse escape and conflicting content for one normalized path;
- Executed and Reused producer-source omission or substitution.

Keep positive controls for ordering, normalized paths, legitimate duplicate instances/inputs and valid reuse. The fake-dotnet fixture validates protocol behavior only and must not replace real MSBuild concurrency coverage. Test any event reader from raw events to its output manifest.

## Finish boundary

Run only the focused controls needed to implement this assignment, then the complete isolated control suite. Do not run either genuine Cooking gate in this source worker. Do not commit or push. Stop after:

- the full control suite passes with exact `249 + N` accounting;
- the real concurrency fixtures pass and old failure reproduction remains an explicit historical failure control;
- only owned files differ;
- the report records commands, native exits, tool/SDK version, source SHA/dirty qualification, coverage counts and limitations;
- `worker_done` is sent exactly once with Task and Dispatch IDs and outcome.

The coordinator will independently inspect, commit/freeze, and dispatch a distinct verifier for full controls plus both original Cooking gates serially.
