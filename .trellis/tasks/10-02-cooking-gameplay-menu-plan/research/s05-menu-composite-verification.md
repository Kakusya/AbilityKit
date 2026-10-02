# S05 / menu composite verification — 2026-10-02 19:36

This is integration-branch evidence, not a master delivery or full singleplayer completion.

Integration base: `03b138cd2`, plus reviewed nested-serving ownership fix committed as `901f465b5`. Includes S05 imports `4b94fadbb` / `fc22662e8`, menu carrier/provenance and 87 ET recovery `30f3e6ab1`, and real manual handoff/partial-batch recovery `03b138cd2`.

Independent review found a live ownership defect in SubmitOrder: consuming a nested serving vessel left the parent container referencing a removed item. The shared submission branch now detaches the serving vessel from its parent for both disposable and washable paths. The outer tray and other contents remain; invalid requests remain atomic. Three public-command tests passed before the composite gate.

Actual `cooking-kitchen-loop` gate passed: 340 focused, 462 Cooking domain, 166 ET; zero failed/skipped. Both production projects built. Existing CS1591 warnings are not a zero-warning claim. Full raw summary is copied beside this document as `s05-menu-composite-gate-summary.json`; its TRX/log pointers refer to the retained integration worktree.

A first invocation used the nonexistent gate name `cooking-kitchen` and failed before executing tests. Corrected to catalog-authoritative `cooking-kitchen-loop`; only the latter is recorded as passing.

The 87-menu tests prove actual spatial production/plating and ET codec/export/dispose/restore controls. The 31 drinks still await RequiredBinding/Disposable mapping and actual bound delivery. New manual handoff tests cover F01/D31 and a partially served D31 batch; they do not prove all 87 routes or complete front-house/supply/layout integration.

S06 domain extension remains partial. Its next work connects front work to the existing Level ET ingress, single fixed tick, trusted restore policy, and kitchen/front manual exclusivity. No Unity or network delivery is claimed here.