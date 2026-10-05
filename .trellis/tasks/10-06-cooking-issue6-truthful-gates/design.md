# Technical decisions pending dot

Base: origin/master `c78ed3de83f69bb53ff1a09bbc23638115928e1e`, clean before task artifacts. New branch: `Kakusya/issue6-cooking-truthful-gates`. One original invoking main coordinates; a new Run will be created for this scoped successor, never bind an old #6 Run. Normal Run cross-main takeover is unsupported and blocks recovery dispatch by a different main.

Options for dot:

1. Selectively reuse prior shared result/provenance helper and runner, remove unnecessary benchmark/Unity instrumentation, preserve and strengthen existing TRX checks, update only Cooking declarations and shared fixtures. Legacy producers remain unadapted and cannot claim validated completed coverage.
2. Implement a smaller Cooking-driven contract on the current shared runner, retaining fail-closed script handling and all required identity/coverage controls. Document the migration boundary for unchanged legacy definitions; no hidden example adaptation.

Prior implementation changed 152 files, including prohibited example tools. Historical scope corrections and independent findings must survive: B1 133 controls had one stderr-format replay failure; empty serialized coverage bindings and noncanonical lowercase status were accepted. B2a claimed fixes and 232 isolated controls on dirty source, without real .NET/Unity execution. Those are historical controls, not current candidate acceptance. Dot must inspect actual reused code before accepting it.

No third-party validator, new schema authority or alternative gate registry. `tools/test-gates.json` remains sole gate definition. Coverage contract and summary/console/CLI need one normalized truth source. Namespace, raw log ownership, path containment, source/dirty fingerprints and build/restore/binary identity must be sufficient for declared coverage; avoid upgrading evidence claims.

Proposed file families (not implementation approval): `tools/run_test_gate.ps1`, a necessary shared result helper, Cooking entries only in `tools/test-gates.json`, `tools/tests/test-gate-result-contract.tests.ps1` and Cooking/shared isolated fixtures, testing specification, this task, and minimal Cooking progress routing. Dot must explicitly justify any Unity mirror/report adapter as shared Cooking evidence tooling before it can be added; no Editor/runtime Unity work is allowed.
