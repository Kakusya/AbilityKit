# Slice 11 exact instance cardinality final review fix

Use a fresh cc-switch Codex session in `D:/MyWorkTree/AbilityKit/issue6-compiler-trace-repair-ccswitch`. Do not communicate with or reuse any settled terminal.

## Scope

Own only:

- `tools/tests/test-gate-result-contract.tests.ps1`
- `.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/slice11-compiler-trace-repair-report.md`

Read other Slice 11 files and receipts as needed; do not modify them. Cooking only. Do not modify examples, Unity, projects, dependencies, gates, SDKs, packages, or business code. Preserve all existing edits.

## Required fix

In the unchanged `current-build-test-JSON-roundtrip` control, require the nonzero unique `Generated.opaque` identity count derived from `compilerEvidence.instances` to equal `compilerEvidence.instances.Count`, as well as equal the aggregate `compilerInputs` count with the existing path/length/SHA-256 bijection. This explicitly proves every instance contributes exactly one matching `Generated.opaque`; the current fixture's observed counts are Tests leaf `2 instances / 2 instance opaque / 2 aggregate opaque` and Build leaf `1 / 1 / 1`.

Update the report to state the literal instance cardinality condition and record final receipts. Preserve prior red and green evidence.

## Acceptance

- Parse checks and `git diff --check` pass.
- Compiler-trace-focused controls pass.
- Complete isolated suite passes exactly `289/289`, retains all 249 original names with no duplicates, source-before/source-after equal, console width Passed.
- Both genuine Cooking gates remain NotRun.
- Do not commit or push.
- Check the inbox, then send exactly one `worker_done` with Task and Dispatch IDs.
