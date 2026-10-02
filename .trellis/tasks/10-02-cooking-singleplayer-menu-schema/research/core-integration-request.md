# Core integration request: precise shared contracts

Coordinator reply to Orca ask confirmed core not yet published at2026-10-02 and prescribed these types. This is a requested integration patch, not applied code or verified runtime capability. Core owns recipe/config/checkpoint; coordinator owns S05 binding/disposal. S04 owns only mapper and source graph.

## Required recipe fields

Append optional `string Execution = "Automatic", int YieldPortions = 1, string? RequiredProcessingContainerDefinition = null` to CookingContentRecipe. Carry typed equivalents through CookingRecipeDefinition, configuration snapshot/canonical and recovery identity. Validate positive yield, defined execution mode, and optional processing container existence/acceptance/capacity. StartProcess and preview matching must require the anchor container definition when specified. In-progress consumption and repeat-input matching must use the full multiset; DefaultInputs remain empty for menu routes. No per-dish commands.

S04 typed mapping after publication:

```csharp
new CookingContentRecipe(step.Id.Value, ExpandInputs(step), step.Output.Value,
    step.Process.Value, step.Capability, step.RequiredTicks,
    Completion: Completion(step), Execution: step.ExecutionKind.ToString(),
    YieldPortions: step.YieldPortions,
    RequiredProcessingContainerDefinition: step.Carrier.Value);
```

For batch yield>1 `Completion` is RetainInputs; batches are extracted using public ServePortion and reuse the remaining authority count. Expanded stages all require their actual carrier (cake mold, bake tray, steam/fry basket etc.). Thermal load -> heating -> unload recipes link explicit stage products. Wrong generic vessel must reject before any mutation.

## Binding and disposable vessel fields

Append `bool RequiresBinding = false` to CookingContentOrderTemplate/CookingOrderTemplateDefinition; carry into validated canonical/config identity and open-order state/checkpoint or derive from validated immutable template. Submit checks binding on the real serving vessel/product. S04 passes menu.RequiresBinding; all31 drinks true, meals/desserts false.

Append `bool DisposableOnSubmission = false` to CookingContentContainer and domain capability. Carry into canonical/config/checkpoint identity. Submit removes disposable vessel and hand/reference after consuming product, does not queue washing. CleanPool non-disposable plates/bowls preserve wash behavior. New hot/cold cups are world supply and enter RefillContainers closure; S07 owns replenishment operation. Legacy defaults unchanged.

## Actual source/canonical identity hook

Requested exact optional metadata contract (in existing formal content/config owner):

```csharp
public sealed record CookingContentProvenance(string CatalogSchema,
    string CatalogSha256, IReadOnlyList<CookingContentSourceIdentity> Sources,
    IReadOnlyList<string> SelectedMenus);
public sealed record CookingContentSourceIdentity(string Path, string Sha256);
// CookingContentDocument and CookingConfigurationCandidate:
public CookingContentProvenance? ContentProvenance { get; init; }
```

CookingContentCatalog.Load copies document.ContentProvenance onto candidate. ValidateCandidateShape/Validate checks v1 catalog schema, lowercase64-hex catalog/source hashes, unique nonempty paths and selected IDs; reject malformed metadata atomically. CreateSnapshot defensively copies and sorts Sources by Path and SelectedMenus ordinal. CookingConfigurationSnapshot retains it. CanonicalConfiguration includes nullable provenance, so CookingConfigurationIdentity derived SHA changes for source/catalog semantics even if recipes are unchanged. Existing checkpoint/config identity comparisons then reject stale contents without a separate menu checkpoint.

S04 ToContentDocument assigns `ContentProvenance = new(CurrentSchema, Sha256, document.Sources.Select(...).ToArray(), selected)` and does not return loaded production contents without this identity installed. Direct LoadContent invokes the same registry as baseline. Required tests: reordered selected IDs/sources same identity; source hash or catalog hash changed -> different actual content.Identity and checkpoint restore mismatch; old soup/toast continue working.

This metadata carries a verified catalog hash, not arbitrary serialized source text. New schema/format version chosen solely by core owner. These are requested hooks; current source-only increment still has the explicitly blocked legacy adapter and does not claim87 runtime acceptance.
