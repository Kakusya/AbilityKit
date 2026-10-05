# Planning result schema and examples

The full dot decision is normative. This field inventory is planning documentation, not another gate registry or a claim of JSON Schema engine validation. The worker's one shared validator must enforce the same model before and after serialization.

| Field group | Required meaning |
| --- | --- |
| schemaVersion, runId, resultId, parentResultId, invocationPath, kind | Exact version/current assigned identity; no duplicate node or reused child ownership; leaf has no children |
| status, cliExitCode, processExitCode, reason | Strict five states; canonical CLI mapping; independently retained actual native exit or null if never launched |
| source | Exact git SHA, dirty details, actual-input closure/fingerprint before and after, declared evidence exclusions |
| configuration | Selected config file hash and resolved project/TFM/Configuration/filter; no unexpanded tokens |
| command, tools, times | Actual argument arrays, executable/SDK/host identities; invocation/process start/end, null execution facts on uninvoked leaves |
| coverage | required, optional, completed, missing sets; disjoint declarations; each completion token binds nonempty unique IDs of validated Passed descendant leaves |
| tests | null for pure build; actual TRX entries/passed/failed/skipped/notExecuted/executed, definitions/assembly/filter and raw counters checked under their real meanings |
| artifacts | Run/result ownership, contained relative path, role, length/hash; no escape/reparse/stale/replacement acceptance |
| provenance | Restore/build/test stage commands/times/exits/source, evaluated actual input closure, assets, project/TFM/config, loaded assemblies/dependencies, reuse manifest identity |
| children, fullGateAccepted | Complete configuration tree including NotRun nodes; independently recomputed aggregate; leaf cannot claim full gate |

Illustrative state rows (not real execution receipts):

| Scenario | Leaf | Root | Coverage/exit |
| --- | --- | --- | --- |
| Current successful build + nonzero passing test, all required evidence valid | Passed/0, true native0 | Passed/0 | Required complete with valid nonempty bindings; fullGateAccepted=true |
| Missing tool before process start | Blocked/2, processExitCode=null | Blocked/2 | Required token missing; later children NotRun/null execution |
| Process native0 but failed/zero/contradictory TRX or missing receipt | Failed/1, native0 retained | Failed/1 | No completed token; original raw failure survives |
| Focused passing leaf, sibling unselected | Passed/0 and NotRun/4 | NotRun/4 | Required missing; fullGateAccepted=false, aggregate invocation may have time |
| Optional declared nonexecution policy, all required complete | Skipped/3 with raw missing reason | Passed/0 | Optional missing visible, required complete; actual optional failure remains Failed |
| Entire plan uninvoked | NotRun/4 | NotRun/4 | Commands/process exits/execution times null; fullGateAccepted=false |

NoBuild/NoRestore stage skips remain explicit and use exact validated prior manifests. Missing provenance is Blocked, corrupt/mismatched evidence Failed; reuse never earns new execution credit. Production rejects synthetic/example evidence. Unsupported selected producers block before launch. N/A is not an execution status.
