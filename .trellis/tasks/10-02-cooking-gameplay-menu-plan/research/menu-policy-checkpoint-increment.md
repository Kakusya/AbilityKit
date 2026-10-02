# Menu policy checkpoint candidate

Root-owned candidate extends Level checkpoint with required nullable MenuConfigurationIdentity and includes it in canonical integrity. Format8 explicitly replaces7; no old-format migration or saved permission grant. Legacy no-policy records serialize explicit null. Trusted Host comparison/wiring is still produced separately and must be integrated before complete recovery acceptance.

Actual focused checks:
- Domain CookingCheckpointRecoveryTests: 9 passed /0 failed /0 skipped, including missing field, explicit null, policy tamper, round-trip and prior format rejection.
- Relevant existing ET binding/front/core tests first failed 2 of18 due two stale literal version7 assertions. Corrected to format8, rerun18/18 passed without skips. Red and green logs retained separately.
- Existing ET supply tests: 5/5 passed after updating its format assertion.

Logs: local/Logs/cooking-menu-policy-checkpoint.log, cooking-menu-policy-checkpoint-et.log (red), cooking-menu-policy-checkpoint-et-green.log, cooking-menu-policy-checkpoint-supply.log. TRX retained in local/Logs/cooking-menu-policy-checkpoint/.

This is a candidate tested in main working tree, not a published complete Host Ready/runtime permission/restart increment. Full integration gates and independent review remain required. Main verified production125ffe906 remains the published Level7 evidence checkpoint until acceptance of the new increment.
