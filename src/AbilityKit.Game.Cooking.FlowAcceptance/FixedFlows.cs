namespace AbilityKit.Game.Cooking.FlowAcceptance;

// Registered code, with no request-controlled script or mutable domain state.
public static class FixedFlows
{
    public static FlowRequest Request(string flowId, string outputRoot) => new(1, "request-" + Guid.NewGuid().ToString("N"),
        flowId, 1, FlowMode.Offline, FlowFixture.Spec,
        new[] { "C13-OWN", "C13-REJECT", "C13-IDEMP", "C13-COMPLETE" }.Select(id => new RuleRef(id, "1.0")).ToArray(),
        null, new(10000, 10000, 10000, 120000, 10000, 2000), new(256, 65536, 4096, 8388608, 262144), outputRoot);

    internal static async Task ExecuteAsync(FlowRequest request, IFlowSession session, FlowRunContext context,
        Func<int> stepBudget, CancellationToken token)
    {
        var initial = context.Cuts["initial"];
        if (!initial.Available || initial.Origin != ObservationOrigin.Authority || initial.Hands.Count != 2 ||
            initial.Hands.Any(h => h.ItemId is not null || h.Evidence != HandEvidence.DomainHandIndex) || initial.Items.Count != 1 ||
            initial.Items[0] is not { Removed: false, Version: 1, LocationKind: "StationSlot" } ||
            initial.Items[0].ItemId != request.Fixture.ItemId || initial.Items[0].SlotId != request.Fixture.InitialSlot)
            throw new FlowExecutionException("FixturePrecondition", "initial");
        var version = initial.Items[0].Version;
        if (request.FlowId == "compete-one-item")
        {
            var actions = request.Fixture.Actors.Select(a => new FlowAction("call-" + a, "pickup-" + a, a,
                FlowVerb.Pickup, request.Fixture.ItemId, version, null)).ToArray();
            var pair = await session.DispatchGroupAsync("compete", actions, stepBudget(), token);
            context.Add(pair); context.CheckAfterAwait(token);
            context.Cuts["pickup"] = await session.CaptureAsync("authority", token);
            context.CheckAfterAwait(token);
            context.CheckProductRules("C13-OWN", "C13-REJECT");
            Require(pair.Complete && pair.Outcomes.Count == 2 && pair.Outcomes.All(IsBusinessTerminal), pair.ErrorCode ?? "TwoBusinessTerminals", "compete");
            var winners = pair.Outcomes.Where(o => o.BusinessResult!.Outcome == CookingRecipeOutcome.Accepted).ToArray();
            Require(winners.Length == 1 && pair.Outcomes.Count(o => o.BusinessResult!.Outcome == CookingRecipeOutcome.Rejected) == 1,
                "ExactlyOneWinner", "compete");
            // Product checks finish at pickup before another action can mask that cut.
            context.ThrowOnProductFailure();
            var replay = await session.ReplayAsync("retry-winner", winners[0].CallId, "call-retry", stepBudget(), token);
            context.Add(replay); context.CheckAfterAwait(token);
            context.Cuts["retry"] = await session.CaptureAsync("authority", token);
            context.CheckAfterAwait(token);
            context.CheckProductRules("C13-IDEMP");
            Require(replay.Complete && replay.Outcomes.Count == 1 && IsBusinessTerminal(replay.Outcomes[0]), replay.ErrorCode ?? "ReplayTerminal", "retry-winner");
            context.ThrowOnProductFailure();
            context.CompletedGoals.AddRange(new[] { "competing-pickup", "exact-replay" });
        }
        else
        {
            var pickup = await session.DispatchGroupAsync("pickup", new[] { new FlowAction("call-pickup", "pickup-A", "A",
                FlowVerb.Pickup, request.Fixture.ItemId, version, null) }, stepBudget(), token);
            context.Add(pickup); context.CheckAfterAwait(token);
            context.Cuts["pickup"] = await session.CaptureAsync("authority", token);
            context.CheckAfterAwait(token);
            context.CheckProductRules("C13-OWN");
            Require(pickup.Complete && pickup.Outcomes.Count == 1 && IsBusinessTerminal(pickup.Outcomes[0]) &&
                pickup.Outcomes[0].BusinessResult!.Outcome == CookingRecipeOutcome.Accepted, "PickupAccepted", "pickup");
            context.ThrowOnProductFailure();
            var picked = context.Cuts["pickup"].Items.Single(i => i.ItemId == request.Fixture.ItemId);
            var drop = await session.DispatchGroupAsync("drop", new[] { new FlowAction("call-drop", "drop-A", "A",
                FlowVerb.Drop, request.Fixture.ItemId, picked.Version, request.Fixture.DropSlot) }, stepBudget(), token);
            context.Add(drop); context.CheckAfterAwait(token);
            context.Cuts["drop"] = await session.CaptureAsync("authority", token);
            context.CheckAfterAwait(token);
            context.CheckProductRules("C13-OWN", "C13-REJECT", "C13-IDEMP");
            Require(drop.Complete && drop.Outcomes.Count == 1 && IsBusinessTerminal(drop.Outcomes[0]) &&
                drop.Outcomes[0].BusinessResult!.Outcome == CookingRecipeOutcome.Accepted, "DropAccepted", "drop");
            context.ThrowOnProductFailure();
            context.CompletedGoals.Add("pickup-drop");
        }
    }

    private static bool IsBusinessTerminal(CommandObservation observation) => observation.Completion == CallCompletion.Terminal &&
        observation.BusinessResult is not null && observation.NativeDisposition is "Executed" or "Duplicate";
    private static void Require(bool condition, string code, string step)
    { if (!condition) throw new FlowExecutionException(code, step); }
}

internal sealed class FlowExecutionException(string code, string step) : Exception(code)
{
    public string Code { get; } = code;
    public string Step { get; } = step;
}

internal sealed class FlowRunContext(RunIdentity run, IReadOnlyList<ApprovedRule> approvals, Action<RuleCheck> record)
{
    public List<CommandObservation> Commands { get; } = new();
    public Dictionary<string, FlowObservation> Cuts { get; } = new(StringComparer.Ordinal);
    public List<string> CompletedGoals { get; } = new();
    private readonly List<RuleCheck> productChecks = new();
    public FlowEvidence Evidence(bool complete = true) => new(Commands.ToArray(), new Dictionary<string, FlowObservation>(Cuts), complete);
    public void Add(DispatchResult result) => Commands.AddRange(result.Outcomes);

    public void CheckAfterAwait(CancellationToken token)
    {
        // Preserve returned facts before checking cancellation; cancellation is never undo.
        token.ThrowIfCancellationRequested();
        if (Cuts.Values.Any(c => c.Fence.RunGeneration != run.RunGeneration || c.Fence.Scope != Cuts["initial"].Fence.Scope))
            throw new FlowExecutionException("BindingFenceChanged", "capture");
    }

    public void CheckProductRules(params string[] ids)
    {
        foreach (var id in ids)
        {
            var approval = approvals.Single(a => a.Rule.Id == id);
            var check = new FlowRuleEvaluator(approval.Rule).Evaluate(Evidence(), approval);
            productChecks.Add(check); record(check);
        }
    }

    public void ThrowOnProductFailure()
    {
        if (productChecks.Any(c => c.Verdict != FlowVerdict.Passed)) throw new FlowExecutionException("ProductRuleNotPassed", "rules");
    }
}
