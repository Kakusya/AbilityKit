using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ReliableBattleEventRetentionTests
{
    [Fact]
    public void AppendCreatesStableMonotonicEnvelopeAndBoundedReplayGap()
    {
        var retention = new ReliableBattleEventRetention("battle-1", "epoch-1", capacity: 2);

        var first = retention.Append(10, 1, new byte[] { 1 });
        retention.Append(11, 2, new byte[] { 2 });
        var third = retention.Append(12, 3, new byte[] { 3 });

        Assert.Equal(1L, first.Sequence);
        Assert.Equal("battle-1:epoch-1:1", first.EventId);
        Assert.Equal(3L, third.Sequence);
        Assert.Equal("battle-1:epoch-1:3", third.EventId);

        var replay = retention.CreateReplay("epoch-1", 0L);
        Assert.True(replay.RetentionGap);
        Assert.Equal(2L, replay.FirstAvailableSequence);
        Assert.Equal(3L, replay.Watermark);
        Assert.Empty(replay.Events);
    }

    [Fact]
    public void FastObserverAckDoesNotTrimEventsNeededBySlowObserver()
    {
        var retention = new ReliableBattleEventRetention("battle-1", "epoch-1", capacity: 8);
        retention.Append(1, 1, null);
        retention.Append(2, 1, null);
        retention.Append(3, 1, null);
        retention.RegisterObserver("fast", "epoch-1", 0L);
        retention.RegisterObserver("slow", "epoch-1", 0L);

        Assert.Equal(3L, retention.Acknowledge("fast", "epoch-1", 3L));
        var slowReplay = retention.CreateReplay("epoch-1", 0L);
        Assert.False(slowReplay.RetentionGap);
        Assert.Equal(new[] { 1L, 2L, 3L }, slowReplay.Events.Select(item => item.Sequence));

        Assert.Equal(1L, retention.Acknowledge("slow", "epoch-1", 1L));
        var afterSlowAck = retention.CreateReplay("epoch-1", 0L);
        Assert.True(afterSlowAck.RetentionGap);
        Assert.Empty(afterSlowAck.Events);

        retention.UnregisterObserver("slow");
        var afterSlowDisconnect = retention.CreateReplay("epoch-1", 3L);
        Assert.Empty(afterSlowDisconnect.Events);
        Assert.Equal(4L, afterSlowDisconnect.FirstAvailableSequence);
    }

    [Fact]
    public void UnknownEpochInitialSubscriptionStartsAtCurrentRetentionWindow()
    {
        var retention = new ReliableBattleEventRetention("battle-1", "epoch-1", capacity: 2);
        retention.Append(1, 1, null);
        retention.Append(2, 1, null);
        retention.Append(3, 1, null);

        var replay = retention.CreateReplay(string.Empty, 0L);

        Assert.False(replay.RetentionGap);
        Assert.Equal(new[] { 2L, 3L }, replay.Events.Select(item => item.Sequence));
    }

    [Fact]
    public void NoActiveObserverAckTrimKeepsEventsForReconnectReplay()
    {
        var retention = new ReliableBattleEventRetention("battle-1", "epoch-1", capacity: 4);
        retention.Append(1, 1, null);
        retention.RegisterObserver("observer", "epoch-1", 0L);
        retention.Acknowledge("observer", "epoch-1", 1L);
        retention.UnregisterObserver("observer");
        retention.Append(2, 1, null);

        var replay = retention.CreateReplay("epoch-1", 1L);
        Assert.False(replay.RetentionGap);
        Assert.Single(replay.Events);
        Assert.Equal(2L, replay.Events[0].Sequence);
    }

    [Fact]
    public void EpochMismatchReturnsExplicitGapWithoutEvents()
    {
        var retention = new ReliableBattleEventRetention("battle-1", "epoch-2", capacity: 4);
        retention.Append(1, 1, null);

        var replay = retention.CreateReplay("epoch-1", 7L);

        Assert.True(replay.RetentionGap);
        Assert.Equal("epoch-2", replay.Epoch);
        Assert.Empty(replay.Events);
        Assert.Equal(1L, replay.Watermark);
    }
}
