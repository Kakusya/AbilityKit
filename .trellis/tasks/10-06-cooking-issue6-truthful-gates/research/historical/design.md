# 2026-10-05 Owner scope correction: original broad design stopped

Only Cooking is authorized. Shooter/MOBA/Orleans example adapters, scripts, tests and dedicated gates are prohibited. The old eighteen-producer design below is retained as history and must not be resumed under its prior approval. Shared work requires a specific Cooking necessity and a revised bounded plan. See [stop record](research/cooking-only-scope-stop.md) and AGENTS.

# Issue 6 approved design and implementation checkpoint

Status: in_progress; parent implementation approval at 5dc303a809 is recorded in [implementation progress](research/implementation-progress.md). User explicitly transferred design approval and final acceptance from dot to parent coordinator; no further dot dependency remains. The recorded parent approval authorizes implementation; final acceptance remains reserved. [Revised contract](research/revision-contract.md) is the normative R1–R5 design, together with [result schema](research/result.schema.json), [provenance schema](research/provenance.schema.json), [complete synthetic trees](research/result-examples.json) and [control matrix](implement.md).

Original design/schema/examples/checks are preserved by [immutable d45f09ee5 Git history](https://github.com/Kakusya/AbilityKit/tree/d45f09ee5a42aa06bf5dcf8600e701c2daf6986b/.trellis/tasks/10-04-test-gate-result-contract); the first R1–R5 revision is preserved at 325ef2b9bd50ea196e14dd8d17b7aca0c503cf3b. Redundant copied plans are removed. The incomplete examples, unsafe path regex and blanket NotRun time rule are superseded; historical checks remain scoped to what actually ran. Current [Issue6](research/issue-6-current.json), [comments](research/issue-6-comments-current.json), [review](research/review-5405864745.json), [Issue5 report/proposal](research/issue-5-current.json) and [Issue5 acceptance](research/issue-5-comments-current.json) snapshots preserve remote wording without introducing a dot gate.

Parent independently reran the prior 48 controls but withheld approval for executed counts including skips and a pre/post source fingerprint including restore-generated assets. The current contract, schemas and synthetic controls correct both; [bounded audit correction report](research/audit-correction-verification.md) preserves that finding and the new measured results. Those corrections were accepted at 5dc303a809; task is now in_progress.

## Accepted directions, pending implementation gate

- core-stability mirror optional only for config-approved missing installation; retain Unity missing in console/JSON. Direct mirror and explicit Unity gates required. Invalid evidence or actual optional failure never becomes Skipped.
- StepName selected leaf may pass while incomplete aggregate is NotRun4/fullGateAccepted=false, retaining real invocation time. Failed1/Blocked2 take precedence.
- Thin helper plus inventoried 18 producers' minimal parameters/terminal/evidence adapters are the proposed future implementation scope, preserving standalone behavior and business/performance criteria.

## Locations and boundaries

Reuse tools/run_test_gate.ps1 Invoke/Assert/aggregation and existing TRX/Unity XML checks. Thin tools/test-gate-result-contract.ps1 owns the single semantic validator and normalized tree; console/JSON/CLI derive from it. tools/test-gates.json remains sole declaration authority. Native exits are recorded independently from canonical Passed0/Failed1/Blocked2/Skipped3/NotRun4.

Unity managed/version globals must actually reach root mirror and six referenced projects. Root csproj changes only for necessary property validation/forwarding. No source/asmdef selection changes, install/upgrade or Cooking Unity scope. Missing external DLL provenance cannot be optional-converted.

[Source inventory](research/source-inventory.json): 32 gates, 130 steps, six configured kinds (8 build,73 test,21 script,9 gate,18 EditMode,1 PlayMode), 21 calls/18 producers. Runner-supported but unconfigured unity-execute-method also included. Contract R4 provides every producer's evidence/token and specific measurement/native2/internal-build/log-routing behavior.

Additional consumer tools/validate_shooter_test_gates.ps1 statically requires StepName smoke/full workflow fragments. Document partial exit4 without inventing online CI; any consumer code change needs parent scope approval. It is not a nineteenth gate producer.

One exclusive evidence root, node UUID roots, pre/post source/dirty/binary fingerprints, raw producer receipts and build/restore manifests. Synthetic fixtures example=true never qualify for production acceptance. Script-entry isolation precedes helper loading and any port/PID/process/service operation.

Parent must approve revised PRD/design/implement/manifests before in_progress. Parent independently audits final acceptance and owns later merge/lifecycle decisions. This dispatch changes only task artifacts and commits locally; no production edits, workloads, posting, push, merge, archive or cleanup.

Durable current schema conformance fixtures live under tools/tests/fixtures, with the approved empty immutable-input clarification in their README. Task schemas remain historical planning snapshots; production semantics enforce the exception and no empty result evidence.
