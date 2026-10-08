# Local integration and branch retirement completed

Owner authorized gradual local branch merges and deletion of other local branches on 2026-10-08. The reviewed Cooking-only plan is fulfilled. This local operation supersedes the prior Issue13 branch-only merge restriction; it does not supersede the prohibition on other examples or authorize remote publication/Issue closure.

## Integration

- Initial master: `271f76c8e4ab88ae93b28fb49e46f4700a399b66`.
- Planning commit: `674671de430370e5fe253ef831719cdafeab088a`.
- Governance merge: `d225c9055` (normal ort merge); no conflicts. It preserves the approved coordinator-only worktree creation rule and archived governance evidence.
- Compiler trace ancestry merge: `0692eeafe`; original `a54b49ab46cd4229d25face493146a5f6b8349f1` equals integrated `2b258b6e` by stable patch ID `e924e4d39b7c1f6a824c3d48586389f0cbfd063f` and `git cherry`.
- Compiler target ancestry merge: `1c38e0227`; original `3fe069c3da5fd574ed6875d225bb9fd76bd69fa1` equals integrated `c13f6bbd` by stable patch ID `c89e0d882391d50debb75fa89cfab6c7938a14b2` and `git cherry`.
- Both redundant repair merges use `-s ours` to retain later accepted master fixes. Full tree before/after each merge is identical: `c8f4d98931142fe1c833f7ef730ea04772b29077`. This adds ancestry only; it does not replace newer source with old repair snapshots.
- Issue13 merge and actually verified source: **`70ed8ec81a6be824476f21fcb613b4037f83f652`** (normal ort merge). All 22 delivery blobs match both accepted implementation `6a41945` and the accepted manifest. Final acceptance `a38ef86` and journal commit `b3bbc4bb5` are ancestors of master. No delivery/source repair was needed.
- API documentation `06825bde9` was already an ancestor of initial master; no redundant content merge.

The master diff from the initial baseline contains no tools changes, Unity source changes or MOBA/Shooter/example changes. The only runtime-related additions are the accepted Cooking acceptance CLI, its focused tests, their project reference and the approved fixtures/documentation.

## Actual post-merge checks

Full associations, command arguments, counters, artifact paths and binary hashes are in [integration-results.json](integration-results.json). Raw originals remain under `local/Logs/branch-integration-20261008/`; private historical workflow records remain at their original addresses.

| Check | Actual result |
| --- | --- |
| `dotnet build src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug --nologo` | Passed/native0; 2095 warnings, 0 errors. Existing dependency XML-doc warnings retained; no warning repair |
| `dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj -c Debug --no-build --no-restore --filter FullyQualifiedName~CookingFixedFlowTests --results-directory local/Logs/branch-integration-20261008/focused-tests --logger trx;LogFileName=fixed-flow-integration.trx` | Passed/native0; TRX total/executed/passed14, failed0, skipped0 |
| Actual CLI `run --request <approved JSON> --output-root <fresh directory>` | Four independent successful flows: compete and pickup/drop, each offline and network, native0; execution/product/evidence Passed and cleanup Complete |
| Network `--diagnostic-fault fail-after-start` | Control Passed with expected native1; actual Flow Failed, product Undetermined, execution incomplete, evidence complete and cleanup Complete. Failure pack identities verified |
| Network `--diagnostic-exit-before-run` | Control Passed with expected native86; only saved request exists. Caller Incomplete; no fabricated successful FlowResult |
| Network child resources | All 9 actual child identities match the newly built Flow DLL; each stopped/native0/readers complete, no cleanup error; later process query finds 0 live FlowAcceptance roles |
| Accepted delivery identity | Passed22/22 Git blobs; no changed accepted delivery file |
| Local historical files | Passed517/517 original cache/private workflow file hashes unchanged |
| Branch preservation/deletion | Passed; 7 original annotated tags verified, incremental bundle verified/native0, 7 local branch deletions/native0; only master remains |
| CI, broad gates, Unity, physical LAN, performance, remote push and Issue close | NotRun; no corresponding pass/completion claim |

SDK10.0.300/runtime10.0.8/Debug. Invocation source is `70ed8ec81` **dirty=true**, qualified by preexisting untracked Python cache and private Cooking workflow records; tracked source is clean. Flow DLL SHA256 `E44CE9ACFAF18655B20B5FD2464F9CFBA08F65E7295D8B2472E8A324D0BB55F1`. Original failures/UNKNOWN from prior tasks remain unchanged. Later task/archive/journal commits are records only and do not claim a new test run; final delivery blobs must still match.

## Branch retirement and recovery

Exact original tips, tags and outcomes are in [branch-retirement.json](branch-retirement.json). Five branch tips are ancestors of master and were deleted using `git branch -d`. Issue13's upstream setting was removed immediately before local deletion; remote branches remain unchanged.

`audit/issue6-b1` (`56ebd215d496554d6a9dd7406b4c1f1d0897958a`) and `audit/issue6-b2a` (`4dd4f3fff0d7d08cf59dfdbbadc73fe35836705f`) contain prohibited MOBA/Shooter/Unity changes and remain **unmerged**. Both are preserved under `archive/local-branches-20261008/audit/issue6-b1` and `archive/local-branches-20261008/audit/issue6-b2a`, and verified as original bundle heads before `git branch -D`. Removing branch names does not discard their commits/evidence or admit their ancestry to master.

All seven original local branch tips are retained as annotated tags under `archive/local-branches-20261008/`. Recover a branch, if needed, with `git branch <original-name> archive/local-branches-20261008/<original-name>`; recovery does not authorize merging quarantined content. The incremental `original-local-branches.bundle` additionally retains unique branch commits and requires ancestors already reachable from initial master; it is not advertised as a standalone full repository backup.

Git and Orca each enumerate only the main AbilityKit worktree; `issue6-test-gate-results` is absent. No worktree/process/filesystem cleanup or new worker occurred. Issue13 stays OPEN and its original completed branch-only task stays at the canonical evidence address. This owning local integration task may be archived as a non-PR-backed task; all private evidence, caches and Practice work stay preserved.
