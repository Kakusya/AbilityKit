# Cooking LiteNet framing storage compatibility — source prepared, execution pending

## Authoritative kind-specific correction (supersedes initial Join fixture paragraphs below)

Root caught a source fixture error in7a9dbf784 and RichFrameControls676eb20fe before any execution: padded8MiB Join is NOT wire-valid. Defaults are ControlBytes4096, CommandBytes16384, FrameBytes8388608; TryDecode's final cap depends on kind. These budgets remain unchanged. Initial fixture paragraphs below are preserved as historical source error, not executable requirements or runtime evidence.

Corrected transport/runner max-wire fixture is a Baseline-kind envelope padded8MiB, with explicitly synthetic parser-only payload; byte echo does not prove typed baseline/business acceptance. SessionV3 now uses real trusted host capture to form a typed Baseline padded8MiB: upstream Session must reject it as MalformedCommand (nonJoin/nonBaselineAck control), with no participant binding/generation/Ready/cleanup. This constructed typed payload is not an actually Host-issued or client-accepted baseline. Genuine Join acceptance instead uses4096 inclusive; Join4097 is rejected ProtocolMismatch before owner admission. Each scenario has a fresh actual trusted ET Session. Actual published typed Host-to-client maximum-baseline acceptance remains a separate execution gate beyond byte echo and wrong-direction rejection.

Eight cases are now source-prepared: three UDP storage boundary, two malformed prefix/body and three actual Session control-inclusive/control+1/unexpected-direction baseline. Planned focus filter is `FullyQualifiedName~CookingLiteNetFramingBoundaryTests|FullyQualifiedName~Actual_ET_Session_admits_control_bound_Join`. No code-budget change, execution, pass or stage acceptance is claimed. Initial7case/8MiBJoin descriptions below are superseded by this correction.

2026-10-03. Root subsequently approved source implementation at six listener compositions excluding RichRunner (owned by rich diagnostics worker). Six source configuration edits and new CookingLiteNetFramingBoundaryTests are ready for review; no .NET execution in this increment. Runner676eb20fe codec files remain frozen. Public framework defaults/APIs and Cooking wire/token/collection/history/time budgets remain unchanged.

## Contract and disposition

Cooking FrameBytes limits the wire payload to8388608 bytes. NetworkPacketHeader.Size is16 and length prefix is4; a maximum legal payload occupies8388628 bytes at the transport message boundary. LiteNetServerChannel.Receive always enqueues the entire message before Drain, and its current default aggregate queued byte ceiling8388608 therefore rejects this legal message even with an active subscriber.

Proposed application composition: pass existing LiteNetChannelListener constructor `maximumBufferedReceiveBytes: checked(bounds.FrameBytes + 4 + NetworkPacketHeader.Size)`. Default bounds yield8388628. This is a20-byte framing storage allowance, not extra accepted wire payload. Keep generic constructor default8MiB, queue count1024, application ingress capacities and Cooking bounded decoder FrameBytes+64 unchanged. Do not modify framework dependencies or expose Cooking types in framework. No new framework API is needed. The storage ceiling remains aggregate pending bytes, not a guarantee of accepting multiple simultaneous maximum messages.

Use explicit per-composition calculation with the same CookingNetworkSessionOptions used by that Session. If reviewed repetition warrants a helper, it belongs in the application tools, never the generic transport package; helper API/design approval would be separate. Do not silently substitute FrameBytes+64 for transport accounting: actual framed bytes are exactly payload+20.

## All discovered Cooking listener composition sites

Full `rg` scan of src for LiteNetChannelListener finds these seven Cooking sites; each currently omits the receive-byte argument:

| File | Current role | Proposed scope |
|---|---|---|
| NetworkAcceptance/Program.cs | UDP acceptance host | explicit application framing storage |
| NetworkMeasurement/Program.cs | measurement host | same; new source/runtime provenance and fresh measurement needed |
| NetworkProcessMeasurement/Program.cs | previously frozen independent-process host | cannot edit under this investigation; separate owner approval and refresh accepted binary evidence |
| NetworkImpairmentMeasurement/Program.cs | frozen relay workload host | separate approval and audited rebuild/new P0 before numerical profiles |
| NetworkConcurrencyAcceptance/Program.cs | observed concurrency host | separate approval and refresh paired evidence |
| NetworkRichRecoveryAcceptance/RichRunner.cs | observed rich host | next narrowly approved implementation candidate |
| Cooking.Tests/CookingNetworkSessionV3Tests.cs | actual UDP test factory | same explicit configuration, generic transport controls still use generic default |

CookingNetworkSessionHost accepts IChannelListener from caller and constructs bounded local/remote NetworkHost codecs; it does not construct the LiteNet listener itself. In-process listener does not use this UDP queued-byte ceiling. RichObserver wrapping forwards the underlying listener; observer frame readers already use8MiB+64. Client LiteNetTransport dispatches received bytes without this server pending-byte queue; actual client bounded codec/wire rejection must still be tested.

## Existing tests and evidence gaps

LiteNetChannelListenerTests contains actual UDP basic framing, peer isolation, deferred copying, reconnect, cancellation and admission controls. `Receive_buffer_overflow_closes_unhandled_peer_instead_of_growing_without_bound` uses a2-byte configured ceiling. `Existing_frame_limit_reports_error_without_delivering_business_request` exercises generic default decoder rejection. Neither proves a legal8MiB Cooking wire payload plus20-byte framing across actual UDP. These existing generic tests must remain unchanged and continue passing. LiteNetLib dependency is2.1.4; maximum reassembly feasibility is not inferred from constructor/source predicates. Actual execution must demonstrate it, with bounded failure artifacts if transport/library rejects it.

## Required controls before accepting compatibility

1. Build a valid Join wire envelope padded only with legal trailing JSON whitespace to exact FrameBytes. Verify Cooking TryDecode passes exact and rejects FrameBytes+1 before transport; do not enlarge token/collection limits. Join is a control envelope, not a fabricated business baseline.
2. Actual LiteNet client sends framed exact8MiB wire payload to an application-configured listener. Observe exact8388628-byte received message, content hash and correctly decoded opcode/payload. Through actual Cooking Session, the valid envelope must enter normal admission and receive genuine Joined/baseline or a legitimate documented admission result, never fake receipt/authority. This exact-bound proof does not replace rich business recovery.
3. Fresh actual channel sends raw framed message of8388629bytes: exact storage+1 must report receive overflow and close without application handler delivery. Separately send8MiB+1 wire payload framed with unchanged header: same overflow; wire TryDecode rejection independently proves the business budget. Do not pretend transport overflow demonstrates wire parser execution.
4. Fresh actual channel sends oversized advertised body prefix, malformed header/payload length or invalid JSON within storage bound. Distinguish bounded frame decoder rejection, wire rejection and channel error/close; no business admission or state mutation. Capture exact reasons, callback counts, session/public state before/after, bytes/hashes and exits. Test without fake port/reflection/private mutation.
5. Server-to-client exact-bound frame traverses actual LiteNetTransport and bounded application codec, with exact bytes/wire validation. Keep generic default4MiB decoder and generic default8MiB listener controls as negative compatibility controls; no global fixes.
6. Reduced configured queue control checks below/exact/+1 aggregate byte behavior and unchanged1024 count semantics using existing public listener/channel subscription seams, without using giant UDP flood as a deterministic queue count test. If activation/subscription synchronization cannot deterministically retain queued data, explicitly defer that additional test rather than invent owner-loop evidence.

Use one fresh bounded pair per negative closure, actual owned handles and separate logs. Existing test deadlines and workload budgets stay unchanged; large-message timeout failure remains evidence, not a reason to increase them. Root serializes .NET and approves file ownership before implementation. Runtime/configuration changed tool evidence must be refreshed; old accepted profiles are historical source-specific results.

No actual transport acceptance, physical two-PC, performance or whole network completion is asserted by this design.

## Prepared source and explicit pending evidence

All six assigned compositions now pass the calculated byte ceiling; NetworkMeasurement uses its actual `options.FrameBytes` (including receipt-capacity control mode). Other five Sessions omit explicit options and therefore use the same default CookingNetworkSessionOptions as their listener calculation. All configuration edits use NetworkPacketHeader.Size rather than a magic header size. RichRunner replacement, handed off through root: `new LiteNetChannelListener(IPAddress.Parse(address), port, "abilitykit-cooking-v3", maximumBufferedReceiveBytes: checked(new CookingNetworkSessionOptions().FrameBytes + 4 + NetworkPacketHeader.Size))`.

New test class has no ET host, fake transport or authority. Positive actual UDP test sends valid whitespace-padded exact8MiB Join wire as a framed message, verifies exact server bytes, echoes over actual reliable UDP, then decodes and validates exact client payload. Negative fresh pairs prove unchanged generic default rejects the valid exact8MiB wire frame, and application storage rejects8MiB+1 wire (frame/storage+1); zero channel delivery, actual error and close are required. Timeout is existing LiteNet test8s, not increased. Independent wire assertions precede transport send. SDK default Compile includes the new test; existing project already references LiteNet.

Root additionally approved genuine Session and malformed source controls. SessionV3's existing actual trusted ET fixture and public authority adapter now cover exact8MiB valid Join received as actual Joined/baseline with matching instance/player/generation and real connected owner projection; a fresh invalid-JSON exact8MiB frame must receive genuine ProtocolMismatch with no participant binding/generation/Ready/cleanup. Owner frames run on the synchronous test owner, preserving ET singleton collection. New transport test class also exercises actual NetworkHost with an explicitly bounded public codec, oversized advertised prefix and header/payload-length mismatch; no request handler delivery and actual decoder SessionError are required. No fake authority or private state mutation.

`git diff --check` passed source-only. Planned sole-window command: `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter "FullyQualifiedName~CookingLiteNetFramingBoundaryTests|FullyQualifiedName~Exact_eight_MiB_wire_reaches_actual_ET_Session"`. Seven cases are prepared (three transport boundary, two malformed, two actual Session valid/invalid). Compilation and all execution remain pending, not a runtime acceptance claim. Deterministic aggregate queued-count control is explicitly deferred; actual whole tool/profile refresh remains required. Original accepted profile binaries/results remain source-specific history.
