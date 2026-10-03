# Independent timer matrix source review

2026-10-03. Reviewed frozen isolated source `b1a57d449` against `352114d2a` in cooking-network-cadence-o03. Seven files,214 additions/22 removals; tree clean at inspection. This is source review only: no .NET/build/socket/runtime grant exercised, no production adoption or network-exit acceptance.

## Findings

No concrete compile-source or normal lifecycle blocker found in this seven-file slice. Root may grant fresh BuildOnly plus separately bounded lifecycle controls; runtime success remains unproven.

- ProcessTimerScope validates exact OFF/ON, defaults OFF, refuses non-Windows/unknown/version major<10 or build<19041 ON before native Begin. Successful Begin records acquisition before telemetry allocation. Endpoint Run establishes outer finally before Acquire; each transport/Session/ET resource disposal is individually caught before timer Dispose. Begin failure causes no End, duplicate Dispose does not repeat End, End return/exception is retained and blocks successful endpoint exit. Historical BEGIN_SUCCEEDED is expressly not measured wake precision or global restoration.
- Reports are assembled after all resource/timer cleanup. Primary and cleanup exceptions remain distinct; failed export attempts a failed fallback and returns nonzero. Schema2 explicitly adds timer/cleanup/before-work/after-cleanup evidence. Original schema1 report files are unchanged; current wrapper requires schema2 for new runs.
- Strict CLI rejects unknown/duplicate/missing pairs before runner construction; process-timer defaults OFF. Both fresh endpoint argv and all supervisor identities include the same explicit option. ON wrapper checks actual Begin/End return0, chronological brackets, released lease and supported version; OFF requires zero native call evidence.
- TimerMatrix is separate from original Controls/Profile: exactly12 mid128 rows,3 repeats/cell, repeat2 reversed, planned/actual ordinals and UTC brackets, unique run IDs and accepted session instances. No silent retry or partial acceptance. Both endpoint native0 and genuine complete typed baseline/Session equality, exact ACK/Ready/current generation/channel-close checks remain.
- Existing owner Task.Delay10, inclusive producer5000-frame/30s,180s endpoint, bounded report/history/wire rules and all recipe/state paths are unchanged. Generic transport defaults15 remain untouched. Manifest inventories include the new source/control project; script fingerprint independently covers wrapper. New control project links the app timer/parser source and has no Cooking/ET/UDP references.

## Required execution evidence, not source acceptance

1. Fresh source/input/binary manifest and actual compiler/native receipt. No type-check/build PASS is inferred here.
2. Deterministic lifecycle controls cover OFF, exact1/1/idempotence, work exception, Begin return/exception, End return/exception, disposed-before-acquire, unsupported/unknown versions and strict CLI. Run and independently inspect all results. Controls currently test scope-level work failure and ExitCode; they do not inject actual ProfileRunner resource-disposal or final-export failures. Source nesting protects those paths, but endpoint integration failure evidence remains absent and should be added or explicitly inspected before broad runtime acceptance.
3. Real Windows no-socket Begin/End control with original numeric receipts/native0. Its report must remain new-file-only.
4. Existing Controls mode exercises OFF only. Before granting twelve TimerMatrix rows, root must arrange separately explicit ON full typed paired controls (both cadence15 and1) from this frozen build, or extend the source-reviewed wrapper with a dedicated control mode. A successful no-socket API control alone cannot establish ON paired correctness.
5. After-cleanup snapshots include machine/OS/runtime/architecture/logical CPU and actual resource/power fingerprint, but no CPU model, physical RAM or ambient system process-load inventory. Preserve current root machine/contention evidence alongside runs; do not call these fields a complete hardware/environment audit.

## Verification

- Git/source diff: reviewed; seven-file scope and clean isolated status inspected.
- Lint / TypeCheck / tests: NOT_RUN, root serial execution window withheld by dispatch.
- No implementation self-fix performed as requested; only this owned research record written. Rich/P6 source and all original artifacts preserved.
## Root-identified blocker and source correction (supersedes initial no-blocker conclusion)

Root correctly identified that prior Cleanup caught a resource exception but then formatted it and appended to List without an enclosing timer finally. A custom Exception.ToString throwing is a concrete non-OOM path that skipped timer End. Initial review missed this structural blocker; b1a57d449 is not approved as lifecycle-safe.

Scoped correction frozen `4ed366217` changes only four isolated files. Actual Runner now uses shared ProfileCleanupFlow.ReleaseAfter, whose nested finally directly invokes timer.Dispose independently of all resource cleanup/diagnostic work. Shared recorder marks Failed before diagnostic formatting/storage and safely describes throwing formatters. Actual Runner uses the same shared Export helper tested by the linked no-socket harness; export/fallback failures retain nonzero exit. Native timer exception formatting is also guarded. Four added controls exercise actual shared resource failure/continued cleanup, throwing exception formatting, outer cleanup-action failure and post-release export failure, checking exact End and failed outcomes. These are source-only controls, NOT_RUN.

New separate ControlsOn mode produces exactly two genuine initial ON rows at cadence15/1 using existing full typed pairing checks. Original Controls/Profile remain default OFF; TimerMatrix stays12mid128 rows. Both endpoint common flags and supervisor identity remain options-aware. Fresh manifest compilation required; originals untouched.

`git diff --check` passed (line-ending normalization notices only). No .NET/build/socket/runtime run performed. Root should independently review this fix before fresh compiler and controls grants; all runtime and remaining product exits remain unverified.