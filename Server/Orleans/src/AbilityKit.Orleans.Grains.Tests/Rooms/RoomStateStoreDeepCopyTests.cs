using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomStateStoreDeepCopyTests
{
    [Fact]
    public async Task WriteThenMutateOriginal_DoesNotAffectSubsequentRead()
    {
        var store = new InMemoryRoomStateStore();
        var state = CreateState("room-1", "a", "b");

        await store.WriteRuntimeStateAsync("room-1", state, CancellationToken.None);

        state.Members[0] = state.Members[0] with { AccountId = "tampered" };
        state.GameplayState.Payload[0] = 0xFF;
        state.Launch.LockedRoster.Add("injected");

        var read = await store.TryGetRuntimeStateAsync("room-1", CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal("a", read!.Members[0].AccountId);
        Assert.NotEqual(0xFF, read.GameplayState.Payload[0]);
        Assert.DoesNotContain("injected", read.Launch.LockedRoster);
    }

    [Fact]
    public async Task ReadTwice_ReturnsIndependentCopies()
    {
        var store = new InMemoryRoomStateStore();
        await store.WriteRuntimeStateAsync("room-2", CreateState("room-2", "a"), CancellationToken.None);

        var first = await store.TryGetRuntimeStateAsync("room-2", CancellationToken.None);
        first!.Members[0] = first.Members[0] with { AccountId = "mutated" };

        var second = await store.TryGetRuntimeStateAsync("room-2", CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal("a", second!.Members[0].AccountId);
    }

    [Fact]
    public async Task GetOrCreateNumericRoomId_UsesSharedProtocolIdAndSupportsReverseLookup()
    {
        var store = new InMemoryRoomStateStore();
        const string roomId = "room-numeric-contract";

        var numericRoomId = await store.GetOrCreateNumericRoomIdAsync(roomId, CancellationToken.None);
        var resolvedRoomId = await store.TryGetRoomIdAsync(numericRoomId, CancellationToken.None);

        Assert.Equal(RoomGatewayIds.CreateNumericRoomId(roomId), numericRoomId);
        Assert.Equal(roomId, resolvedRoomId);
    }

    [Fact]
    public async Task UpsertRoom_RegistersWireNumericRoomIdForReverseLookup()
    {
        var store = new InMemoryRoomStateStore();
        var state = CreateState("room-upsert-mapping", "owner");
        var numericRoomId = RoomGatewayIds.CreateNumericRoomId(state.Summary.RoomId);

        await store.UpsertRoomAsync("dir", state.Summary, CancellationToken.None);

        var resolvedRoomId = await store.TryGetRoomIdAsync(numericRoomId, CancellationToken.None);
        Assert.Equal(state.Summary.RoomId, resolvedRoomId);
    }

    [Fact]
    public async Task RemoveRuntimeState_ClearsRecord()
    {
        var store = new InMemoryRoomStateStore();
        await store.WriteRuntimeStateAsync("room-3", CreateState("room-3", "a"), CancellationToken.None);

        await store.RemoveRuntimeStateAsync("room-3", CancellationToken.None);

        var read = await store.TryGetRuntimeStateAsync("room-3", CancellationToken.None);
        Assert.Null(read);
    }

    private static RoomPersistentState CreateState(string roomId, params string[] accountIds)
    {
        var summary = new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: roomId,
            RoomType: "moba",
            Title: "Room",
            IsPublic: true,
            MaxPlayers: 8,
            PlayerCount: accountIds.Length,
            OwnerAccountId: accountIds.Length == 0 ? "owner" : accountIds[0],
            CreatedAtUnixMs: 0,
            Tags: null);

        var members = accountIds
            .Select((accountId, index) => new RoomPersistentMember(
                accountId,
                new RoomMemberState(true, 0, 0, false, index + 1)))
            .ToList();

        return new RoomPersistentState(
            SchemaVersion: RoomPersistentState.CurrentSchemaVersion,
            Summary: summary,
            DirectoryKey: "dir",
            Phase: RoomPhase.Lobby,
            PhaseReason: string.Empty,
            Members: members,
            NextJoinOrdinal: accountIds.Length + 1,
            GameplayState: new RoomGameplayPersistentState("moba.room.v1", 1, new byte[] { 1, 2, 3 }),
            Revision: 1,
            EventSequence: 1,
            Launch: new RoomLaunchPersistentState(0, 0, 0, null, new List<string>()),
            BattleCommit: new RoomBattleCommitPersistentState(0, null, RoomBattleCommitStatus.None, null, null, 0, null, 0, null),
            CommandDedupEntries: new List<RoomCommandDedupEntry>(),
            TerminalReason: null,
            UpdatedAtUnixMs: 0);
    }
}
