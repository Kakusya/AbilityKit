# S2 bootstrap design — deferred until S1 acceptance

Source: [full S1 review](dot-s1-review-reply-raw.txt), request AK-I13-S1-REVIEW-20261007-01 / reviewed b387179fd4ae60339c3aa7f3a6efb3f12f7a976a. This confirms design only. S1 currently needs revision; no S2 dispatch permission before accepted new candidate. Owner full Issue13 scope remains in effect. Original operation interfaces/role DTOs and bounded amendment remain current.

## Exact implementation-private entry (verbatim)

```csharp
internal static Task<int> RunAsync(
    string roleId,
    Stream controlInput,
    Stream replyOutput,
    TextWriter diagnostics,
    CancellationToken cancellationToken)
```

In NetworkRoleHost, called by Program inside the existing SingleThreadOwner.Run; no second owner loop. CLI child entry is role --id server / client-a / client-b, excludes run/request/output/diagnostic options. First bounded NDJSON Initialize within10s must carry exact schema/run/role/sequence1/ControlId, approved fixture only for server; later messages cannot reinitialize/switchrun. Follow full reply for all preconditions and exits, do not invent DTOs or fill missing events.

Server creates real ET/session/listener with loopback and constructor port0, calls parameterless Start(), reports actual Endpoint plus authority observation. Parent starts both external clients then passes exact endpoint. Clients use real Join/baseline to establish current scope/server instance and own connection generation; no snapshot installation over control. Only ProcessOwnerFrame drives authority, keep owner pump progressing while stdin/join waits. Pipe reader validates/enqueues only. Reply sequence and event sequence are distinct contiguous streams; Armed cannot create event gaps.

Arm/Release/Observe/Stop/replay consume exact accepted RoleControl. Stdout only RoleReply NDJSON, stderr bounded; parent alone writes final events/report. Child0 is confirmed normal Stop/cleanup,1 runtimefault,2 precondition/start/protocol rejection, never gameplay success. EOF/crash/unknownexit/late sent-command cancellation stay honest; close sessions, idle-owner ET authority, readers and processes separately with independent bounded cleanup. ExtraAPI/file stops for review.

## Confirmed14 files

New NetworkFlowAdapter.cs, NetworkRoleHost.cs, FlowRoleProtocol.cs. Existing FlowAcceptance.csproj (only existingLiteNet ref), Program.cs, FlowContracts.cs, FixedFlows.cs, FlowRuleEvaluator.cs, FlowOrchestrator.cs, FlowReport.cs; same CookingFixedFlowTests.cs and fixed-flow.md; new compete-network.json/pickup-drop-network.json. All under existing approved FlowAcceptance/test/docs directories. Others read-only.

Once accepted S1: one focused S2 build/window, two real three-process positives, necessary wrong-binding/oldprojection/replay/EOF/childfault controls and short affectedS1 regression. Pickup convergence before replay/drop. No old Rich matrix/physicalLAN/productAPI change. S3 later uses existing approved three CLI routes.
