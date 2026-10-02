# Menu authority hooks and checkpoint hardening

2026-10-02 coordinator-owned integration increment; existing Recipe/Order/configuration owners remain authoritative.

`RequiredProcessingContainerDefinition` is optional in content/domain recipe definitions. Validation requires a declared container that fits and accepts every explicit input. Matching and actual StartProcess use the physical carrier definition, including preview's isolated authority. Frozen configuration canonical identity includes this requirement. Active/completed checkpoint and fixed Tick checks use the same actual carrier and counted input matching.

`ContentProvenance` is optional metadata in content/candidate and a defensively frozen property of the validated configuration snapshot. Its catalog schema, catalog/source lowercase SHA-256 hashes, source paths and selected menu IDs are validated; source/menu order is canonicalized. Actual configuration identity includes it. This does not itself wire the catalog adapter: that remains the menu owner’s next step. No second configuration or checkpoint authority was introduced.

Checkpoint hardening rejects missing/repeated/foreign material locks, multiple processes sharing a station/anchor/material, and raw/product Recipe spoofing including tombstones. Physical DefaultInputs remain valid, alongside virtual defaults. Fixed Tick also enforces complete physical input identity and counted recipe matching before staging any completion.

## Actual verification

- Focused `CookingMenuIntegrationContractTests|CookingCheckpointTamperTests`: 19/19, failed/skipped 0; `cooking-hooks-checkpoint-final.log`.
- Production ingress/fixed Tick/restore `CookingCoreEtExpansionTests`: 6/6, failed/skipped 0; `cooking-hooks-core-et.log`.
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop`: two builds exit 0, focused 213/213, Cooking 334/334, ET 73/73, failed/skipped 0. Copied `integration-hooks-gate-summary.json` identifies actual raw logs/TRX in `local/Logs/test-gates/20261002-190108-cooking-kitchen-loop/cooking-kitchen-loop/`.
- The gate includes a frozen, uncommitted S06 work-in-progress with seven new front-house cases. Those files are excluded from this core-hook increment; passing that snapshot does not establish final S06 behavior or ET wiring. Do not relabel this gate as validation of a later commit with additional changes.
- Prior gate failed on shared front-house source mid-edit; first new focused run was 13/14 due to a Discard fixture missing TakeOut. Original failures are retained in the root execution record. No zero-warning build claim is made.

Full menu production, binding/disposable cups, procurement/layout/guest ET ingress, S14, network and Unity are outside this increment’s completion evidence. No feature is merged into master by this record.

## Final station-capability consistency check

The follow-up also rejects active process restoration onto an unavailable appliance or an existing station without the recipe's required capability; fixed Tick enforces the same invariant. One additional case checks both alterations and a valid control restore.

Actual final focused run: **20/20**, failed/skipped 0 (`cooking-hooks-checkpoint-final2.log`). The fresh kitchen gate at `20261002-190511-cooking-kitchen-loop` passed both builds exit 0, focused **218/218**, Cooking **339/339**, ET **73/73**, failed/skipped 0 (`integration-hooks-final-gate-summary.json`). This frozen snapshot included eleven new S06 cases still separately owned and excluded from the core-hook commit; it is a combination check, not proof of later front-house edits or complete S06. The earlier 19/334 results above remain historical actual runs.
