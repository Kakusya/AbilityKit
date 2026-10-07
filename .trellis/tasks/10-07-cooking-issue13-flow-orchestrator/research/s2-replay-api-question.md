# S2 observed replay contract gap: dot clarification required

S1 candidate `2201931e1846acb9ab8a205d501e39731b5a39bb` is accepted. Current S2 is an approved 14-file assignment; its worker found this gap during source audit, before changing delivery files. Main independently verified the source paths below. This is a technical question, not a proposed product change or new implementation acceptance.

## Observed source facts

- `CookingNetworkSessionHost.Map`, lines 303-305: matching cached mapping returns the original `mapping.Terminal` with only `Result.IsDuplicate=true` and empty result events. The original native wire disposition is retained, commonly Executed.
- `Complete`, lines 315-319: the result carries its actual domain command ID; non-Duplicate terminal is cached. Network `DomainId` maps server-instance/scope/participant/stable ID to domain identity. Product source is read-only.
- Current S1 `FlowRuleEvaluator`, lines 113-121: exact replay requires NativeDisposition="Duplicate"; authority CommandEvents are attributed by BusinessId. Offline IDs coincide, while network has a separate DomainCommandId.
- Approved C13-IDEMP card requires same scope/participant/stable ID/payload, same formal result, no new attributable business effect/event and unchanged target item/hands. It explicitly permits replay of the original result and does not fix delivery count. Native wire status must stay truthful.

At source `ce43d43929e01c8c9af72b60109d7ff7e4c4c682`, read:

- [actual network mapping](https://github.com/Kakusya/AbilityKit/blob/ce43d43929e01c8c9af72b60109d7ff7e4c4c682/src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs#L281-L320)
- [existing evaluator](https://github.com/Kakusya/AbilityKit/blob/ce43d43929e01c8c9af72b60109d7ff7e4c4c682/src/AbilityKit.Game.Cooking.FlowAcceptance/FlowRuleEvaluator.cs#L87-L123)
- [approved category card](https://github.com/Kakusya/AbilityKit/blob/ce43d43929e01c8c9af72b60109d7ff7e4c4c682/.trellis/tasks/10-07-cooking-issue13-flow-orchestrator/research/rule-categories.md)
- [accepted original API](accepted-api-design.md) and [precedence amendment](accepted-api-amendment.md)

## Narrow question, still unapproved

May the S2 adapter preserve NativeDisposition verbatim and may C13-IDEMP accept an actual network cached replay with Executed + IsDuplicate, provided it is the same current binding/stable ID/frozen payload and same DomainCommandId, same formal outcome/reason/state version, no returned/new attributable events and unchanged target ownership? Should command-event attribution use actual DomainCommandId consistently for C13-IDEMP and C13-REJECT, with no fabricated fallback from wire BusinessId?

Please provide the concrete minimal API/type/method or an explicit unchanged-public-API design statement, affected Cooking callers and behavior, exact validation requirements and negative controls. Explain treatment of missing domain identity, wrong scope/session/generation, wrong payload/result, unknown/nonformal native status and in-flight duplicates. Keep offline requirements and B1 missing-facts behavior intact. If this would change the approved category meaning or relax its guarantees, return blocked/needs-revision instead of silently broadening Owner scope.

No product/session/codec/transport/API change, new rule category/version, new dependency, file beyond the 14-file S2 scope, extra fault framework or broad testing is requested. Only unambiguous approved S2 work can continue after the response. S2 tests/CLI remain NotRun at this audit boundary; no runtime claim is made from source inspection.
