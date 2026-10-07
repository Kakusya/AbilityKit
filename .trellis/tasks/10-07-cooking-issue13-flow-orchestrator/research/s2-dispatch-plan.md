# S2 approved dispatch plan and change boundary

Current S1 accepted candidate: `2201931e1846acb9ab8a205d501e39731b5a39bb`; see [new decision](dot-s1-revision-review-decision.md) and [full reply](dot-s1-revision-review-reply-raw.txt). Owner full Issue13 approval and original API/amendment remain current. S2 can now start; historical S0-only and deferred-S2 passages preserve their original facts rather than revoking this later authorization.

Gap: the two fixed flows currently run only against offline ET authority. Add a parent adapter controlling one independent real ET server and two external clients, using existing v3 session/codec/LiteNet transport. Do not alter product APIs or domain state; the test harness observes actual installed projections and formal command terminals.

## Exact file ownership, Cooking consumer and acceptance

| File (FlowAcceptance directory unless stated) | Necessity and observable acceptance |
|---|---|
| New NetworkFlowAdapter.cs | Parent IFlowOperations for both fixed network flows; real child processes, command replay, current projections and confirmed exits |
| New NetworkRoleHost.cs | Child server/client owner lifecycle; real session/Join/baseline and unique authority frame driver |
| New FlowRoleProtocol.cs | Bounded NDJSON control/reply validation; wrong binding/sequence/payload fails closed before domain use |
| AbilityKit.Game.Cooking.FlowAcceptance.csproj | Reference existing LiteNet project for these Cooking children; no version or source change |
| Program.cs | Approved private role CLI entry inside existing SingleThreadOwner; stdout remains protocol-only |
| FlowContracts.cs | Implement existing approved role DTO/API shapes and canonical serializer; no new public design |
| FixedFlows.cs | Select real network operations and explicit pickup convergence before replay/drop; retain offline behavior |
| FlowRuleEvaluator.cs | Approved C13-CONVERGE checks against current installed client projections and field/version fences |
| FlowOrchestrator.cs | Select adapter, maintain common budgets, cancellation and completion contracts |
| FlowReport.cs | Report real network resources/evidence and honest remaining environments |
| src/AbilityKit.ET.Runtime.Tests/CookingFixedFlowTests.cs | Focused real S2 positives and necessary protocol/projection/replay/EOF/fault controls; short affected S1 regression |
| Docs/design/CookingGame/testing/fixed-flow.md | Actual S2 command, process, evidence and limits documentation |
| Docs/design/CookingGame/testing/requests/compete-network.json | Approved fixture/request for real competition plus exact replay |
| Docs/design/CookingGame/testing/requests/pickup-drop-network.json | Approved second composable flow request |

## Authoritative contracts, not new proposals

Read [exact API](accepted-api-design.md), [precedence amendment](accepted-api-amendment.md), [deferred bootstrap](accepted-s2-bootstrap.md), [original detailed bootstrap reply](dot-s1-review-reply-raw.txt) and [current acceptance](dot-s1-revision-review-reply-raw.txt). These jointly supply exact RoleInit/RoleControl/RoleReply shapes and private entry. Semantic/API uncertainty must stop that portion and be asked through main to dot.

Private entry: `internal static Task<int> RunAsync(string roleId, Stream controlInput, Stream replyOutput, TextWriter diagnostics, CancellationToken cancellationToken)` inside NetworkRoleHost, invoked by Program in existing SingleThreadOwner.Run. Child syntax `role --id server|client-a|client-b`; no run/request/output/diagnostic options. No second owner loop.

First bounded Initialize within 10s: schema1, control sequence1, exact role and unique ControlId, frozen Run/Attempt/generation; fixture only for server, endpoint only for clients. Reject reinitialize/run switch. Enforce message bound before unbounded ReadLine. Server listener constructs loopback/port0 then parameterless Start; report actual bound LocalPort. No pre-reserve/close race or Start(0) assumption. ServerReady contains actual authority cut/endpoint; parent then initializes two external clients with that endpoint, and clients establish current binding through actual Join/installed baseline. No snapshots sent over control or fabricated readiness. Compare server instance/scope and each client's own connection generation; no equality assumption across separate clients.

ArmAction ControlId equals invocation CallId. New Action.CallId matches; replay Action=null and ReplayOfCallId names same current role/run/scope/session/generation original frozen payload. Release has a distinct ID, duplicate Arm/Release cannot repeat execution. WireCorrelation is null when unavailable. RoleReply host sequence and event host sequence are separate contiguous streams; Armed must not create event gaps and parent cannot invent fillers.

Pipe reader only bounded parse/validate/enqueue. Child owner initializes, pumps, observes and releases; server ProcessOwnerFrame is the sole authority tick, network command batch0. Pump remains active while awaiting stdin/Join. Stdout only RoleReply NDJSON, bounded stderr; parent alone collector/file writer. Child0 requires normal Stop/cleanup,1 runtime fault,2 start/protocol precondition rejection. EOF/crash/cancellation/wrong binding preserve actual facts, don't invent Stopped. Independent Stop budget separately disposes session, idle-owner ET authority, readers and confirms actual child process exit. No global kill/cleanup. Cancellation of a sent command does not undo its effect; unexpected reconnect/scope change stops this attempt.

Pickup convergence must precede replay/drop: actual current installed baseline, version>=authority fence plus necessary item/hands fields. IsSynchronized alone and transport ACK are insufficient; do not claim precise baseline ACK. Existing fixed-step failure/first-cause/late-cancellation S1 behavior remains preserved.

## Focused verification and stop point

One affected-source Debug build; one focused FlowStage=S2 window; both real three-process network CLI positives; necessary wrong-binding/old-projection/replay/EOF/child-fault controls and short affected S1 regression. Reuse unchanged passing binaries, no broad gates/Rich matrix/physical LAN or repeated stable tests. If actual failure requires repair, retain original result and rerun only affected checks. Synthetic controls must be labelled; do not add arbitrary diagnostic DTO hooks.

Record source SHA/dirty, tools, exact command/options, native exits, actual counters/skips, raw artifacts, binary identity, parent and child identities/exit evidence, execution/product/evidence/cleanup separately. After exactly one current worker_done, idle at S2. Main independently checks, freezes candidate plus delivered hashes, and requests new dot review. S3 remains gated and NotRun.

Worker is not alone: only these 14 product/test/docs files are writable. Main owns this Trellis task, research/manifests and coordination records. Preserve user journals/Practice, accepted S1 and previous failure evidence. Other product/source/examples/Unity, oldIssue6, dependency upgrades, merge, close, publication and worktree/process cleanup are outside this assignment.
