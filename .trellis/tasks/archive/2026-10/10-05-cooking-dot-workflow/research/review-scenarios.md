# Raw forward-review scenarios

Review the completed skill as a future coordinator receiving these inputs. For each scenario give the concrete next actions, whether any mutations are allowed, what identity/evidence is required, and remaining ambiguities. This is a static behavior rehearsal, not live dot/GitHub delivery. Do not edit live orchestration/files or contact external recipients while rehearsing.

1. A new user explicitly invokes the skill, provides one Cooking feature, and approves its requirements and acceptance. Dot can read the pushed commit. No prior flow exists.
2. Dot has been visibly generating for 12 minutes. One Orca worker is midway through its current accepted task; another dependent task is ready.
3. A complete dot reply accepts commit A, while current candidate is commit B. The user wants automatic delivery after acceptance.
4. The 30-minute dot deadline expires. Worker W is still live. The workflow should stop the main coordinator turn.
5. A new main session loads the skill after the original coordinator crashed 20 minutes into a dot wait. The original prompt may have been sent, but no browser receipt was persisted. The worker may have completed.
6. There are two unfinished skill flows in separate worktrees; another unrelated legacy Trellis task is in_progress. No task selector is supplied.
7. Orca accepted a worker-start but its result was lost. A saved request identity exists; liveness cannot initially be determined.
8. GitHub Issue creation lost its response; there is an intention checkpoint but no Issue number. A later merge request similarly lost its response.
9. Worker F has a proven failed Dispatch and retained source changes. Worker U has no reachable host and stale status.
10. The original coordinator is still actively using the same Run, while a new conversation explicitly loads the skill to resume.
11. Git origin identifies Kakusya/AbilityKit, Orca projectId identifies hobobo/abilitykit. The coordinator is about to create an Issue or PR.
12. Dot says the implementation is acceptable, but a required test is NotRun. Separately, all premerge checks passed but required postmerge integration failed.
13. Dot and the coordinator disagree on a technical design; dot then recommends changing a Shooter-specific script or implementing the currently prohibited Cooking Unity stage.
14. An accepted worker needs a new technical decision while dot is unavailable. Another worker has finished and emitted valid worker_done.
15. The installed skill folder exists in the repository, but the current host has not refreshed its available skills. The user wants a first real automatic delivery pilot.
