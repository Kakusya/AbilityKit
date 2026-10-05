# Independent forward review — explicit Cooking dot workflow

Review date: 2026-10-05. Reviewer Dispatch: `task_242226e6257d` / `ctx_9c41c459c9dd`; observed Run `run_b97f93be47e5`. Sole write ownership: this report. This dispatch reviews installation artifacts; it does not invoke the reviewed workflow.

## Verdict and limits

**Passed — static document/behavior review, 15/15 raw scenarios.** No blocking defect or Owner-policy conflict found in the six delivered files. Installation can be assessed independently of a future delivery pilot. This verdict does not authorize a pilot, merge, installation, cleanup, or any Cooking implementation.

**NotRun — live dot/GitHub automatic delivery and crash/rebind pilot.** A real recovery depends on capabilities and identities of the actual Run/host; this review does not establish that normal Run takeover is supported. Unsupported takeover must stop under recovery.md:26, rather than be treated as a successful recovery. **N/A — .NET, Unity, physical LAN tests and full repository gates:** no code/runtime behavior changed by this review, and these are outside the document-review assignment. Actual test execution count: zero; static scenarios are not runtime tests.

Findings by severity: **blockers: 0; required document fixes: 0.** The capability and host-loading prerequisites below are operational limits expressly handled by the documents, not invented requirements or self-fixed findings.

## Inputs, independence and source identity

Decision inputs read in full: the four skill files, current root AGENTS, delivery SOP, raw owner-decisions.md and review-scenarios.md. Did **not** read implementation-report.md or coordinator conclusions. Trellis bootstrap/index and current progress were consulted for routing/current constraints, not used as additional Owner approval. No dot, GitHub, worker-start, installation, commit or cleanup action performed. No other files edited.

Source HEAD: `0ca6d2d2df4af789948857f1f960be285313936b`, branch `docs/supervised-issue-sop`, dirty at entry. Entry and final pre-report status: modified SOP and AGENTS; untracked skill directory and this installation task directory. These are concurrent owners' artifacts and were preserved. HEAD does not identify uncommitted delivered content; hashes below bind that content.

Observed own effective UI model/effort: **GPT-6.1-Sol / high**, from `orca terminal show --terminal term_a0377b8b-900c-4050-822b-67df91d68710 --json`, terminal preview. This is a runtime UI observation, not a claim derived from a configured default or proof of a provider-internal model build. Terminal execution host: `local`; Orca runtime `ff0c5f2c-d341-4455-bc1c-c113650bdd60`.

Initial hashes were captured at first delivered-file reads, before scenario analysis. A second sweep at `2026-10-05T06:51:00Z` matched all six. Final sweep is recorded in the completion section; any change requires re-reading the changed rules and reconsidering affected scenarios before completion.

| Delivered file | Initial SHA256 | Final SHA256 |
| --- | --- | --- |
| .agents/skills/cooking-dot-workflow/SKILL.md | BC8E32EF48BB11DCADE21A2417CDFE618165E0FC35C4751FF99EB798CAAF9131 | BC8E32EF48BB11DCADE21A2417CDFE618165E0FC35C4751FF99EB798CAAF9131 |
| .agents/skills/cooking-dot-workflow/references/dot-dialogue.md | ABAF6B77EDE1B01303C6260DB74F9A13C4E14A2208684625247DB9FEBEF964D9 | ABAF6B77EDE1B01303C6260DB74F9A13C4E14A2208684625247DB9FEBEF964D9 |
| .agents/skills/cooking-dot-workflow/references/recovery.md | 8DD16325E0F9E6EAEB41EC13C835A90BD4C4B6F0ACAB5C46367DCE755B1E051F | 8DD16325E0F9E6EAEB41EC13C835A90BD4C4B6F0ACAB5C46367DCE755B1E051F |
| .agents/skills/cooking-dot-workflow/agents/openai.yaml | A1499D95ABD8447558C535FE5554ADCC3C9B988A0A39264A6283D430EFFE1E94 | A1499D95ABD8447558C535FE5554ADCC3C9B988A0A39264A6283D430EFFE1E94 |
| .trellis/spec/abilitykit/supervised-issue-delivery.md | 46C4086993D49397DFE67112B7EE74B23473EFE97A35EDAB0DCAFB13A40CE410 | 46C4086993D49397DFE67112B7EE74B23473EFE97A35EDAB0DCAFB13A40CE410 |
| AGENTS.md | 7BDB10E865C3F2D21EB939A84A4A68C193DAB2D76E747073E1041E6EB5F58D6F | 7BDB10E865C3F2D21EB939A84A4A68C193DAB2D76E747073E1041E6EB5F58D6F |

Repository worktree discovery: `git worktree list --porcelain` and `orca worktree list --json` agreed on two AbilityKit trees, root above and `C:/Users/Administrator/orca/workspaces/AbilityKit/issue6-test-gate-results` at `15e79fefd5ba1d9294491122c91df286ef94e2f8`. Orca's other two listed repositories are not AbilityKit and were not inspected. Local host coverage only; no claim that unseen hosts were searched.

The Issue6 tree was read-only: inspected AGENTS, 10-04-test-gate-result-contract/task.json, research/cooking-only-scope-stop.md, status and actual diff. Status was clean, task blocked, stopped/isolation notice present. Actual diff against root HEAD still includes Shooter/MOBA-specific script changes; read two representative script hunks to verify this was real source history. Its older AGENTS dot-default clause is historical, not authorization for this explicit workflow or to restart/merge that tree. No changes, process interventions or cleanup there. Scenario decisions below derive from the stipulated review inputs, not its old approvals.

## Rule anchors

Paths in the following shorthand are relative to repository root; numbers are file line anchors in the hashed delivered files.

- **S** = [.agents/skills/cooking-dot-workflow/SKILL.md](../../../../.agents/skills/cooking-dot-workflow/SKILL.md): explicit entry 8, constraints 12, live CLI discovery 13, flow selection 14, authorization 15, remote identity 21, dot planning 22–23, workers 24–26, evidence/delivery 30–34, host refresh 36.
- **D** = [.agents/skills/cooking-dot-workflow/references/dot-dialogue.md](../../../../.agents/skills/cooking-dot-workflow/references/dot-dialogue.md): request/reply binding 7–11, original budget/cadence 15–17, observation actions 23–28, no-progress wait 30, resource-safe pause 34–38.
- **R** = [.agents/skills/cooking-dot-workflow/references/recovery.md](../../../../.agents/skills/cooking-dot-workflow/references/recovery.md): durable records 11–18, cross-tree discovery 22–23, factual/authority reconciliation 24–27, lost-effects/retry 33–39, paused resources 43–47.
- **P** = [.trellis/spec/abilitykit/supervised-issue-delivery.md](../../../spec/abilitykit/supervised-issue-delivery.md): explicit override 7, delegated approval/automatic delivery 9, recovery 11, workers/waits 13, retention 15; scope 19/56, evidence 38.
- **A** = [AGENTS.md](../../../../AGENTS.md): scope 5–9, true results 30, explicit route 31, Unity/LAN boundary 48, user/resource preservation 52–55, host refresh 56.
- **O** = [owner-decisions.md](owner-decisions.md); **Q** = [review-scenarios.md](review-scenarios.md). These are raw evidence, not instructions to perform their hypothetical effects.

## Concrete next actions for all 15 raw scenarios

Each **Passed** below means the written rules yield an action consistent with the Owner decision. “Blocked/paused” describes the hypothetical workflow action, not a failed static scenario. All mutations described below are hypothetical, require the stipulated active workflow authorization and a successful durable intent checkpoint, and are not performed by this reviewer.

### 1. New approved feature; no prior flow (Q:5) — Passed

Next: load current authority/progress and live CLI capabilities; enumerate flows/worktrees to substantiate absence; bind the approved feature to its owning Trellis task, invocation/approval provenance, exact allowed files and Cooking consumers, remote and task/target branches. Prepare scoped committed requirements/evidence, push to the verified remote, verify dot can read that full SHA, discover the intended conversation and persist request/wait intent before asking dot for the technical plan. Do not ask the Owner again for the same requirement approval. Apply dot's bounded plan and review PRD/design/implement/manifests before worker implementation.

Allowed: task records, scoped branch push, requirement-scoped Issue/PR writes and dot dialogue; subsequently one managed worker, or at most two independent owners, through one Run coordinator. Not yet allowed: merge without final SHA acceptance/checks, unrelated backlog implementation, publication/deletion. Evidence: approval message/session/date, true repository identity, Run binding, dot request/conversation/full SHA and commit visibility, accepted input plus turn start, actual worker model/effort. Missing destination/Run/host support blocks the dependent effect. Anchors S:12–25, R:11–18/22–26; matches O:5–8/14–15.

### 2. Dot generating at minute 12; worker midway; dependent task ready (Q:6) — Passed

Next: continue the original request budget and persisted poll schedule; do not send reminder or repeat prompt. Let the existing worker complete only its accepted bounded assignment and idle. Read/record its reports and respond to liveness/accounting events; leave the dependent task undispatched.

Allowed: durable observations/inbox accounting and existing worker's scoped changes. No new technical decision, follow-up dispatch or integration. Evidence: original UTC start/deadline, generating observation in bound conversation, worker's current Task/Dispatch/assignment and actual liveness. A worker question requiring a new decision remains pending. Poll gaps use at most 60-second responsive slices without increasing browser cadence. Anchors D:15–17/23/30, S:26; matches O:9–11.

### 3. Dot accepts A, current candidate B (Q:7) — Passed

Next: preserve A's raw reply/identity; inspect B's diff and affected evidence, push the authorized candidate as needed, then request explicit final acceptance of **B**. Do not reinterpret A's reply as accepting B or automatically merge B.

Allowed: task evidence, scoped repairs/checks and a new dot request for B under valid authority. Evidence: complete reply tied to current request/conversation and full reviewed SHA; final PR head/target/checks. B must satisfy coordinator scope/evidence review too. Generic approval or a different SHA is insufficient. Anchors S:30–32, D:8–10; matches O:7 and A:30.

### 4. Deadline expires; W remains live; main must stop (Q:8) — Passed

Next: persist timeout, last proven boundary, raw evidence and W's host/Run/Task/Dispatch/current assignment. Mark implementation awaiting review, preserve task status and resource obligations, report blocker/recovery step, then end the main turn. W may finish only the already approved bounded task and idle; a new decision waits. Owner scope withdrawal would stop affected work immediately.

Allowed: timeout/handoff evidence and supported settlement accounting; for accepted settlements use live worker-retain with recorded approved paused-retention provenance. No cleanup, release of unsettled W, replacement worker, new dispatch, technical substitute, merge or refreshed budget. An unreachable live host is recorded unknown with remaining obligations; it does not authorize destruction or prevent a documented safe stop. No main session means no continued polling. Anchors D:28/34–38, R:43–47, P:15; matches O:9–11.

### 5. Crash at minute 20; send receipt missing; worker may be done (Q:9) — Passed

Next: enumerate and select the unique genuine skill flow, read its complete artifacts/records, and perform read-only reconciliation. Prove the original coordinator inactive on its owning host; discover supported rebinding for the actual Run and verify sole authority before resuming effects. Inspect dot's verified conversation after the saved prior-message boundary for the request fingerprint/SHA, generation and full reply. Recover a posted request's identity; do not send it again. If authoritative absence and no generation are proved, the same intended request may be sent within the original budget.

Allowed: observations and recovery records; rebind only with supported operation/receipt and positive authority proof; conditional retransmission after proof. Recover/validate worker settlement using both IDs and account/retain, without starting another writer. Restore the original deadline (roughly 10 minutes remained at crash, minus elapsed downtime), reminder state and cadence; if already expired, pause immediately. Missing trusted start time, authority, host visibility or normal Run rebind support blocks automatic continuation. Never use takeover-legacy as a normal Run workaround. Anchors R:22–27/33–34/39, D:9/15–17/36–38; matches O:11–12/14.

### 6. Two unfinished flows; unrelated in_progress task (Q:10) — Passed

Next: identify the two flows by explicit workflow bindings in both worktrees, distinguish the legacy task, record candidates/coverage, and obtain Owner selection. Do not choose the freshest task, create a replacement flow, or resume both. After selection reconcile original coordinator and actual Run facts.

Allowed: read-only discovery and task-owned recovery evidence. No external workflow mutation until selection and authority reconciliation. Needed evidence: owning task/approval/skill binding, Git/Orca identities, missing-host coverage. An ordinary in_progress status does not establish a skill flow. Anchors S:14, R:11/22–26; matches O:12.

### 7. Accepted worker-start; lost result; saved identity; unknown liveness (Q:11) — Passed

Next: inspect supported durable replay/recovery of that exact request, failed-stage/residual resources, worker list, Dispatch and execution-host processes. Use returned replay/next-action argv unchanged with the same executable/host where available. Accepted input alone does not prove turn start. Live worker: wait; exact valid completed settlement: collect/account; still unknown: retain and pause.

Allowed: factual observations, intent/receipt recovery and supported reconciliation. No speculative start, new worktree, second editor/process or resend merely because receipt was lost. Needed evidence: request identity, actual Run/Task/current Dispatch/host, turn-start stage, positive liveness or exit, settlement IDs. Failure/unreachability of retrieval preserves uncertainty. Anchors S:25, R:14/34–35/39; matches O:14 and A:53.

### 8. Lost Issue-create response and later lost merge response (Q:12) — Passed

Next for Issue: verify explicit remote and search actual intended objects by task provenance/branch/exact content; recover number/receipt if found. No visible match after incomplete retrieval is not proof of absence; several matching objects require reconciliation. Retry only proven non-application, reusing supported request identity if available.

Next for merge: inspect actual PR merged state, merge SHA, target containment and candidate. If applied, record merge identity and run pending required integration checks on actual merged source; never merge again. If definitely unapplied, recheck authorization/current head/base/dot acceptance/actual checks before retry. Unknown state blocks writes.

Allowed: read-only reconciliation and recovered records; retry only under the stated proof/gates. Evidence: pre-effect operation/target/body fingerprint/head, actual repository/object/state/merge identity. No blind duplicate Issue, merge or close. Anchors R:13–18/36–39, S:32–33; matches O:7 and A:30.

### 9. F failed Dispatch with changes; U unreachable/stale (Q:13) — Passed

Next for F: preserve source changes and original failure; verify positive agent exit/failure and reconcile all prior Dispatch/resource obligations. A proven failed Dispatch **alone** is insufficient to prove no writer remains. With exit/accounting and valid main/technical authority, retry the same bounded assignment via a new authoritative Dispatch linking prior evidence. If still in a dot wait, no new retry dispatch.

Next for U: record host/liveness unknown, retain evidence/resources and pause dependent work. Do not treat stale status as exit or restart U. Allowed: observations/records and conditional F retry after all preconditions; no cleanup or blind replacement. Evidence: F's writer exit, actual resource inventory/settlement, new Dispatch ownership; U's missing host and remaining obligations. Anchors R:12/34–35/45, S:25–26; matches O:11/14 and A:53.

### 10. Original coordinator still active in same Run (Q:14) — Passed

Next: record/report active-authority conflict and stop takeover. Explicit invocation by the new conversation is not proof that the existing coordinator relinquished its Run.

Allowed: read-only authority observations and conflict record. No second binding, worker dispatch, takeover-legacy or forced termination. Needed evidence for later resume: positive original coordinator inactivity and supported normal Run rebind receipt plus verified sole binding. Anchors R:24–26, S:8/14, P:11; matches O:14.

### 11. origin Kakusya/AbilityKit; Orca projectId hobobo/abilitykit (Q:15) — Passed

Next: inspect actual Git remote and requirement/workflow repository binding, resolve which repository the task authorization targets, and record the metadata discrepancy before any write. If Kakusya/AbilityKit is the positively verified authorized Git destination and Orca projectId is stale, explicitly target Kakusya/AbilityKit. Do not route an Issue/PR to hobobo based only on projectId. If intended authorized destination cannot be established, pause that write and obtain clarification.

Allowed: read-only identity reconciliation/task records; remote writes only once destination and task authorization are proven and checkpointed. Needed evidence: actual remote owner/repository, approval/binding, intended Issue/PR provenance and scope. The documents do not require an unsolicited global Orca metadata migration. Anchors S:15/21, R:11/13/24/36; matches O:7 and P:23.

### 12. Dot accepts but required test NotRun; postmerge integration fails (Q:16) — Passed

Premerge next: retain NotRun with reason, stop merge/delivery and obtain the missing required evidence if possible within scope; dot's endorsement cannot turn NotRun into Passed. Missing required environment remains a blocker. Do not replace physical two-PC evidence with same-machine evidence or repeatedly ask for unavailable hardware.

Postmerge next: retain actual merged SHA/binary identity and failure, keep Issue/task completion exits open, and arrange bounded technically reviewed repair or explicitly reviewed rollback. No automatic close/archive or failure-hiding completion. Allowed: evidence records and authorized same-scope remediation through technical review; no laundering of old products or out-of-scope environment work. Evidence: commands/exits, declared/actual coverage, raw logs/source/binary and actual required CI facts. Anchors S:30–33, R:47, A:30/48, P:9/38; matches O:7/13.

### 13. Technical disagreement; dot recommends Shooter or forbidden Unity (Q:17) — Passed

For the in-scope technical disagreement, present complete competing reasoning/raw findings and follow dot's explicit final technical decision. For Shooter-specific script or currently unauthorized Cooking Unity implementation, stop that affected part, present the Owner-boundary conflict to dot and seek an in-scope plan; do not dispatch or merge it. Dot's technical authority does not change product authorization.

Allowed: conflict evidence and task-scoped dot dialogue; only authorized Cooking implementation after valid plan. Need per-file Cooking consumer/necessity/acceptance for shared changes, fixed ET closure, and a fresh explicit Owner scope change to unlock prohibited stages. Old labels/#6 approval cannot do this; unrelated discoveries are backlog candidates only. Anchors S:12/23/34, D:11, A:5–9/15/48, P:7/19/56; matches O:13/15.

### 14. Worker needs new decision; another emits valid worker_done; dot unavailable (Q:18) — Passed

Next: leave the decision-dependent worker waiting; process and persist the valid completion's report/outcome/identities, record implementation awaiting review, and account for the settled terminal using approved paused retention and live worker-retain. Preserve the original dot request/deadline and follow resource-safe timeout/failure rules. Do not fabricate a technical answer, accept the implementation as final, reuse the worker for a dependent task, or close the Issue.

Allowed: inbox/receipt/recovery records and supported settlement retention. Evidence: both Task/current Dispatch IDs, explicit outcome, report/source identity, retention approval provenance, actual Run/host and original dot wait state. Process delivered messages before acknowledgment; stale completion cannot settle a newer attempt. Unknown retention support/host creates recorded remaining obligations, not blanket release permission. Anchors S:25–26/34, D:30/34–38, R:39/43–45; matches O:9–11/14.

### 15. Folder exists; host catalog not refreshed; first live pilot desired (Q:19) — Passed

Next: explain host refresh/new-session requirement and verify actual skill availability before claiming a loaded pilot. Preserve current installation as static-only. A subsequent explicit invocation must bind an approved specific Cooking requirement and acceptance; then scenario 1 and capability gates apply. A general desire for a pilot does not supply a missing feature scope, accepted SHA, deletion permission or evidence.

Allowed now: discovery/explanation and task evidence; this installation dispatch still permits no pilot. Future scope-approved invocation authorizes its task-scoped effects, not installation/read alone. Needed evidence: refreshed catalog, explicit invocation/approval provenance, verified live CLI/dot/Run identities. Metadata policy allows no implicit invocation; neither disk presence nor this static report proves live automation. Anchors agents/openai.yaml:1–2, S:3/8/15/36, A:31/56, P:9/15; matches O:6/17.

## Routing, recovery usability and Owner comparison

Explicit-only routing is consistent across SKILL description/entry, agents policy, root AGENTS and SOP. SOP expressly overrides default coordinator technical planning only for an invoked workflow; it preserves independent Owner-scope/evidence checks. S:24 and P:13 use Orca-configured defaults, not the historical #6 model. Maximum two independent workers and serial exclusive .NET checks agree with current engineering constraints. No native subagent is substituted for a managed worker.

Owner decisions on delegated approval, dot technical primacy, bounded wait, worker continuation, one coordinator, recovery selection and stopping after this requirement have concrete actions above. The paused settled-terminal permission is explicitly expressed in P:15/R:45/D:36 as part of the Owner-approved workflow; it authorizes retention/accounting, not new work or deletion. The raw “finish task” statement does not override a later scope withdrawal, which all three rule layers explicitly handle.

Recovery has actionable preconditions and failure boundaries without inventing commands: discover live guides first, read-only reconcile, prove old authority inactive, use only supported actual-Run rebind, preserve original wait budget and ambiguous side effects. Lack of a fixed checkpoint filename/schema is not a defect: owning-task research index plus append-only intents/receipts and required record fields provide a usable contract while keeping Trellis authoritative. Live normal Run rebind availability remains NotRun and may legitimately Block a future resume.

Read-only local Markdown check: **Passed**, 30 file links in the six delivered files resolved, zero missing files/heading anchors. Included AGENTS/SOP skill routes and AGENTS's explicit SOP fragment. This checks filesystem/heading resolution, not every host's UI rendering or installed-skill portability; no skill installation was attempted.

## Actual verification and completion record

Tools observed: Git `2.56.0.windows.1`, Python `3.14.8`, Orca `1.4.220`; PowerShell is Windows PowerShell (the attempted `Get-Date -AsUTC -Format o` reported unsupported parameter). That timestamp attempt **Failed**; it did not mutate anything or invalidate hashes. The later `[DateTime]::UtcNow.ToString('o')` succeeded; the earlier combined command's exit 0 is not used to call its failing timestamp operation Passed. Trellis `get_context.py --mode phase` exited 0 but printed `Phase Index section not found in workflow.md`; phase lookup is **Blocked**, and no phase/task mutation was made.

Actual read-only commands: Trellis get_context (normal/phase/packages); Git rev-parse/status/worktree list and other-tree diff; Orca skills get orca-cli, worktree list, own terminal show, version and inbox check; numbered UTF-8 file reads; Get-FileHash SHA256; a Python stdin script resolving local Markdown links/fragments and hashing the six files. All identity/hash/link commands exited 0. Scenario coverage: 15 reasoned cases, with 12a/12b and 13's permitted/prohibited branches examined separately. No test suite, compilation, dot browser interaction, GitHub access, normal Run takeover, skill installation or future delivery was run.

Dispatch-only Orca messages: preamble-required inbox checkpoints, heartbeat and one final worker_done. No messages to dot/GitHub/Slack or another worker. Raw command outputs remain in this reviewer session; this report contains the relevant IDs, versions, results, coverage and complete content hashes. Document artifact has no binary identity requirement; runtime binary identity is N/A.

Final delivered-file sweep: **Passed** at `2026-10-05T06:55:05.8869756Z`, all six final hashes equal the initial hashes above; HEAD and the four dirty status entries were unchanged. No changed-rule reevaluation was needed. Coordinator retains responsibility for installation task lifecycle and for any future explicitly authorized live pilot.
