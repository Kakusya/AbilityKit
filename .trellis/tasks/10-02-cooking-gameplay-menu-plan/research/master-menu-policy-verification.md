# Reviewed scoped menu Host/runtime master verification

Production master `fa2f60e17` imports reviewed Host `d1176c5c9`, runtime `2ba021764`, root Level8 codec and immutable private-copy policy fields. All16 source/test files were byte-identical to the final integration gate input before commit.

Actual final integration:
- Kitchen `20261003-002025-cooking-kitchen-loop`: 630 focused /758 Cooking /266 ET, zero failures/skips, 34.3s.
- ET `20261003-002218-cooking-et-level-runtime`: 758 Cooking /266 ET, zero failures/skips, 23.3s.
- Earlier kitchen `20261003-001730-cooking-kitchen-loop` passed630 focused but failed domain1/758 due historical enum total count. Prior reason numbers remain asserted; Supply and appended MenuNotAuthorized numbers now also have explicit stable assertions. No production permission failure was hidden; original failed summary/log retained.

Actual master after commit:
- Kitchen `20261003-002402-cooking-kitchen-loop`: 630/758/266, zero failures/skips, 33.9s.
- ET `20261003-002645-cooking-et-level-runtime`: 758/266, zero failures/skips, 22.8s.
- Logs: local/Logs/cooking-master-menu-policy-kitchen-gate.log and cooking-master-menu-policy-et-gate.log; summary JSON and TRX remain under actual gate directories.

Independent source review: menu-host-runtime-independent-review.md. Producer evidence: runtime24/24 and Host12/12; root codec9/9, legacyET18/18 and supply5/5. Earlier wrong configuration-window, null factory fallback, repeated trusted resolution and incidental comment encoding findings were fixed before final review. Actual Created/Running guard red/green is preserved separately from fixture/setup failures.

Accepted behavior: per-Level menus are a subset of loaded graph range; matching catalog identity, actual owned kitchen Ready checks, exact scoped Front templates, effective material intersection and selected recipe closure, new supply/manufacturing/binding authorization, authorized in-flight reception and carry recovery, same-policy preview and generation private staging. Restore freezes trusted scope configuration once, compares saved required nullable menu identity and installs actual runtime policy. Saved state is not a permission grant.

Current formats: definition3 / Recipe5 / Level8; typed major baseline2 is separate, old Level formats reject. Natural F01+D31 operating/replay/restore, real narrowed successor carry, durable Host baseline load/commit and full S14 exit remain open. S06/S07/S08/S14 remain in_progress. No network or Unity acceptance is inferred.
