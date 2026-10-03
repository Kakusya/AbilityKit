> Later root checkpoint: first10 configurations/30 fresh sample instances now independently reconciled with the same executable and ET/Session MVIDs. Exact paths/numbers are in local/Artifacts/network-load-partial-review/current-reviewed-configurations.json; repeatable root verifier is verify.py beside it. Remaining2 configurations are not accepted here.

# Load matrix partial independent review - 2026-10-03

Root independently parsed the first seven completed configuration artifacts, covering21 distinct fresh sample server instances plus their capacity controls. Exact local artifact paths and extracted numbers are retained in local/Artifacts/network-load-partial-review/first-seven-configurations.json. Remaining five configurations are not accepted by this report. The owner continues original exec71580; no restart or rebuild was requested.

Artifacts: 20261003-051230-9336941,051602-9489459,051943-8324407,052316-6363906,052658-8856255,053031-2798601,053410-0362800 under local/Logs/cooking-network-measurement (each has the full20261003-prefix). All seven executable manifests match SHA2561582FA92D8FF89926B0282DFFBBA710E58E70170718F17D7B5964B2E6A0F7568. Actual reports preserve performanceTarget UNSET, physicalTwoPc NOT_VERIFIED and forward-goal applicability pending.

All21 samples have exact10s warmup/60s offered denominators; every participant offered=issued+backpressure+schedulerSkip; issued=admitted=accepted=completed=projected, independently per participant and aggregate. Terminal/projection timing cohorts equal issued; rejection/cancellation/pending/timeouts/ingress rejection are zero. Baseline byte/token counts retain original bounds. Exit projection has exactly2/4 participants, each ConnectedOwnerBinding/Ready/noCleanup and synchronized client instance/participant/generation. Capacity controls retain16accepted/1rejected and cachedDuplicate. Distinct sample instances were independently checked.

Readiness does NOT require asynchronous UDP clients to hold the same most-recent Session hash. Root's first local verification attempt incorrectly asserted equal clientBaselineHashes; it failed on genuine UDP timing differences. Source review confirms each client recomputes its OWN baseline hash, validates current scope/generation, while Session capture is compared to SAME-FRAME authority. The incorrect cross-client assertion was removed from the root verifier, not from producer or production. No reports were rewritten. Individual command identities and complete typed graph assertions are checked by the producer source but are not exported as raw per-command traces in these aggregate artifacts; do not claim independent raw replay of absent fields.

## Observed results

| Configuration | Accepted across three samples | Full-projection p95 milliseconds |
|---|---|---|
| InProcess2x5 |584/575/573|153.98/156.59/156.31|
| UDP2x5 |75/73/72|3075.09/3048.09/3068.72|
| InProcess4x5 |829/834/829|330.84/336.27/335.60|
| UDP4x5 |137/135/136|3128.72/3210.34/3154.70|
| InProcess2x20 |809/770/759|185.92/191.25/188.92|
| UDP2x20 |72/73/70|3172.37/3175.40/3188.72|
| InProcess2x50 |817/789/798|195.65/201.48/198.42|

These are bounded correctness/observed cost checkpoints, not smoothness acceptance. Stress20/50 are offered opportunities, not sustained admitted command frequency. UDP remains well outside forward ordinary performance goals. No physical LAN, raw impairment or Release acceptance follows from this partial review. Final matrix review/source freeze and assembled master gates remain pending.

## Checkpoint10 extension and timing cohorts

Additional accepted artifacts: UDP2x50 20261003-053742-2353395 (71/73/72), InProcess4x20 20261003-054124-0873631 (812/806/798), UDP4x20 20261003-054456-7171413 (138/139/135). Each preserves per-participant reconciliation, zero errors/pending, baseline bounds and Ready identities. Root verifier also requires a single matching ET/Session MVID pair across all configurations.

UDP2x50 repeat2 has73 issued/accepted/projected sample commands but72 Host timing samples. This is valid because command cohorts use offered sample index, while Host timings select receipt timestamp in the sample window; the source and report explicitly document distinct cohorts. The first broader root verifier incorrectly required equality and stopped; root corrected it to require client RTT/projection samples=issued and Host metric samples=timingSamples, preserving all original data. This does not weaken command admission/completion checks or authorize rewriting metric denominators.
