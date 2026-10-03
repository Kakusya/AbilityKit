# Rich command diagnostics independent source review

Reviewed frozen eight-file diff82ff86592..2c5fe0bb0 on2026-10-03. SOURCE ONLY; no build, test, socket or runtime grant consumed. N03 artifacts/check references and rich diagnostic design/source audit consulted. P6 dirty files and cache preserved. No production correction, timer adoption, rich four-cut, P6, performance or physical acceptance inferred.

## Findings (not fixed)

1. BLOCKER: RichObserver.OnBytes and Send both use `if (header.OpCode != ... || !TryDecode(...)) continue;`. A complete undecodable frame therefore produces no diagnostic callback/error row: correlation legitimately remains unknown, but actual callback byte count/mapping/copy/forward/decode outcome is lost. Existing Session forwarding must remain exactly once and ordering unchanged. Record a bounded diagnostic-only failure row before the existing continue, without converting this observation into a new production exception. DecodeEnd must describe the completed failed attempt. Exercise both unexpected opcode and codec rejection in a real observer-path harness.
2. VERIFICATION GAP BEFORE RUNTIME: RichDiagnosticControls currently tests helper records, synthetic rings/waits/mapping/options and optional legacy deserialization only. It never invokes RichPeer.Open callback or Poll, observer forwarding, exception/export preservation, nor controlled ON/OFF legal-command graph/result equivalence. Thus its synthetic callback row cannot establish actual incarnation-ignore, queue/dequeue/install and terminal association behavior. Add meaningful app harness controls for actual peer/observer paths (fake existing interfaces permitted, no new authority), and preserve original errors through diagnostic/export faults. Full legal graph equivalence remains an actual paired endpoint obligation; do not label helper controls as such.
3. TIMESTAMP SCOPE LIMIT: RichObserver decode span brackets TryDecode only, not the following typed Read calls. RichPeer Installed is post-Receive and covers typed validation, but no explicit pre-Receive timestamp exists beyond Dequeued. These fields must be described as such; do not claim isolated typed decode or Receive CPU spans. This is a reporting clarification, not a proven network cause.

No fixes made: dispatch expressly requests source review and findings before producer corrections.

## Verified source properties within scope

OFF constructs no collector; null conditional invocation suppresses row/getter argument evaluation; no new CaptureFullState, RichProof.Capture, hash-per-poll or owner ticks added by diagnostic hooks. Existing Pump capture is pre-existing. Existing observer copy-before-forward-once-then-observe order retained. Actual bytes.Count/FramePayloadBytes are recorded rather than encoded size probes. Getter reservations capped8, selected timing rows capped16 with original total disclosed; waits64/callbacks256/frames64/snapshots8 and text4096 with truncation disclosure. Private Waiting/DeferredAck/internalACK explicitly UNOBSERVABLE_PRIVATE. Nested wait IDs preserve original deadlines; normal terminal and subsequent projection wait starts/targets are distinct. Selection matches typed participant and stable ID rather than synthetic366 command execution. Exact effective selectors and wrapper provenance are recorded. Getter-only optional endpoint extensions leave legacy business/verifier fields unchanged; actual legacy read remains NOT_RUN. Snapshot and frame records reference small identities/participants/dispositions, not repeated full business graphs.

## Verification

- Lint: NOT_RUN (source-only grant).
- TypeCheck/build: NOT_RUN (root serial .NET window).
- Tests: NOT_RUN. Source review identifies missing meaningful controls above.
- Reviewed source status: not ready for runtime acceptance; correct observability blocker and review focused controls before compile grant.

## Scoped producer correction follow-up

Root granted source-only edits in RichObserver/RichPeer/RichDiagnosticControls plus this record. Raw opcode/codec rejection now emits bounded diagnostic failure rows before preserving the original continue, both directions, without reparsing or a new production exception. Actual typed Read/validation has separate `observer.typed-read-validation` rows, and peer owner Receive has separate actual DecodeStart/DecodeEnd brackets under source `peer.typed-receive-validation`. No shared report schema fields were added.

Meaningful controls now instantiate the real RichObserver and RichPeer/ConnectionManager with fake public IChannelListener/IServerChannel/ITransport interfaces, without sockets or authority. They assert fragmented and multiple-frame forwarding exactly once; actual callback mapping; inbound/outbound malformed/opcode rows while original skip behavior remains; original forwarding exception identity and bounded diagnostic text; real packet callback queue-before-Poll, Reopen queued old-incarnation ignored, current typed Joined installation, malformed callback error and subsequent Poll exception. Both diagnostic ON/OFF actual observer/peer paths are exercised. Joined messages are explicitly synthetic parser/lifecycle fixtures, not real authority grants or receipts; no full business equivalence is claimed.

Outstanding shared RichRunner failure/export preservation and full ON/OFF legal-business graph equality require separate runner seam review or real paired endpoint execution. Root informed of ownership limitation; not replaced by helper mocks. New controls and source remain NOT_BUILT/NOT_RUN pending root serial grant. `git diff --check` passed (line-ending conversion notices only). No P6/timer/planner/framework/budget edits.


### Additional root-authorized actual failure/export correction

Root granted RichRunner catch-path ownership after finding the fallback write could throw before the original error reached stderr. Catch now emits bounded original and export errors first, guards the actual fallback write, emits bounded fallback error if that also fails, and returns1. Fallback string fields remain strings with additional truncation-detail objects; this is failed/incomplete export metadata, never a successful endpoint report. No Host/Client/authority/transport paths or budgets changed.

ActualRunFailurePaths invokes real RichRunner.Run with invalid-role so the first role check rejects before Host/Client/authority/socket creation. It verifies a normal fallback file records original Role failure and export failure; an existing directory as destination forces both actual normal Write and fallback Write failure, verifies original/export/fallback stderr and return1. It owns a GUID temporary directory, restores Console.Error and checks resolved temp boundary before cleanup. These controls remain NOT_RUN until root grants serial .NET. Full ON/OFF legal business graph equivalence still requires real paired endpoints.
