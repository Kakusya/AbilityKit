# Local Cooking branch integration and retirement

## Authorization and scope

Owner on 2026-10-08: “请逐渐把本地分支合并到master,然后删除其他本地分支”. This authorizes local integration and local branch deletion, superseding Issue13's earlier branch-only merge restriction for this exact accepted delivery. It does not change the Cooking-only product scope, authorize remote pushes, close Issues, or revive historical out-of-scope implementation.

Only root coordinator performs Git operations. Existing user caches, private workflow records, Practice files, processes and remote refs remain preserved. Git and Orca inventory show one AbilityKit checkout; issue6-test-gate-results is absent.

## Acceptance

- Preserve every original local branch tip under explicit archival tags before deletion; independently verify an incremental Git bundle of unique commits.
- Integrate the accepted Issue13 candidate and approved governance documentation into local master, with actual integrated-source checks.
- Recognize the already integrated API documentation and patch-equivalent Cooking compiler repairs; preserve newer master fixes.
- Quarantine audit/issue6-b1 and audit/issue6-b2a: their MOBA/Shooter/Unity changes are prohibited by AGENTS and remain unmerged.
- Delete all non-master local branch refs only after merged reachability or verified quarantine preservation. Do not delete remote refs.
- Record original tips, decisions, commands, native results, source/binary identity, exact local evidence and remaining limitations. End on master.

## Not included

Product repairs, ET/dependency changes, unrelated examples, Unity, physical LAN, whole-repository gates, Issue closure, remote publishing and filesystem/process cleanup.
