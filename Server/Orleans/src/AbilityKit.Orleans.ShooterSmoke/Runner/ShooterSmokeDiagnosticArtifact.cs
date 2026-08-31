using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Network.Runtime.Sync;

internal static class ShooterSmokeDiagnosticArtifactWriter
{
    private const int SchemaVersion = 1;
    private const int MaxHealthEvents = 64;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ShooterSmokeDiagnosticWriteResult Write(
        string outputPath,
        string runRootPath,
        in ShooterSmokeDiagnosticCapture capture)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return default;
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var fullRunRootPath = ResolveRunRoot(runRootPath, fullOutputPath);
        EnsureUnderRunRoot(fullRunRootPath, fullOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

        var context = capture.Context;
        var health = BuildHealthSummary(capture.HealthEvents, in context);
        var observer = new ShooterSmokeObserverSummary
        {
            Source = "client-observed-with-server-delivery-metrics",
            SnapshotPushes = Math.Max(0, capture.SnapshotPushes),
            NetworkInboundReceived = Math.Max(0, capture.NetworkInboundReceived),
            NetworkInboundDropped = Math.Max(0, capture.NetworkInboundDropped),
            PureStateFullBaselinesApplied = Math.Max(0, capture.PureStateFullBaselinesApplied),
            PureStateDeltasApplied = Math.Max(0, capture.PureStateDeltasApplied),
            BaselineResyncRequests = Math.Max(0, capture.BaselineResyncRequests),
            ServerQueueLength = capture.ServerQueueLength,
            ServerDroppedItems = capture.ServerDroppedItems,
            ServerCoalescedItems = capture.ServerCoalescedItems,
            ServerBaselineInvalidations = capture.ServerBaselineInvalidations,
        };
        var reliable = new ShooterSmokeReliableEventSummary
        {
            Epoch = capture.ReliableEventEpoch ?? string.Empty,
            LastAcknowledgedSequence = Math.Max(0L, capture.LastReliableEventAck),
            GapCount = capture.NeedsReliableEventResync ? 1 : 0,
            NeedsResync = capture.NeedsReliableEventResync,
        };

        var replay = CreateReference(fullRunRootPath, capture.ReplayPath, "frame-record");
        var minimizedReplay = CreateReference(fullRunRootPath, capture.MinimizedReplayPath, "minimized-frame-record");
        var diff = WriteDiff(fullOutputPath, fullRunRootPath, capture.ReplayPath, in capture);

        var artifact = new ShooterSmokeDiagnosticArtifact
        {
            SchemaVersion = SchemaVersion,
            Correlation = context,
            Health = health,
            Observer = observer,
            ReliableEvents = reliable,
            Replay = replay,
            MinimizedReplay = minimizedReplay,
            Diff = diff,
        };
        File.WriteAllText(fullOutputPath, JsonSerializer.Serialize(artifact, JsonOptions));

        return new ShooterSmokeDiagnosticWriteResult(
            ToRelativePath(fullRunRootPath, fullOutputPath),
            ComputeSha256(fullOutputPath),
            diff.Report?.Path ?? string.Empty,
            diff.Report?.Sha256 ?? string.Empty,
            diff.Status);
    }

    private static ShooterSmokeHealthSummary BuildHealthSummary(
        IReadOnlyList<SyncHealthEvent>? events,
        in SyncCorrelationContext context)
    {
        var buffer = new SyncHealthEventBuffer(MaxHealthEvents);
        if (events != null)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var healthEvent = events[i];
                var correlated = healthEvent.Context.HasCorrelation
                    ? healthEvent
                    : healthEvent.WithContext(in context);
                buffer.Publish(in correlated);
            }
        }

        var report = buffer.CreateReport();
        return new ShooterSmokeHealthSummary
        {
            SchemaVersion = report.SchemaVersion,
            TotalCount = report.EventCount,
            InfoCount = report.InfoCount,
            WarningCount = report.WarningCount,
            CriticalCount = report.ErrorCount,
            HighestSeverity = report.HighestSeverity,
            FirstFrame = report.FirstFrame,
            LastFrame = report.LastFrame,
            FirstCorrelation = report.FirstCorrelation,
            ObserverQueuedCount = report.ObserverQueuedCount,
            ObserverDroppedCount = report.ObserverDroppedCount,
            ObserverCoalescedCount = report.ObserverCoalescedCount,
            ObserverBaselineInvalidatedCount = report.ObserverBaselineInvalidatedCount,
            ReliableGapCount = report.ReliableGapCount,
            RetainedEventCount = report.RetainedEvents.Length,
        };
    }

    private static ShooterSmokeDiffSummary WriteDiff(
        string diagnosticOutputPath,
        string runRootPath,
        string replayPath,
        in ShooterSmokeDiagnosticCapture capture)
    {
        var summary = new ShooterSmokeDiffSummary { Projection = "same-frame-nonzero-authoritative-vs-client" };
        if (string.IsNullOrWhiteSpace(replayPath) || !File.Exists(replayPath))
        {
            summary.Status = "MissingReplay";
            summary.Reason = "ReplayArtifactMissing";
            return summary;
        }

        try
        {
            var source = FrameRecordCodecs.Current.Load(replayPath);
            var authoritative = CreateProjection(source, capture.AuthoritativeFrame, capture.AuthoritativeStateHash, includeContext: true);
            var client = CreateProjection(source, capture.ClientFrame, capture.ClientStateHash, includeContext: true);

            if (capture.AuthoritativeFrame <= 0
                || capture.ClientFrame <= 0
                || capture.AuthoritativeFrame != capture.ClientFrame
                || capture.AuthoritativeStateHash == 0u
                || capture.ClientStateHash == 0u)
            {
                authoritative.StateHashes.Clear();
                client.StateHashes.Clear();
            }

            var report = new FrameRecordDiffAnalyzer().Compare(
                authoritative,
                client,
                new FrameRecordDiffOptions { ContextFrames = 2 });
            var diffPath = Path.ChangeExtension(diagnosticOutputPath, ".diff.json");
            File.WriteAllText(diffPath, JsonSerializer.Serialize(report, JsonOptions));
            summary.Status = report.Status.ToString();
            summary.Reason = report.Reason.ToString();
            summary.Matched = report.Matched;
            summary.FirstDivergentFrame = report.FirstDivergence?.Frame;
            summary.Report = CreateReference(runRootPath, diffPath, "frame-record-diff");
            return summary;
        }
        catch (Exception exception)
        {
            summary.Status = "UnreadableReplay";
            summary.Reason = exception.GetType().Name;
            return summary;
        }
    }

    private static FrameRecordFile CreateProjection(
        FrameRecordFile source,
        int frame,
        uint hash,
        bool includeContext)
    {
        return new FrameRecordFile
        {
            Meta = new FrameRecordMeta
            {
                WorldId = source.Meta?.WorldId ?? string.Empty,
                WorldType = source.Meta?.WorldType ?? string.Empty,
                TickRate = source.Meta?.TickRate ?? 0,
                RandomSeed = source.Meta?.RandomSeed ?? 0,
                PlayerId = source.Meta?.PlayerId ?? string.Empty,
                StartedAtUnixMs = source.Meta?.StartedAtUnixMs ?? 0L,
            },
            Inputs = includeContext ? source.Inputs ?? new List<FrameRecordInputFrame>() : new List<FrameRecordInputFrame>(),
            Snapshots = includeContext ? source.Snapshots ?? new List<FrameRecordSnapshotFrame>() : new List<FrameRecordSnapshotFrame>(),
            StateHashes = frame > 0 && hash != 0u
                ? new List<FrameRecordStateHashFrame>
                {
                    new() { Frame = frame, Version = 1, Hash = hash },
                }
                : new List<FrameRecordStateHashFrame>(),
            Index = new List<FrameRecordChunkIndex>(),
        };
    }

    private static ShooterSmokeArtifactReference? CreateReference(string runRootPath, string path, string kind)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        EnsureUnderRunRoot(runRootPath, fullPath);
        var info = new FileInfo(fullPath);
        return new ShooterSmokeArtifactReference
        {
            Kind = kind,
            Path = ToRelativePath(runRootPath, fullPath),
            Bytes = info.Length,
            Sha256 = ComputeSha256(fullPath),
        };
    }

    private static string ResolveRunRoot(string runRootPath, string outputPath)
    {
        if (!string.IsNullOrWhiteSpace(runRootPath))
        {
            return Path.GetFullPath(runRootPath);
        }

        return Path.GetDirectoryName(outputPath)!;
    }

    private static void EnsureUnderRunRoot(string runRootPath, string path)
    {
        var relative = Path.GetRelativePath(runRootPath, path);
        if (relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException($"Diagnostic artifact must be under the run root. Path={path}");
        }
    }

    private static string ToRelativePath(string runRootPath, string path)
        => Path.GetRelativePath(runRootPath, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal readonly record struct ShooterSmokeDiagnosticCapture(
    SyncCorrelationContext Context,
    IReadOnlyList<SyncHealthEvent>? HealthEvents,
    int SnapshotPushes,
    int NetworkInboundReceived,
    int NetworkInboundDropped,
    int PureStateFullBaselinesApplied,
    int PureStateDeltasApplied,
    int BaselineResyncRequests,
    int? ServerQueueLength,
    long? ServerDroppedItems,
    long? ServerCoalescedItems,
    long? ServerBaselineInvalidations,
    string ReliableEventEpoch,
    long LastReliableEventAck,
    bool NeedsReliableEventResync,
    string ReplayPath,
    string MinimizedReplayPath,
    int AuthoritativeFrame,
    uint AuthoritativeStateHash,
    int ClientFrame,
    uint ClientStateHash);

internal readonly record struct ShooterSmokeDiagnosticWriteResult(
    string ArtifactPath,
    string ArtifactSha256,
    string DiffPath,
    string DiffSha256,
    string DiffStatus);

internal sealed class ShooterSmokeDiagnosticArtifact
{
    public int SchemaVersion { get; set; }
    public SyncCorrelationContext Correlation { get; set; }
    public ShooterSmokeHealthSummary Health { get; set; } = new();
    public ShooterSmokeObserverSummary Observer { get; set; } = new();
    public ShooterSmokeReliableEventSummary ReliableEvents { get; set; } = new();
    public ShooterSmokeArtifactReference? Replay { get; set; }
    public ShooterSmokeArtifactReference? MinimizedReplay { get; set; }
    public ShooterSmokeDiffSummary Diff { get; set; } = new();
}

internal sealed class ShooterSmokeHealthSummary
{
    public int SchemaVersion { get; set; }
    public long TotalCount { get; set; }
    public long InfoCount { get; set; }
    public long WarningCount { get; set; }
    public long CriticalCount { get; set; }
    public SyncHealthSeverity HighestSeverity { get; set; }
    public int FirstFrame { get; set; }
    public int LastFrame { get; set; }
    public SyncCorrelationContext FirstCorrelation { get; set; }
    public long ObserverQueuedCount { get; set; }
    public long ObserverDroppedCount { get; set; }
    public long ObserverCoalescedCount { get; set; }
    public long ObserverBaselineInvalidatedCount { get; set; }
    public long ReliableGapCount { get; set; }
    public int RetainedEventCount { get; set; }
}

internal sealed class ShooterSmokeObserverSummary
{
    public string Source { get; set; } = string.Empty;
    public int SnapshotPushes { get; set; }
    public int NetworkInboundReceived { get; set; }
    public int NetworkInboundDropped { get; set; }
    public int PureStateFullBaselinesApplied { get; set; }
    public int PureStateDeltasApplied { get; set; }
    public int BaselineResyncRequests { get; set; }
    public int? ServerQueueLength { get; set; }
    public long? ServerDroppedItems { get; set; }
    public long? ServerCoalescedItems { get; set; }
    public long? ServerBaselineInvalidations { get; set; }
}

internal sealed class ShooterSmokeReliableEventSummary
{
    public string Epoch { get; set; } = string.Empty;
    public long LastAcknowledgedSequence { get; set; }
    public int GapCount { get; set; }
    public bool NeedsResync { get; set; }
}

internal sealed class ShooterSmokeDiffSummary
{
    public string Projection { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public int? FirstDivergentFrame { get; set; }
    public ShooterSmokeArtifactReference? Report { get; set; }
}

internal sealed class ShooterSmokeArtifactReference
{
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
