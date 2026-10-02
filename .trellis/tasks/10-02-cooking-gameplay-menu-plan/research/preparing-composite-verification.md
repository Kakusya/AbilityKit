# First preparation runtime: coordinator verification

Integration commit `585e15982`, based on `4e32fbe61`, records the reviewed limited increment. Master remains `7a043b6cb` for production source (Recipe4/Level6); integration Recipe5/Level7 remains a candidate.

Actual coordinator gates, 2026-10-02:

- `20261002-213444-cooking-kitchen-loop`: builds passed; 462 focused, 589 Cooking and 223 ET tests passed; zero failures/skips; 38.5 seconds.
- `20261002-213554-cooking-et-level-runtime`: analyzer/runtime builds and 589 Cooking / 223 ET tests passed; zero failures/skips; 25.7 seconds.

Raw logs and TRX remain in integration `local/Logs/test-gates/` under those exact run IDs. Copied gate summaries beside this report preserve actual command/result details. This is .NET evidence; no Unity or real two-PC LAN validation was performed.

The increment initializes the kitchen through an optional trusted preparation factory, reuses it at Start, admits preparation movement/objects/processes/supply through the existing ET fixed tick, and excludes order binding/submission/front work. Front time starts at the service offset, so preparation time does not consume service time. Frozen trusted policy identity and offset are required checkpoint fields. Legacy factory timing remains unchanged.

Independent read-only review: [preparing-runtime-independent-review.md](preparing-runtime-independent-review.md). Producer findings and focused verification: [preparing-et-increment-review.md](preparing-et-increment-review.md). Earlier failed focused runs remain preserved; full gates above ran after those fixes.

Limits: export/recovery currently supports Running only; `InstalledLayout` is null and no initial/dynamic layout is installed through this helper. The positive Running recovery test compares immediate export and one resumed tick, without an uninterrupted final canonical control. Preparing-state recovery and actual trusted geometry/front installation are subsequent increments. S06/S07/S08/S14 remain in progress.

Next serialized ownership: host producer owns only host and Preparing ET tests for Preparing export/recovery and uninterrupted-versus-restored controls; root owns the minimal Lifecycle recovered-version admission change. Layout admission/installation follows afterward. This report does not verify later WIP.
