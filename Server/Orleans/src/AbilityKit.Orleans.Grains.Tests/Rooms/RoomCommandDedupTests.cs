using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class RoomCommandDedupTests
{
    [Fact]
    public void Find_WithMatchingCommandId_ReturnsEntry()
    {
        var entries = new List<RoomCommandDedupEntry>
        {
            new("a", "cmd-1", "BeginLoading", "", true, true, RoomOperationErrorCode.None, 5, 0)
        };

        var found = RoomCommandDedup.Find(entries, "a", "cmd-1");

        Assert.NotNull(found);
        Assert.Equal("cmd-1", found!.CommandId);
    }

    [Fact]
    public void Find_WithNullCommandId_ReturnsNull()
    {
        var entries = new List<RoomCommandDedupEntry>
        {
            new("a", "cmd-1", "BeginLoading", "", true, true, RoomOperationErrorCode.None, 5, 0)
        };

        Assert.Null(RoomCommandDedup.Find(entries, "a", null));
    }

    [Fact]
    public void Record_AddsEntryAndPreservesOthers()
    {
        var entries = new List<RoomCommandDedupEntry>
        {
            new("a", "old", "BeginLoading", "", true, true, RoomOperationErrorCode.None, 1, 0)
        };

        var result = RoomCommandDedup.Record(
            entries,
            "a",
            "new",
            "CancelLoading",
            RoomOperationResult.AppliedAt(2),
            nowUnixMs: 10);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, entry => entry.CommandId == "new");
        Assert.Contains(result, entry => entry.CommandId == "old");
    }

    [Fact]
    public void Record_WithNullCommandId_ReturnsOriginalList()
    {
        var entries = new List<RoomCommandDedupEntry>();

        var result = RoomCommandDedup.Record(
            entries,
            "a",
            null,
            "BeginLoading",
            RoomOperationResult.AppliedAt(1),
            nowUnixMs: 10);

        Assert.Empty(result);
    }

    [Fact]
    public void Record_ReplacesExistingEntryForSameCommandId()
    {
        var entries = new List<RoomCommandDedupEntry>
        {
            new("a", "cmd-1", "BeginLoading", "", true, true, RoomOperationErrorCode.None, 1, 0)
        };

        var result = RoomCommandDedup.Record(
            entries,
            "a",
            "cmd-1",
            "BeginLoading",
            RoomOperationResult.AppliedAt(2),
            nowUnixMs: 10);

        Assert.Single(result);
        Assert.Equal(2, result[0].AppliedRevision);
    }
}
