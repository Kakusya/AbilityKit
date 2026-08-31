using System.Linq;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

/// <summary>
/// Proves audit §10.4 is closed: the Shooter recovery layer now wraps and drives the framework's
/// <see cref="FastReconnectSession"/>. Each Shooter <see cref="ShooterClientRecoveryState"/> transition
/// projects onto the framework <see cref="FastReconnectPhase"/> machine, and the unified
/// <see cref="SyncHealthEvent"/>s the session emits are captured for DemoHarness telemetry — all without
/// changing Shooter's observable recovery behaviour (the existing characterization tests stay green).
/// </summary>
public sealed class ShooterFastReconnectIntegrationTests
{
    [Fact]
    public void FastReconnectDrivenThroughSmallCatchUpReachesResumingThenReconnects()
    {
        var start = StartPayload("fast-reconnect-catch-up", 12001);
        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var policy = new ShooterClientDriftRecoveryPolicy(
            smallCatchUpThreshold: 4,
            replayThreshold: 120,
            maxCatchUpTicksPerUpdate: 2,
            snapshotTimeoutTicks: 0L);
        var controller = new ShooterClientFrameSyncController(
            local, presentation, tickRate: 30, decoder: null, rollbackWorldId: 0ul, rollbackBufferFrames: 240, policy);

        // Steady state projects onto the framework Connected phase.
        Assert.Equal(FastReconnectPhase.Connected, controller.FastReconnectPhase);

        // Entering a small catch-up drives the session onto the Resuming recovery path.
        Assert.True(controller.TryEnterCatchUp(3));
        Assert.Equal(ShooterClientRecoveryState.CatchUp, controller.RecoveryState);
        Assert.Equal(FastReconnectPhase.Resuming, controller.FastReconnectPhase);

        // Draining the catch-up returns Shooter to Normal and the framework back to a steady phase.
        controller.Tick(1f / 30f);
        controller.Tick(1f / 30f);
        Assert.Equal(ShooterClientRecoveryState.Normal, controller.RecoveryState);
        Assert.True(
            controller.FastReconnectPhase is FastReconnectPhase.Recovered or FastReconnectPhase.Connected,
            $"expected a steady phase after catch-up drain, got {controller.FastReconnectPhase}.");
    }

    [Fact]
    public void ImportFailureProjectsAwaitingFullSnapshotPhaseAndEmitsRecoveryHealthEvents()
    {
        var start = StartPayload("fast-reconnect-import-failed", 12002);
        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        var invalidPacked = new ShooterPackedSnapshotPayload(
            version: ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 12992ul,
            frame: 1,
            serverTick: 10L,
            snapshotFlags: ShooterPackedSnapshotFlags.Full,
            stateHash: 0u,
            entityCount: 1,
            extensionPayload: System.Array.Empty<byte>(),
            componentChunks: System.Array.Empty<ShooterPackedComponentChunk>());
        var payload = CreatePackedPushPayload(in invalidPacked, timestamp: 1010.5, serverTicks: 1010500L);

        var applyResult = controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload);

        // Shooter behaviour unchanged.
        Assert.Equal(ShooterSnapshotApplyResult.ImportFailed, applyResult);
        Assert.True(controller.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientRecoveryState.AwaitingFullSnapshot, controller.RecoveryState);

        // Framework consumption proof: the session reached the matching phase and emitted the
        // SnapshotGap + FullSnapshotRequested recovery telemetry.
        Assert.Equal(FastReconnectPhase.AwaitingFullSnapshot, controller.FastReconnectPhase);
        var events = controller.LastFastReconnectHealthEvents;
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Kind == SyncHealthEventKind.FullSnapshotRequested);
    }

    [Fact]
    public void RecoveryFromResyncWalksFullPhaseSequenceAndAppliesFullSnapshot()
    {
        var start = StartPayload("fast-reconnect-recovered", 12003);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(12993ul, isFullSnapshot: true, authorityOverride: true);
        var corruptedHashPacked = new ShooterPackedSnapshotPayload(
            packed.Version,
            packed.WorldId,
            packed.Frame,
            packed.ServerTick,
            packed.SnapshotFlags,
            packed.StateHash + 1u,
            packed.EntityCount,
            packed.ExtensionPayload,
            packed.ComponentChunks);
        var mismatchPayload = CreatePackedPushPayload(in corruptedHashPacked, timestamp: 1030.5, serverTicks: 1030500L);
        Assert.True(authority.Tick(1f / 30f));
        var matchedPacked = authority.ExportPackedSnapshot(12993ul, isFullSnapshot: true, authorityOverride: true);
        var matchedPayload = CreatePackedPushPayload(in matchedPacked, timestamp: 1031.5, serverTicks: 1031500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        // Mismatch drives the session into the full-snapshot recovery phase.
        Assert.Equal(
            ShooterSnapshotApplyResult.AppliedPackedSnapshot,
            controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, mismatchPayload));
        Assert.True(controller.NeedsFullSnapshotResync);
        Assert.Equal(FastReconnectPhase.AwaitingFullSnapshot, controller.FastReconnectPhase);

        // Matched snapshot clears resync; framework completes recovery into the Recovered phase and
        // emits the FullSnapshotApplied + InterpolationRecovered closure telemetry.
        Assert.Equal(
            ShooterSnapshotApplyResult.AppliedPackedSnapshot,
            controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, matchedPayload));
        Assert.False(controller.NeedsFullSnapshotResync);
        Assert.Equal(ShooterClientRecoveryState.Recovered, controller.RecoveryState);
        Assert.Equal(FastReconnectPhase.Recovered, controller.FastReconnectPhase);

        var events = controller.LastFastReconnectHealthEvents;
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Kind == SyncHealthEventKind.FullSnapshotApplied);
        Assert.Contains(events, e => e.Kind == SyncHealthEventKind.InterpolationRecovered);
    }

    [Fact]
    public void CleanSnapshotReceiptHeartbeatsFrameworkWithoutLeavingSteadyState()
    {
        var start = StartPayload("fast-reconnect-heartbeat", 12004);

        var authority = new ShooterBattleRuntimePort();
        Assert.True(authority.StartGame(in start));
        Assert.True(authority.Tick(1f / 30f));
        var packed = authority.ExportPackedSnapshot(12994ul, isFullSnapshot: true, authorityOverride: true);
        var payload = CreatePackedPushPayload(in packed, timestamp: 1040.5, serverTicks: 1040500L);

        var local = new ShooterBattleRuntimePort();
        Assert.True(local.StartGame(in start));
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientFrameSyncController(local, presentation, tickRate: 30);

        Assert.Equal(
            ShooterSnapshotApplyResult.AppliedPackedSnapshot,
            controller.ApplyGatewayPush(RoomGatewayOpCodes.SnapshotPushed, payload));

        // A clean receipt keeps Shooter in Normal and the framework Connected (heartbeat ObserveServerFrame).
        Assert.Equal(ShooterClientRecoveryState.Normal, controller.RecoveryState);
        Assert.Equal(FastReconnectPhase.Connected, controller.FastReconnectPhase);
        Assert.False(controller.NeedsFullSnapshotResync);
    }

    private static ShooterStartGamePayload StartPayload(string name, int seed)
    {
        return new ShooterStartGamePayload(
            name,
            30,
            seed,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 8f, 0f)
            });
    }

    private static System.ArraySegment<byte> CreatePackedPushPayload(in ShooterPackedSnapshotPayload packed, double timestamp, long serverTicks)
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = timestamp,
            ServerTicks = serverTicks,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };
        return WireRoomGatewayBinary.Serialize(in wire);
    }
}
