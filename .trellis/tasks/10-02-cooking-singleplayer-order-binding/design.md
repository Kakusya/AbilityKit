# S05 reviewed concrete design

## Existing owner and API
Append BindOrder, UnbindOrder, RebindOrder enum values; reuse Item, Order and ExpectedItemVersion payload. Append nullable BoundOrder to physical ItemState and snapshot/checkpoint item. Binding operations use existing submission scope, shape, eligibility, dedup, stable ordering and Commit lane. Bind/rebind validate live final product, open matching order and correct reachable serving vessel before mutation; rebind never removes old ownership before target validation. Unbind checks current ownership and keeps the food.

## Configuration and delivery
Append RequiresBinding=false to CookingContentOrderTemplate and CookingOrderTemplateDefinition; append DisposableOnSubmission=false to CookingContentContainer and CookingItemContainerCapability. Frozen mapping and canonical identity include both. Definition schema stays v3 because optional configuration defaults retain compatibility. Binding conflict/required/not-found reject reasons are explicit. Submit validates binding before food/order/settlement mutations. Disposable serving vessels are tombstoned and clear occupancy; washable vessels follow the existing washing path. Conflicting washable/disposable configuration and disposable cleanPool supplies reject.

## Persistence and versions
Recipe schema increments once from 3 to 4 and Level format once from 4 to 5. BoundOrder is JsonRequired even when null, so current-format checkpoints cannot silently omit authority state. Canonical snapshot and checkpoint include binding. Restore validates binding ownership, final product/recipe, physical vessel and Open matching order; no duplicate order owners. Existing provenance, full processing input locks, capabilities, poses and portion validation remain.

## Change ownership
Core partial/loop/checkpoint owns behavior; configuration/content mapping owns defaults and identity; ET fingerprint currently already includes Item/Order/ExpectedItemVersion and operation. Enum values append, so existing vectors remain valid; new binding vectors prove payload distinction. No new owner or ticket entity. Coordinator reserved ET fingerprint and Level-format ownership for this dispatch.
