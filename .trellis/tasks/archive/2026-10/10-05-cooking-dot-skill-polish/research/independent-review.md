# Independent review — preparation

Reviewer Dispatch: `task_0658d87815e2` / `ctx_51d6193106de`, terminal `term_05f2d02e-37fa-44a3-9627-ef6375d69e08`. Main remains the sole coordinator; dot owns final technical decisions. Review completion does not mean source acceptance.

## Boundary and preparation identity

Only this report, `independent-controls.py` and independent-control outputs under this owning task may be written. No implementation/SOP/plan/record edits, commits, pushes, other-tree access, new workers/Run/rebind or resource closure. Existing mutable implementation has not been tested or accepted. Preparation HEAD was `4cf5b38bcb06b2f58d49fd73fc9de26e5cec02fe` on `Kakusya/cooking-dot-skill-polish`, with implementation/coordinator dirty files present.

Loaded trellis-start, trellis-before-dev, trellis-check, skill-creator and version-matched orca-cli guidance; read PRD/design/implement, both context manifests, prior six findings, dot-plan and dot-timing-rule, applicable AbilityKit/Cooking/shared guidance and testing scope. The approved task already exists; no new task required. `get_context.py --mode phase` returned `Phase Index section not found in workflow.md`; this is a context-loader limitation, not evidence of workflow validation.

Cross-tree source audit is represented by main's `research/source-search.md`; this Dispatch explicitly forbids other-tree access, so its inventory was not independently reproduced. Product/runtime/Unity gates are N/A to this skill validation. Their absence cannot imply product acceptance.

## Contract-derived controls prepared before freeze

| Contract | Independent observable check / expected refusal |
| --- | --- |
| Canonical authority | Move/copy a whole flow and compare actual directory versus declared authority; a copied owner declaration must not authorize writes. Missing authority, conflict in same operation bytes, divergent authority declarations must block mutations. An old prefix copy may be evidence, not a second writer. |
| Immutable operation identity | Intent action/identity and receipt must correlate. An unresolved action cannot be treated as complete or blindly retried. A receipt for a different action, request or dispatch must be rejected. |
| Crash boundaries | Intent-only, externally observed action without receipt, receipt without index update and damaged index must preserve the last provable boundary. Read-only reconstruction must leave all canonical bytes unchanged. |
| Read-only recovery | Attempt record/index writes using a different session and a copied entry; denial must occur before any filesystem change. No record owner field can prove normal Run exclusive takeover. |
| Expired original budget | At a time after deadline with no saved pause, even a valid timely-but-late-read decision must require persisting expiration/pause and must not auto-apply. Saved pause cannot disappear when a reply is reconciled. |
| Complete observation | Positive: null posted UTC, reliable complete matching final reply observed before deadline with raw provenance. Negatives: fragment first observed before deadline, completed after; clock uncertainty crosses deadline; missing/verifiability failure; other conversation, request, message boundary or SHA. |
| Late/unknown timing | Posted after deadline or first observed after deadline with no reliable posted time remains expired. A preserved complete observation before deadline may establish reply validity while execution authority remains separately blocked. |
| One supplemental quota | An intent for reminder/confirmation/clarification consumes the single quota even if receipt is lost. A second supplement is refused; neither budget nor request identity resets. |
| Session versus completion | Pause, unresolved effects, missing acceptance, wrong candidate, failed checks or unaccounted workers refuse completion/archive. Branch-only acceptance needs no merge; it must not authorize production integration. |
| Production finish and auto-close | Missing merge/postmerge evidence, failed integration, keyword association, server auto-close link or unknown association must block appropriate gate. Branch-only mode always refuses merge. |
| Source/evidence | Verify announced frozen SHA and delivered file hashes against actual Git blob and local bytes before/after controls; inspect original failed and successful implementation raw outputs, expected/actual counts and source/dirty identities. |
| Instructions | Resolve relevant local links, inspect single-authority reference routing, concise entry path, templates, documented supported envelope and original six finding coverage. |

Cases will be adapted only to the frozen helper's public interface after main supplies identity. Expected outcomes above come from accepted contracts, not implementation test names. The controls use isolated local fixtures and controlled UTC; they do not validate real transport idempotency, takeover or production merge/postmerge.

## Pre-freeze findings and status

No blocking inconsistency found in approved PRD/design/implement/manifests and dot plan/timing supplement: plan acceptance is explicitly distinct from final acceptance, resource retention remains authorized while closure does not, and branch-only completion is distinct from production integration. Mutable skill/helper final review is **NotRun** pending main's frozen SHA and delivered hashes. Real ordinary-Run exclusive takeover remains **Blocked** (positive exclusivity proof unavailable); production merge/postmerge/automatic Issue closure remain **NotRun**. Pilot output review is **NotRun** until its identity is supplied.

No helper tests have run during preparation. Original implementation failures will be retained and referenced, never overwritten.

## Frozen core review outcome

**Review completed with one blocking source finding; core candidate is not accepted.** Main supplied the explicit frozen candidate `32b400a5c6d375be366faa606e867ef68037cfa8` via inbox `msg_8732107e5766` after implementation Task `task_b1846da24106` / Dispatch `ctx_2ede5e596818` settled. This audit covers that commit's ten delivered files (nine changed delivery files plus unchanged openai.yaml), original implementation report/control outputs and validator evidence. Pilot `operator-example.md`, its eventual SKILL link and any repair/new candidate are **NotRun / outside this frozen review**; their identity had not been supplied before this review concluded.

Read final SKILL, both references, all three templates, openai.yaml, records.py/test_controls.py, and SOP. Verified all ten actual Git-blob SHA-256 values and working-file bytes against main's `implementation-freeze-receipt.json` and implementation's original precommit manifest. Frozen helper source was compiled directly from the announced Git blob for independent pure/API controls; no latest branch source was substituted. One actual diagnostic subprocess used the hash-matched local helper. Exact Python 3.14.8 interpreter hash, before/after dirty state, script hash, file hashes and subprocess argv/stdout/stderr/exit are in each `source-identity.json`. No product binaries were built (N/A). Tests wrote only isolated fixtures under these independent outputs; they invoked no live Orca/GitHub/product effects.

### B1 — same flow ID can silently group two conflicting canonical authorities

Frozen `scripts/records.py:264-276`, especially `groups.setdefault(flow["flow_id"], ...)`, validates each root against **its own** canonical binding but never compares bindings already seen for that flow ID. Independent fixtures `canonical/flow.json` and `second-self-canonical/flow.json` have the same stable UUID, separately reachable/self-declared canonical roots, valid evidence and internally valid records. `copies([canonical, second-self-canonical])` returns one group instead of refusing the inconsistent immutable authority. The recovery contract at `references/recovery.md:60` explicitly requires immutable bindings including canonical host/location to match.

Expected: Blocked before a flow is chosen or mutated. Actual: Allowed/grouped. This is a discovery/authority conflict failure, not malformed-input rejection and not a claim that the helper grants real Orca exclusivity. Minimal recommendation for implementation owner: compare immutable binding across all roots sharing a flow ID and reject disagreement before grouping; retain reachable canonical comparisons. Add positive same-binding/equal/prefix-copy and negative separate-self-canonical controls. Main/dot decide the repair; reviewer changed no implementation files. Escalated promptly as `msg_2c0250981d80`. A repair must receive a new main-announced frozen identity before independent verification.

### E1 — frozen diff-check coverage differs from the original dirty check

Actual frozen-range command `git diff --check 4cf5b38bcb06b2f58d49fd73fc9de26e5cec02fe 32b400a5c6d375be366faa606e867ef68037cfa8 -- .agents/skills/cooking-dot-workflow .trellis/spec/abilitykit/supervised-issue-delivery.md` exits **2** for two-space Markdown hard breaks in new dot-request/dot-decision templates. Original dirty-diff exit 0 is preserved as its actual earlier scope, but untracked templates were not represented by that command. This formatting is intentional Markdown syntax, not a behavioral blocker; the frozen check must nonetheless not be reported Passed. Raw output: `independent-control-run-01/frozen-diff-check.txt`; exit/interpretation in `original-evidence-audit.json`.

### Independent commands and actual results

| Command / declared coverage | Native exit | Actual result |
| --- | --- | --- |
| `python -B .trellis/tasks/10-05-cooking-dot-skill-polish/research/independent-controls.py` | 1 | **Failed**: 92 checks, 91 Passed / 1 Failed; B1 discovered |
| `python -B .trellis/tasks/10-05-cooking-dot-skill-polish/research/independent-controls.py independent-control-run-02` | 1 | **Failed**: 93 checks, 92 Passed / 1 Failed; internally valid conflicting-copy precheck added; B1 persists |
| `Get-Content -Raw -Encoding UTF8 <evidence-audit-input.py> \| python -B -` (same program originally supplied as a PowerShell here-string) | 0 | **Passed**: 21 expected/actual evidence checks; 25 frozen local link destinations resolved |
| Frozen-range `git diff --check` above | 2 | **Failed check**, intentional Markdown hard-break whitespace; E1 |
| Frozen helper `inspect` subprocess (inside each independent run) | 0 | **Passed**: durable record-derived boundary, no unresolved operation, stale canonical index left byte-identical |

Counts are local assertions including source-identity checks, not product tests. Run 02 supersedes the strength of one copy subcase in Run 01: Run 01's altered copy had a stale receipt hash; Run 02 updates that receipt and proves internal validity first. Its B1 case is unchanged and valid in both runs. Original run 01 results and original script bytes (`controls-source.py`) are preserved. No failed run was overwritten.

Each independent `results.json` preserves every expected/actual result and rejection reason; full flow/record fixture bytes remain in the corresponding directories. `request-reply-fixtures.json` saves the controlled request/full observation and expired-unsaved-pause result. Source/pause/no-mutation controls, the absent-index unresolved intent, wrong action/Task/Dispatch/Run receipts, unknown receipt immutability, correctly correlated reconciliation, old/corrupt/conflicting/missing authority, lost supplement intent quota, complete/null-posted timely observation versus fragments/wrong identities/boundary/clock uncertainty, late-read no-apply/no-budget-refresh, branch completion and production auto-close/integration gates were all actually exercised. Gates consume separately verified attestations as documented; they do not authenticate external receipts or obtain real execution authority.

Evidence/link audit loaded the **original** controls-output/fixtures, recomputed expected/actual equality and count, parsed all 12 raw subprocess returns and four intentional nonzero fault exits (71/72/73/74), checked persisted unique-operation ledgers and recovered unresolved state, matched original manifest to frozen bytes, inspected original stock-validator success and retained missing-yaml failure, and resolved Markdown local links at the frozen Git SHA using `git cat-file -e`. It did not simply rerun implementation test names. Audit raw file hashes/checks/link paths and diff exit are in `independent-control-run-01/original-evidence-audit.json`. Independent scope was read-only against originals; no network dependency download or validator rerun occurred.

The exact audit stdin program is preserved at `independent-control-run-01/evidence-audit-input.py` (the first comment documents capture; it was not separately rerun). Both runs' ten delivered files remain byte-identical before and after controls and equal the frozen working manifest, independently read back after report preparation. No repeated testing was used to mask B1.

### Original evidence corroboration and six finding coverage

Original `controls-run-04/controls-output.json` has 122 Passed expected/actual assertions, zero failed assertions and 12 raw subprocess executions with their prescribed exits. Raw recovery subprocess output and saved ledgers support no duplicate **local mock** invocation (0 pre-effect, 1 lost-receipt, 1 stale-index); raw restored timing reports valid timely-late-read yet `can_apply=false` and preserved pause. Test source explicitly declares controlled times, local simulation and self-only fault exits. No raw output proves real Orca/GitHub idempotency or coordinator takeover. Implementation source bytes match its dirty precommit manifest and main's Git freeze manifest; no claim is made that intermediate run 01–03 validate the final helper.

Original `quick-validate-output.txt` still contains ModuleNotFoundError/PyYAML failure; coordinator's subsequent `original-validator-ram-stdout.txt`, empty stderr and result show original stock validator succeeded using pinned PyYAML 6.0.3 RAM import. Reviewed that command's no-install/fixed-hash/readback scope, not independently executed it. This only corroborates frontmatter validation, not workflow behavior.

Historical findings 2–6 are addressed by explicit supported original-coordinator envelope, sole dialogue timing/quota rules, paused finish prohibition, production closing-link conditions, and short normal path/templates/progressive disclosure. No missing relevant local link found; conditional detail stays in references. Finding 1 is partly addressed by fixed/versioned immutable records and index reconstruction, but **B1 prevents accepting its canonical-copy discovery contract**. The helper is optional, uses existing Trellis I/O, and contains no runtime API scheduler/lease/background service. The original six findings are not turned into unconditional production claims.

### Limits and next owner

- Ordinary-Run exclusive cross-main takeover: **Blocked**, still lacks positive runtime exclusivity proof; local denial is not a concurrency proof.
- Real production merge/postmerge/automatic Issue close/CI and actual Trellis archive command during pause: **NotRun**. Pure conditions and documentation checked only.
- Real fresh coordinator conversation restore and live transport idempotency: **NotRun**. One real subprocess diagnostic per independent run is not coordinator takeover.
- Pilot/manual-loading workflow delivery and eventual pilot link: **NotRun in this audit**; main may inspect separately and provide a new pinned delta for review.
- Cross-tree inventory: main's existing source-search provenance read; not independently rerun because this Dispatch forbids other-tree access.
- Runtime/product/.NET/Unity/LAN/performance: **N/A** to these changed skill files; no broader acceptance implied.
- Dot exact-candidate acceptance/task completion/archive: pending main/dot, **not authorized by this review**.

Main owns B1 routing, any new frozen identity, pilot/integration checks and retention. Read-only review can succeed as a dispatched assignment while the source checks fail. Reviewer's only durable outputs are this report, `independent-controls.py`, and `independent-control-run-01/` / `independent-control-run-02/`; no implementation/SOP/plan/Run/flow records or external resources were changed.

Discovery command errors retained in session evidence: a pre-read guessed `workflow_records.py` filename and later guessed validator output filename did not exist; corrected by manifest/`rg --files` to `records.py` and `original-validator-ram-stdout.txt`. They were read-only lookup errors and are not Passed validation.
