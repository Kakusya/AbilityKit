# Independent scoped menu Host/runtime review

Review boundary: Host integration commit d1176c5c9c026fa8633cbfad8792b3fb57573c6b in cooking-integration-s06-s14, runtime producer commit 2ba0217645785c01e7153a7e9b3af0d7900a6b6f, and root-owned uncommitted Level checkpoint format8, five legacy test updates and generation-copy policy assignments. Main baseline c60bc8446. This is a read-only source review; no production edits or independent .NET execution.

## Findings (fixed)

No new mechanical defect found. Earlier coordinator findings have actual fixes: ConfigureMenuPolicy uses trusted Created/Preparing installation admission rather than ordinary gameplay admission; optional null catalog/policy/Front cannot fall back to legacy; Restore resolves and freezes trusted menu/Front/preparation once and passes those values into construction. The alternating factory test asserts one policy read and matching restored identity.

## Findings (not fixed)

No blocking defect identified within this increment. Remaining acceptance obligations are full combined gates, natural operating replay/continuation, narrowed successor material/recipe permission controls across real carry, durable trusted Host loading and singleplayer exit. These are design/task completion obligations, not inferred passes from this source review. Published-format/spec routing must be updated when the format8 source is actually imported, keeping Recipe5 and major baseline2 distinct.

## Reviewed code paths

- Runtime policy is frozen and installed once; repeat identical policy is allowed only within the trusted installation window. Closed, reentrant, owner-denied and bound Running installation rejects. Ordinary gameplay gates are not widened.
- Material permission is (Base union ConfirmedUnlocks) intersect Allowed. Selected catalog recipe closure gates new StartProcess and new portion allocation, including inputs/output/processing carrier. PutIn and ordinary Pour recursively inspect physical contents; existing-object Pickup/Drop/TakeOut/Clear/Discard remain recovery paths.
- New RequestSupply and TakeSupply require unit permission and finite package permission before ledger or allocator mutation. ReceiveSupply is intentionally exempt so approved deliveries survive narrowing. Infinite takes do not require a package never materialized by that action.
- Bind/Unbind/Rebind obey BindingCommandsEnabled after shared ingress and item validity/version checks. Existing processing Continue/Stop and autonomous completion are not newly blocked by material permission. Preview's sandbox copies all three immutable menu fields before executing candidate commands; private generation copies carry the same trusted immutable references.
- Host validates current selected menus as a subset of loaded provenance with matching catalog identity and exact scoped Front template set. Ready queries the actual owned kitchen and returns structured diagnostics without lifecycle/stock/layout/process changes on rejection. Legacy no-menu factories retain previous admission.
- Preparing/Running Restore checks saved MenuConfigurationIdentity against external trusted reconstruction before gameplay creation; one frozen resolution is reused. Running menu Restore restores Recipe before Ready/Start, then restores Front and adopts saved frame metadata. Required nullable MenuConfigurationIdentity participates in canonical/hash; Level codec8 explicitly replaces7 without pretending a migration. Payload identity is not an authorization grant.

## Verification

- Whitespace: integration git diff --check passed (CRLF normalization warnings only).
- Lint/type-check: not independently run; natural operating producer owns the serialized .NET window. Host producer reports final compile/test command passed; root must run full combined gates.
- Tests: reviewed actual assertions in CookingMenuReadyEtTests and CookingMenuRuntimePolicyTests. Producer Host evidence reports12 passed, zero failed/skipped; runtime producer/root reports24 passed. These are attributed producer/coordinator results, not reviewer-run tests. No claim of full S14/network/Unity completion.
