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
        if (document.SchemaVersion != 1 || document.Rules.Count != 5 || document.Rules.Select(r => r.Rule.Id).Distinct().Count() != 5 ||
            mode != FlowMode.Offline || requested.Count != OfflineRules.Length ||
            !requested.Select(r => r.Id).Order().SequenceEqual(OfflineRules.Order()))
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

public sealed class FlowRuleEvaluator(RuleRef identity) : IFlowRule
{
    public RuleRef Identity { get; } = identity;

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
        var actors = initial.Hands.Select(h => h.ActorId).ToArray();
        if (actors.Length != 2 || actors.Distinct().Count() != 2 || initial.Items.Count != 1)
            return Check(FlowVerdict.Failed, "Two distinct actors and one item", "Invalid initial fixture");
        var itemId = initial.Items[0].ItemId;
        var compete = evidence.Commands.Any(c => c.StepId == "compete");
        switch (Identity.Id)
        {
            case "C13-OWN":
                if (!evidence.Cuts.ContainsKey("pickup")) return Check(FlowVerdict.Undetermined, "Pickup authority cut", "Missing pickup");
                foreach (var cut in evidence.Cuts.Values)
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
                        loser.BusinessResult.Events.Count != 0 || evidence.Cuts.Values.Any(c => c.CommandEvents.Any(e =>
                            e.Command.Value == loser.BusinessId && e.Player.Value == loser.ActorId)) ||
                        evidence.Cuts.TryGetValue("pickup", out var rejectedCut) &&
                        rejectedCut.Hands.Single(h => h.ActorId == loser.ActorId).ItemId is not null)
                        return Check(FlowVerdict.Failed, "Rejected command has no own success effects", "Rejected action has effects or unexpected reason");
                }
                var competingCalls = evidence.Commands.Where(c => c.StepId == "compete").ToArray();
                if (losers.Any(c => c.Completion != CallCompletion.Terminal || c.NativeDisposition is not ("Executed" or "Duplicate")) ||
                    compete && (competingCalls.Length != 2 || competingCalls.Any(c => c.Completion != CallCompletion.Terminal ||
                        c.BusinessResult is null || c.NativeDisposition is not ("Executed" or "Duplicate"))))
                    return Check(FlowVerdict.Undetermined, "Formal domain rejection and competing business terminals", "Business terminal unavailable; admission/protocol is not rejection");
                if (compete && losers.Length != 1) return Check(FlowVerdict.Failed, "One actual business rejection", "Rejected business result count differs");
                return Check(FlowVerdict.Passed, "Reject only its own side effects", losers.Length == 0 ? "N/A: no rejection in pickup/drop flow" : "Other legal actor changes allowed");

            case "C13-IDEMP":
                if (!compete) return Check(FlowVerdict.Passed, "Exact replay when flow declares replay", "N/A: pickup/drop uses new business IDs");
                if (!evidence.Cuts.TryGetValue("retry", out var after) || !after.Available ||
                    !evidence.Cuts.TryGetValue("pickup", out var before)) return Check(FlowVerdict.Undetermined, "Pickup and retry cuts", "Missing replay evidence");
                var retry = evidence.Commands.SingleOrDefault(c => c.StepId == "retry-winner");
                var original = retry is null ? null : evidence.Commands.FirstOrDefault(c => c.CallId != retry.CallId && c.BusinessId == retry.BusinessId && c.ActorId == retry.ActorId);
                if (retry?.BusinessResult is null || original?.BusinessResult is null) return Check(FlowVerdict.Undetermined, "Original and retry business terminals", "Terminal missing");
                var originalEvents = before.CommandEvents.Where(e => e.Command.Value == original.BusinessId && e.Player.Value == original.ActorId).ToArray();
                var retryEvents = after.CommandEvents.Where(e => e.Command.Value == original.BusinessId && e.Player.Value == original.ActorId).ToArray();
                if (retry.FrozenCommand != original.FrozenCommand || retry.CallId == original.CallId || !retry.BusinessResult.IsDuplicate ||
                    retry.NativeDisposition != "Duplicate" || retry.BusinessResult.Events.Count != 0 || originalEvents.Length != 1 ||
                    !originalEvents.SequenceEqual(retryEvents) || retry.BusinessResult.Outcome != original.BusinessResult.Outcome ||
                    retry.BusinessResult.Reason != original.BusinessResult.Reason || retry.BusinessResult.StateVersion != original.BusinessResult.StateVersion ||
                    !before.Items.SequenceEqual(after.Items) || !before.Hands.SequenceEqual(after.Hands) || after.Fence != before.Fence)
                    return Check(FlowVerdict.Failed, "Frozen payload replay has no new command effects/events", "Replay changed command, item, hand or business events");
                return Check(FlowVerdict.Passed, "Command effects stay identical", "Global tick/version changes are allowed");

            case "C13-COMPLETE":
                var keys = compete ? new[] { "initial", "pickup", "retry" } : new[] { "initial", "pickup", "drop" };
                if (keys.Any(k => !evidence.Cuts.TryGetValue(k, out var c) || !c.Available) || evidence.Commands.Count != (compete ? 3 : 2) ||
                    evidence.Commands.Any(c => c.Completion != CallCompletion.Terminal || c.BusinessResult is null || c.ErrorCode is not null) ||
                    evidence.Commands.Select(c => c.CallId).Distinct().Count() != evidence.Commands.Count)
                    return Check(FlowVerdict.Undetermined, "All goal cuts, invocation terminals and events", "Execution incomplete");
                return Check(FlowVerdict.Passed, "All declared execution facts; cleanup assessed separately", "Execution facts complete");
            default:
                return Check(FlowVerdict.Undetermined, "Supported S1 category", "CONVERGE N/A in offline S1");
        }
    }
}
