# Implementation

Approved boundary: explicit-only Cooking workflow skill; Owner approved final proposed_plan with “Implement the plan.” on 2026-10-05. No unresolved owner decisions; this approval is not permission to run a Cooking workload or contact dot/GitHub now.

## Ordered checklist

- [x] Coordinator prepares converged PRD/design/implement and real spec/research manifests; validates before task start.
- [x] One Orca supervised implementation worker owns new skill folder, SOP opt-in addition and AGENTS route only. Exact current Orca worktree is allowed; coordinator writes only this task's artifacts concurrently, no file ownership conflict. No extra checkout is necessary for this documentation change.
- [x] Worker creates explicit-only skill and references, validates frontmatter/links/format and reports actual commands/exit/results. No unrelated source, commits, GitHub/dot writes or worker spawning.
- [x] Independent Orca reviewer reads final skill and raw behavior scenarios, gives actual action decisions, checks scope and records findings in this task research. Review output is not approval to coordinator edit worker-owned files; fixes routed to implementer and re-reviewed. No blocker/fix required.
- [x] Coordinator inspects final diff and targeted checks, records status/identities and remaining NotRun live paths. Integrates only these doc changes in current branch; no master/remote merge, publication or worktree deletion. Routine accepted-settlement terminal release was completed through Orca, separate from future paused-flow resource deletion.
- [ ] Record final task status/evidence and journal according to local workflow. Do not archive unrelated tasks or commit developer/runtime/cache/log.

## Validation

Run skill-creator `scripts/quick_validate.py` against `.agents/skills/cooking-dot-workflow`; check referenced local files and anchors, UTF-8, absence of scaffold placeholders and `git diff --check`. Validate task context with `python .trellis/scripts/task.py validate 10-05-cooking-dot-workflow`.

Independent behavioral scenarios: normal approved request; dot generating; old-SHA reply; 30-minute timeout with a live worker; main crash before reply; two unfinished flows; lost start/Issue-create/merge receipt; proven worker failure versus unverified liveness; old coordinator still active; wrong Orca project identity; final code changed after dot approval; dot says accept but required test NotRun; postmerge failure. Record predicted actions and relevant skill rules, not only heading/regex matches. Scenario replay is document reasoning, not live browser/network/worker workload testing.

No .NET/Unity/gameplay/physical LAN tests apply to document skill implementation (N/A); live dot/GitHub end-to-end NotRun. Preserve validator failures and independent review findings in research. First real Cooking workflow invocation is the separate live pilot; do not infer it passed from document checks.
