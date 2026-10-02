# S05 production order binding and disposable serving vessels

Owner authorization: current dispatched S05 assignment supersedes historical planning-only text. Base: 501bf383d. Architecture stays pure C# Recipe/Order authority and fixed Tick ET ingress.

## Outcome
Implement final product ItemId -> same-Level Open OrderId binding, unbinding and atomic target-first rebinding. One product has one binding; one order has one waiting product. Bind requires valid final recipe identity, correct serving vessel, reachability, player capabilities and current item version. Unbound goods remain movable and shareable; binding is separate from delivery. Every mutation increments observable item/global version.

Submit requires binding only for RequiresBinding templates; already-bound goods submit only to their bound order. Meal/dessert direct submit remains supported. Cancel, disposal and success handoff clear bindings; same-Level restore retains and validates unique ownership. DisposableOnSubmission vessels consume on correct Submit, release their hand/location and never request washing. Historical washable and nonwashable defaults remain compatible. CleanPool cannot dispense disposable vessels; physical replenishment is S07.

## Acceptance
Domain command tests cover rejection atomicity, stale scope/version, mismatch, completed orders, reachability, contention, shared goods, target rejection preserving old binding, once-only settlement, lifecycle cleanup and restore tampering. Actual ET TryEnqueue -> Tick -> codec -> destroy/restore -> delivery proves production path. Run focused tests and both cooking gates and record actual logs/TRX; incomplete checks are never passes.

## Boundaries
No menu/generated edits, FrontOfHouse ownership changes, root dashboards, Unity, networking implementation, master merge or push. Menu owner opts the 31 drinks in separately.
