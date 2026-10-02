# N01–N03 network resume audit — 2026-10-03

## Scope and evidence boundary

Read-only preparation while root closes singleplayer S14. Read parent completion-contract; N01/N02/N03 PRD, design, implementation checklist and manifests; ADR-0001/0002/0003, long-term goals, technical roadmap, LAN spec; current Session source and retained network worktree. No source changes, .NET execution, transport acceptance or physical LAN verification occurred in this audit. Historical task text saying planning-only is superseded by latest owner authorization; stage dependency and Unity prohibition remain.

Main read boundary: ac3769465. Retained `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-n01-n03` HEAD ce3110b53, branch cooking-network-n01-n03, with substantial uncommitted changes and untracked files. A retained worktree is not an active worker or accepted delivery. Build outputs found there are not verification evidence.

## N01: reconcile formal contracts first

The explicit owner source is `.trellis/tasks/09-19-cooking-productization-network-slice-planning/research/discussion-notes.md` sections 28–29, lines 947–964: unique real transport generic LiteNet reliable UDP, InProcess for local/tests, generic Listener/ServerChannel, callbacks enqueue only, fixed Tick authority first-received arbitration. N01 design correctly routes this source.

Formal documents still need dated reconciliation rather than deletion of history:

- ADR-0002 retains September-21 KCP/singleplayer note and broadly stated stable sorting. ADR long-term-goals lines 7/14/25 and technical-roadmap line 9 still present KCP and unapproved-network statements. Later owner authorization and UDP selection must be clearly routed from these entry points.
- Technical-roadmap lines 63/75, ADR-0003 line 17, interaction-foundation lines 71/85/90 and recipe-loop line 319 need an explicit distinction: singleplayer deterministic ordering remains unchanged; network assigns ordinal atomically at accepted ingress and consumes that order. First-received does not mean arbitrary dictionary or callback scheduling order.
- cooking-lan-session line 113 still requires TCP/UDP comparison and D1 owner selection before production transport. That historical requirement conflicts with later unique UDP selection. Keep historical limited L01–L09 evidence, add current applicability and exact N01–N03 contract.
- N task metadata remains planning with obsolete notes despite implementation_authorized=true; check manifests are generic planning evidence and lack concrete transport/ET integration verification. Update them only after reviewed contract and actual dependency evidence, not from worker directory existence.

ADR-0001 remains compatible: player Host and remote Client share authority, without implying host migration, cloud services or a dedicated server requirement. No new architecture is needed.

## Retained source: useful material, not ready to merge

Dirty source includes Session host/client/protocol/snapshot, generic LiteNet listener and package/csproj/asmdef wiring, loopback harness adaptations, independent-process executable and portable PowerShell runner. Dirty research/session-boundary.md states old core schema integration was awaiting S01–S03: that boundary is obsolete and cannot establish compatibility with current S14.

Concrete controls requiring review/fix before acceptance:

1. Retained CookingSessionHost.cs lines 76–113 handles handshake and changes logical binding directly in PacketReceived callbacks. SessionClosed lines 91–98 immediately changes peer ownership and removes pending commands. N03 requires connection effects committed at fixed Tick. Recipe commands are queued, but the callback-only decode/enqueue rule is not satisfied for all session events.
2. Retained CookingSessionHost.cs public ExecuteHostCommand (around line 159) directly invokes Submit/BroadcastSnapshot, bypassing fixed-Tick ingress, although the new local Client uses InProcess framing. Remove or constrain this authority bypass using the reviewed API contract; do not claim both local entry points are equivalent.
3. Retained Session host still owns CookingRecipeSimulation and old CookingMajorCheckpointStore cross-level transition. It does not compose the accepted CookingLevelEtHost durable lifecycle/generation boundary. N02 must adapt the network application layer to the existing singleplayer authority rather than create a parallel gameplay/lifecycle implementation.
4. Retained protocol is version 2 and snapshot exposes Recipe, optional FrontOfHouse, Progress, Level/epoch/sequence/pause. Enumerate every current S14 typed kitchen/layout/menu/supply/purchase/portion/manual-process/claims/allocator/carry state and ensure actual wire + canonical hashes cover it. Current field names or a matching old Recipe hash alone do not prove this. Audit stale-schema rejection and baseline restore permissions together.
5. Retained runner starts an old clean-pool bread/tomato-egg-soup fixture, then transition/reconnect. It is useful process infrastructure, but does not prove complete current physical stock, receipt, scoped menu and S14 natural lifecycle. Program report emits command-response samples and formalPerformanceTarget=UNSET, not required receive/consume/commit percentiles, queue highwater, message bytes and allocation measurements.

Preserve dirty source; port deliberately onto the accepted singleplayer boundary with explicit ownership and source diff review. Generic LiteNet shared-source changes require SDK and applicable shared-package checks; Unity gameplay remains deferred.

## Sequential verification route

N01: close formal decision/spec conflicts, define typed full-state schema, ownership/ingress/event ordering, rebind credentials and client sequence reset, dependency evidence and exact check manifests. Record reviewed source boundary. Research can proceed while S14 closes; do not infer N02 start from this report.

N02 after S14 exit: integrate generic listener via NetworkHost/ServerNetworkSession; one ET authority; local InProcess Client and remote UDP use identical framing/validation/queue. Add actual callback-before-Tick zero-state-change, bounded overflow, two-player contention, sequential/parallel processing, physical receipt/portion/container handoff, menu binding/reassignment/submission, rejection zero-change, natural close and durable successor tests. Compare complete typed state at each checkpoint, not only an end hash.

N03: pending-disconnect removal at owner Tick, pause freezes all business clocks/consumption, token rebind/current scope/fresh baseline before input, committed duplicate cached and conflicting duplicate rejected, old peer/Match/Level/epoch/sequence rejected, real post-baseline continuation through processing/delivery/close. Distinguish reliable ordered transport from deliberate application delay/reorder/duplicate injection. Measure receive-to-consume-to-commit, p50/p95/p99, throughput, bytes, queue highwater/rejections and allocations with SDK/commit/Tick/topology/config provenance. No host migration or storage-scope expansion.

Commands to execute later, not run by this audit: focused Cooking session/measurement/ET tests; `tools/run_test_gate.ps1 -Gate network-sdk` for shared transport changes; `-Gate cooking-kitchen-loop` plus applicable ET runtime gate from tools/test-gates.json; reviewed `tools/run-cooking-network-process-acceptance.ps1 -Mode SameMachine` after adapting runner. Preserve TRX and independently generated Host/Client JSON with different PIDs and complete-state agreement. Same-machine InProcess, same-process loopback UDP and independent-process UDP are separate evidence classes.

The retained runner has Host/Client modes with BindIp/RemoteIp/Port, making a later remote run possible. **Second physical LAN computer is unavailable: two-PC LAN acceptance is BLOCKED / NOT_VERIFIED.** Local adapters, negative controls and two independent local processes can be verified without it, but N02/N03 complete physical-LAN exits cannot be marked passed or replaced by same-machine results.

## Result

No production edits or new test claims. Next actionable step is N01 dated formal reconciliation plus source rebase/port plan; current blockers are S14 dependency signoff, obsolete retained authority/schema integration, identified callback/direct-host bypasses, absent current measurement evidence and unavailable second-PC physical verification.
