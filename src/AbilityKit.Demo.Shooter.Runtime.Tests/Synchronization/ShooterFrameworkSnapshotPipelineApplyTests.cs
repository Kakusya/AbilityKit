using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterFrameworkSnapshotPipelineApplyTests
{
    [Fact]
    public void FrameworkSnapshotPipelineOverwritesLocalRuntimeAndViewModel()
    {
        var source = new ShooterBattleRuntimePort(ShooterEntityLimitOptions.Default, ShooterEnemyWaveOptions.EnabledOption);
        var sourceStart = new ShooterStartGamePayload(
            "source",
            30,
            901,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 4f, 0f)
            });
        Assert.True(source.StartGame(in sourceStart));
        source.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) });
        Assert.True(source.Tick(1f / 30f));

        var target = new ShooterBattleRuntimePort();
        var targetStart = new ShooterStartGamePayload(
            "target",
            30,
            902,
            new[]
            {
                new ShooterStartPlayer(9, "Other", -5f, -5f)
            });
        Assert.True(target.StartGame(in targetStart));
        target.SubmitInput(0, new[] { new ShooterPlayerCommand(9, 0f, 1f, 0f, 1f, false) });
        Assert.True(target.Tick(1f / 30f));
        Assert.NotEqual(source.ComputeStateHash(), target.ComputeStateHash());

        var packed = source.ExportPackedSnapshot(777ul, isFullSnapshot: true, authorityOverride: true);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 456.5,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
        };
        var gatewayPayload = WireRoomGatewayBinary.Serialize(in wire);
        var decoder = new ShooterGatewaySnapshotDecoder();
        var snapshot = decoder.Decode(gatewayPayload);
        var presentation = new ShooterPresentationFacade();
        using var pipeline = new ShooterFrameworkSnapshotPipeline(target, presentation);

        var result = pipeline.ApplyGatewaySnapshot(in snapshot);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, result);
        Assert.Equal(source.CurrentFrame, target.CurrentFrame);
        Assert.Equal(source.ComputeStateHash(), target.ComputeStateHash());
        Assert.Equal(packed.Frame, pipeline.LastAppliedFrame);
        Assert.Equal(packed.StateHash, pipeline.LastAppliedStateHash);
        Assert.Equal(packed.Frame, presentation.ViewModel.Frame);
        Assert.Equal(2, presentation.ViewModel.Current.EntityChanges.Count(change => change.Kind == ShooterViewEntityKind.Player));
        Assert.Single(presentation.ViewModel.Current.EntityChanges, change => change.Kind == ShooterViewEntityKind.Bullet);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Kind == ShooterViewEntityKind.Enemy);
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 1)));
        Assert.Contains(presentation.ViewModel.Current.EntityChanges, change => change.Key.Equals(new ShooterViewEntityKey(ShooterViewEntityKind.Player, 2)));

        var imported = target.ExportPackedSnapshot(777ul, isFullSnapshot: true, authorityOverride: true);
        Assert.NotNull(FindPackedChunk(imported, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy));
        Assert.NotNull(FindPackedChunk(imported, ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Enemy));
        Assert.NotNull(FindPackedChunk(imported, ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Enemy));
    }

    [Fact]
    public void FrameworkSnapshotPipelineAcceptsPreviousPackedVersion()
    {
        var source = CreateStartedRuntime("previous-source", 911);
        Assert.True(source.Tick(1f / 30f));
        var packed = source.ExportPackedSnapshot(778ul, isFullSnapshot: true, authorityOverride: true);
        packed.Version = ShooterStateSyncCompatibilityPolicy.MinimumPackedVersion;
        var target = CreateStartedRuntime("previous-target", 912);
        var presentation = new ShooterPresentationFacade();
        using var pipeline = new ShooterFrameworkSnapshotPipeline(target, presentation);
        var snapshot = CreateGatewaySnapshot(in packed);

        var result = pipeline.ApplyGatewaySnapshot(in snapshot);

        Assert.Equal(ShooterSnapshotApplyResult.AppliedPackedSnapshot, result);
        Assert.Equal(source.CurrentFrame, target.CurrentFrame);
        Assert.Equal(source.ComputeStateHash(), target.ComputeStateHash());
        Assert.Equal(packed.Frame, presentation.ViewModel.Frame);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ShooterPackedSnapshotCodec.CurrentVersion + 1)]
    public void FrameworkSnapshotPipelineRejectsUnsupportedPackedVersionBeforeRuntimeMutation(int version)
    {
        var target = CreateStartedRuntime("unsupported-target", 913);
        Assert.True(target.Tick(1f / 30f));
        var frameBefore = target.CurrentFrame;
        var hashBefore = target.ComputeStateHash();
        var packed = target.ExportPackedSnapshot(779ul, isFullSnapshot: true, authorityOverride: true);
        packed.Version = version;
        packed.Frame++;
        var presentation = new ShooterPresentationFacade();
        using var pipeline = new ShooterFrameworkSnapshotPipeline(target, presentation);
        var snapshot = CreateGatewaySnapshot(in packed);

        var result = pipeline.ApplyGatewaySnapshot(in snapshot);

        Assert.Equal(ShooterSnapshotApplyResult.UnsupportedVersion, result);
        Assert.Equal(frameBefore, target.CurrentFrame);
        Assert.Equal(hashBefore, target.ComputeStateHash());
        Assert.Equal(0, presentation.ViewModel.Frame);
        Assert.Equal(0, pipeline.LastAppliedFrame);
    }

    private static ShooterBattleRuntimePort CreateStartedRuntime(string matchId, int seed)
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            matchId,
            30,
            seed,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 4f, 0f)
            });
        Assert.True(runtime.StartGame(in start));
        return runtime;
    }

    private static ShooterGatewaySnapshot CreateGatewaySnapshot(in ShooterPackedSnapshotPayload packed)
    {
        return new ShooterGatewaySnapshot(
            packed.WorldId,
            packed.Frame,
            456.5,
            packed.ServerTick,
            isFullSnapshot: true,
            Array.Empty<ShooterGatewayActorSnapshot>(),
            ShooterOpCodes.Snapshot.PackedState,
            packed);
    }

    private static ShooterPackedComponentChunk? FindPackedChunk(in ShooterPackedSnapshotPayload packed, int componentKind, int entityKind)
    {
        for (int i = 0; i < packed.ComponentChunks.Length; i++)
        {
            var chunk = packed.ComponentChunks[i];
            if (chunk.ComponentKind == componentKind && chunk.EntityKind == entityKind)
            {
                return chunk;
            }
        }

        return null;
    }
}
