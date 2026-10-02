# S14 recovery corrections: actual focused evidence

Current source remains uncommitted work on accepted master `07bb27d54`; this report does not replace its broad gate evidence or declare S14 complete.

## Reproduced and corrected

- Removed disposable objects retained historical PlayerHand locations. Restoring indexed those tombstones as live hand occupants. Two actual tests failed (duplicate hand occupancy or resurrected removed cup); checkpoint validation and installation now index only nonremoved items. Binding/recovery/tamper focus: 35 passed, zero failures/skips.
- Confirmed cooking acceleration produces a legitimate duration different from the recipe base duration. Fixed-tick validation, checkpoint restore and private generation staging rejected that legitimate work. Four new controls initially produced three failures and one pass. Validation now accepts the recipe base duration or the duration computed by independently trusted current major progress; arbitrary saved durations remain rejected. Progress is installed before generation-copy, durable handoff/load and trusted same-Level restore validation. Combined timing/binding/recovery/tamper/major-progress focus: 45 passed, zero failures/skips.
- Public retry-choice application moved process bindings before rejecting an unknown or duplicate unlock. Two controls actually failed full-checkpoint equality. Application now stages privately and publishes only accepted validated state; the public unknown-choice contract remains strict. Combined focus including these controls: 47 passed, zero failures/skips.

Actual root evidence: `local/Logs/tombstone-hand-red.log`, `tombstone-hand-green.log`, `effective-timing-red.log`, `effective-timing-green.log`, `retry-atomic-red.log`, `retry-atomic-timing-green.log`; corresponding TRXs under `local/Logs/tombstone-hand/`, `effective-timing/`, `retry-atomic/`. These are focused product tests, not full gate results.

## Integration under verification

Additional actual control: removing the grant required by existing accelerated work initially accepted invalid future state (1/1 red). Staged state now validates against intended new progress before publication. Combined focus passed 48/48, zero failures/skips. Evidence: `local/Logs/retry-grant-red.log`, `retry-atomic-grant-green.log` and retry-atomic TRXs.

Technical ET final producer focus passed 5/5, zero failures/skips; frozen owned test/report commit `14d6d8462`, detailed `technical-recovery-increment.md`. The fifth missing-current-material global unlock control is now green. These focused results do not constitute assembled broad-gate evidence; the paragraph below summarizes the original reproduced boundary.

The technical ET fixture reproduced duplicate placement of a confirmed raw-material unlock and rejection of legitimate accelerated work. Four cases passed after root corrections. A fifth case then actually failed when a retained global unlock did not exist in the current level content. The Host now selects eligible placements from current trusted content, standard supply and scoped menu permission while retaining complete global choices. Its final actual five-case focus passed; integrated gate acceptance remains pending.

Clean-pool duplication was considered and retracted: the pool is seeded by the original fixture constructor; standard initial supply skips clean-pool entries. Preserving those entries in the temporary standard-supply filter is clarity, not evidence of a reproduced pool defect.

Remaining acceptance: independently review these corrections; validate actual catalog standard-stock retry and known-but-currently-forbidden unlocks; assemble frozen natural successor, narrowed carry and technical recovery tests; run integration and master kitchen/ET gates; reconcile S14 exit records. Healthy owner-declared technical Failed and runtime exception quarantine/dispose/cold successful-baseline recovery remain separate routes. No new normal business failure, network or Unity completion is inferred.

Actual full-catalog natural fixture retry: two controls failed with GameplayInitializationFailed; diagnostic located duplicate pristine tool placement at world:h0. The trusted factory already seeded the declared standard empty tool. Host now fills only missing standard entries when matching initial objects are pristine, empty and have no process anchored to them. Producer subsequently passed all four natural cases, zero failures/skips, preserving natural service/replay and durable successor continuation. The registered-but-currently-disallowed global unlock is retained without physical placement. Exact frozen input and broad acceptance are still pending.

Static records verifier rerun passed:21 tasks/20 children/124 features/87 menus/160 links, acyclic dependencies and byte-identical original files. It did not execute product tests.
