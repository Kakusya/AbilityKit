using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;

namespace AbilityKit.Orleans.Grains.Rooms;

/// <summary>
/// 基于 (accountId, commandId) 的命令去重工具。同 account+commandId 返回缓存结果。
/// </summary>
internal static class RoomCommandDedup
{
    public const int MaxEntries = 64;

    public static RoomCommandDedupEntry? Find(IReadOnlyList<RoomCommandDedupEntry> entries, string accountId, string? commandId)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            return null;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.Equals(entry.AccountId, accountId, StringComparison.Ordinal) &&
                string.Equals(entry.CommandId, commandId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    public static List<RoomCommandDedupEntry> Record(
        IReadOnlyList<RoomCommandDedupEntry> entries,
        string accountId,
        string? commandId,
        string commandName,
        RoomOperationResult result,
        long nowUnixMs)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            return entries as List<RoomCommandDedupEntry> ?? entries.ToList();
        }

        var entry = new RoomCommandDedupEntry(
            accountId,
            commandId,
            commandName,
            PayloadHash: string.Empty,
            result.Success,
            result.Applied,
            result.ErrorCode,
            result.RoomRevision,
            nowUnixMs);

        var next = new List<RoomCommandDedupEntry>(entries.Count + 1) { entry };
        var kept = 0;
        for (var index = 0; index < entries.Count && kept < MaxEntries - 1; index++)
        {
            var existing = entries[index];
            if (string.Equals(existing.AccountId, accountId, StringComparison.Ordinal) &&
                string.Equals(existing.CommandId, commandId, StringComparison.Ordinal))
            {
                continue;
            }

            next.Add(existing);
            kept++;
        }

        return next;
    }
}
