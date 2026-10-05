---
name: cooking-dot-workflow
description: Explicitly invoke $cooking-dot-workflow for an approved Cooking requirement using Trellis plans, dot technical decisions and supervised Orca workers. Supports branch-only or authorized production delivery and safe diagnostic recovery; reading or installing does not activate it.
---

# Cooking dot workflow

Use only on explicit invocation. The invoking main is the sole coordinator, dot owns technical planning and final technical decisions, and Orca managed-worktree workers implement. Owner scope and raw evidence prevail. This is an active-session workflow, not a background service.

## Enter once

Read [AGENTS](../../../AGENTS.md), [Cooking progress](../../../Docs/design/CookingGame/progress.md), [Cooking index](../../../.trellis/spec/cooking/index.md) and [SOP](../../../.trellis/spec/abilitykit/supervised-issue-delivery.md). These supply Cooking scope, stopped historical work, host limits and authorization. Load `orca-cli`, resolve its executable once and fetch its live CLI/orchestration guides; discover handles, dot conversation, capabilities and actual effective model/effort. Unknown capabilities block dependent actions; do not install tools or change models to work around them.

Read [recovery](references/recovery.md) **before creating a flow or dispatching**. Enumerate Git/Orca worktrees and task records, reconcile copies and unfinished effects. Supported execution is the original verified sole coordinator continuing; another main can discover/reconcile read-only but cannot claim normal-Run takeover from `run-use`. Record this limitation before first dispatch.

Trellis owns plans/status/evidence. Record invocation/Owner approval provenance, allowed files and Cooking consumers, verified remote repository, branch/target and mode (`branch-only` or `production`). Obtain requirement/scope approval once; delegated dot planning does not repeat it. Explicit invocation of an approved requirement authorizes scoped dialogue, branch pushes and needed Issue/PR writes, plus production merge/close only when authorized and gated below. Branch-only approval withholds merge. Publication and cleanup need separate authorization.

## One path, explicit exits

Use [checkpoint/operation](assets/checkpoint-operation.md), [request](assets/dot-request.md) and [decision](assets/dot-decision.md) templates. [Dialogue](references/dot-dialogue.md) is the sole request/wait rule; [recovery](references/recovery.md) is the sole record/reconciliation rule. Persist/read back intent before external effects and receipt afterwards.

For a first operator handoff or a complete worked example, read [operator example](references/operator-example.md).

| Stage | Input and action | Exit required to continue |
| --- | --- | --- |
| Requirement | Approved scope/observable acceptance, task/source and declared delivery mode | Durable binding and verified original coordinator/write authority; no conflicting flow/effect |
| Dot plan | Publish scoped requirement/source/evidence commit to verified remote; send `planning` request with full SHA/conflicts | Explicit dot plan for request/SHA within Owner scope; preserve full reply |
| Prepare | Apply plan to PRD/design/implement/manifests and review; create or reread an Issue only when needed, bind current scope/dependencies | Reviewed plans/needed Issue contract; enter implementation via existing Trellis lifecycle |
| Implement | Bounded Orca dispatch with exact files/consumer/invariants/checks/stop boundary; normally one worker, at most two independent owners | Accepted input **and** turn-start proof, then `worker_done` matching both Task and current Dispatch, explicit outcome/raw evidence |
| Check/freeze | Main independently checks diff/scope/results; freeze candidate commit plus delivered-file SHA-256 manifest | Required checks Passed with source/dirty/tool/command/exit/count/raw/binary evidence; failures preserved, missing environments/tests not passed |
| Dot review | `final-review` request with frozen candidate and validation envelope | Explicit current request/full-SHA acceptance plus main scope/evidence approval; changed delivery files require new candidate/review |
| Deliver | Follow declared mode below and account for workers/resources | All mode-specific evidence exits satisfied before complete/archive |

Workers use their live preamble for questions, heartbeats, inbox checkpoints and exactly one `worker_done`. No native subagent substitutes for an Orca worker. Workers are not alone and preserve others' edits. Serialize exclusive .NET checks across trees. Failed/quiet dispatch does not prove writer exit; reconcile before retry. During dot waits follow dialogue's dispatch/decision freeze.

## Delivery and session end

**Branch-only:** exact candidate acceptance/unchanged delivered hashes, required implementation checks/actual pilot/independent inspection, authorized branch push/readback and every worker accounted for permit completion of this declared bounded task. Merge/postmerge/GitHub auto-close are NotRun; no production acceptance is implied. A pilot may deliver a useful Cooking workflow operator/handoff example without decorative Issues/PRs. Record explicit project-path **manual loading** if the host catalog has not refreshed; disk presence/static checks do not prove automatic invocation.

**Production:** immediately before merge reread PR head/base, exact acceptance, scope, actual required CI/checks and [premerge conditions](references/recovery.md#production-merge-and-close). Inspect automatic-closing PR/commit keywords and server-side associations; handle within authorization, otherwise block merge. Merge only approved candidate, save actual merge SHA, then run required integration on that merged source. Failed/NotRun required integration leaves completion open. Only Passed integration permits explicit Issue close/readback and archive; repairs/rollback need bounded reviewed authority.

Freeze SHA and delivered hashes **before** review. Later reply/accounting/archive evidence commits list their diff from accepted candidate and prove all delivered hashes unchanged; they are not automatically accepted new implementations. Required source checks still apply to actual integrated source.

**Paused session:** save handoff/unresolved intents/original deadline/failures/worker obligations via recovery, then stop the turn without completing the requirement. Awaiting review, timeout, unknown authority or failed integration must not set completed, call `task.py archive` or use `trellis-finish-work`'s complete/archive path. Session notes alone may be recorded. Only declared delivery exits permit completion/archive.

Account for each settled worker by approved reuse, explicitly approved retention or separately authorized release/cleanup. Keep unknown live resources. Owner-approved paused retention is not permission to close processes/delete worktrees. End at this requirement's boundary; other discoveries are candidate backlog.
