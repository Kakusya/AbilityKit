# Supply ET and geometry primitive composite checkpoint

Integration commit 4e32fbe61 on abc1d653f contains the reviewed supply domain sequence fixes, ET SUPP fingerprint extension, Closing request gate, candidate Level7/Recipe5 restore, Created preference/placement atomicity and effective geometry internal primitive. This checkpoint is not merged to master, whose production remains 7a043b6cb Recipe4/Level6.

Coordinator kitchen first gate 20261002-205133 actually failed: 437 focused passed / 1 failed / zero skips. C03 still tried replacing literal envelope format6 after the candidate became7, so no corruption happened. Updated the test to actually tamper7; original failure logs/TRX/summary retained.

Final coordinator kitchen gate 20261002-205320 actually passed: both builds, 438 focused / 563 Cooking / 218 ET, zero failures/skips, 30.9s. Coordinator ET Level gate 20261002-205426 actually passed: both builds, 563 Cooking / 218 ET, zero failures/skips, 21.4s. Raw evidence remains integration local/Logs/test-gates; three summaries are copied alongside this record. Shared transport warnings remain.

The allocator double-zero blocker is fixed and covered by real failing-then-passing Consume/Serve controls. Legitimate AddItem seed items and Retain anchors keep zero sequence; IsProduct requires positive sequence. The geometry primitive now additionally requires all empty configured appliance anchors, with a third rejection control passing in the complete focused set. Final F08 checkpoint assertion proves second restore canonical equality, not an uninterrupted final control branch.

Outstanding: first Preparing kitchen publication/reuse, preparation fixed ticks and service-start offset, trusted equipment/capability binding, front path derivation and live installation, installed-layout recovery, next-Level permissions and the full S14 natural operating fixture. No S06/S07/S08/S14 full exit or network/Unity completion is claimed.
