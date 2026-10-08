# Accepted S2 replay and event-identity contract

Authority: [full dot reply](dot-s2-replay-api-reply-raw.txt), request AK-I13-S2-REPLAY-API-20261008-01 / source929b2fbfa2a89d86dee40548ab29612d2a9a29c3, accept-plan. This supplements original API/amendment and S2 bootstrap; no dirty implementation accepted.

Public API/DTO/wire remain unchanged. Reuse CommandObservation.CallId/BusinessId/DomainCommandId/FrozenCommand/NativeDisposition/WireReason/BusinessResult, FlowObservation.BindingFence, FlowEvidence.Commands/Cuts, IFlowSession.ReplayAsync, IFlowRule.Evaluate and RoleControl/RoleReply. Adapter call associations are immutable run-local role/actor/stable-ID/payload/actual binding data, not a new identity service or proof of current validity.

Explicitly approved private configuration, verbatim:

```csharp
internal FlowRuleEvaluator(RuleRef identity, FlowMode mode)
```

Existing public FlowRuleEvaluator(RuleRef identity) preserves Offline. FixedFlows/internal context passes already-validated request mode, rejects unknown mode. Do not infer network mode from seeing Executed or add self-attested verification fields.

For successful fixed competition replay, original winner is a formal Accepted result. Same run/attempt/generation, role/actor, full scope, server instance and that client's own unchanged connection generation; actual Join/installed baseline/current role state prove binding before and after relevant awaits. No cross-client generation equality. Save supporting observations in existing Cuts/events, not a boolean claim.

New CallId, same BusinessId and byte-equivalent semantic FrozenCommand including Command/ExpectedItemVersion/network batch0. Do not rebuild from latest projection or overwrite wire payload with domain mapping. Original and replay actual DomainCommandId both present/equal; actual response stable ID matches request. Codec.DomainId may compare expected identity but never fill missing actual identity and never replaces generation proof. Unknown wire correlation stays null.

Both formal terminals: Completion=Terminal, present BusinessResult, no call error. Offline replay still NativeDisposition=Duplicate. Network permits actual Duplicate or cached Executed when original completed native Executed; both require IsDuplicate=true and all identity/result/effect checks. Conflicted/Cancelled/protocol/unknown/missing terminal are not successful replay; don't copy original result to fabricate a reply.

Outcome/Reason/StateVersion and other actual business fields remain equal, returned Events empty, Pickup Supply null in both. Cached Executed keeps actual original wire disposition/reason; native Duplicate keeps its actual wire reason. Event identity/quantity for actual DomainCommandId+ActorId at verified authority scope cannot increase; original successful Pickup event must exist (zero-to-zero is insufficient), item/independent hands unchanged. Unrelated tick/global version may progress.

REJECT and IDEMP consistently attribute `event.Command.Value == observation.DomainCommandId` and `event.Player.Value == observation.ActorId`. Offline also uses its real DomainCommandId; no BusinessId fallback. Missing identity/formal facts -> Undetermined, never evidence of no effects. Preserve independently proven reject success-event/hand contradiction even when the other call is incomplete. Wrong binding/response identity stops correlation and new actions with honest harness/protocol/evidence errors. Harness changed-payload mistakes are not product idempotency bugs. Only correctly bound identical payload with changed formal result/new effects proves rule failure.

Ordering remains formal competition -> authority OWN/REJECT -> pickup client convergence -> original winner exact replay -> formal replay/current-binding checks -> authority retry cut -> IDEMP. In-flight requests may await separate formal results in original budget; queue/Arm/ACK/Duplicate text alone proves nothing. Preserve actual timeout/cancel/disconnect/unknown facts, no automatic resend or budget reset. No new long in-flight stress test required.

Focused window: real cached-network replay positive; fold absent/replaced domain ID, wrong binding, unknown/no-result, changed payload/result, repeated events/hand changes into a small parameterized set. Include synthetic BusinessId-versus-DomainCommandId event-attribution regression for REJECT and IDEMP; preserve short existing offline replay regression. Synthetic controls are not real product bugs/in-flight stress. No broad matrix, public fault hooks, rule/category/version change or file beyond approved14.
