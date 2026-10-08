namespace AbilityKit.Game.Cooking.FlowAcceptance;

public sealed record FlowRuleCatalogDocument(int SchemaVersion, IReadOnlyList<ApprovedRule> Rules);

public sealed class FlowRuleCatalog : IFlowRuleCatalog
{
    public const string OwnerApproval = ".trellis/tasks/10-07-cooking-issue13-flow-orchestrator/research/owner-approval-20261007.md@6ad948d0c72255873b4fbd1cd6b5fce7594e3d13";
    private static readonly string[] OfflineRules = { "C13-OWN", "C13-REJECT", "C13-IDEMP", "C13-COMPLETE" };
    private readonly FlowRuleCatalogDocument document;
    public FlowRuleCatalog(string? path = null) => document = FlowJson.Read<FlowRuleCatalogDocument>(
        File.ReadAllText(path ?? Path.Combine(AppContext.BaseDirectory, "flow-rules.json")));

    public IReadOnlyList<ApprovedRule> ResolveApproved(IReadOnlyList<RuleRef> requested, FlowMode mode)
    {
        var required = mode == FlowMode.Network ? OfflineRules.Append("C13-CONVERGE").ToArray() : OfflineRules;
        if (document.SchemaVersion != 1 || document.Rules.Count != 5 || document.Rules.Select(r => r.Rule.Id).Distinct().Count() != 5 ||
            !Enum.IsDefined(mode) || requested.Count != required.Length ||
            !requested.Select(r => r.Id).Order().SequenceEqual(required.Order()))
            throw new InvalidDataException("Unsupported catalog or required rule set.");
        var resolved = requested.Select(r => document.Rules.SingleOrDefault(a => a.Rule == r)
            ?? throw new InvalidDataException("Unapproved rule version.")).ToArray();
        if (document.Rules.Any(a => a.Rule.Version != "1.0" || a.ApprovalRef != OwnerApproval ||
            !OfflineRules.Append("C13-CONVERGE").Contains(a.Rule.Id) || a.Parameters.Count != 1 ||
            a.Parameters.GetValueOrDefault("fixture") != "one-item/1.0"))
            throw new InvalidDataException("Unapproved catalog category, parameters or approval reference.");
        return Array.AsReadOnly(resolved);
    }
}

public sealed class FlowRuleEvaluator : IFlowRule
{
    public RuleRef Identity { get; }
    private readonly FlowMode mode;
    public FlowRuleEvaluator(RuleRef identity) : this(identity, FlowMode.Offline) { }
    internal FlowRuleEvaluator(RuleRef identity, FlowMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new InvalidDataException("Unknown evaluator mode");
        Identity = identity; this.mode = mode;
    }

    public RuleCheck Evaluate(FlowEvidence evidence, ApprovedRule approval)
    {
        var refs = evidence.Cuts.Keys.Select(k => "cut:" + k).Concat(evidence.Commands.Select(c => "call:" + c.CallId)).ToArray();
        RuleCheck Check(FlowVerdict verdict, string expected, string actual) => new(Identity, verdict,
            Identity.Id == "C13-IDEMP" ? "retry-winner" : Identity.Id == "C13-COMPLETE" ? "completion" : "pickup",
            expected, actual, refs);
        if (approval.Rule != Identity || approval.ApprovalRef != FlowRuleCatalog.OwnerApproval || Identity.Version != "1.0")
            return Check(FlowVerdict.Undetermined, "Approved category version", "Approval mismatch");
        if (!evidence.RequiredEventsComplete) return Check(FlowVerdict.Undetermined, "Complete required facts", "Evidence incomplete");
        if (!evidence.Cuts.TryGetValue("initial", out var initial) || !initial.Available)
            return Check(FlowVerdict.Undetermined, "Available initial authority cut", "Initial cut missing");
        if (mode == FlowMode.Network && string.IsNullOrWhiteSpace(initial.Fence.ServerSessionInstance))
            return Check(FlowVerdict.Undetermined, "Actual authority session identity", "Server instance missing");
        var actors = initial.Hands.Select(h => h.ActorId).ToArray();
        if (actors.Length != 2 || actors.Distinct().Count() != 2 || initial.Items.Count != 1)
            return Check(FlowVerdict.Failed, "Two distinct actors and one item", "Invalid initial fixture");
        var itemId = initial.Items[0].ItemId;
        var compete = evidence.Commands.Any(c => c.StepId == "compete");
        switch (Identity.Id)
        {
            case "C13-OWN":
                if (!evidence.Cuts.ContainsKey("pickup")) return Check(FlowVerdict.Undetermined, "Pickup authority cut", "Missing pickup");
                foreach (var cut in evidence.Cuts.Values.Where(c => c.Origin == ObservationOrigin.Authority))
                {
                    if (!cut.Available) return Check(FlowVerdict.Undetermined, "Available authority cut", cut.ErrorCode ?? "Unavailable");
                    if (cut.Origin != ObservationOrigin.Authority || cut.Fence != initial.Fence || cut.Hands.Count != 2 ||
                        !cut.Hands.Select(h => h.ActorId).Order().SequenceEqual(actors.Order()) ||
                        cut.Hands.Any(h => h.Evidence != HandEvidence.DomainHandIndex) || cut.Items.Count != 1 ||
                        cut.Items[0].ItemId != itemId || cut.Items[0].Removed)
                        return Check(FlowVerdict.Failed, "Independent scoped item and hand facts", "Missing, removed or inconsistent facts");
                    var item = cut.Items[0];
                    var holders = cut.Hands.Where(h => h.ItemId == itemId).ToArray();
                    if (cut.Hands.Any(h => h.ItemId is not null && h.ItemId != itemId) || holders.Length > 1 ||
                        (item.LocationKind == "PlayerHand" ? holders.Length != 1 || holders[0].ActorId != item.OwnerId || item.SlotId is not null
                            : item.LocationKind != "StationSlot" || holders.Length != 0 || item.OwnerId is not null || string.IsNullOrWhiteSpace(item.SlotId)))
                        return Check(FlowVerdict.Failed, "Unique item location matches independent hand index", "Item/hand mismatch");
                }
                var pickups = evidence.Commands.Where(c => c.StepId is "compete" or "pickup").ToArray();
                if (pickups.Length != (compete ? 2 : 1) || pickups.Any(c => c.Completion != CallCompletion.Terminal || c.BusinessResult is null))
                    return Check(FlowVerdict.Undetermined, "Formal pickup business results", "Business terminal missing");
                var winners = pickups.Where(c => c.BusinessResult!.Outcome == CookingRecipeOutcome.Accepted).ToArray();
                var picked = evidence.Cuts["pickup"].Items[0];
                if (initial.Hands.Any(h => h.ItemId is not null) || initial.Items[0].Version != 1 ||
                    initial.Items[0].LocationKind != "StationSlot" || winners.Length != 1 || picked.Version != 2 ||
                    picked.LocationKind != "PlayerHand" || picked.OwnerId != winners[0].ActorId)
                    return Check(FlowVerdict.Failed, "Exactly one legal winner and versioned pickup", "Pickup target not reached");
                if (evidence.Cuts.TryGetValue("drop", out var dropped))
                {
                    var drop = evidence.Commands.SingleOrDefault(c => c.StepId == "drop");
                    if (drop?.BusinessResult?.Outcome != CookingRecipeOutcome.Accepted || drop.FrozenCommand.ExpectedItemVersion != picked.Version ||
                        drop.BusinessId == winners[0].BusinessId || dropped.Items[0].Version != 3 || dropped.Items[0].LocationKind != "StationSlot" ||
                        dropped.Items[0].SlotId != drop.FrozenCommand.Station?.Value || dropped.Hands.Any(h => h.ItemId is not null))
                        return Check(FlowVerdict.Failed, "New-ID latest-version drop to configured slot", "Drop target not reached");
                }
                return Check(FlowVerdict.Passed, "Unique versioned ownership at each committed cut", "Independent item and hands agree");

            case "C13-REJECT":
                var losers = evidence.Commands.Where(c => c.BusinessResult?.Outcome == CookingRecipeOutcome.Rejected).ToArray();
                foreach (var loser in losers)
                {
                    if (loser.Completion != CallCompletion.Terminal || loser.NativeDisposition is not ("Executed" or "Duplicate"))
                        continue;
                    if (loser.BusinessResult!.Reason is not (CookingRecipeRejectionReason.ItemStale or CookingRecipeRejectionReason.TargetOutOfRange) ||
                        loser.BusinessResult.Events.Count != 0 || evidence.Cuts.Values.Where(c => c.Origin == ObservationOrigin.Authority &&
                            c.Fence == initial.Fence).Any(c => c.CommandEvents.Any(e =>
                            loser.DomainCommandId is not null && e.Command.Value == loser.DomainCommandId && e.Player.Value == loser.ActorId)) ||
                        evidence.Cuts.TryGetValue("pickup", out var rejectedCut) &&
                        rejectedCut.Fence == initial.Fence &&
                        rejectedCut.Hands.Single(h => h.ActorId == loser.ActorId).ItemId is not null)
                        return Check(FlowVerdict.Failed, "Rejected command has no own success effects", "Rejected action has effects or unexpected reason");
                }
                var competingCalls = evidence.Commands.Where(c => c.StepId == "compete").ToArray();
                if (losers.Any(c => c.Completion != CallCompletion.Terminal || c.NativeDisposition is not ("Executed" or "Duplicate") ||
                        string.IsNullOrWhiteSpace(c.DomainCommandId) || c.ErrorCode is not null) ||
                    compete && (competingCalls.Length != 2 || competingCalls.Any(c => c.Completion != CallCompletion.Terminal ||
                        c.BusinessResult is null || c.NativeDisposition is not ("Executed" or "Duplicate") || string.IsNullOrWhiteSpace(c.DomainCommandId) || c.ErrorCode is not null)))
                    return Check(FlowVerdict.Undetermined, "Formal domain rejection and competing business terminals", "Business terminal unavailable; admission/protocol is not rejection");
                if (losers.Any(c => c.DomainCommandId != (mode == FlowMode.Network ?
                    AbilityKit.Game.Cooking.Session.CookingNetworkWireCodec.DomainId(initial.Fence.ServerSessionInstance!, initial.Fence.Scope,
                        new(c.ActorId), c.BusinessId).Value : c.FrozenCommand.Command.Value)))
                    return Check(FlowVerdict.Undetermined, "Actual current-scope response identity", "Response identity mismatch");
                if (compete && losers.Length != 1) return Check(FlowVerdict.Failed, "One actual business rejection", "Rejected business result count differs");
                return Check(FlowVerdict.Passed, "Reject only its own side effects", losers.Length == 0 ? "N/A: no rejection in pickup/drop flow" : "Other legal actor changes allowed");

            case "C13-IDEMP":
                if (!compete) return Check(FlowVerdict.Passed, "Exact replay when flow declares replay", "N/A: pickup/drop uses new business IDs");
                if (!evidence.Cuts.TryGetValue("retry", out var after) || !after.Available ||
                    !evidence.Cuts.TryGetValue("pickup", out var before)) return Check(FlowVerdict.Undetermined, "Pickup and retry cuts", "Missing replay evidence");
                var retry = evidence.Commands.SingleOrDefault(c => c.StepId == "retry-winner");
                var original = retry is null ? null : evidence.Commands.FirstOrDefault(c => c.CallId != retry.CallId && c.BusinessId == retry.BusinessId && c.ActorId == retry.ActorId);
                if (retry?.BusinessResult is null || original?.BusinessResult is null || retry.Completion != CallCompletion.Terminal ||
                    original.Completion != CallCompletion.Terminal || retry.ErrorCode is not null || original.ErrorCode is not null ||
                    string.IsNullOrWhiteSpace(original.DomainCommandId) || string.IsNullOrWhiteSpace(retry.DomainCommandId))
                    return Check(FlowVerdict.Undetermined, "Original and retry formal business terminals and actual domain identities", "Terminal or identity missing");
                if (before.Origin != ObservationOrigin.Authority || after.Origin != ObservationOrigin.Authority ||
                    before.Fence != initial.Fence || after.Fence != initial.Fence || retry.DomainCommandId != original.DomainCommandId)
                    return Check(FlowVerdict.Undetermined, "Scoped authority cuts and same actual response identity", "Binding or identity mismatch");
                if (mode == FlowMode.Network)
                {
                    var role = original.ActorId == "A" ? "client-a" : "client-b";
                    var observations = new[] { "call." + original.CallId + ".before", "call." + original.CallId + ".after",
                        "call." + retry.CallId + ".before", "call." + retry.CallId + ".after" };
                    if (!evidence.Cuts.TryGetValue("initial." + role, out var first) || observations.Any(key =>
                        !evidence.Cuts.TryGetValue(key, out var current) || !current.Available || current.Origin != ObservationOrigin.Client ||
                        current.ObserverId != role || current.Fence != first.Fence || current.Fence.Scope != initial.Fence.Scope ||
                        current.Fence.RunGeneration != initial.Fence.RunGeneration || current.Fence.ServerSessionInstance != initial.Fence.ServerSessionInstance ||
                        current.Fence.ConnectionGeneration is not > 0 || current.BaselineSequence is not > 0))
                        return Check(FlowVerdict.Undetermined, "Actual original/replay before-and-after current client binding", "Binding evidence unavailable or changed");
                    if (retry.FrozenCommand != original.FrozenCommand || retry.CallId == original.CallId ||
                        retry.BusinessId != original.BusinessId || retry.FrozenCommand.Command.Value != retry.BusinessId ||
                        retry.FrozenCommand.SimulationBatch != 0 || original.NativeDisposition != "Executed" ||
                        retry.NativeDisposition is not ("Executed" or "Duplicate") || original.BusinessResult.Outcome != CookingRecipeOutcome.Accepted ||
                        original.DomainCommandId != AbilityKit.Game.Cooking.Session.CookingNetworkWireCodec.DomainId(
                            initial.Fence.ServerSessionInstance!, initial.Fence.Scope, new(original.ActorId), original.BusinessId).Value)
                        return Check(FlowVerdict.Undetermined, "Correctly bound original Accepted winner and exact wire payload/formal status", "Harness payload or correlation precondition mismatch");
                }
                var originalEvents = before.CommandEvents.Where(e => e.Command.Value == original.DomainCommandId && e.Player.Value == original.ActorId).ToArray();
                var retryEvents = after.CommandEvents.Where(e => e.Command.Value == original.DomainCommandId && e.Player.Value == original.ActorId).ToArray();
                if (retry.FrozenCommand != original.FrozenCommand || retry.CallId == original.CallId || !retry.BusinessResult.IsDuplicate ||
                    (mode == FlowMode.Offline ? retry.NativeDisposition != "Duplicate" : retry.NativeDisposition == "Executed" && retry.WireReason != original.WireReason) ||
                    retry.BusinessResult.Events.Count != 0 || originalEvents.Length != 1 ||
                    !originalEvents.SequenceEqual(retryEvents) || retry.BusinessResult.Outcome != original.BusinessResult.Outcome ||
                    retry.BusinessResult.Reason != original.BusinessResult.Reason || retry.BusinessResult.StateVersion != original.BusinessResult.StateVersion ||
                    retry.BusinessResult.Supply is not null || original.BusinessResult.Supply is not null ||
                    !before.Items.SequenceEqual(after.Items) || !before.Hands.SequenceEqual(after.Hands) || after.Fence != before.Fence)
                    return Check(FlowVerdict.Failed, "Frozen payload replay has no new command effects/events", "Replay changed command, item, hand or business events");
                return Check(FlowVerdict.Passed, "Command effects stay identical", "Global tick/version changes are allowed");

            case "C13-COMPLETE":
                var keys = compete ? new[] { "initial", "pickup", "retry" } : new[] { "initial", "pickup", "drop" };
                if (keys.Any(k => !evidence.Cuts.TryGetValue(k, out var c) || !c.Available) || evidence.Commands.Count != (compete ? 3 : 2) ||
                    evidence.Commands.Any(c => c.Completion != CallCompletion.Terminal || c.BusinessResult is null || c.ErrorCode is not null || string.IsNullOrWhiteSpace(c.DomainCommandId)) ||
                    evidence.Commands.Select(c => c.CallId).Distinct().Count() != evidence.Commands.Count)
                    return Check(FlowVerdict.Undetermined, "All goal cuts, invocation terminals and events", "Execution incomplete");
                if (initial.Fence.ServerSessionInstance is not null && new[] { "client-a", "client-b" }.Any(id =>
                    !evidence.Cuts.ContainsKey("pickup." + id) || !compete && !evidence.Cuts.ContainsKey("drop." + id)))
                    return Check(FlowVerdict.Undetermined, "Network goal projections at each required cut", "Projection goal incomplete");
                return Check(FlowVerdict.Passed, "All declared execution facts; cleanup assessed separately", "Execution facts complete");
            case "C13-CONVERGE":
                if (mode != FlowMode.Network)
                    return Check(FlowVerdict.Undetermined, "Network installed projections", "N/A in offline mode");
                foreach (var point in new[] { "pickup", "drop" }.Where(evidence.Cuts.ContainsKey))
                {
                    var target = evidence.Cuts[point];
                    foreach (var id in new[] { "client-a", "client-b" })
                    {
                        if (!evidence.Cuts.TryGetValue("initial." + id, out var first) ||
                            !evidence.Cuts.TryGetValue(point + "." + id, out var projection) || !projection.Available ||
                            projection.Fence != first.Fence || projection.Fence.RunGeneration != target.Fence.RunGeneration ||
                            projection.Fence.Scope != target.Fence.Scope || projection.Fence.ServerSessionInstance != target.Fence.ServerSessionInstance ||
                            projection.Fence.ConnectionGeneration is not > 0 || projection.BaselineSequence is not > 0 ||
                            projection.BaselineSequence < first.BaselineSequence || projection.StateVersion < target.StateVersion)
                            return Check(FlowVerdict.Undetermined, "Current binding and installed version reaches authority fence", "Missing, old or wrong-bound projection");
                        var fence = new ProjectionFence(target.Fence, new[] { "client-a", "client-b" }, target.StateVersion ?? long.MaxValue, target.Items, target.Hands);
                        if (!NetworkFlowAdapter.ProjectionMatches(projection, fence))
                            return Check(FlowVerdict.Failed, "Installed target item/version/location and both hands", "Target projection fields disagree");
                    }
                }
                if (!evidence.Cuts.ContainsKey("pickup")) return Check(FlowVerdict.Undetermined, "Pickup before replay/drop", "Pickup missing");
                return Check(FlowVerdict.Passed, "Installed target projections in both current clients", "Version fence and item/hand fields reached; no exact target ACK claim");
            default:
                return Check(FlowVerdict.Undetermined, "Supported S1 category", "CONVERGE N/A in offline S1");
        }
    }
}
