# Durable checkpoints and recovery

Read on entry, before external effects and after interruptions. Keep records under the **owning Trellis task**, using its research directory for a workflow recovery index and append-only intent/receipt evidence. These are recovery records, not a new scheduler or second task completion database; engineering status stays in existing Trellis artifacts. Do not change task schema or generic workflow infrastructure.

## Minimal durable record

Record actual values, explicit unknowns and links to raw receipts; never manufacture identities. Do not checkpoint credentials or unrestricted payloads.

| Record | Required facts |
| --- | --- |
| Workflow binding | Skill name, owning task path, requirement/acceptance, explicit invocation and Owner approval provenance; allowed files/Cooking consumers; remote repository, task/target branches; original and current coordinator session/terminal, Run identity and binding authority |
| Resource inventory | Git and Orca worktree identities/paths/branches/HEAD/dirty; execution hosts; worker Task/Dispatch IDs, unique file ownership, actual effective model/effort, turn-start proof, liveness observations and settlement/retention obligations |
| External intent, persisted before effect | Unique local operation identity, action and exact target, authorization source, UTC time, expected preconditions/head SHA, request body fingerprint or task-owned content path, supported client request identity if available |
| Receipt, persisted after effect | Operation identity, exact command/exit and raw result path; remote request/message/Run/Dispatch/Issue/PR identities, postcondition/SHA, replay status and observed stages; unresolved uncertainty if response was lost |
| Dot wait/review | Conversation and original request identity/fingerprint/SHA; original UTC start/deadline and poll schedule; reminder intent/receipt, raw reply identity/content, explicit final decision and accepted SHA |
| Resume boundary | Last proven operation, unresolved intents, original failures and NotRun exits, next bounded action/preconditions; live/unknown/settled workers and explicit paused retention permission |

Checkpoint before sends, branch pushes, Issue/PR creation or edits, worker placement/start/dispatch, merge/close, retention and any separately authorized cleanup. Save response identities afterwards and update the recovery index to point at those raw records. If durable checkpointing fails, do not perform the effect. Reconcile unresolved intents before planning later effects; no receipt does not mean no effect.

## Discover and bind once

1. Enumerate this repository's Git and Orca worktrees. Inspect relevant other trees' AGENTS, tasks, research and real diffs, especially `issue6-test-gate-results`; preserve its stopped isolation. Search for explicit skill workflow records rather than choosing every unfinished Trellis task. Record search coverage, missing hosts and ambiguous candidates.
2. A unique unfinished skill flow is selected automatically; several require Owner selection before any continuation. Never create a replacement flow to avoid ambiguity. Missing required records/cross-host visibility is a blocker, not proof of absence. Read the selected task's complete PRD/design/implement/manifests, research and latest Issue contracts.
3. Reconcile Git remote and local branch/dirty state, actual GitHub Issue/PR heads/merge status, actual dot conversation/request and Orca Run/Dispatch/processes by read-only observations. Saved paths, titles, copied handles, old provider transcripts and task status do not prove current authority. Recover runtime handles from current listings, never send to old and replacement handles together.
4. Positively verify original coordinator inactive, using live session/process/Run observations on the owning host; a stale timestamp or silent terminal is insufficient. If still active, do not bind another coordinator: report the conflict and stop. If inactive cannot be proved, pause with unknown authority.
5. Inspect live orchestration guide/capabilities and command help for this **actual Run**. Bind a new coordinator only through a supported operation with proof of authority and its returned receipt; verify the resulting sole-coordinator binding. If normal Run rebinding/takeover is unsupported or unknown, preserve records and stop for an explicit supported recovery path. Never invent a generic takeover command or use `takeover-legacy` for a normal Run. Legacy compatibility requires an actual legacy receipt and its dedicated live contract, not a workaround for this workflow.
6. Only after authority and unresolved effects reconcile, resume at the last proven boundary. Restore the original dot wait deadline/reminder/cadence; expired waits enter resource-safe pause immediately, never a fresh 30 minutes. No automatic continuation happens without a main session.

## Reconcile before retry

| Lost/uncertain effect | Read-only reconciliation and allowed next action |
| --- | --- |
| Browser/dot send | Inspect the verified conversation against the saved prior-message boundary, request fingerprint, exact SHA and posted-message identity; check generating/full reply. If posted, record recovered receipt and wait on it. If absence is proven and no generation is running, send the same intended request within its original deadline. Ambiguous send or reminder remains paused; never repeat a running prompt |
| Worker create/start/dispatch | Inspect supported durable request replay, failed stage/residual resources, Run worker list, exact Dispatch and owning execution-host observations. Accepted input is not proven turn start. If live, wait; if done, validate/collect the exact settlement; if unknown, retain and pause. Do not create another worktree/process/editor to resolve silence |
| Proven failed worker | Preserve changes and original failure. Require positive agent exit/failure evidence and reconciliation of outstanding Dispatch/resource obligations through the live contract before a retry; a failed task row alone does not prove no writer remains. Retry only the same bounded assignment under valid main/technical authority, with a new authoritative Dispatch and prior evidence linked |
| GitHub push or Issue/PR create/edit | Verify explicit remote repository and actual ref/object state; find the intended object by task provenance, branch and exact intended content. Record its recovered identity if found. Zero visible matches is not proof if retrieval failed or was incomplete; multiple matches stop for reconciliation. Retry only after authoritative absence/non-application is established, reusing supported request identity where available |
| Merge or Issue close | Inspect actual PR merged state, merge SHA, target branch containment, reviewed candidate and Issue state. If applied, recover receipt and perform pending integration checks; never merge again. If definitely unapplied, revalidate authorization, current head/base, dot acceptance and actual checks before retry. Unknown state blocks writes |

When supported client receipts expose an exact replay/recovery request or next-action argv, use it unchanged with the same executable and host. Discover it in the live guide; do not guess idempotency flags. Process inbox deliveries fully, validate completions against both Task and current Dispatch IDs, and persist accounting before acknowledgment so replay cannot duplicate actions.

## Pause without destroying resources

Live workers continue only their already approved current bounded assignment, then idle. A new technical decision waits; no main session starts follow-ups. Scope withdrawal takes precedence and stops affected work. Collect valid completion reports and label implementations awaiting review when dot is unavailable; they are not accepted delivery.

The Owner-approved workflow includes explicit retention permission for settled terminals while paused. Record its provenance and account for each accepted settlement through live `worker-retain`; retain branches, worktrees, evidence and unknown processes. Record remaining obligations if an unreachable host prevents accounting. Never infer exit from silence, call blanket close/delete, release unsettled workers or restart an unverifiable writer. Main crash does not terminate workers.

Report what is proven, unknown and required for safe resumption. Preserve original failed results and absent environments. Completion/merge/close/archive/cleanup are distinct exits; no task status or dot endorsement erases outstanding integration checks. Publication and resource deletion require separate authorization even after success.
