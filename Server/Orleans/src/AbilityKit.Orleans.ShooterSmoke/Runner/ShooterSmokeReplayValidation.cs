using System.Globalization;
using System.Text;
using System.Text.Json;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

internal static class ShooterSmokeReplayValidation
{
    private const float ReplayFixedDeltaSeconds = 1f / 30f;

    public static ShooterSmokeReplayValidationResult Skipped => default;

    public static ShooterSmokeReplaySummary SummarizeReplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ShooterSmokeReplaySummary.Empty;
        }

        return SummarizeReplay(path, FrameRecordCodecs.Current.Load(path));
    }

    public static ShooterSmokeReplayValidationResult ValidateReplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Skipped;
        }

        var record = FrameRecordCodecs.Current.Load(path);
        return ShooterSmokeReplayTypes.ResolveKind(record.Meta) switch
        {
            ShooterSmokeReplayKind.InputState => ValidateInputStateReplay(path, record),
            ShooterSmokeReplayKind.InputLogic => ValidateInputLogicReplay(path, record),
            _ => throw new InvalidOperationException($"Shooter replay world type is not supported. Path={path}, WorldType={record.Meta?.WorldType ?? string.Empty}"),
        };
    }

    public static ShooterSmokeReplayValidationResult ValidateInputStateReplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Skipped;
        }

        return ValidateInputStateReplay(path, FrameRecordCodecs.Current.Load(path));
    }

    public static ShooterSmokeReplayValidationResult ValidateInputLogicReplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Skipped;
        }

        return ValidateInputLogicReplay(path, FrameRecordCodecs.Current.Load(path));
    }

    private static ShooterSmokeReplayValidationResult ValidateInputStateReplay(string path, FrameRecordFile record)
    {
        var snapshots = record.Snapshots ?? new List<FrameRecordSnapshotFrame>();
        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException($"Shooter input-state replay has no snapshots. Path={path}");
        }

        var runtime = new ShooterBattleRuntimePort();
        var bytesCodec = new ShooterPackedSnapshotBytesCodec();
        var pureStateSync = new ShooterPureStateSnapshotSyncController(
            static _ => { },
            new ShooterGatewaySnapshotDecoder());
        var consumedSnapshots = 0;
        var importedPackedSnapshots = 0;
        var appliedPureStateSnapshots = 0;
        var appliedPureStateBaselines = 0;
        var lastFrame = -1;

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            var payload = DecodeBase64(snapshot.PayloadBase64);
            if (payload.Length == 0)
            {
                throw new InvalidOperationException($"Shooter input-state replay snapshot has empty payload. Path={path}, Frame={snapshot.Frame}");
            }

            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(new ArraySegment<byte>(payload));
            if (wire.Payload == null || wire.Payload.Length == 0)
            {
                throw new InvalidOperationException($"Shooter input-state replay wire snapshot has empty payload. Path={path}, Frame={snapshot.Frame}");
            }

            if (wire.Frame < lastFrame)
            {
                throw new InvalidOperationException($"Shooter input-state replay frame regressed. Path={path}, Previous={lastFrame}, Current={wire.Frame}");
            }

            consumedSnapshots++;
            lastFrame = wire.Frame;

            if (wire.PayloadOpCode == ShooterOpCodes.Snapshot.PackedState || wire.PayloadOpCode == ShooterOpCodes.Snapshot.PackedStateDelta)
            {
                if (bytesCodec.Import(runtime, wire.Payload))
                {
                    importedPackedSnapshots++;
                }

                continue;
            }

            if (wire.PayloadOpCode == ShooterOpCodes.Snapshot.PureState || wire.PayloadOpCode == ShooterOpCodes.Snapshot.PureStateDelta)
            {
                var gatewaySnapshot = ShooterGatewaySnapshotMapper.ToGatewaySnapshot(in wire);
                var applyResult = pureStateSync.ApplyGatewaySnapshot(in gatewaySnapshot);
                if (applyResult == ShooterPureStateSnapshotApplyResult.AppliedFullBaseline)
                {
                    appliedPureStateBaselines++;
                    appliedPureStateSnapshots++;
                }
                else if (applyResult == ShooterPureStateSnapshotApplyResult.AppliedDelta)
                {
                    appliedPureStateSnapshots++;
                }
            }
        }

        if (importedPackedSnapshots == 0 && appliedPureStateSnapshots == 0)
        {
            throw new InvalidOperationException($"Shooter input-state replay did not contain an importable packed or applicable pure-state snapshot. Path={path}");
        }

        if (appliedPureStateSnapshots > 0 && appliedPureStateBaselines == 0)
        {
            throw new InvalidOperationException($"Shooter input-state replay applied pure-state snapshots without a full baseline. Path={path}");
        }

        if (appliedPureStateSnapshots > 0 && pureStateSync.NeedsFullBaselineResync)
        {
            throw new InvalidOperationException($"Shooter input-state replay ended with an unresolved pure-state baseline recovery. Path={path}, Reason={pureStateSync.LastResyncReason}");
        }

        var replayFrame = appliedPureStateSnapshots > 0
            ? pureStateSync.LastAppliedFrame
            : runtime.CurrentFrame;
        var replayStateHash = appliedPureStateSnapshots > 0
            ? pureStateSync.LastAppliedStateHash
            : runtime.ComputeStateHash();
        return new ShooterSmokeReplayValidationResult(
            path,
            true,
            record.Inputs?.Count ?? 0,
            consumedSnapshots,
            record.StateHashes?.Count ?? 0,
            replayFrame,
            replayStateHash,
            true,
            SummarizeReplay(path, record));
    }

    private static ShooterSmokeReplayValidationResult ValidateInputLogicReplay(string path, FrameRecordFile record)
    {
        var inputs = record.Inputs ?? new List<FrameRecordInputFrame>();
        if (inputs.Count == 0)
        {
            throw new InvalidOperationException($"Shooter input-logic replay has no inputs. Path={path}");
        }

        var replaySpec = BuildInputLogicReplaySpec(path, inputs);
        var replayResult = new ShooterDeterminismSpecRunner().Run(in replaySpec);

        var consumedInputs = 0;
        var lastFrame = -1;
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.Frame < lastFrame)
            {
                throw new InvalidOperationException($"Shooter input-logic replay input frame regressed. Path={path}, Previous={lastFrame}, Current={input.Frame}");
            }

            if (input.OpCode != ShooterOpCodes.Input.PlayerCommand)
            {
                throw new InvalidOperationException($"Shooter input-logic replay has unexpected input opCode. Path={path}, Frame={input.Frame}, OpCode={input.OpCode}");
            }

            var commands = ShooterInputCodec.Deserialize(DecodeBase64(input.PayloadBase64));
            if (commands.Length == 0)
            {
                throw new InvalidOperationException($"Shooter input-logic replay input has no commands. Path={path}, Frame={input.Frame}");
            }

            consumedInputs++;
            lastFrame = input.Frame;
        }

        var snapshots = record.Snapshots ?? new List<FrameRecordSnapshotFrame>();
        ValidateInputLogicSnapshots(path, snapshots, replayResult);

        return new ShooterSmokeReplayValidationResult(
            path,
            true,
            consumedInputs,
            snapshots.Count,
            record.StateHashes?.Count ?? 0,
            replayResult.Frame,
            replayResult.StateHash,
            replayResult.RoundTripMatched,
            SummarizeReplay(path, record));
    }

    private static ShooterDeterminismSpec BuildInputLogicReplaySpec(string path, List<FrameRecordInputFrame> inputs)
    {
        var groupedFrames = new Dictionary<int, List<ShooterPlayerCommand>>();
        var playerIds = new HashSet<int>();
        var maxFrame = 0;

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.OpCode != ShooterOpCodes.Input.PlayerCommand)
            {
                throw new InvalidOperationException($"Shooter input-logic replay has unexpected input opCode. Path={path}, Frame={input.Frame}, OpCode={input.OpCode}");
            }

            var commands = ShooterInputCodec.Deserialize(DecodeBase64(input.PayloadBase64));
            if (commands.Length == 0)
            {
                throw new InvalidOperationException($"Shooter input-logic replay input has no commands. Path={path}, Frame={input.Frame}");
            }

            if (!groupedFrames.TryGetValue(input.Frame, out var frameCommands))
            {
                frameCommands = new List<ShooterPlayerCommand>();
                groupedFrames[input.Frame] = frameCommands;
            }

            for (var j = 0; j < commands.Length; j++)
            {
                var command = commands[j];
                frameCommands.Add(command);
                playerIds.Add(command.PlayerId);
            }

            if (input.Frame > maxFrame)
            {
                maxFrame = input.Frame;
            }
        }

        if (playerIds.Count == 0)
        {
            throw new InvalidOperationException($"Shooter input-logic replay did not resolve any player ids. Path={path}");
        }

        var startPlayers = CreateStartPlayers(playerIds);
        var frames = new ShooterDeterminismFrameInput[maxFrame + 1];
        for (var frame = 0; frame <= maxFrame; frame++)
        {
            groupedFrames.TryGetValue(frame, out var commands);
            frames[frame] = new ShooterDeterminismFrameInput(frame, commands?.ToArray() ?? Array.Empty<ShooterPlayerCommand>());
        }

        var start = new ShooterStartGamePayload(
            "input-logic-replay",
            30,
            0,
            startPlayers);
        return new ShooterDeterminismSpec(start, frames, ReplayFixedDeltaSeconds);
    }

    private static ShooterStartPlayer[] CreateStartPlayers(HashSet<int> playerIds)
    {
        var ordered = playerIds.ToList();
        ordered.Sort();
        var result = new ShooterStartPlayer[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            var playerId = ordered[i];
            var offset = i * 1.5f;
            result[i] = new ShooterStartPlayer(playerId, $"player-{playerId}", offset, 0f);
        }

        return result;
    }

    private static void ValidateInputLogicSnapshots(string path, List<FrameRecordSnapshotFrame> snapshots, ShooterDeterminismResult replayResult)
    {
        BattleSnapshot? lastSnapshot = null;
        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            var payload = DecodeBase64(snapshot.PayloadBase64);
            var battleSnapshot = JsonSerializer.Deserialize<BattleSnapshot>(payload);
            if (battleSnapshot == null)
            {
                throw new InvalidOperationException($"Shooter input-logic replay snapshot cannot be consumed. Path={path}, Frame={snapshot.Frame}");
            }

            if (battleSnapshot.Frame != snapshot.Frame)
            {
                throw new InvalidOperationException($"Shooter input-logic replay snapshot frame mismatch. Path={path}, Record={snapshot.Frame}, Payload={battleSnapshot.Frame}");
            }

            if (lastSnapshot != null && battleSnapshot.Frame < lastSnapshot.Frame)
            {
                throw new InvalidOperationException($"Shooter input-logic replay snapshot frame regressed. Path={path}, Previous={lastSnapshot.Frame}, Current={battleSnapshot.Frame}");
            }

            lastSnapshot = battleSnapshot;
        }

        if (lastSnapshot == null)
        {
            return;
        }

        var replaySnapshot = replayResult.Snapshot;
        if (replaySnapshot.Frame < lastSnapshot.Frame - 1 || replaySnapshot.Frame > lastSnapshot.Frame)
        {
            throw new InvalidOperationException($"Shooter input-logic replay final frame mismatch after local playback. Path={path}, Replay={replaySnapshot.Frame}, Record={lastSnapshot.Frame}");
        }
    }

    private static ShooterSmokeReplaySummary SummarizeReplay(string path, FrameRecordFile record)
    {
        var inputs = record.Inputs ?? new List<FrameRecordInputFrame>();
        var snapshots = record.Snapshots ?? new List<FrameRecordSnapshotFrame>();
        var stateHashes = record.StateHashes ?? new List<FrameRecordStateHashFrame>();
        var inputOpCodes = CountInputOpCodes(inputs);
        var snapshotOpCodes = CountSnapshotOpCodes(snapshots);
        var firstFrame = MinFrame(inputs, snapshots, stateHashes);
        var lastFrame = MaxFrame(inputs, snapshots, stateHashes);

        return new ShooterSmokeReplaySummary(
            path,
            ShooterSmokeReplayTypes.ResolveKind(record.Meta).ToString(),
            record.Meta?.WorldId ?? string.Empty,
            record.Meta?.WorldType ?? string.Empty,
            record.Meta?.TickRate ?? 0,
            record.Meta?.RandomSeed ?? 0,
            record.Meta?.PlayerId ?? string.Empty,
            record.Meta?.StartedAtUnixMs ?? 0L,
            inputs.Count,
            snapshots.Count,
            stateHashes.Count,
            firstFrame,
            lastFrame,
            FirstFrame(inputs),
            LastFrame(inputs),
            FirstFrame(snapshots),
            LastFrame(snapshots),
            FirstFrame(stateHashes),
            LastFrame(stateHashes),
            GetCount(snapshotOpCodes, ShooterOpCodes.Snapshot.PackedState),
            GetCount(snapshotOpCodes, ShooterOpCodes.Snapshot.PackedStateDelta),
            GetCount(snapshotOpCodes, ShooterOpCodes.Snapshot.PureState),
            GetCount(snapshotOpCodes, ShooterOpCodes.Snapshot.PureStateDelta),
            GetCount(snapshotOpCodes, ShooterSmokeReplayOpCodes.ServerBattleSnapshot),
            GetCount(inputOpCodes, ShooterOpCodes.Input.PlayerCommand),
            FormatOpCodes(inputOpCodes),
            FormatOpCodes(snapshotOpCodes));
    }

    private static Dictionary<int, int> CountInputOpCodes(List<FrameRecordInputFrame> inputs)
    {
        var result = new Dictionary<int, int>();
        for (var i = 0; i < inputs.Count; i++)
        {
            AddOpCode(result, inputs[i].OpCode);
        }

        return result;
    }

    private static Dictionary<int, int> CountSnapshotOpCodes(List<FrameRecordSnapshotFrame> snapshots)
    {
        var result = new Dictionary<int, int>();
        for (var i = 0; i < snapshots.Count; i++)
        {
            AddOpCode(result, snapshots[i].OpCode);
        }

        return result;
    }

    private static void AddOpCode(Dictionary<int, int> counts, int opCode)
    {
        counts.TryGetValue(opCode, out var count);
        counts[opCode] = count + 1;
    }

    private static int GetCount(Dictionary<int, int> counts, int opCode)
    {
        return counts.TryGetValue(opCode, out var count) ? count : 0;
    }

    private static string FormatOpCodes(Dictionary<int, int> counts)
    {
        if (counts.Count == 0)
        {
            return string.Empty;
        }

        var keys = counts.Keys.ToList();
        keys.Sort();
        var builder = new StringBuilder();
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            var key = keys[i];
            builder.Append(key.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(counts[key].ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static int MinFrame(List<FrameRecordInputFrame> inputs, List<FrameRecordSnapshotFrame> snapshots, List<FrameRecordStateHashFrame> stateHashes)
    {
        var result = int.MaxValue;
        if (inputs.Count > 0) result = Math.Min(result, FirstFrame(inputs));
        if (snapshots.Count > 0) result = Math.Min(result, FirstFrame(snapshots));
        if (stateHashes.Count > 0) result = Math.Min(result, FirstFrame(stateHashes));
        return result == int.MaxValue ? -1 : result;
    }

    private static int MaxFrame(List<FrameRecordInputFrame> inputs, List<FrameRecordSnapshotFrame> snapshots, List<FrameRecordStateHashFrame> stateHashes)
    {
        var result = int.MinValue;
        if (inputs.Count > 0) result = Math.Max(result, LastFrame(inputs));
        if (snapshots.Count > 0) result = Math.Max(result, LastFrame(snapshots));
        if (stateHashes.Count > 0) result = Math.Max(result, LastFrame(stateHashes));
        return result == int.MinValue ? -1 : result;
    }

    private static int FirstFrame(List<FrameRecordInputFrame> frames) => frames.Count == 0 ? -1 : frames[0].Frame;

    private static int LastFrame(List<FrameRecordInputFrame> frames) => frames.Count == 0 ? -1 : frames[^1].Frame;

    private static int FirstFrame(List<FrameRecordSnapshotFrame> frames) => frames.Count == 0 ? -1 : frames[0].Frame;

    private static int LastFrame(List<FrameRecordSnapshotFrame> frames) => frames.Count == 0 ? -1 : frames[^1].Frame;

    private static int FirstFrame(List<FrameRecordStateHashFrame> frames) => frames.Count == 0 ? -1 : frames[0].Frame;

    private static int LastFrame(List<FrameRecordStateHashFrame> frames) => frames.Count == 0 ? -1 : frames[^1].Frame;

    private static byte[] DecodeBase64(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Array.Empty<byte>() : Convert.FromBase64String(value);
    }
}

internal readonly record struct ShooterSmokeReplayValidationResult(
    string Path,
    bool Consumed,
    int InputCount,
    int SnapshotCount,
    int StateHashCount,
    int ReplayFrame,
    uint ReplayStateHash,
    bool ReplayRoundTripMatched,
    ShooterSmokeReplaySummary Summary);

internal readonly record struct ShooterSmokeReplaySummary(
    string Path,
    string ReplayKind,
    string WorldId,
    string WorldType,
    int TickRate,
    int RandomSeed,
    string PlayerId,
    long StartedAtUnixMs,
    int InputCount,
    int SnapshotCount,
    int StateHashCount,
    int FirstFrame,
    int LastFrame,
    int FirstInputFrame,
    int LastInputFrame,
    int FirstSnapshotFrame,
    int LastSnapshotFrame,
    int FirstStateHashFrame,
    int LastStateHashFrame,
    int PackedStateSnapshotCount,
    int PackedStateDeltaSnapshotCount,
    int PureStateSnapshotCount,
    int PureStateDeltaSnapshotCount,
    int ServerBattleSnapshotCount,
    int PlayerCommandInputCount,
    string InputOpCodeDistribution,
    string SnapshotOpCodeDistribution)
{
    public static ShooterSmokeReplaySummary Empty => default;

    public int PureStateRelatedSnapshotCount => PureStateSnapshotCount + PureStateDeltaSnapshotCount;

    public int PackedStateRelatedSnapshotCount => PackedStateSnapshotCount + PackedStateDeltaSnapshotCount;

    public bool ContainsResyncOrPureStateDiagnostics => PureStateRelatedSnapshotCount > 0 || PackedStateRelatedSnapshotCount > 0;
}
