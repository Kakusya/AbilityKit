using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Protocol.Shooter;
using AbilityKit.Protocol.Room;
using AbilityKit.Ability.FrameSync;
using Xunit;

public sealed class ShooterSmokeReplaySummaryTests
{
    [Fact]
    public void SummarizeReplayReportsMetaFramesAndOpCodeDistribution()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shooter-summary-{Guid.NewGuid():N}.record.json");
        try
        {
            var meta = new FrameRecordMeta
            {
                WorldId = "room-1:battle-1:42",
                WorldType = ShooterSmokeReplayTypes.CreateInputStateWorldType(ShooterSmokeClientProcessMode.Create),
                TickRate = 30,
                RandomSeed = 1234,
                PlayerId = "player-1",
                StartedAtUnixMs = 5678L,
            };

            using (var writer = FrameRecordCodecs.Current.CreateWriter(path, meta))
            {
                writer.Append(new PlayerInputCommand(new FrameIndex(2), new PlayerId("player-1"), ShooterOpCodes.Input.PlayerCommand, new byte[] { 1, 2 }));
                writer.AppendSnapshot(3, ShooterOpCodes.Snapshot.PackedState, new byte[] { 3, 4 });
                writer.AppendSnapshot(4, ShooterOpCodes.Snapshot.PureState, new byte[] { 5, 6 });
                writer.AppendSnapshot(5, ShooterOpCodes.Snapshot.PureStateDelta, new byte[] { 7, 8 });
                writer.AppendSnapshot(6, ShooterSmokeReplayOpCodes.ServerBattleSnapshot, new byte[] { 9, 10 });
                writer.AppendStateHash(7, 1, 0x12345678u);
            }

            var summary = ShooterSmokeReplayValidation.SummarizeReplay(path);

            Assert.Equal(path, summary.Path);
            Assert.Equal(ShooterSmokeReplayKind.InputState.ToString(), summary.ReplayKind);
            Assert.Equal("room-1:battle-1:42", summary.WorldId);
            Assert.Equal(ShooterSmokeReplayTypes.CreateInputStateWorldType(ShooterSmokeClientProcessMode.Create), summary.WorldType);
            Assert.Equal(30, summary.TickRate);
            Assert.Equal(1234, summary.RandomSeed);
            Assert.Equal("player-1", summary.PlayerId);
            Assert.Equal(5678L, summary.StartedAtUnixMs);
            Assert.Equal(1, summary.InputCount);
            Assert.Equal(4, summary.SnapshotCount);
            Assert.Equal(1, summary.StateHashCount);
            Assert.Equal(2, summary.FirstFrame);
            Assert.Equal(7, summary.LastFrame);
            Assert.Equal(2, summary.FirstInputFrame);
            Assert.Equal(2, summary.LastInputFrame);
            Assert.Equal(3, summary.FirstSnapshotFrame);
            Assert.Equal(6, summary.LastSnapshotFrame);
            Assert.Equal(7, summary.FirstStateHashFrame);
            Assert.Equal(7, summary.LastStateHashFrame);
            Assert.Equal(1, summary.PackedStateSnapshotCount);
            Assert.Equal(0, summary.PackedStateDeltaSnapshotCount);
            Assert.Equal(1, summary.PureStateSnapshotCount);
            Assert.Equal(1, summary.PureStateDeltaSnapshotCount);
            Assert.Equal(1, summary.ServerBattleSnapshotCount);
            Assert.Equal(1, summary.PlayerCommandInputCount);
            Assert.Equal("5101:1", summary.InputOpCodeDistribution);
            Assert.Equal("5204:1|5207:1|5208:1|100001:1", summary.SnapshotOpCodeDistribution);
            Assert.Equal(2, summary.PureStateRelatedSnapshotCount);
            Assert.Equal(1, summary.PackedStateRelatedSnapshotCount);
            Assert.True(summary.ContainsResyncOrPureStateDiagnostics);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ValidateInputStateReplayAppliesPureStateBaselineAndDelta()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shooter-pure-state-replay-{Guid.NewGuid():N}.record.json");
        try
        {
            using (var writer = FrameRecordCodecs.Current.CreateWriter(path, CreateInputStateMeta()))
            {
                AppendPureStateSnapshot(
                    writer,
                    frame: 10,
                    snapshotKind: ShooterPureStateSnapshotKinds.FullBaseline,
                    baselineFrame: 10,
                    baselineHash: 0x10101010u,
                    stateHash: 0x10101010u);
                AppendPureStateSnapshot(
                    writer,
                    frame: 11,
                    snapshotKind: ShooterPureStateSnapshotKinds.Delta,
                    baselineFrame: 10,
                    baselineHash: 0x10101010u,
                    stateHash: 0x11111111u);
            }

            var result = ShooterSmokeReplayValidation.ValidateInputStateReplay(path);

            Assert.True(result.Consumed);
            Assert.Equal(2, result.SnapshotCount);
            Assert.Equal(11, result.ReplayFrame);
            Assert.Equal(0x11111111u, result.ReplayStateHash);
            Assert.True(result.ReplayRoundTripMatched);
            Assert.Equal(1, result.Summary.PureStateSnapshotCount);
            Assert.Equal(1, result.Summary.PureStateDeltaSnapshotCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ValidateInputStateReplayRejectsPureStateDeltaWithoutBaseline()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shooter-pure-state-delta-only-{Guid.NewGuid():N}.record.json");
        try
        {
            using (var writer = FrameRecordCodecs.Current.CreateWriter(path, CreateInputStateMeta()))
            {
                AppendPureStateSnapshot(
                    writer,
                    frame: 11,
                    snapshotKind: ShooterPureStateSnapshotKinds.Delta,
                    baselineFrame: 10,
                    baselineHash: 0x10101010u,
                    stateHash: 0x11111111u);
            }

            var exception = Assert.Throws<InvalidOperationException>(
                () => ShooterSmokeReplayValidation.ValidateInputStateReplay(path));

            Assert.Contains("applicable pure-state snapshot", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static FrameRecordMeta CreateInputStateMeta()
    {
        return new FrameRecordMeta
        {
            WorldId = "room-1:battle-1:42",
            WorldType = ShooterSmokeReplayTypes.CreateInputStateWorldType(ShooterSmokeClientProcessMode.Create),
            TickRate = 30,
            PlayerId = "player-1",
        };
    }

    private static void AppendPureStateSnapshot(
        IFrameRecordWriter writer,
        int frame,
        int snapshotKind,
        int baselineFrame,
        uint baselineHash,
        uint stateHash)
    {
        const ulong worldId = 42UL;
        var pureState = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            worldId,
            frame,
            frame * TimeSpan.TicksPerMillisecond,
            snapshotKind,
            baselineFrame,
            baselineHash,
            stateHash,
            ShooterPureStateSyncSettings.Default,
            Array.Empty<ShooterPureStateEntityDelta>(),
            Array.Empty<ShooterPureStateVisibilityHint>());
        var payloadOpCode = snapshotKind == ShooterPureStateSnapshotKinds.FullBaseline
            ? ShooterOpCodes.Snapshot.PureState
            : ShooterOpCodes.Snapshot.PureStateDelta;
        var payload = ShooterPureStateSyncCodec.Serialize(in pureState);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = worldId,
            Frame = frame,
            Timestamp = 0d,
            IsFullSnapshot = snapshotKind == ShooterPureStateSnapshotKinds.FullBaseline,
            Actors = null,
            PayloadOpCode = payloadOpCode,
            Payload = payload,
            ServerTicks = pureState.ServerTick,
        };
        var wirePayload = WireRoomGatewayBinary.Serialize(in wire).ToArray();
        writer.AppendSnapshot(frame, payloadOpCode, wirePayload);
    }
}
