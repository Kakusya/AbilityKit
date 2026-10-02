# Session v3 independent review

Latest decision: ACCEPT the bounded Session v3 producer increments `2f5437e58` and `b43988a996a53ee66ff69f6398938efd8fd0ac02` for integration. The four initial boundary findings and the source-backed outbound-bound finding are closed. Independent rich-process rerun and broad network/Cooking/ET gates remain root-owned acceptance steps; this is not N02 completion or physical LAN/Unity acceptance. Review was read-only, without new .NET execution.

## Reviewed source and evidence

Managed tree: `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-et-s14-integration`. Reviewed Session host/client/wire/frame codec, new Session and wire tests, project references, and producer report. Initial authority base includes `23b4c4124`; Session display increment is `2c9089197`. Uncommitted producer fixes must be reviewed separately.

Independently parsed `src/AbilityKit.Game.Cooking.Tests/TestResults/session-v3-projection-green.trx`: total/executed/passed 19, failed 0, notExecuted 0. Corresponding producer log is `local-session-v3-projection-green.log`. This evidence predates the four findings below. It proves neither their corrections nor a physical second-PC LAN run.

## Findings not fixed by this reviewer

1. **Already-bound physical Join can replace or corrupt ownership.** `CookingNetworkSessionHost.Join` initially accepts an already-bound connection. Joining another authenticated participant overwrites its Participant while the original participant still references that same active connection. Repeating its own Join also advances generation/source without retiring the previous source or clearing the issued baseline. Require an explicit zero-change AlreadyBound rejection or equivalent reviewed idempotent behavior. Producer chose AlreadyBound rejection.

2. **Rebind token survives generation changes.** Participant.Token initially has only a getter and is generated once. A successful new-generation rebind returns the same credential, allowing the old token to authorize another takeover. Rotate the token on successful live rebind while retaining the logical business mapping; reject the old token. Existing assertions expecting unchanged tokens must be corrected to the accepted contract.

3. **Client accepts inconsistent scope chains and nested state.** `CookingNetworkSessionClient.TryInstallBaseline` initially rejects only decreasing epoch and non-increasing snapshot sequence. A rehashed baseline with another Level at the same epoch, or another Match/RestaurantRuntime, can pass. It also lacks nested observation/lifecycle/recipe/full-recipe/resumable scope binding checks. Pin Match/runtime for the same server instance, require strictly increasing epoch on scope changes, and validate the complete typed state. Created FullRecipe.LevelScope may legitimately be null; do not reject that valid shape.

4. **Faulted authority can keep advertising old Running state.** `ProcessOwnerFrame` initially skips installing failed captures but retains LatestCapture and connection Ready/Issued. Join/Publish can therefore issue an old healthy baseline after the real ET authority has faulted. Faulted/Disposed must revoke ready admission, retire pending callers, and return structured Unavailable without issuing the stale baseline. Busy is temporary and must not publish an intermediate state. Add actual allocator-fault coverage for both an existing client and a new Join.

The producer acknowledged all four findings and planned actual duplicate-Join, old-token, malformed scope, and allocator-fault controls. No production source was changed by this reviewer.

## Positive bounded review

The v3 Session injects the pure authority port and does not create a second simulation. Framed local and reliable UDP remote inputs share the existing NetworkHost path. Transport callbacks enqueue immutable bounded envelopes; owner-prefix processing performs authority work. Strict wire decoding checks required nullable fields, unknown/duplicate properties, enum values, frame/string/depth/collection bounds and readonly list freezing.

First command mapping retains its domain ID, original payload/batch and accepted ordinal across retransmission. Pending conflicts go through the authority group cancellation path; terminal conflicts do not replay old batches. Caller cancellation and scope retirement are source-specific. Issued baseline acknowledgement compares the actual issued identity rather than a guessed current hash. Session projection is sorted, frozen and hash-covered; LastTerminal is a maximum response watermark, not a contiguous permission or business-execution cursor. Major progress is display-only, including future unlocks, and gives no client mutation authority.

The observed tests include reverse-player arrival with one ET tick, real loopback UDP/local composition, pause/rebind, pending and terminal conflicts, source cancellation, bound rejection, complete capture roundtrip, major projection and non-contiguous watermark controls. They do not establish independent-process rich operating acceptance, physical LAN, Unity, or final broad gates.

## Verification

- Static source review: completed for the bounded initial increment; four blockers remain pending corrected-source review.
- Existing producer TRX: independently confirmed 19/19 passed, zero failed/skipped.
- New tests, lint/build/type-check and broad gates: not run by this read-only reviewer; root serializes those windows.
- Final readiness: withheld until corrected source and actual focused evidence close the findings.

## Additional bounded-policy review - pending measured rich payload

Root reported a rich operating run where Procure was accepted but the client projection stopped advancing. The exact failing decoder condition is not yet measured; this report does not attribute that run to a particular threshold without its actual payload.

Two static facts are established in `CookingNetworkWireCodec.TryDecode`, `CookingNetworkSessionHost.Send/Publish`, and client `OnPacket`:

- ReceiptCapacity defaults to 16384 per Level, but the wire scanner rejects any array/object containing over 4096 values. Complete Recipe capture includes receipt collections, so a full baseline at the advertised receipt capacity cannot satisfy this collection bound. Full capture may additionally repeat Recipe data inside the resumable Level checkpoint. The effective operational state capacity is therefore smaller than the identity-admission cap unless the accepted contract explicitly defines how this boundary is handled.
- The scanner's 65536 counter counts every Utf8JsonReader token, including property names, start/end delimiters and scalar values. The design describes scalar/collection nodes; those are not identical metrics. A rich nested state can exhaust the implementation counter substantially before 65536 semantic values. Actual token totals and the intended metric need owner adjudication, not a silent constant increase.
- Encode serializes without applying the scanner. Host Send validates only total FrameBytes, not token/collection/string/depth or per-kind limits. Publish increments SnapshotSequence and stores Issued before sending. Thus a baseline can be issued and transmitted while the same codec will reject it on receipt; client OnPacket returns silently on decode failure. Byte overflow instead throws an ordinary exception, also after issuance changes. Neither path currently provides a structured baseline-capacity disposition.

Required decision and evidence: capture the exact failing rich frame, byte/token/depth/largest-collection counts and decoder rejection; resolve the advertised receipt and complete-state capacity together. Validate the frozen outgoing baseline using the agreed wire rules before publishing its issued identity. A rejected complete baseline must not truncate receipts/state or appear issued; it needs a structured bounded failure, with client synchronization/admission withdrawn as appropriate. A previously valid business command must not be replayed merely because its subsequent projection cannot fit. Do not infer whole-frame/business rollback from a post-Tick publication failure.

No producer source was modified and no .NET was run for this additional review. Root and producer were notified. This remains an explicit pending contract/runtime finding, separate from the original four fixes.

## Four-boundary corrected-source review

Reviewed frozen commit `2f5437e58` in the Session worktree. All four original findings are closed for this bounded increment: already-bound Join rejects before mutation; successful new physical rebind rotates the current token; client pins the same-instance Match/runtime and enforces advancing scope epoch plus nested checkpoint scope consistency; owner capture precedes control/admission and Faulted/Disposed latch unavailability, clear Ready/Issued, terminalize waiting callers and notify clients. Client terminal unavailable state cannot be reversed by a delayed Ready or baseline. Existing display remains readonly rather than being presented as a new healthy authority capture.

Independently parsed `session-boundary-red.trx`: four executed tests, four failures against the previous source. `session-boundary-final.trx`: 23 executed/passed, zero failed/notExecuted. Both are under the Session worktree `src/AbilityKit.Game.Cooking.Tests/TestResults/`; producer logs are `.trellis/session-boundary-red.log` and `.trellis/session-boundary-final.log`. The repaired tests exercise actual physical Join/rebind, recomputed-invalid scope baseline, and real ET allocator fault. No tests were rerun by this reviewer.

## Approved wire-bound contract revision, awaiting implementation evidence

Root measured rich action 171 baseline: 828796 bytes, 65672 JSON tokens; authority frame 358 versus client 356. This is an actual self-decode failure at the previous 65536-token cap, not an inferred receipt-limit failure. Root approved baseline-only injectable limits of 1048576 tokens and 16384 collection entries. Command/control retain 65536 tokens and 4096 entries; byte limits remain 8 MiB / 16 KiB / 4 KiB. Receipt identities 16384 is an upper bound intersected with actual byte/token/state capacities, not a guarantee that every possible 16384-receipt baseline fits.

Required pending review: outgoing full baseline must pass the same codec bounds before SnapshotSequence/Issued publication. Failure must expose FullStateExceedsWireBounds, revoke Session admission/synchronization, terminalize pending callers, reject new Join, and preserve complete authority receipts/state without truncation. The client must recognize that unavailable reason. Actual boundary red/green evidence and a representative rich rerun are still pending; the 23-test boundary result does not prove this later revision.

## Frozen wire-bound increment independent verification

Final reviewed commit: `b43988a996a53ee66ff69f6398938efd8fd0ac02`, clean producer tree. Reviewed exact codec/Host/client diff and all four added bound controls. Encoder serializes then validates with TryDecode using the caller's options. The reader selects baseline limits from the root kind, then performs the complete bounded scan that still rejects duplicates, depth/string excess and malformed enums/properties. Non-baseline token/collection limits remain unchanged. The expanded limits are explicitly baseline-only; all byte caps and Recipe5/Level8 remain unchanged.

Host Publish uses a candidate next SnapshotSequence without mutating the counter. Complete encoded baseline validation precedes both counter and Issued assignment. Bounds rejection calls MakeUnavailable(FullStateExceedsWireBounds): waiting callers receive terminal dispositions, all live bindings lose Ready/Issued, the projection is refreshed, and clients receive structured rejection. That reason is terminal for this Session, unlike temporary Busy. Existing client display remains retained but unsynchronized; delayed baseline/Ready cannot reopen its admission. New Join is rejected and subsequent owner calls return before authority ConsumeFrame. The authority data is neither truncated nor rewritten. Already accepted domain work stays accepted even when later projection publication fails.

Independently parsed `src/AbilityKit.Game.Cooking.Tests/TestResults/session-bounds-final3.trx`: 27 executed/passed, zero failures and notExecuted. Producer log `.trellis/session-bounds-final3.log`. The real receipt fixture executes 1600 accepted alternating Pickup/Drop commands against the same ET Host and compares complete Recipe and resumable Level canonical text after wire roundtrip. It asserts actual token count exceeds 65536 and the old injected policy rejects those same bytes. Other controls cover initial token-limit-one Join failure, initially Ready growth into explicit unavailable with retained receipts/no subsequent state advancement, and baseline-only collection expansion.

Earlier bounds runs are preserved fixture failures, not an old-production red claim: the first fixture generated only 44904 tokens, later growth initially crossed the cap during handshake, and a world Drop fixture lacked spatial permission. The actual old-policy failure evidence is root's rich 65672-token self-decode plus the final old-limit rejection assertion on the accepted 1600-command state. Original four-boundary red remains independently parsed four failures against prior source, followed by 23 passing controls.

No remaining blocker was found in this bounded corrected source. Representative rich process acceptance, full SDK/Cooking/ET gates and second-PC physical LAN remain outside these focused results. The report preserves the earlier findings as review history; its latest conclusion is the paragraph at the top.
