# Cooking fixed flow acceptance — S1

This offline test CLI runs two registered C# flows through the existing ET Cooking owner. It does not add gameplay state, a workflow DSL, a watcher or network execution. The accepted test API is [dot's API design](../../../../.trellis/tasks/10-07-cooking-issue13-flow-orchestrator/research/accepted-api-design.md), with [the accepted invocation/diagnostic amendment](../../../../.trellis/tasks/10-07-cooking-issue13-flow-orchestrator/research/accepted-api-amendment.md) taking precedence for its changed declarations; category approval is [Owner continuation](../../../../.trellis/tasks/10-07-cooking-issue13-flow-orchestrator/research/owner-approval-20261007.md).

`compete-one-item/v1` submits both actors' pickup commands in one positive batch before any Tick or await. Either actor may win. It requires one real business Accepted and one real business Rejected, captures independent item and hand facts, then replays the winner's complete original command with a new CallId. `pickup-drop-one-item/v1` captures pickup before dropping with a new BusinessId and the current item version. Drop is not evidence for an earlier competing pickup.

The registered `one-item/1.0` fixture has actors A/B, `flow-item` initially on `source`, and an empty `drop` station. Both available actors have `cook` eligibility and legal reach. Integer poses `(800,1000)` and `(1200,1000)`, radius40 and interaction radius800 put both within reach of source `(1000,1000)` and drop `(1000,1200)` without actor overlap. These are test configuration values, not game balance. Commands use `CookingRecipeOperation.Pickup`/`Drop`; fixture initialization seeds one raw item before host ownership.

Admission, native disposition, business result and complete authority observation are separate facts. An admission refusal without a terminal is recorded as NotAdmitted, never a fabricated business rejection. Tick dispositions and both terminal histories merge with immediate admission terminals by invocation correlation; the network notification queue is not the offline ledger. Real fixture competition can legally reject with ItemStale or TargetOutOfRange: the global reach check precedes pickup's stale-item check. Executed alone does not mean business success.

The authority captures `CaptureReadOnlyFullState` and copies `ItemInHand(A/B)` at the same idle owner boundary. It immediately discards the borrowed kitchen reference. Item location alone cannot prove the independent hand index. Replay freezes the original batch/version/payload; command-associated events and item/hand effects stay unchanged even if unrelated fixed ticks advance global watermarks. A later fixed-step fault retains already committed effects, events and Executed terminals and leaves frame/tick unadvanced. Cancellation stops a wait, never undoes an accepted command.

## Commands

Run the focused build once, then reuse its output on unchanged source:

```powershell
dotnet build src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~CookingFixedFlowTests&FlowStage=S1" --logger "trx;LogFileName=flow-s1.trx" --results-directory local/Logs/issue13-s1-worker/<fresh-validation-directory>
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request Docs/design/CookingGame/testing/requests/compete-offline.json
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request Docs/design/CookingGame/testing/requests/pickup-drop-offline.json
```

Samples intentionally reserve distinct fresh output directories. Change `outputRoot` to another fresh directory for a later run. Old outputs are rejected before host creation and remain untouched. `seed` is recorded; these fixed flows do not randomize action ordering. Required fields, unknown/duplicate fields, numeric/unknown enums, versions, fixture registration, duplicate actors, unapproved rule references and out-of-range limits reject before host creation. Requests cannot supply their own approval. Test schema1 makes no wire-v3 compatibility claim.

The approved `--output-root <fresh-directory>` option overrides only OutputRoot, then runs the same validation. The saved `request.json` contains that actual effective directory. For example, use a new directory under `local/Logs/issue13-s1-worker` when the sample's first output already exists. All API callers explicitly provide `FlowRunOptions` with the actual executable, argument array and working directory; no legacy overload invents that provenance.

Two narrow diagnostic options are implemented for the coordinator's later completion probes:

```powershell
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request Docs/design/CookingGame/testing/requests/compete-offline.json --output-root local/Logs/issue13-s1-worker/fresh-diagnostic --diagnostic-fault fail-after-start
dotnet src/AbilityKit.Game.Cooking.FlowAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.FlowAcceptance.dll run --request Docs/design/CookingGame/testing/requests/compete-offline.json --output-root local/Logs/issue13-s1-worker/fresh-no-result --diagnostic-exit-before-run
```

`fail-after-start` first reaches a real valid initial observation, then raises `HarnessOrEnvironmentFailure/DiagnosticFailAfterStart` at `diagnostic-after-start` before any gameplay dispatch. It returns Failed1, incomplete execution and product Undetermined, with unexecuted goals NotRun. Produced initial/diagnostic/cleanup evidence can still be complete. Normal independent cleanup and bounded reporting apply; this deliberately injected harness fault is not a product bug.

`--diagnostic-exit-before-run` validates the request and catalog, reserves fresh output and saves the effective request, then returns native86 before creating an orchestrator/session/collector/child. It emits `DiagnosticExitBeforeRun` to stderr with known request/invocation/output facts. It allocates no RunIdentity and writes no result or failure pack. Native86 is not a FlowStatus mapping. The caller must report an incomplete run and must not read another directory's old result. Unknown or duplicate options, unknown diagnostic values and mutually exclusive diagnostic options reject with exit2 before host creation. Fault configuration is explicit argv/options input, not a JSON field or ambient setting.

## Rules and limits

[flow-rules.json](flow-rules.json) is the reviewed category catalog. All five cards are version1.0. Offline requests require OWN, REJECT, IDEMP and COMPLETE. CONVERGE is N/A in S1. REJECT forbids only the rejected action's own success effects; another legal actor may change the world. REJECT and IDEMP are explicitly N/A for the separate pickup/drop flow when it declares no rejection or replay. OWN still checks both pickup and drop cuts. COMPLETE records execution facts; overall Passed additionally requires all approved product checks, complete evidence, complete cleanup and successful final publication.

Default startup/step/convergence budgets are10s, overall120s, including reset10s and publish2s. Convergence may tighten to1–10s; it remains N/A offline. No other budget or log-cap relaxation is accepted. Active work stops by108s; waits use the lesser local/remaining budget. A blocked owner-thread synchronous operation needs caller process supervision; no missing result is inferred to be a success.

The collector uses queue256, event64KiB, total4096 events/8MiB and diagnostic256KiB/host caps. Only its I/O worker writes `events.jsonl`; owner publication is nonblocking and never calls back into gameplay. Wrong run/generation, host sequence gaps, duplicate invocation events, queue/size/count overflow, partial writes and missing required facts latch evidence incomplete. Collector sequence is receipt order, not cross-host causal proof. S1 launches no child host and captures no unbounded host stderr.

## Read a result

Native exit codes are Passed0 / Failed1 / Blocked2 / Skipped3 / NotRun4. Startup/environment validation or startup failure is Blocked; faults after successful startup are Failed. `executionComplete`, product `verdict`, `evidenceComplete` and `cleanup` are independent. A confirmed product failure survives a later environment or cleanup failure. An incomplete verdict stays Undetermined.

Read `summary.txt` first and `report.html` for category/step detail. `identity.json`, saved `request.json`, `provenance.json` and bounded `events.jsonl` identify the run. Source provenance is the invocation checkout commit/dirty/configuration/SDK/runtime, with the built assembly SHA256; it does not archive or prove every compiler input. Build duration is NotRun in the CLI because the caller built the assembly separately. Total timing includes report preparation through the final result commit boundary; final file move/return overhead is not a separate performance claim.

When execution fails, `first-failure.json` freezes the first cause, cuts and event window before session release. `failure-pack.json` keeps that first cause and adds actual cleanup results, Source and ActualInvocation. Its Run/Source/Cleanup match the delivered result. Argument arrays retain boundaries, including spaces. ReproduceCommand uses concrete PowerShell quoting, this run's saved `request.json` and a fresh `--output-root`; diagnostic reproduction retains `--diagnostic-fault fail-after-start`. ActualInvocation records the executed call, while ReproduceCommand suggests a new call. Cleanup uses an independent token and idempotent owner disposal. It does not invoke restaurant natural-success completion. Old draft packs missing Source/ActualInvocation remain historical and do not meet the amended schema.

`result.json` is atomic and published last after summary/HTML/failure artifacts. Report dependencies and matching run/source identity must exist before the CLI accepts its terminal. A publication/deadline/identity failure returns nonzero; a rejected marker is retained as `incomplete-result.json`, leaving no successful completion marker. Files demonstrate normal process/file delivery, not OS or power-loss durability.

The completion point is the orchestrator's validated publication/readback confirmation, including its caller-cancellation check. Cancellation observed before final status or at that confirmation prevents overall Passed/exit0, while preserving actual execution, product verdict, evidence and independent cleanup. A Passed marker written before that acceptance is retained only as `incomplete-result.json`, with no valid success marker. Cancellation after the completion point does not retroactively invalidate the result.

Focused controls use real ET flows, immediate duplicate/admission cases and staged frame-fault preservation. Injected hand/location/rejection-event/replay violations are labelled synthetic evaluator controls; they do not claim product bugs were fixed. Startup, cancellation/timeout, catalog/old output, missing/overflow/partial/write evidence, publication and cleanup controls test failure classification. Two fresh runs per flow verify released resources and new identities. Unity, networking, physical two-PC LAN and automatic Orca wake remain NotRun; S2/S3 are separate review stops.
