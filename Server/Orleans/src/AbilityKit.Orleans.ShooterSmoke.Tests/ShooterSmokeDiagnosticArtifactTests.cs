using System.Text.Json;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

public sealed class ShooterSmokeDiagnosticArtifactTests
{
    [Fact]
    public void Write_EmitsCorrelatedBoundedArtifactAndIdenticalDiff()
    {
        using var scope = new TemporaryDirectory();
        var replayPath = CreateReplay(scope.Path, "records/client.record.bin", frame: 12, hash: 123u);
        var health = Enumerable.Range(1, 70)
            .Select(frame => SyncHealthEvent.Warning(SyncHealthEventKind.SnapshotGap, frame, frame))
            .ToArray();
        var capture = CreateCapture(replayPath, health, 12, 123u, 12, 123u, needsReliableResync: true);
        var outputPath = Path.Combine(scope.Path, "diagnostics", "client.diagnostic.json");

        var result = ShooterSmokeDiagnosticArtifactWriter.Write(outputPath, scope.Path, in capture);

        Assert.Equal("diagnostics/client.diagnostic.json", result.ArtifactPath);
        Assert.Equal(64, result.ArtifactSha256.Length);
        Assert.Equal("Identical", result.DiffStatus);
        Assert.Equal("diagnostics/client.diagnostic.diff.json", result.DiffPath);
        Assert.Equal(64, result.DiffSha256.Length);

        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-1/client-1", root.GetProperty("correlation").GetProperty("correlationId").GetString());
        Assert.Equal(70, root.GetProperty("health").GetProperty("totalCount").GetInt64());
        Assert.Equal(64, root.GetProperty("health").GetProperty("retainedEventCount").GetInt32());
        Assert.Equal(7, root.GetProperty("observer").GetProperty("snapshotPushes").GetInt32());
        Assert.Equal(1, root.GetProperty("observer").GetProperty("serverQueueLength").GetInt32());
        Assert.Equal(3_000_000_001L, root.GetProperty("observer").GetProperty("serverDroppedItems").GetInt64());
        Assert.Equal(3_000_000_002L, root.GetProperty("observer").GetProperty("serverCoalescedItems").GetInt64());
        Assert.Equal(3_000_000_003L, root.GetProperty("observer").GetProperty("serverBaselineInvalidations").GetInt64());
        Assert.Equal(1, root.GetProperty("reliableEvents").GetProperty("gapCount").GetInt32());
        Assert.Equal("records/client.record.bin", root.GetProperty("replay").GetProperty("path").GetString());
        Assert.Equal("Identical", root.GetProperty("diff").GetProperty("status").GetString());
        Assert.DoesNotContain("token", File.ReadAllText(outputPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_ReportsFirstDivergenceWithFrameContext()
    {
        using var scope = new TemporaryDirectory();
        var replayPath = CreateReplay(scope.Path, "records/client.record.bin", frame: 18, hash: 40u);
        var capture = CreateCapture(replayPath, Array.Empty<SyncHealthEvent>(), 18, 40u, 18, 41u);
        var outputPath = Path.Combine(scope.Path, "diagnostics", "client.json");

        var result = ShooterSmokeDiagnosticArtifactWriter.Write(outputPath, scope.Path, in capture);

        Assert.Equal("Diverged", result.DiffStatus);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(scope.Path, result.DiffPath)));
        Assert.Equal(18, document.RootElement.GetProperty("firstDivergence").GetProperty("frame").GetInt32());
        var context = document.RootElement.GetProperty("context");
        Assert.Equal(16, context.GetProperty("startFrame").GetInt32());
        Assert.NotEmpty(context.GetProperty("left").GetProperty("inputs").EnumerateArray());
        Assert.NotEmpty(context.GetProperty("left").GetProperty("snapshots").EnumerateArray());
    }

    [Fact]
    public void Write_DegradesTruthfullyForNonComparableOrMissingReplay()
    {
        using var scope = new TemporaryDirectory();
        var replayPath = CreateReplay(scope.Path, "records/client.record.bin", frame: 10, hash: 10u);
        var nonComparable = CreateCapture(replayPath, null, 10, 10u, 11, 10u);
        var nonComparablePath = Path.Combine(scope.Path, "diagnostics", "non-comparable.json");

        var nonComparableResult = ShooterSmokeDiagnosticArtifactWriter.Write(nonComparablePath, scope.Path, in nonComparable);

        Assert.Equal("NoComparableStateHashes", nonComparableResult.DiffStatus);
        using (var document = JsonDocument.Parse(File.ReadAllText(nonComparablePath)))
        {
            Assert.Equal("BothStateHashesMissing", document.RootElement.GetProperty("diff").GetProperty("reason").GetString());
            Assert.False(document.RootElement.GetProperty("diff").GetProperty("matched").GetBoolean());
        }

        var missing = CreateCapture(Path.Combine(scope.Path, "records", "missing.bin"), null, 10, 10u, 10, 10u);
        var missingPath = Path.Combine(scope.Path, "diagnostics", "missing.json");
        var missingResult = ShooterSmokeDiagnosticArtifactWriter.Write(missingPath, scope.Path, in missing);

        Assert.Equal("MissingReplay", missingResult.DiffStatus);
        Assert.Equal(string.Empty, missingResult.DiffPath);
    }

    private static ShooterSmokeDiagnosticCapture CreateCapture(
        string replayPath,
        IReadOnlyList<SyncHealthEvent>? health,
        int authoritativeFrame,
        uint authoritativeHash,
        int clientFrame,
        uint clientHash,
        bool needsReliableResync = false)
    {
        return new ShooterSmokeDiagnosticCapture(
            new SyncCorrelationContext(
                "run-1/client-1",
                runId: "run-1",
                accountId: "account-1",
                playerId: "1",
                roomId: "room-1",
                battleId: "battle-1",
                worldId: "42",
                observerId: "account-1:room-1",
                syncMode: "packed",
                tick: 20,
                commandSequence: 3,
                snapshotSequence: authoritativeFrame,
                reliableEventSequence: 9,
                reliableEventEpoch: "epoch-1"),
            health,
            SnapshotPushes: 7,
            NetworkInboundReceived: 8,
            NetworkInboundDropped: 1,
            PureStateFullBaselinesApplied: 1,
            PureStateDeltasApplied: 2,
            BaselineResyncRequests: 1,
            ServerQueueLength: 1,
            ServerDroppedItems: 3_000_000_001L,
            ServerCoalescedItems: 3_000_000_002L,
            ServerBaselineInvalidations: 3_000_000_003L,
            ReliableEventEpoch: "epoch-1",
            LastReliableEventAck: 9,
            NeedsReliableEventResync: needsReliableResync,
            ReplayPath: replayPath,
            MinimizedReplayPath: string.Empty,
            AuthoritativeFrame: authoritativeFrame,
            AuthoritativeStateHash: authoritativeHash,
            ClientFrame: clientFrame,
            ClientStateHash: clientHash);
    }

    private static string CreateReplay(string root, string relativePath, int frame, uint hash)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var meta = new FrameRecordMeta
        {
            WorldId = "42",
            WorldType = "shooter-test",
            TickRate = 20,
            RandomSeed = 7,
            PlayerId = "1",
            StartedAtUnixMs = 1,
        };
        using var writer = FrameRecordCodecs.Current.CreateWriter(path, meta);
        writer.Append(new AbilityKit.Ability.Host.PlayerInputCommand(
            new AbilityKit.Ability.FrameSync.FrameIndex(frame),
            new AbilityKit.Ability.Host.PlayerId("1"),
            100,
            new byte[] { 1, 2, 3 }));
        writer.AppendSnapshot(frame, 200, new byte[] { 4, 5, 6 });
        writer.AppendStateHash(frame, 1, hash);
        return path;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "abilitykit-shooter-diagnostics", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
