using AbilityKit.Game.Cooking.Session;

namespace AbilityKit.Game.Cooking.NetworkImpairmentMeasurement;

// Cached identity metadata only: no capture, hash, serialization or owner-frame work.
internal sealed record LoadWaitView(bool Ready, CookingNetworkBaselineIdentity? LatestIdentity,
    long? LatestStateVersion, CookingNetworkBaselineIdentity? ExactReadyIdentity, string ReadyIdentitySource);

internal sealed class LoadWaitEvidence
{
    internal const int SlotBound = 350;
    public int Player { get; }
    public long Frequency { get; } = System.Diagnostics.Stopwatch.Frequency;
    public int AcceptedResponses { get; set; }
    public int CompletedProjections { get; set; }
    public DiagnosticError? Failure { get; set; }
    public List<LoadWaitSlot> Slots { get; } = new(SlotBound);
    public LoadWaitEvidence(int player) => Player = player;
    public static double ResponseElapsed(long started, long responseAt) =>
        (responseAt - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    public static void RecordSkipped(LoadWaitSlot slot, bool scheduler) =>
        slot.Outcome = scheduler ? "scheduler-skipped" : "backpressure-skipped";
    public void RecordResponse(LoadWaitSlot slot, CookingNetworkWireResult result)
    {
        slot.TerminalResult = result;
        slot.DomainId = result.DomainCommandId?.Value;
        slot.TargetStateVersion = result.Result?.StateVersion;
        slot.AcceptedResponse = result.Result?.Outcome == CookingRecipeOutcome.Accepted;
        slot.Duplicate = result.Result?.IsDuplicate;
        if (slot.AcceptedResponse) AcceptedResponses++;
    }
    public void RecordProjected(LoadWaitSlot slot)
    {
        CompletedProjections++;
        slot.Outcome = "projected-complete";
    }
    public void RecordFailure(LoadWaitSlot slot, Exception error, string boundary, string outcome)
    {
        slot.Error = DiagnosticError.From(error);
        slot.FailureBoundary = boundary;
        slot.Outcome = outcome;
        slot.FinishedAt ??= System.Diagnostics.Stopwatch.GetTimestamp();
        Failure = slot.Error;
    }
    public LoadWaitSlot Add(int index, long offeredAt)
    {
        if (Slots.Count >= SlotBound) throw new InvalidOperationException("Diagnostic offered-slot bound.");
        var slot = new LoadWaitSlot { OfferedIndex = index, Cohort = index < 50 ? "warmup" : "sample", OfferedAt = offeredAt };
        Slots.Add(slot);
        return slot;
    }
}

internal sealed class LoadWaitSlot
{
    public int OfferedIndex { get; init; }
    public string Cohort { get; init; } = "";
    public string Outcome { get; set; } = "offered";
    public string? StableId { get; set; }
    public string? DomainId { get; set; }
    public string CorrelationEvidence { get; } = "NOT_EXPOSED_BY_EXISTING_SEND_API";
    public long OfferedAt { get; init; }
    public long? SendStartedAt { get; set; }
    public long? ResponseAt { get; set; }
    public long? TargetStateVersion { get; set; }
    public CookingNetworkWireResult? TerminalResult { get; set; }
    public bool AcceptedResponse { get; set; }
    public bool? Duplicate { get; set; }
    public long? WaitStartedAt { get; set; }
    public long? DeadlineAt { get; set; }
    public string WaitClockDefinition { get; } = "WaitStartedAt follows original Stopwatch.StartNew; DeadlineAt is nominal, not the internal timer start.";
    public string? FailureBoundary { get; set; }
    public long? FinishedAt { get; set; }
    public double? WaitElapsedMilliseconds { get; set; }
    public long? DecisionObservedAt { get; set; }
    public long? PredicateObservedAt { get; set; }
    public bool? FinalVersionPredicate { get; set; }
    public bool RecordDecision(TimeSpan elapsed, long observedAt)
    {
        // The original healthy decision depends only on this frozen elapsed value.
        DecisionObservedAt = observedAt;
        WaitElapsedMilliseconds = elapsed.TotalMilliseconds;
        DeadlineExpired = elapsed.TotalSeconds >= 30;
        return !DeadlineExpired.Value;
    }
    public bool? DeadlineExpired { get; set; }
    public LoadWaitView? BeforeSend { get; set; }
    public LoadWaitView? AtResponse { get; set; }
    public LoadWaitView? AfterWait { get; set; }
    public DiagnosticError? Error { get; set; }
}

internal sealed record DiagnosticError(string Text, int OriginalLength, bool Truncated)
{
    internal static DiagnosticError From(Exception error)
    {
        return FromText(error.ToString());
    }
    internal static DiagnosticError FromText(string original)
    {
        return new(original.Length > 4096 ? original[..4096] : original, original.Length, original.Length > 4096);
    }
}

// Synthetic pure diagnostic controls: no authority, UDP or gameplay acceptance.
internal static class LoadWaitControls
{
    public static int Run(string report)
    {
        var checks = new List<string>();
        try
        {
            void Check(bool valid, string name)
            {
                if (!valid) throw new InvalidOperationException(name);
                checks.Add(name);
            }
            var evidence = new LoadWaitEvidence(1);
            var first = evidence.Add(0, 1);
            var accepted = new CookingNetworkWireResult("synthetic-first", new RecipeCommandId("synthetic-domain"), "synthetic",
                new CookingRecipeCommandResult(CookingRecipeOutcome.Accepted, CookingRecipeRejectionReason.None, 7, false, Array.Empty<CookingRecipeEvent>()), null);
            evidence.RecordResponse(first, accepted);
            evidence.RecordProjected(first);
            Check(evidence.AcceptedResponses == 1 && evidence.CompletedProjections == 1 && ReferenceEquals(first.TerminalResult, accepted), "actual-terminal-retained-independent-counters");
            foreach (var seconds in new[] { 29.999, 30.0, 30.001 })
            foreach (var predicate in new[] { false, true })
            {
                var slot = new LoadWaitSlot();
                var eligible = slot.RecordDecision(TimeSpan.FromSeconds(seconds), 10);
                slot.FinalVersionPredicate = predicate;
                slot.PredicateObservedAt = 20; // Deliberately later observation cannot alter frozen decision.
                Check(eligible == (seconds < 30) && slot.DeadlineExpired == (seconds >= 30) && slot.DecisionObservedAt == 10,
                    $"elapsed-only-{seconds}-{predicate}");
            }
            var failing = evidence.Add(1, 2);
            evidence.RecordResponse(failing, accepted with { StableCommandId = "synthetic-failing" });
            evidence.RecordFailure(failing, new InvalidOperationException("projection deadline"), "complete", "failed");
            Check(evidence.AcceptedResponses == 2 && evidence.CompletedProjections == 1 && failing.TerminalResult != null, "accepted-unprojected-survives");
            var unsent = evidence.Add(2, 3);
            evidence.RecordFailure(unsent, new InvalidOperationException("not Ready"), "readiness", "offered-slot-failed-outside-complete");
            Check(unsent.TerminalResult == null && unsent.SendStartedAt == null && unsent.FailureBoundary == "readiness", "unsent-readiness-not-fabricated");
            var sendFailure = evidence.Add(3, 4);
            sendFailure.SendStartedAt = 4;
            evidence.RecordFailure(sendFailure, new InvalidOperationException("send failed"), "complete", "failed");
            Check(sendFailure.TerminalResult == null && !sendFailure.AcceptedResponse && evidence.AcceptedResponses == 2, "send-failure-no-result-or-acceptance");
            var rejection = evidence.Add(4, 5);
            evidence.RecordResponse(rejection, accepted with { Result = CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.PlayerUnavailable, 7) });
            Check(rejection.TerminalResult != null && !rejection.AcceptedResponse && evidence.AcceptedResponses == 2, "actual-rejection-retained");
            var partial = System.Text.Json.JsonSerializer.Serialize(new { evidence = (object?)null, loadWaitEvidence = evidence, laterFailure = "synthetic recovery/export failure" });
            using (var doc = System.Text.Json.JsonDocument.Parse(partial))
            {
                var slots = doc.RootElement.GetProperty("loadWaitEvidence").GetProperty("Slots");
                Check(slots[0].GetProperty("TerminalResult").GetProperty("StableCommandId").GetString() == "synthetic-first" &&
                    slots[1].GetProperty("TerminalResult").GetProperty("StableCommandId").GetString() == "synthetic-failing", "partial-serialization-retains-earlier-and-failing-terminals");
            }
            Check(LoadWaitEvidence.ResponseElapsed(0, System.Diagnostics.Stopwatch.Frequency) == 1000, "response-timestamp-rtt-boundary");
            var skipped = new LoadWaitSlot();
            LoadWaitEvidence.RecordSkipped(skipped, true);
            Check(skipped.Outcome == "scheduler-skipped" && skipped.TerminalResult == null, "scheduler-skip-not-issued");
            LoadWaitEvidence.RecordSkipped(skipped, false);
            Check(skipped.Outcome == "backpressure-skipped" && skipped.TerminalResult == null, "backpressure-skip-not-issued");
            var prior = new LoadWaitSlot();
            evidence.RecordFailure(prior, new InvalidOperationException("prior terminal"), "prior-flight-terminal", "offered-slot-failed-outside-complete");
            Check(prior.FailureBoundary == "prior-flight-terminal" && prior.TerminalResult == null, "prior-flight-boundary-unsent");
            var bounded = new LoadWaitEvidence(0);
            for (var i = 0; i < LoadWaitEvidence.SlotBound; i++) bounded.Add(i, i);
            bool refused = false;
            try { bounded.Add(350, 350); } catch (InvalidOperationException) { refused = true; }
            Check(refused && bounded.Slots.Count == 350 && bounded.Slots[49].Cohort == "warmup" && bounded.Slots[50].Cohort == "sample", "350-plus1-refused-cohorts");
            var shortError = DiagnosticError.FromText(new string('x', 4096));
            var longError = DiagnosticError.FromText(new string('x', 4097));
            Check(shortError.Text.Length <= 4096 && !shortError.Truncated && longError.Text.Length == 4096 && longError.Truncated && longError.OriginalLength > 4096, "diagnostic-error-clipping");
            Save(true, null);
            return 0;
        }
        catch (Exception error) { Save(false, error.ToString()); return 1; }
        void Save(bool passed, string? failure)
        {
            var path = Path.GetFullPath(report);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { passed, failure, checks,
                scope = "SYNTHETIC_DIAGNOSTIC_HELPERS_ONLY_NO_GAMEPLAY_OR_NETWORK_PROOF" }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
