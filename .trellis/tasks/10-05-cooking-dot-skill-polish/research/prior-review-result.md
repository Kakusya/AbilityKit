# External skill review result

Request: AK-CDF-REVIEW-20261005-01
Reviewed source: 86d44e8f8a6ac2ca958f4c453555817da7b1f15c
Remote branch: docs/supervised-issue-sop
Source commit: https://github.com/Kakusya/AbilityKit/commit/86d44e8f8a6ac2ca958f4c453555817da7b1f15c
Conversation: https://chatgpt.com/dots/01a10258-c084-74bb-8bcc-ec01958e5023

## Outcome and limits

The authorized push and substantive external review are complete. Dot did NOT accept this as a mature skill for reliable autonomous delivery and interruption recovery. This is a review-only task; its completion does not approve the reviewed skill or activate a Cooking workflow. No skill revisions, Issue/PR creation, merge, live delivery pilot or resource deletion occurred.

Dot reports reading all seven requested files at the pinned commit and the related archived plans and Trellis lifecycle code. This is its read claim, preserved in full; the browser conversation and matching request/SHA were observed directly. Actual installed Orca normal-Run rebinding capability was unavailable to dot in GitHub sources, so that capability remains unverified, not disproved.

## Prioritized findings

1. Fix restart contracts first: a stable task-local checkpoint entry/version/flow ID, authoritative location, write ordering and copy deduplication; verify normal Run recovery and exclusive authority before dispatch; specify late reply timing, one shared follow-up quota and timeout reopening.
2. Prevent false completion: ending a paused session cannot archive its unfinished task; inspect GitHub automatic Issue closure before merge; separate frozen accepted code from subsequent review/evidence commits.
3. Improve the skill entry: one ordered normal path and two concise request/decision templates; remove duplicated SOP/reference rules and retain live Orca capability discovery.

Keep explicit invocation, sole coordinator/dot/Orca role boundaries, Cooking authorization, honest evidence, request/SHA/Dispatch matching, original wait budget and bounded workers. No new scheduler or mandatory scripts are recommended.

## Local corroboration

Inspected unchanged local source matching the reviewed commit:
- recovery.md lists record fields but specifies no fixed filename/version/flow ID/authority/deduplication/write order.
- common/io.py:107-147 provides temporary-file replacement, not workflow transactions or a power-loss guarantee.
- common/active_task.py:732-758 writes per-session task context, not an exclusive dispatch lease.
- common/task_store.py:1317-1352 sets archived task status to completed; 1428-1457 clears bindings and moves the task without dot or integration gates.
- dot-dialogue.md permits ambiguity confirmation but separately counts reminders; late response time versus observation time is unspecified.
- SKILL.md requires integration before Issue close but has no guard for merge-triggered automatic closure.

No actual broken PR or runtime recovery is claimed. These are contract gaps and an unverified prerequisite.

## Verification

| Result | Declared coverage | Evidence |
| --- | --- | --- |
| Passed | Exact source branch push, exit 0; explicit repository/ref destination | push-intent.json, push-receipt.json |
| Passed | Remote docs tip equals reviewed SHA; master remains 7aa3e8b67c13a469192edaa761cb3dfc1fa9294b | push-receipt.json |
| Passed | One request posted and read in bound conversation; no reminder sent | dot-request.txt, dialogue-state.json, dot-acknowledgment.txt |
| Passed | Full stable matching review captured, 6157 characters, same message/text in observations 03 and 04; no generating indicator on completion | dot-observation-03.json, dot-observation-04.json, dot-review.md |
| NotRun | Real autonomous delivery, crash/rebind, concurrent takeover, delayed-reply fault injection | No pilot authorized in this review task |
| N/A | Product/.NET/Unity tests | No product or skill implementation change |

Dot recommends observable bounded scenarios for recovery, duplicate effects, timeout/late replies, paused session end and postmerge integration failure. Those scenarios were proposed, not executed or approved by this review.

Original push setup and read-only browser evaluation errors remain in push-receipt.json; they do not invalidate the subsequent observed successful push/review. No old static acceptance evidence is rewritten.

## Next boundary

Report the review to Owner. Skill refinements and a live pilot remain separate future work; this request asked to hear dot first.
