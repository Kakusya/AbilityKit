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
    public bool AcceptedResponse { get; set; }
    public bool? Duplicate { get; set; }
    public long? WaitStartedAt { get; set; }
    public long? DeadlineAt { get; set; }
    public string WaitClockDefinition { get; } = "WaitStartedAt follows original Stopwatch.StartNew; DeadlineAt is nominal, not the internal timer start.";
    public string? FailureBoundary { get; set; }
    public long? FinishedAt { get; set; }
    public double? WaitElapsedMilliseconds { get; set; }
    public bool? FinalVersionPredicate { get; set; }
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
        var original = error.ToString();
        return new(original.Length > 4096 ? original[..4096] : original, original.Length, original.Length > 4096);
    }
}
