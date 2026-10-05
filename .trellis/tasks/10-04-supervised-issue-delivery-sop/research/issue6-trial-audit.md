# Issue 6 SOP trial — in progress

2026-10-05 owner correction supersedes the continuation below: Cooking is the sole product scope; Shooter/MOBA example adaptation and the original whole-branch integration plan are stopped. Existing commits/evidence remain isolated, not accepted or merged. See [scope correction and cross-worktree source search](cooking-only-scope-correction.md). Historical audit findings below retain their original scope.

2026-10-05: coordinator owns former dot duties; implementation uses GPT-6.1-Sol / medium in the existing managed worktree. Full Issue acceptance, integration and cleanup remain pending.

- Slice A `086e73491b35fa0e8fe76708f3cb3956bd7e7505`: coordinator independently reran 86 isolated controls successfully, then demonstrated actual runner acceptance of a step receipt masquerading as a gate. The original failure was retained and returned to the worker.
- Slice B1 `56ebd215d496554d6a9dd7406b4c1f1d0897958a`: worker frozen suite executed 133/133 with zero failures. Earlier 119 registered controls had only 118 executed and skipped runner integration; that subset is not full acceptance.
- Coordinator checked B1 in a separate clean audit branch, preserving the SOP branch and leaving master unchanged. The exact type-confusion positive/negative controls now behave correctly.
- Independent full B1 replay executed 133 controls with one failure: native stderr formatting inserted PowerShell error metadata into an assertion's original text. Source fingerprint stayed `bbb147a2027705d28febf5b30b707b0db9ee7db8c1fdaf18869c9f77d139b119` before/after; source was clean.
- Further real-runner controls found empty parent coverage bindings after parsing child JSON, despite Passed/full acceptance, and acceptance of lowercase `passed` rejected by the durable schema. The canonical leaf passes schema; the lowercase leaf fails SchemaEnum. Both emitted parent summaries fail SchemaMinItems because of the independent binding defect.
- B1 is a checkpoint, not accepted delivery. All three findings are assigned to the next worker owning the shared validator. Raw evidence remains in coordinator `local/Artifacts/issue6-coordinator-audit/`, especially `b1-56ebd215d`, `canonical-status` and `type-fix-443160fe-3372-43de-8000-327ba4ebc3a9`; originals are not overwritten. These synthetic controls do not claim real .NET, Unity, service or performance coverage.

This trial demonstrates why worker self-checks, independent source/serialization-boundary controls, raw evidence inspection and final integration are separate exits. The complete SOP has not yet reached its end-to-end exit.
