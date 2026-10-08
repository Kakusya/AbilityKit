# Integration decisions

Initial master is 271f76c8e4ab88ae93b28fb49e46f4700a399b66. Freeze each of the seven branch tips before mutations.

| Local branch | Decision | Evidence / Cooking consumer |
| --- | --- | --- |
| Kakusya/cooking-dot-api-design-review-20261007 | Already merged; preserve tag and delete branch | 06825bde9 is an ancestor of master; accepted Cooking dot API design |
| Kakusya/issue6-compiler-trace-repair-ccswitch | Record ancestry using ours merge, no content change | a54b49ab4 patch-equivalent to integrated 2b258b6e; current master contains subsequent compiler fixes |
| Kakusya/issue6-compiler-input-target-repair-ccswitch | Record ancestry using ours merge, no content change | 3fe069c3d patch-equivalent to integrated c13f6bbd; Cooking gate compiler attribution |
| docs/supervised-issue-sop | Normal merge | Two governance commits; coordinator-only worktree creation and historical cleanup audit; Cooking worker lifecycle |
| Kakusya/issue13-flow-s0 | Normal merge | b3bbc4bb5 includes exact accepted implementation 6a41945 and final evidence a38ef86; Cooking fixed-flow CLI, focused tests, documentation and journal |
| audit/issue6-b1 | Quarantine tag/bundle; delete branch only | Old unaccepted gate implementation modifies MOBA tooling and carries historical broad scope |
| audit/issue6-b2a | Quarantine tag/bundle; delete branch only | Extends the preceding audit branch with Shooter/Unity/performance adapters; explicitly prohibited |

Patch equivalence is checked against both Git patch IDs and repository cherry output. Ours is permitted only for the two redundant repair tips: merging their older trees would risk reintroducing superseded fixes. Before and after each such merge, the full Git tree must be identical.

Merge docs first, redundant repairs next, then Issue13. For Issue13, inspect actual post-merge delivery blobs against its accepted 22-file manifest. Build/test from integrated master rather than reuse old binaries. Run the focused CookingFixedFlowTests once, four approved short offline/network CLI positives and the two existing caller diagnostics. Evaluate diagnostic native1/native86 as expected controls, never successful product runs. No broad suite is necessary for this thin acceptance host change; original product domain, session and transport sources remain unchanged.

Archive tags and an independently verified local incremental Git bundle retain original commits without admitting them to master. Deleted local branches can be recovered from tags. Do not create secondary worktrees or dispatch workers.
