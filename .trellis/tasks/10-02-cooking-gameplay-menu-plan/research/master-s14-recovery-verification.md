# Reviewed S14 recovery increment

Production master `86c3eb41d` imports exact frozen natural tests `cf4d7c467`, narrowed carry `9cabcd347`, technical recovery `14d6d8462` and root-reviewed recovery corrections. All twelve source/test files were byte-identical to accepted integration input before source commit. Architecture remains Recipe/Front/Level application owners with one ET fixed tick; definition3/Recipe5/Level8/typed major baseline3 are unchanged.

Actual integration gates:

- `20261003-015225-cooking-kitchen-loop`: 644 focused /772 Cooking /298 ET, zero failures/skips,55.444s.
- `20261003-015338-cooking-et-level-runtime`: 772 Cooking /298 ET, zero failures/skips,43.790s.

Actual postcommit master kitchen `20261003-015510-cooking-kitchen-loop`: passed644/772/298, zero failures/skips,57.178s. Postcommit master ET `20261003-015625-cooking-et-level-runtime` passed772/298, zero failures/skips,44.208s. Actual summary JSON copies are adjacent to this report.

Commands are `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop` and the same runner with `-Gate cooking-et-level-runtime`. Integration summary JSON copies are adjacent; exact gate directories retain TRXs and step logs. Master outer logs: `local/Logs/cooking-s14-recovery-master-kitchen.log`, `cooking-s14-recovery-master-et.log`.

## Behavior and review

Actual natural source service ends Success with finite procurement, two-player work handoff, F01/D31 deliveries, one unmet natural departure and cleaning. Durable successor/fresh-store Created load compares full live/cold created state and319 next-service frames through actual F01 manufacture/customer delivery. Records are61,554 characters under the unchanged1,048,576 bound. The second service remains Running; a second natural End is not claimed.

Narrower-scope controls retain existing drink/raw/manual/batch/pending state while rejecting new forbidden procurement/material use/process/portion actions; approved pending receipt and exact-once semantics, take/drop/clear/discard and same-Level recovery remain usable. This is a scoped unit; the separate natural test proves actual Front operating.

Technical controls start from a trusted durable successful load: healthy owner-declared Failed discards failed purchases/processes/claims/layout changes, restores standard stock and supplier/allocator state, preserves confirmed global choices, rejects old epoch input and actually executes trusted acceleration. Actual fixed-Tick allocator failure quarantines the faulted Host; disposal and fresh trusted load continue only the unchanged successful baseline. It does not invent normal business failure or clear the fault latch. Active accelerated work survives same-Level recovery, durable carry and Created cold load only with independently trusted progress.

Root corrections and actual red/green controls are recorded in `recovery-fixes-red-green.md`; independently reviewed in `s14-recovery-fixes-independent-review.md`. Tombstones no longer rebuild live hand occupants. Duration accepts base or current trusted modified duration, rejecting arbitrary saved ticks. Retry choices stage and validate against intended progress before atomic commit. Current eligible unlock placement is separate from complete global records; absent/disallowed unlocks do not spawn. Pristine matching trusted factory seed fulfills its declared standard entry, avoiding duplicate slot placement; mismatched/occupied seeds still reject.

## Remaining singleplayer exit

Final audit requires explicit ET Preparing floor expansion, movement into added area and codec restore continuation. Existing expansion validator alone does not prove that full path. A focused control is being prepared; S06/S07/S08/S14 remain in_progress until that evidence and final applicable gates/review close. Network/Unity and second physical LAN verification are not inferred. `network-resume-audit.md` records the next-stage formal reconciliation and retained source risks without starting network implementation.
