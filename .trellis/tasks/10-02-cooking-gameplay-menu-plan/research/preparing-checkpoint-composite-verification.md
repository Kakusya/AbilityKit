# Preparing recovery: reviewed coordinator result

Integration commit `d9b0e5603`, after `585e15982`. Master production remains `7a043b6cb`, Recipe4/Level6. Integration Recipe5/Level7 is not yet merged.

Actual final coordinator gates after the pristine-front fix:

- `20261002-214615-cooking-kitchen-loop`: builds and 462 focused / 590 Cooking / 227 ET tests passed, zero failures/skips, 36.5 seconds.
- `20261002-214713-cooking-et-level-runtime`: analyzer/runtime builds and 590 Cooking / 227 ET tests passed, zero failures/skips, 26.0 seconds.

Raw logs/TRX stay in integration local/Logs/test-gates under those exact run IDs. Gate summaries are copied beside this report. Earlier root runs214151/214247 passed the earlier WIP but predated the independently discovered zero-clock customer forgery; they are not final evidence for the fix. The producer preserved a real failing poisoned-customer test before correcting the code.

Preparing can export and restore without Start, including zero-tick publication already bound to its LevelScope. Recovery preserves the initialized kitchen, stock delivery, partial processing, lifecycle state/version and frame watermarks. A real uninterrupted control and a restored branch execute the same remaining preparation, Ready and Running operations and end with identical complete checkpoint canonical text.

Before creating a kitchen, recovery rejects unsupported State/Outcome, non-null unimplemented InstalledLayout, untrusted preparation identity, invalid offset and non-pristine front. The front check compares the complete fresh trusted initial checkpoint; zero service time alone is insufficient. With no trusted front configuration, Preparing requires null front payload. Counter/customer/table/companion/countdown/Closing poison controls verify CreateCount=0. Legitimate kitchen preparation remains allowed.

Read [producer evidence](preparing-checkpoint-increment-review.md) and [independent findings and resolution](preparing-checkpoint-independent-review.md). The independent reviewer ran no .NET; coordinator gates above provide actual complete verification. No Unity or physical two-PC validation occurred.

Remaining: trusted initial/dynamic layout installation, effective front geometry recovery and cross-Level permission/carry behavior, followed by full S14 operating exit. This limited recovery increment does not close S06/S07/S08/S14.
