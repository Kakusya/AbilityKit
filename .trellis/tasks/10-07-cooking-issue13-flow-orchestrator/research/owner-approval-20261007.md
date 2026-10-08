# Owner continuation authorization — 2026-10-07

After the main presented the complete S0 review and asked for the five categories/S1 scope, Owner supplied the active-thread objective:

> 请你完成issue#13,禁止过度测试,有模棱两可的问题及时向dot询问.

This subsequent instruction authorizes pursuing the full Issue13 implementation using the just-presented dot plan, including the five documented rule categories/version1.0 and bounded S1→S2→S3 scope. The prior S0-only restriction is superseded for implementation. Stage review stops remain technical gates, not repeated routine Owner approvals. Ambiguous API/semantics/acceptance changes go to dot; no relaxation of approved rules to erase failures.

Approved categories: C13-OWN, C13-REJECT, C13-IDEMP, C13-CONVERGE, C13-COMPLETE, version1.0, exactly the revised cards/API/Flow semantics published in `71609c5015f799f0304a365b06910102975583ae`. S1 CONVERGE is N/A; S2 consumes it. Default startup/step/convergence10s, overall120s including reset10s/publish2s reserved. Existing card allowance for stricter convergence1–10s is retained; no invented broader parameters. Only the two registered fixtures/flows and modes in the reviewed design are approved. New categories/semantic changes/threshold relaxation still need Owner review.

Testing instruction is binding: one focused build/window per unchanged source, necessary positive/negative controls and real two-flow executions, short related regression only. No default full-repo gate, repeated compiler-input archive, all historical Rich scenarios, control-number inflation or repeated successful reruns. Any repeat requires source change, failure or identified coverage gap.

Main remains the original sole coordinator (live env/fleet matches immutable flow), Orca worker owns product implementation; main owns task/approval/dot dialogue/independent inspection. No native subagent substitutes for the requested Orca provenance. Exact files/consumers/checks are in implement.md and worker specs. Worker is not alone, preserves main task edits and two pre-existing journals.

The existing immutable flow still declares branch-only; this authorization expands approved implementation scope, not the immutable record format. Production merge/Issue-close mode will be resolved against a concrete accepted candidate before any such effect. Publication and worktree/process deletion are not implied. No Unity/examples/dependency upgrades/old Issue6 recovery are authorized.
