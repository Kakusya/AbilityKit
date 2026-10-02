# Independent S05 increment review

Reviewed immutable production commit `c6bc46c5b` and evidence commit `580033908`, worktree `cooking-order-s05` observed clean. Read main reviewed S05 design, s05-worker-brief.md and final-review.md. No .NET commands or source edits were made in the reviewed worker branch.

## Blocking finding

`CookingRecipeLoop.cs:1984–1999` consumes a disposable serving container or removes a washable serving container, but clears only hand ownership. It does not detach that container from an enclosing container's `_containerItems` list. Public PutIn allows a compatible outer tray to contain a serving cup (the BoundOrder is on the product, not on the cup); BindOrder and SubmitOrder correctly resolve its reachable position and do not forbid nesting. Successful nested-cup Submit therefore leaves a parent index referring to a Removed cup. Exported checkpoint restoration rejects ContainerContentUnavailable. Same parent-detach omission exists for washable recovery. This is a concrete source-path finding; no failing runtime reproduction was run in the worker worktree. Coordinator authorized a minimal integration self-fix and public-command regression; acceptance remains pending that fix and rerun.

## Verified implementation boundaries

Binding uses existing product ItemId/OrderId/ExpectedItemVersion and fixed-Tick ingress; versions/counters preflight, rebind validates its destination before replacing the old binding, per-order uniqueness is enforced. Bound products must remain in a clean correct serving vessel, with one material and no processing locks; their vessel can move and be shared. Bound-food removal/processing is rejected until explicit unbind, while ClearContents/Discard and order cancellation clear binding state. Same-Level restore checks order existence/Open status/recipe, vessel compatibility, reverse index ownership and uniqueness. Success handoff clears bindings; schema4/format5 requires explicit BoundOrder fields. RequiresBinding and DisposableOnSubmission propagate through content, frozen configuration and identity. Disposable clean-pool supply/washable overlap is rejected. Meals retain direct submission; disposable hand-held cups are removed without washing. Actual outer-container consumption is the unresolved defect above.

## Independent execution evidence inspection

Parsed raw TRX referenced by the final worker gate summaries, checking files exist and counters rather than trusting worker text:

| Gate | Scope | Passed / failed / not executed |
|---|---|---|
| cooking-kitchen-loop, 20261002-192317 | Kitchen focused | 221 / 0 / 0 |
| same | Cooking domain | 343 / 0 / 0 |
| same | ET runtime | 76 / 0 / 0 |
| cooking-et-level-runtime, 20261002-192356 | Cooking domain | 343 / 0 / 0 |
| same | ET runtime | 76 / 0 / 0 |

Both ET TRX contain all **three CookingOrderBindingEtTests**, Passed. Source inspection confirms actual TryEnqueue→Tick production/plating, unbound Submit rejection, binding, codec export, destruction/restoration, once-only submission and cup hand release; another test demonstrates same-Tick two-player binding arbitration, unbind/rebind and wrong-order rejection, plus a real fingerprint golden vector. Their fixtures do not exercise a cup nested in another container. Full worker gates are passing evidence for the tested cases, not proof the new ownership finding is absent.

## Remaining integration scope

31 actual menu drinks must opt into binding/disposable hooks and rerun complete production/ET delivery. S07 replenishment, S06/S08/S14 integration, network codecs and Unity are separate work. This review does not reopen accepted core movement or treat metadata as completion.

## Authorized integration self-fix

In the integration worktree only, SubmitOrder now detaches a removed disposable/washable vessel from its enclosing container index before applying its existing disposal/washing branch. The outer tray and other materials are preserved, and hand cleanup remains unchanged. No format/config/menu/ET changes were made.

New `CookingNestedServingVesselTests` uses real StartProcess/FixedTick/Pickup/PutIn commands to produce and plate food, nests the cup into a compatible tray, then Bind/Submit while carrying the tray. Two cases cover disposable and washable vessel removal, retained spare material, tray hand ownership, no dangling cup contents, washing status and full checkpoint reconstruction. A third case asserts bound nested-cup extra material and repeated binding rejection without unbinding/consumption. Existing S05 two-product/one-order tests separately cover occupied-order rejection. Whitespace checks are clean; tests await coordinator-run compilation and no new pass claim is made yet. No commit or worker-branch source change was performed.
