using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

/// <summary>
/// Verifies the migration-step-3 seam: both Shooter sync controllers are reachable through the
/// gameplay-agnostic framework contract <see cref="IClientSyncStrategy{TInput, TSample}"/>, and
/// the contract surface maps onto the same demo behaviour exposed by the concrete controllers.
/// </summary>
public sealed class ShooterClientSyncStrategyContractTests
{
    private static ShooterGatewaySnapshot RemoteSnapshot(int frame, long serverTicks, float actorX)
    {
        return new ShooterGatewaySnapshot(
            worldId: 9001ul,
            frame: frame,
            timestamp: 0d,
            serverTicks: serverTicks,
            isFullSnapshot: true,
            actors: new[]
            {
                new ShooterGatewayActorSnapshot(actorId: 7, x: actorX, y: 0f, rotation: 0f, velocityX: 0f, velocityY: 0f, hp: 100f, hpMax: 100f, teamId: 1)
            });
    }

    private static ShooterStartGamePayload SinglePlayerStart() =>
        new ShooterStartGamePayload(
            "authoritative-interpolation-contract",
            30,
            9001,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

    [Fact]
    public void PredictRollbackControllerIsReachableThroughFrameworkContract()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var controller = new ShooterClientPredictRollbackSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null);

        IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> strategy = controller;

        Assert.Equal(NetworkSyncModel.PredictRollback, strategy.SyncModel);

        // Before any divergence the framework report is empty.
        var report = strategy.GetReconciliationReport();
        Assert.Equal(SyncReconciliationReason.None, report.Reason);
        Assert.Equal(SyncRecoveryState.Normal, report.RecoveryState);
        Assert.False(report.DidReconcile);

        // Ticking through the contract returns the same frame/tick the demo Tick produces.
        var contractTick = strategy.Tick(0f);
        Assert.Equal(controller.CurrentFrame, contractTick.Frame);
    }

    [Fact]
    public void InterpolationControllerObservesRemoteSamplesThroughFrameworkContract()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade();
        var config = new InterpolationConfig(ticksPerSecond: 1000L, interpolationDelayTicks: 100L, bufferCapacity: 16, catchUpRate: 0d);
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null, config);

        IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> strategy = controller;

        Assert.Equal(NetworkSyncModel.AuthoritativeInterpolation, strategy.SyncModel);

        // Feeding remote samples through the framework contract buffers them for delayed playback,
        // the same path BufferRemoteSnapshot uses.
        strategy.ObserveRemote(new ShooterRemoteSnapshotSample(9001ul, frame: 1, serverTicks: 1000L,
            actors: RemoteSnapshot(1, 1000L, 0f).Actors));
        strategy.ObserveRemote(new ShooterRemoteSnapshotSample(9001ul, frame: 2, serverTicks: 1100L,
            actors: RemoteSnapshot(2, 1100L, 10f).Actors));

        Assert.Equal(2, controller.BufferedRemoteSnapshotCount);
        Assert.Equal(1100L, controller.EstimatedServerTicks);

        // Remote-only interpolation does not reconcile the local simulation.
        var report = strategy.GetReconciliationReport();
        Assert.Equal(SyncReconciliationReason.None, report.Reason);
        Assert.False(report.DidReconcile);
    }

    [Fact]
    public void InterpolationControllerReportsActualLocalAuthorityCorrection()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade { ControlledPlayerId = 1 };
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null);
        var start = SinglePlayerStart();
        Assert.True(controller.StartGame(in start));

        controller.BufferRemoteSnapshot(new ShooterGatewaySnapshot(
            worldId: 9001ul,
            frame: 1,
            timestamp: 0d,
            serverTicks: 1000L,
            isFullSnapshot: true,
            actors: new[]
            {
                new ShooterGatewayActorSnapshot(
                    actorId: 1, x: 2f, y: 0f, rotation: 0f,
                    velocityX: 0f, velocityY: 0f, hp: 100f, hpMax: 100f, teamId: 1)
            }));

        IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> strategy = controller;
        var report = strategy.GetReconciliationReport();
        Assert.Equal(SyncReconciliationReason.LocalAuthorityCorrection, report.Reason);
        Assert.True(report.DidReconcile);
        Assert.Equal(1, report.AuthoritativeFrame);
    }
}
