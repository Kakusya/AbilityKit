# N02 process runner independent source review

2026-10-03. Reviewer owns only this main-tree report. Reviewed the untracked NetworkAcceptance csproj, Program, SingleThreadOwner, ProcessServiceFixture, NetworkActionPlanner and run-cooking-network-process-acceptance.ps1 in cooking-network-process-current. No runner/source edits and no .NET execution. Compilation evidence is not process acceptance. Root reports an actual rich-baseline 65536 token-bound failure under investigation; no pre-acceptance pass is asserted.

## Findings requiring disposition

1. PowerShell endpoint consensus checks detail.hash = Hash(authority state) only. Both reports also contain Session projection, but it is not compared. This misses participant/connectivity/baseline-ack/mapping projection disagreement. Define whether consensus is authority-only or full authority+Session; use a combined canonical hash or explicit projection comparison after an agreed quiescent point for the latter. Avoid falsely promising complete-state consensus from the current check.
2. Endpoint reports record Host and Session assembly ModuleVersionId, but the paired script does not compare them. Fixture ID is a constant and does not identify actual catalog/config/source bytes. A pair of different builds can pass fixture/protocol checks. Compare both build IDs and actual configuration/menu identity or catalog hash to make paired evidence reproducible.
3. Finished unbound cup is produced, bound and submitted by the same remote Partner. The current planner proves unbound staging and the original manual component handoff, but does not prove a DIFFERENT player accepts a finished cup and binds a customer order. Add that ownership handoff if the acceptance label includes this S14 cooperative exit; do not infer it from two players existing.
4. Natural completion checks Success, two accepted orders and one unmet departure, but omits S14 explicit zero stars, Closing, empty customers/wash queue, free tables and released work checks. Existing TryFinishService enforces CanSucceed, so this is an acceptance evidence gap rather than a demonstrated production failure. Add explicit final assertions or narrow labels.

## Verified source boundaries

SingleThreadOwner installs a single SynchronizationContext and pumps queued scenario continuations on the constructing thread. Runner awaits do not use ConfigureAwait(false); ET creation/owner pumping/lifecycle/grant operations stay there. Transport callbacks retain transport threads and Session queues ingress. The planner has only captures, framed send delegates and wait delegates; it never calls a simulation or places/grants items directly. Simulation initialization is the trusted Host factory's responsibility.

Fixture configuration follows the accepted S14 source: F01+D31 subset, trusted catalog copy RequiredTicks=6 with recomputed catalog load/hash, finite supplier packages, empty initial tools, menu projections, compact geometry, two actors and the same front/score settings. Original catalog artifacts are read, not rewritten. This is a bounded menu proof, not all 87 recipes. Framed real Request/Receive/Move/TakeOut/PutIn/Start/Continue/Stop/ServePortion/Bind/Submit commands remain the authority path. Procure is real finite supply; local manual Stop leaves work for remote Continue. Automatic work moves the actor to parking and waits for completion; batch yield uses ServePortion.

Host natural finish calls TryFinishService/CompleteEnd, not BeginEnd. Durable successor is an actual CreateSuccessor with locked progress and a checkpoint store. Remote waits for epoch two and performs live-instance Reconnect, checking changed token/same instance/synchronized Created baseline. This does not prove cold process/store reload or cross-machine operation and does not claim either.

## Metrics/provenance and process safety

Reports distinguish SameMachineIndependentProcessesUdp from SeparateHostsRequiresPairedEvidence and mark physicalTwoPc NOT_VERIFIED. Independent launched PID checks and READY PID matching prevent claiming two endpoints from one process. SameMachine launcher uses only its retained child Process objects for kill/dispose and Hidden windows; no global dotnet kill, filesystem delete or shell-built destructive cleanup. Readiness/exit loops are bounded at 120/180 seconds. Standalone Host/Client mode is foreground and does not provide the paired automated PID/consensus validation; physical evidence still requires paired artifacts and machine identities.

Host timings use local monotonic receive/consume/commit timestamps, not cross-machine subtraction. Allocated bytes sum current owner-thread deltas only around synchronous ProcessOwnerFrame and explicitly exclude await/callback/native/client allocations. Throughput includes setup/final hold and is diagnostic with formal target UNSET. Session Sent/ReceivedBytes are payload bytes, not UDP/IP overhead. snapshotBytes is a synthetically encoded diagnostic full baseline, not measured actual issued socket bytes; keep this distinction in conclusions. Current owner/state hash is a serialization hash, not a build/config provenance identity.

## Disposition

Pending root corrections/disposition and a real frozen successful rich process run after codec-bound repair. No pass is accepted from compile alone. The four findings above were sent to root; integration/master gates, bounded codec regression, rich process traces and physical LAN remain distinct exits.

## Root disposition of review findings

Root confirms the accepted display-supplement/process plan uses Hash(full authority capture) for gameplay consensus; endpoint Session views are intentionally distinct and may change after the client reports/exits/closes. Therefore equal final Session hashes are not required. Each endpoint must instead record/validate its own combined baseline hash, with paired server instance/configured participants and legal generation/owner binding checks. Rename/narrow any entire-baseline consensus label accordingly. This resolves the proposed equal-projection comparison into the correct accepted contract; implementation/evidence remain pending.

Root will enforce paired Host/Session MVID equality, move Running D31 binding/submission to local Chef after remote production/parking (remote retains F01 delivery), and add actual-source zero-star/closing/clearing/wash/released-work assertions. Those stated corrections are plans, not independently reviewed implementation or executed passes. Reviewer remains read-only/no .NET pending frozen source.

## Static re-review of root corrections

Read the updated root runner/script without execution. Different-player D31 handoff is now explicit: remote prepares the unbound cup, local Chef calls the real planner Deliver (pickup/bind/submit), remote delivers F01 and waits for two commits. Explicit Stars==0/IsCompleted, Closing, free tables, empty wash and no owned/working front jobs are present before successor creation. Paired ET/Session MVID equality and server instance equality are now hard script checks. These source changes close findings 2–4 at the static level; actual compilation/rich process results remain pending.

Session evidence follow-up remains: current endpoint baselineHash is newly recomputed for its report, and PS checks only nonblank. It does not explicitly compare the received baseline identity hash against Hash(State,Session). Configured participant arrays are compared by joined values but two empty lists also pass; no runner assertion of expected two participants/legal generations/owner binding was seen. Production client may enforce these through codec/baseline acceptance, but that contract must be cited or checked explicitly before claiming report-level validation. The old "Complete-state consensus hash" script wording also remains despite the accepted authority-capture-only consensus boundary. These precise residual points were sent to root; no game/Session equal-final-hash requirement is introduced.

Disposition remains actual-pending, with one bounded Session report-validation follow-up. No .NET invocation, source modification or pre-accepted process pass.

## Return-route/successor coordination re-review

Read root latest Program guard and planner Deliver read-only. Prior actual route-barrier run 20261002-205329-9068975 failed during remote return-to-parking with BaselineRequired after real major.checkpoint.json had been created; this is evidence of prematurely retired scenario scope, not failed normal business. No successful rerun is inferred here.

New guard requires local D31 Deliver fully returned, two committed orders and both authority poses exactly at distinct configured parking centers before TryFinishService/CreateSuccessor. Deliver's final command is the parking Move, and remote thereafter only waits for two commits/new-scope baseline; therefore no remaining route command is needed after the guard. Both parking destinations are reachable distinct cells, and waiting does not stop Host Pump/Tick/Session processing. No circular wait/deadlock introduced by these conditions was found. An unreachable/missing parking position yields existing bounded scenario timeout rather than false completion.

The cached state is read before awaited local Deliver. That iteration has at most one committed order before local D31 submission, so it cannot pass the two-order guard accidentally; the next Pump refreshes it. Conservative stale poses can defer completion, not retire a route early. Conditions use accepted business state, not direct pose mutation or a relaxation of natural completion.

Remaining publication boundary: authoritative parking proves the final Move executed, not that its response/projection was already processed by the remote. Correct completion still relies on Session reliable ordered result-before-baseline/old-result terminal handling; preserve actual trace of that final response across successor baseline. This is a separate protocol invariant, not an established guard defect. Coordination is statically acceptable for the reported premature-scope-retirement scenario, with actual rich rerun still pending. Reviewer ran no .NET and changed no runner/source.
