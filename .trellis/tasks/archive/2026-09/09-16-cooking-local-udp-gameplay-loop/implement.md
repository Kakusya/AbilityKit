# Implementation checklist: Cooking 同机 UDP 最小经营闭环

1. Start the approved task and record the Cooking/UDP specs used for implementation.
2. Extract the minimal Cooking gameplay authority seam needed to execute the existing recipe fixture through one serialized host authority path; preserve existing pure recipe and interaction regressions.
3. Define fixed collaboration fixture data and a minimal observable order completion/settlement record; expose canonical gameplay snapshot/hash and typed command results.
4. Extend Cooking UDP protocol/host/client only as needed for typed gameplay commands, baseline/delta and command correlation; retain existing envelope validation and callback isolation.
5. Add a typed harness event contract plus single JSONL writer/streaming reader/reducer. Test invalid, truncated, wrong-run, wrong-role and missing required events.
6. Update the console harness to accept a unique run ID and a role-owned artifact directory. Host and client write only within their own directories and emit required structured events.
7. Update the same-machine PowerShell runner to allocate role directories before process launch, redirect stdout/stderr inside those directories, validate logs/results, atomically publish manifest/acceptance summary, and clean only surplus successful runs. Add `--keep-artifacts` and configurable success retention with default 10.
8. Extend loopback and harness tests for cross-player workflow, order exactly-once behavior, state convergence, path isolation and streaming log acceptance.
9. Keep/add the appropriate `CookingUdp` trait coverage; update gate/documentation only if the existing P1 gate’s stated scope changes.
10. Run targeted builds/tests, `cooking-udp` gate, Cooking regression tests, same-machine scenario, artifact/log inspection and `git diff --check`. Record actual results in `check.jsonl`; physical two-PC LAN remains not-run.

## Expected files

- `src/AbilityKit.Game.Cooking/`: shared gameplay authority/fixture seam and typed domain records.
- `src/AbilityKit.Game.Cooking.Udp/`: typed gameplay wire mapping and host/client integration.
- `src/AbilityKit.Game.Cooking.UdpHarness/Program.cs`: role-owned artifacts and JSONL event writing.
- `src/AbilityKit.Game.Cooking.Udp.Tests/`: UDP gameplay and log-contract tests.
- `src/AbilityKit.Game.Cooking.Tests/`: pure gameplay authority regressions as required.
- `tools/run_cooking_udp_harness.ps1`: isolated process paths, streaming acceptance and retention.
- `tools/test-gates.json` / `Docs/AbilityKit测试门禁与批量回归规范.md`: only if the approved gate scope expands.

## Rollback and risk points

- Do not alter the current interaction UDP path until shared gameplay authority tests demonstrate unchanged existing semantics.
- The 1200-byte datagram cap remains a hard constraint; do not introduce hidden fragmentation.
- Artifact cleanup must run only after the final result is published and may target only explicitly successful prior run directories.
- Logs are an acceptance contract: add tests before relying on the runner’s summary status.
