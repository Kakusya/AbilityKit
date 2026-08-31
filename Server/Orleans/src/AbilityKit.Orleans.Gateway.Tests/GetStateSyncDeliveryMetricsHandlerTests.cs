using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Handlers;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class GetStateSyncDeliveryMetricsHandlerTests
{
    [Fact]
    public void CanReadMetrics_accepts_matching_room_member_and_battle()
    {
        Assert.True(GetStateSyncDeliveryMetricsHandler.CanReadMetrics(
            CreateSnapshot(),
            "room-a",
            "room-a",
            "battle-a",
            "account-a"));
    }

    [Theory]
    [InlineData("room-other", "room-a", "battle-a", "account-a")]
    [InlineData("room-a", "room-a", "battle-other", "account-a")]
    [InlineData("room-a", "room-a", "battle-a", "account-other")]
    public void CanReadMetrics_rejects_mismatched_authorization_context(
        string mappedRoomId,
        string requestedRoomId,
        string requestedBattleId,
        string accountId)
    {
        Assert.False(GetStateSyncDeliveryMetricsHandler.CanReadMetrics(
            CreateSnapshot(),
            mappedRoomId,
            requestedRoomId,
            requestedBattleId,
            accountId));
    }

    [Theory]
    [InlineData(null, "room-a", "room-a", "battle-a", "account-a")]
    [InlineData("snapshot", null, "room-a", "battle-a", "account-a")]
    [InlineData("snapshot", "room-a", null, "battle-a", "account-a")]
    [InlineData("snapshot", "room-a", "room-a", null, "account-a")]
    [InlineData("snapshot", "room-a", "room-a", "battle-a", null)]
    public void CanReadMetrics_rejects_missing_authorization_context(
        string? snapshotMarker,
        string? mappedRoomId,
        string? requestedRoomId,
        string? requestedBattleId,
        string? accountId)
    {
        var snapshot = snapshotMarker == null ? null : CreateSnapshot();

        Assert.False(GetStateSyncDeliveryMetricsHandler.CanReadMetrics(
            snapshot,
            mappedRoomId,
            requestedRoomId,
            requestedBattleId,
            accountId));
    }

    [Fact]
    public void ToWireResponse_preserves_complete_delivery_metrics()
    {
        var response = GetStateSyncDeliveryMetricsHandler.ToWireResponse(
            new StateSyncDeliveryMetrics
            {
                ProducedBytes = 3_000_000_001L,
                SentBytes = 3_000_000_002L,
                DroppedBytes = 3_000_000_003L,
                MergedBytes = 3_000_000_004L,
                QueueLength = 1,
                QueueAgeTicks = 3_000_000_005L,
                BaselineAgeTicks = 3_000_000_006L,
                ResyncCount = 3_000_000_007L
            });

        Assert.True(response.Success);
        Assert.Equal(3_000_000_001L, response.ProducedBytes);
        Assert.Equal(3_000_000_002L, response.SentBytes);
        Assert.Equal(3_000_000_003L, response.DroppedBytes);
        Assert.Equal(3_000_000_004L, response.MergedBytes);
        Assert.Equal(1, response.QueueLength);
        Assert.Equal(3_000_000_005L, response.QueueAgeTicks);
        Assert.Equal(3_000_000_006L, response.BaselineAgeTicks);
        Assert.Equal(3_000_000_007L, response.ResyncCount);
        Assert.Equal(string.Empty, response.Message);
    }

    private static RoomSnapshot CreateSnapshot()
    {
        return new RoomSnapshot(
            new RoomSummary("cn", "server-a", "room-a", "shooter", "Room A", true, 2, 2, "account-a", 1, null),
            new List<string> { "account-a", "account-b" },
            new List<RoomPlayerSnapshot>
            {
                new("account-a", 1, true, 1001, 1, 1, 1, 1, null, 1u),
                new("account-b", 1, true, 1002, 2, 1, 1, 1, null, 2u)
            },
            false,
            "battle-a",
            null,
            9001ul,
            new Dictionary<string, RoomMemberState>
            {
                ["account-a"] = new(true, 1, 0),
                ["account-b"] = new(true, 2, 0)
            });
    }
}
