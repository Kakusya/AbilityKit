# S3 actual caller pilot and final branch-only check

**Passed** for the bounded caller acceptance, not every underlying run. [Inspection](s3-caller-inspection.json) contains exact native/result/resource associations, timestamps, tool chunks and SHA-256 of full locally retained tool receipts/artifacts. Source checkout is 6a41945b348bdf2cc0d62fc8e2d61e7a72049292 **dirty=true Debug**, because plans/user journals/raw local evidence exist. DLL matches accepted S2 implementation: 7A1F355392452B7B66EA7266E22D1903536B69B82C101676385FC40EB67F2DB8. No build/test rerun or product changes.

Original main functions.exec → tools.exec_command → PowerShell → dotnet submitted each of three cases exactly once, waited in that bounded tool call, received complete native terminal return and associated corresponding artifacts. Native exit propagated explicitly by shell exit; no yielded session, watcher, MCP or automatic Orca wake proof.

| Caller case | Native exit | Actual result | Main interpretation |
| --- | --- | --- | --- |
| Normal competition | 0 | run-188e84b8fcf24588be0cb1acbf5ba6b2, execution/evidence/product Passed, cleanup Complete | Full accepted result |
| fail-after-start | 1 | run-45b196d87e6b4f51aa521e0962339dda, Failed, DiagnosticFailAfterStart, HarnessOrEnvironmentFailure; product Undetermined; cleanup Complete | Expected harness diagnostic, no gameplay dispatch/product bug claim |
| exit-before-run | 86 | Valid saved request only; no result, failure pack, identity or invented RunId | Caller Incomplete; expected negative-control behavior, not a Passed flow |

Success parent87624/children84336,83376,91464/endpoint127.0.0.1:50708; fail parent79140/children75152,57792,80164/endpoint127.0.0.1:53714. Both actual three-child Ready identities match DLL, Stopped/native0/readers complete with no cleanup errors. Diagnostic failure interpretation reads only its 10,707-byte small failure pack; includes real initial cuts, five-event window, actual Source/Invocation, reproduce command and cleanup. Resource file inspected separately only for structural cleanup/identity proof. No-result folder contains only request.json, no resource/session/collector identity was created by that branch.

Old worker competition caller transcript retained with clipping limitation; not counted as fully captured main-caller pilot. One new short success is the explicitly allowed S3 caller check, not repeated S2 validation. Existing FlowResults still declare S3CallerDiagnostics:NotRun; untouched, this external caller record is the S3 authority. Both legacy timeout/parser unknown exits, original failures, synthetic adapter unknown exit remain UNKNOWN. No physical LAN, Unity, benchmark, broad gate or automatic wake claim.

Main independently checked all22 workspace+accepted Git blob hashes unchanged, exact actual caller commands/native/request/run/cleanup/binary associations, no unapproved product/source files, and branch-only scope. Final dot evidence review still required; accepted implementation remains6a41945, no source acceptance is implied for unrelated dirty user journals. Worker explicitly retained and accounted, no new dispatch or resource release.
