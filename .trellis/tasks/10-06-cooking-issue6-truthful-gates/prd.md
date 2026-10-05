# Issue 6 Cooking-only continuation

Owner explicitly invoked cooking-dot-workflow on 2026-10-06: publish a new origin branch, obtain dot's implementation direction, then execute. [Approval and inventory](research/entry-approval.md). Status: planning; no implementation authorized before the current dot plan.

Cooking consumes the existing `cooking-et-level-runtime` and `cooking-kitchen-loop` gates through the shared runner. Those gates must report actual declared tests/builds and source/binary evidence rather than accept missing/invalid/stale results. Preserve the accepted Issue5 five-state and coverage contract. Existing Issue6 text demanding all eighteen producers conflicts with Owner's stronger Cooking-only scope; revise this contract after dot explicitly resolves it. The stopped broad branch is evidence, never the merge source.

Required acceptance:

- Isolated original skip-to-Passed reproduction and its corrected negative result; valid current success stays successful.
- Required/optional coverage and nested/focused aggregation derive from validated current child results; incomplete `StepName` cannot imply full gate acceptance.
- Missing tools/results, malformed/empty artifacts, zero declared tests, failures with exit0, wrong run/SHA, reused/overwritten binaries and contradictory state/exits cannot pass.
- `NoBuild`/`NoRestore` report actual skipped stages and enforce provenance before accepting tests; cancellation/timeout and remaining NotRun nodes retain evidence.
- Exact committed-source real Cooking gate execution with nonzero TRX entries, current commands/exits/tools/raw results and binary identity; independent main inspection plus dot final exact-SHA decision.
- Scope comparison proves no Shooter/MOBA/Orleans code, config, protocols, dedicated scripts/tests/gates changed. Shared config edits confined to approved Cooking declarations; no dependency/SDK/ET upgrade, gameplay migration, Unity execution, services or benchmark launch.

Dot must decide the smallest coherent reuse of prior shared helper/runner work, the needed fail-closed treatment for unadapted legacy producers, a per-file Cooking consumer/necessity/acceptance list, and which Issue6 old exits become N/A or remain blocked. No claim of whole Issue completion until the current contract and required exits agree.

Delivery follows the invoked skill's conditional production acceptance; target is master, but push/plan alone does not permit merge. Resource deletion requires separate authorization. Current explicit workflow loading is manual through the supplied project path; automatic host dispatch is NotRun.
