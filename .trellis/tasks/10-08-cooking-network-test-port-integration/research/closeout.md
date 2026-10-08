# Integration closeout — 2026-10-08
Source HEAD aa5e26cc8b05d24f86e7952b7ffa495a03f535b1; dirty=true. Cooking-only .NET test startup integration implemented and reviewed. No commits/push/worktree creation, Unity/example/domain ownership/protocol/dependency modifications. Original unrelated/private changes retained.

## Implemented consumer paths
Four ordinary PS launchers and fixed-flow Network adapter obtain Windows automatic host port through existing GetPort, prefer installed fixed tool and shared config, pass candidate before host startup, verify READY before clients. Explicit nonzero PS Port remains caller-owned; Client/BuildOnly never allocate; each measurement repeat/rich case reselects. Non-Windows Flow and offline/in-process fixtures preserve semantics. Binding races fail and a new run reacquires. No port lease, automatic bind retries, firewall mutation or admin prompts.
Rich frozen manifests now require current PS helper identity; stale/missing identity deliberately rejects. Role/control/Flow result schemas preserved; provenance goes in sidecars. Existing cooking-et-level-runtime includes both C# test classes through its complete ET.Runtime.Tests step; no generic gate or other examples modified.

## Validation with distinct result scopes
| Check | Result | Actual coverage / native exit |
|---|---|---|
| Existing firewall controls | Passed | 34 isolated controls, native0 |
| New PS launch controls | Passed | 16 isolated controls, native0; timeout/owned cleanup, JSON failures, custom range, explicit/Client/BuildOnly, READY mismatch |
| PS parser / whitespace | Passed | Six scripts, zero parser errors; git diff --check native0 |
| Focused C# regression | Passed | 59/59 (existing14 + new45), zero failed/skipped, native0; first58 run retained |
| Primary full process acceptance | Passed | Native0, Host74436/Client79588, both exit0; root-held UDP18090, installed GetPort chose18091, actual READY18091, full paired gameplay hash consensus |
| Concurrency wrapper | Passed | Native0, actual GetPort/READY18092, full paired concurrency controls |
| Process measurement wrapper | Passed | Repeats1, native0, bounded correctness/fault/load only; formalPerformanceTarget UNSET, no performance threshold acceptance |
| Rich wrapper ConcurrencyControls | Passed | Native0; remote-first/local-first both actual GetPort/READY18090; fresh frozen manifest reused |
| Rich manual-paused full scene | Failed | Wrapper native1, Host85064/Client90884 each native1; Whole600s budget: terminal-chef-process-action-169 / genuine-Running-start. Original JSON/stderr retained. Port selection/READY18093 succeeded before scenario failure; not rewritten Passed |
| Actual exhausted port range | Passed control | Fixture temporarily bound remaining range; selector native1 and launch selection Failed, no fallback/host; fixtures released |
| Frozen helper identity | Passed controls | Valid manifest BuildOnly native0; deliberately missing portHelperSha256 native1 with exact required-identity rejection |
| Process cleanup | Passed | All12 endpoint PIDs recorded by PS actual runs absent, no UDP endpoint for them; C# worker independently found all9 role PIDs absent and cancellation control confirmed own selector exit |
| Machine state preservation | Passed | Saved config, installed tool SHA, PersistentStore owned firewall rule and port filter unchanged |
| Physical two-PC / CI / broad Cooking gates | NotRun | LAN NOT_VERIFIED; focused test/run evidence does not claim complete broad gate or CI |
| Separate PS/C# lint CLI | NotRun | PSScriptAnalyzer absent; no distinct configured C# lint CLI found. Compiler/type checks Passed; no Unity/AOT claim |

The failed long rich scene is a validation limitation, not a successful recovery proof. This task's startup acceptance is supported by exact READY, actual client operations, existing registered network flows and positive/negative controls. No budget enlargement, gameplay/performance repair or suppression was introduced. Implementation task completed only for this explicit port integration scope.

## Raw evidence and reproducible commands
Root: local/Logs/cooking-network-test-port-integration. Source/binary SHA256 identities: integration-inputs.json (19 entries), worker flow-integration.md. Cleanup: endpoint-cleanup.json; before/after machine state JSON. Agent commands/raw TRX and hashes in research/flow-integration.md; PS isolated commands in research/ps-integration.md.

Actual root commands (powershell -NoProfile -ExecutionPolicy Bypass -File prefix for PS):
- tools/cooking-firewall.tests.ps1 → firewall-controls.log, native0.
- tools/cooking-test-ports.tests.ps1 → launch-port-controls.log, native0.
- dotnet build each NetworkAcceptance / NetworkConcurrencyAcceptance / NetworkProcessMeasurement csproj -m:1 --verbosity quiet → respective build logs, native0; SDK10.0.300, PowerShell5.1.
- tools/run-cooking-network-process-acceptance.ps1 -NoBuild -OutputDirectory local/Logs/cooking-network-test-port-integration/process-occupied (owned UdpClient18090 in finally) → process-occupied-run.log, native0.
- tools/run-cooking-network-concurrency-acceptance.ps1 -NoBuild -OutputDirectory local/Logs/cooking-network-test-port-integration/concurrency → concurrency-run.log, native0.
- tools/run-cooking-network-process-measurement.ps1 -Repeats 1 -NoBuild -OutputDirectory local/Logs/cooking-network-test-port-integration/measurement → measurement-run.log, native0.
- tools/run-cooking-network-rich-recovery-acceptance.ps1 -Case manual-paused -OutputDirectory local/Logs/cooking-network-test-port-integration/rich → rich-run.log, native1; its three original noincremental builds all native0, serialized -m:1.
- Same rich wrapper -Mode ConcurrencyControls -Case manual-paused -NoBuild -FrozenManifest rich/20261008-065255-8585844/frozen-manifest.json -Source aa5e26cc8b05d24f86e7952b7ffa495a03f535b1 -Dirty dirty -OutputDirectory local/Logs/cooking-network-test-port-integration/rich-controls → rich-controls-run.log, native0; mode selects actual remote-first/local-first cases.
- Rich -Mode BuildOnly -NoBuild with same Source/Dirty/current frozen manifest → frozen-positive.log native0; copied manifest without helper identity → frozen-negative.log native1 expected. No port allocation in BuildOnly.

Primary/concurrency/measurement actual runs overlapped briefly on distinct tool-selected ports; rich build was serialized with C# compile work and source frozen before hashing. Therefore no isolated performance conclusion is drawn from elapsed times. Full primary natural completion and all port/PID proofs are genuine actual results. Git/Orca inventory only master; removed historical issue6 worktree not present, no cross-tree cleanup.