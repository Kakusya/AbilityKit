# Session v3 independent review

Latest decision: NOT READY. Four source-backed blockers were reported to root and the Session producer. The producer has acknowledged them and is implementing corrections; this report does not pre-accept those corrections. Review is read-only, without a new .NET invocation.

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
