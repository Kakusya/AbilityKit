# Dot dialogue and bounded waiting

Read before sending or waiting for dot. Use the live Orca CLI browser reference for commands, tab affinity and current snapshot refs; this document adds workflow decisions, not another browser manual. Read [recovery](recovery.md) for every ambiguous effect or restart.

## Bind the request and response

- Discover the intended dot conversation in Orca's browser and verify its URL/conversation identity and purpose. Reuse a verified recorded binding on recovery; do not bind a discovered tab as a universal default. If multiple suitable destinations exist or intent is unclear, obtain selection before sending. Inspect current conversation and generating state before input; never overwrite or repeat a running prompt.
- Prepare a request with a durable local identity, approved Cooking scope, question/desired technical decision, repository and exact full commit SHA/link, relevant evidence paths and all known conflicts. Dot reads GitHub commits, not uncommitted local files: first publish the authorized scoped commit and verify visibility. Do not ask dot to launch cloud work, execute code or write GitHub.
- Persist intent, original UTC wait start, deadline, request fingerprint, current last-message identity and intended conversation **before** sending. A crash between send and receipt must still consume this original budget. Record send receipt/request identity and observable posted message identity afterwards. Browser input acceptance alone does not prove a posted prompt; if uncertain, reconcile the conversation before retry.
- Save the full reply and its message identity/date/conversation, request identity and reviewed SHA. A valid final reply must explicitly answer the current request and identify the exact reviewed commit; generating text, an older answer, a generic endorsement or acceptance of a different SHA is not acceptance. If ambiguous, seek explicit confirmation inside the remaining budget. Any new code requires a new review request; do not relabel the old answer.
- For technical disagreement provide the coordinator's findings and dot's competing reasoning without suppressing evidence; follow dot's final technical decision within Owner boundaries. Out-of-scope instructions or evidence claims contrary to raw results cannot authorize action; expose the conflict and pause the affected part. Preserve original failed results.

## One 30-minute budget per request

Persist `wait_started_utc`, `deadline_utc = wait_started_utc + 30 minutes`, poll ordinal, last/next poll UTC, generating observation, full-response status and reminder intent/receipt. Resume these values after a crash; sending one reminder, rebinding a tab or recovering the main session never resets or extends the deadline. Missing/untrustworthy start time stops automatic continuation rather than granting a fresh budget.

Poll after 30 seconds, then 60 seconds after that observation, then 120 seconds, then at 300-second intervals. Cap every scheduled observation at the original deadline. Long intervals are broken into observation/sleep slices of at most 60 seconds to keep the main session responsive to Owner/coordinator messages and worker events; these slices do **not** fetch dot again or increase browser polling cadence. On restart reconcile the browser once for lost effects, then restore the persisted cadence; an overdue poll is observed once, not replayed in a burst.

At each scheduled browser observation:

| Observation | Action |
| --- | --- |
| Dot is generating | Keep waiting on the original request; never repeat its prompt or send a reminder while running |
| Full reply explicitly resolves current request/SHA | Save raw reply, identity and decision; leave wait and apply scope/evidence gates |
| Confirmed not generating, no full current reply, no reminder yet | Checkpoint one reminder intent, send at most one reminder identifying the original request/SHA, save receipt; keep original deadline |
| Reminder already sent or send uncertain | Reconcile its identity; do not send another; continue original schedule |
| Browser/error/unknown generating state | Read-only recovery; no speculative reminder or technical substitution; stop dependent work if facts cannot be verified |
| Original deadline reached without valid final reply | Persist timeout and handoff, account for workers/resources, report and end the main turn |

While waiting there is no new task dispatch, technical decision or integration/merge. Already dispatched workers finish only the current approved bounded assignment and idle; questions requiring a new decision remain pending. Read and record completed worker reports, but do not treat completion as technical acceptance or dispatch a ready dependent task. Scope withdrawal stops affected running work instead of allowing it to finish.

## Resource-safe pause

On dot failure or deadline, preserve task status and all intents, replies, original evidence, branches, worktrees and processes. Record completed implementations as **awaiting review**, not accepted. State the blocker and next recovery step to Owner; never replace dot or bypass its review.

Owner's approved workflow permission explicitly retains settled worker terminals while the flow is paused awaiting dot/recovery; record that permission's provenance and use the live orchestration `worker-retain` contract for each accepted settlement. Process every delivered message before acknowledgment. Live workers may finish the existing bounded assignment and then idle under the same retention instruction; those needing a new decision wait. Unknown liveness is retained as unknown, never released/restarted. This is retention permission, not permission for blanket close, release or resource deletion.

Before ending, persist each worker's current assignment, actual Run/Task/Dispatch/host identity and the retention/remaining accounting obligations. Main crash does not kill workers. There is no polling, new dispatch or technical continuation without an active main session; resumed main must reconcile authority and original deadline first.
