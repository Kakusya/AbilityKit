# Master: reviewed supply, preparation and same-Level layout

Production source merged locally to `d0eeb4b42`, from reviewed integration `b503c1a7d` and its prerequisite supply/preparation commits. A source diff of the Cooking domain/runtime and both test directories was empty immediately after import. The only cherry-pick conflict was supply task metadata: retained newer coordinator evidence, incoming verified domain status and in_progress state. No source conflict, remote push or Unity changes.

Actual final integration gates before import:

- `20261002-221130-cooking-kitchen-loop`: 468 focused / 596 Cooking / 236 ET passed; zero failures/skips; 33.2 seconds.
- `20261002-221408-cooking-et-level-runtime`: 596 Cooking / 236 ET passed; zero failures/skips; 23.0 seconds.

Actual post-merge master gates:

- `20261002-221725-cooking-kitchen-loop`: builds and 468 focused / 596 Cooking / 236 ET passed; zero failures/skips; 34.6 seconds.
- `20261002-221902-cooking-et-level-runtime`: analyzer/runtime builds and 596 Cooking / 236 ET passed; zero failures/skips; 22.5 seconds.

Master raw logs/TRX are under local/Logs/test-gates with those exact run IDs. Summary copies beside this report record commands/results. Historical shared dependency warnings are not represented as a blanket zero-warning repository claim.

Verified limited behavior: finite/infinite physical supply, allocation provenance/watermarks, ET supply fingerprints and Closing rules; one preparation kitchen shared with Start, with zero front service time during preparation; Preparing and Running recovery with service offset; actual trusted initial/dynamic layout installation, same-layout idempotence, live/reference-state validation, movement and derived front routes, same-Level restoration before Recipe/front state.

Direct controls include occupied equipment relocation preserving item identity, locks and elapsed progress, actual old-position Continue/Pickup rejection and new-position ET continuation; manual claim release and reclaim; preserved movement watermarks; complete uninterrupted-versus-restored final checkpoints for both Preparing and Running changed layouts. Invalid layouts preserve the complete checkpoint. Invalid trusted initial references fault and release the unpublished host. A real malformed JSON test found null layout seed/entry hash exceptions; the guard now returns structured RecordTruncated, with red/green evidence retained in integration local/Logs/prepared-layout-codec.

Independent review and producer evidence: [layout ET review](prepared-layout-et-independent-review.md), [layout producer](prepared-layout-et-increment-review.md), [core review](preparing-layout-core-independent-review.md), [Preparing recovery](preparing-checkpoint-composite-verification.md), [supply composite](supply-composite-verification.md).

Current master formats: definition3 / Recipe5 / Level7. Previously unpublished candidate7 variants are not save migration contracts. Legacy factories have explicit null preparation/layout fields; preparation factories require a validated installed layout.

Remaining: cross-Level new-layout/permission/carry transaction, confirmed-baseline failure retry and durable product loading, complete menu Ready availability and frozen aggregate Observe, full S14 natural operating/replay controls. S06/S07/S08/S14 remain in progress. Network and Unity are not verified by these .NET gates. Observation source and host integration are subsequent integration WIP, not part of this master result.
