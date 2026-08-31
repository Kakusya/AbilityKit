using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Handlers;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class AcknowledgeReliableBattleEventsHandlerTests
{
    [Fact]
    public void CanAcknowledge_accepts_matching_room_member_and_battle()
    {
        var snapshot = CreateSnapshot();

        Assert.True(AcknowledgeReliableBattleEventsHandler.CanAcknowledge(
            snapshot,
            "room-a",
            "room-a",
            "battle-a",
            "account-a"));
    }

    [Fact]
    public void CanAcknowledge_rejects_mapped_room_mismatch()
    {
        var snapshot = CreateSnapshot();

        Assert.False(AcknowledgeReliableBattleEventsHandler.CanAcknowledge(
            snapshot,
            "room-other",
            "room-a",
            "battle-a",
            "account-a"));
    }

    [Fact]
    public void CanAcknowledge_rejects_account_that_is_not_a_room_member()
    {
        var snapshot = CreateSnapshot();

        Assert.False(AcknowledgeReliableBattleEventsHandler.CanAcknowledge(
            snapshot,
            "room-a",
            "room-a",
            "battle-a",
            "account-other"));
    }

    [Fact]
    public void CanAcknowledge_rejects_battle_mismatch()
    {
        var snapshot = CreateSnapshot();

        Assert.False(AcknowledgeReliableBattleEventsHandler.CanAcknowledge(
            snapshot,
            "room-a",
            "room-a",
            "battle-other",
            "account-a"));
    }

    [Theory]
    [InlineData(null, "room-a", "room-a", "battle-a", "account-a")]
    [InlineData("snapshot", null, "room-a", "battle-a", "account-a")]
    [InlineData("snapshot", "room-a", null, "battle-a", "account-a")]
    [InlineData("snapshot", "room-a", "room-a", null, "account-a")]
    [InlineData("snapshot", "room-a", "room-a", "battle-a", null)]
    public void CanAcknowledge_rejects_missing_authorization_context(
        string? snapshotMarker,
        string? mappedRoomId,
        string? requestedRoomId,
        string? requestedBattleId,
        string? accountId)
    {
        var snapshot = snapshotMarker == null ? null : CreateSnapshot();

        Assert.False(AcknowledgeReliableBattleEventsHandler.CanAcknowledge(
            snapshot,
            mappedRoomId,
            requestedRoomId,
            requestedBattleId,
            accountId));
    }

    [Fact]
    public void ToWireResponse_maps_accepted_ack()
    {
        var response = AcknowledgeReliableBattleEventsHandler.ToWireResponse(
            new ReliableBattleEventAckResult
            {
                Accepted = true,
                AcceptedSequence = 12,
                RequiresResync = false
            });

        Assert.True(response.Success);
        Assert.Equal(12L, response.AcceptedAckSequence);
        Assert.Equal(string.Empty, response.Message);
    }

    [Fact]
    public void ToWireResponse_maps_resync_as_unsuccessful_ack()
    {
        var response = AcknowledgeReliableBattleEventsHandler.ToWireResponse(
            new ReliableBattleEventAckResult
            {
                Accepted = true,
                AcceptedSequence = 7,
                RequiresResync = true
            });

        Assert.False(response.Success);
        Assert.Equal(7L, response.AcceptedAckSequence);
        Assert.Equal("resync required", response.Message);
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
