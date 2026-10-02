# Reviewed S07 domain increment

Dispatch ctx_aa1cb5f5926a; base 901f465b5; owner authorization supersedes historical planning-only text. Existing Orca-created cooking-supply-s07 checkout is the new supervised worktree for this dispatch.

## Design and acceptance before implementation

The gap is that CookingSupply.cs plans reservations but the single Recipe authority does not own physical delivery creation. Add an optional frozen Supply configuration to fixture/content/config identity, requiring real world anchors, raw (non-container) unit definitions and movable package containers accepting those units with adequate capacity. Ordinary receiving anchors have one physical slot. Old fixtures without Supply remain valid.

Append explicit operations 20=RequestSupply, 21=ReceiveSupply, 22=TakeSupply; preserve 0..16 and reserve 17..19 for S06. Payload uses SupplierId, DeliveryId and SupplyRequestId, never Station or Recipe aliases. One request reserves one package. Supply advances on the kitchen fixed-tick candidate and commits with kitchen state. Receive preflights permissions/range/slot/counters and every allocator result against the complete registry including tombstones, then installs candidate ledger, package, N raw objects, indexes and allocation watermark together. Infinite Take alone creates one raw object in an empty hand. Finite TakeOut reuses existing objects.

Persist immutable provenance for every received delivery and infinite request: original package and raw ItemIds survive movement, processing and tombstones. Different command identities retrying a received delivery return its original physical result. Required Recipe schema 5 fields contain supply ledger and origins; same-Level restore validates identity, complete one-to-one associations, definitions and counts before install, preserves Closing and pending clocks. Success handoff carries the ledger and origins, reopening requests only after valid handoff admission.

Acceptance: real domain request→fixed tick→receive→pickup/drop/takeout→processing/order; exhaustion, occupied receiving slot, range and permissions; allocator failure/duplicate/tombstone/overflow zero physical or ledger mutation; semantic duplicates return original identities; checkpoint roundtrip and corrupt aliases/missing/ghosts reject without mutation; success handoff preserves stock/pending/origins and reopens requests. Focused tests plus cooking-kitchen-loop gate when build succeeds.

## Owned files and deferred integration

Recipe supply partial and tests; RecipeLoop fixture/command/result/snapshot/fixed tick; RecipeCheckpoint and extended validation; content/configuration supply validation/freeze/identity; CookingSupply.cs only necessary support. No menu or SubmitOrder changes. S06 exclusively owns Level lifecycle, ET host, Level checkpoint and manual routing. Coordinator must integrate full ET fingerprint payload, preparation same-instance admission, front-close StopNewSupplyRequests hook, pause/failed-retry/success-baseline host coverage and Level envelope version after S06 format 6. These are remaining product exit requirements, not claimed domain completion. No network, Unity, prices, cancellation or penalties.
