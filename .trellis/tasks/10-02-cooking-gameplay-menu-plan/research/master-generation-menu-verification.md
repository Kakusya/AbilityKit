# Master generation transaction and menu helper verification

Source master `125ffe906`; reviewed integration transaction `a4dd95ebd`, baseline/staging core `11a49537a`, menu helper imports `cd3aeb632` and `f4d8967cb`.

Actual final master gates:
- `20261002-233737-cooking-kitchen-loop`: 605 focused / 733 Cooking / 254 ET, zero failures/skips, 33.3 seconds.
- `20261002-234527-cooking-et-level-runtime`: 733 Cooking / 254 ET, zero failures/skips, 21.5 seconds.
- Logs: `local/Logs/cooking-master-generation-menu-kitchen-gate.log` and `local/Logs/cooking-master-generation-menu-et-gate.log` (UTF-16); original summary JSON and TRX retained under the gate directories.

Final integration before merge: kitchen `20261002-232904-cooking-kitchen-loop`, 598/726/254, 31.7 seconds; ET `20261002-233444-cooking-et-level-runtime`, 726/254, 22.3 seconds. Master adds seven typed baseline/staging tests; do not conflate these counts.

Reviewed behavior: private next-generation staging, ownership and match-scope checks before mutation, actual source-state/tree rollback on publication failure, retry after rejection, trusted next seed geometry and handoff of stock/process/portions/pending delivery/allocator. Foreign-owned and wrong-match real red cases were fixed and rechecked. Legacy root failure test now asserts the approved recoverable rejection contract and compares actual prior tree disposition.

Pure menu helper validates real manufacturing availability, trusted material intersections, recipe equality, player eligibility and static reachability. Its three independently found failures (DefaultInputs, finite/infinite supply qualification, final delivery player qualification) have preserved red/green evidence. Host Ready, runtime permission and checkpoint format8 are not yet connected.

Published formats remain definition3 / Recipe5 / Level7; typed major baseline2 is separate. Store-level success does not prove Host restart. S06/S07/S08/S14 remain in progress; natural full service, narrowed per-Level permissions and durable Host loading are still open. Network and Unity are not verified by these gates.
