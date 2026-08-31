using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Grains.Rooms;

internal sealed class RoomMemberTracker
{
    private readonly HashSet<string> _members = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoomMemberState> _memberStates = new(StringComparer.Ordinal);

    public int Count => _members.Count;

    public bool HasMemberStates => _memberStates.Count > 0;

    public bool Contains(string accountId)
    {
        return _members.Contains(accountId);
    }

    public bool Add(string accountId)
    {
        return _members.Add(accountId);
    }

    public bool Remove(string accountId)
    {
        return _members.Remove(accountId);
    }

    public void RemoveMemberAndState(string accountId)
    {
        _members.Remove(accountId);
        _memberStates.Remove(accountId);
    }

    public void Clear()
    {
        _members.Clear();
        _memberStates.Clear();
    }

    public List<string> MembersSnapshot()
    {
        return _members
            .OrderBy(accountId => _memberStates.TryGetValue(accountId, out var state) ? state.JoinOrdinal : long.MaxValue)
            .ThenBy(accountId => accountId, StringComparer.Ordinal)
            .ToList();
    }

    public void Restore(IEnumerable<KeyValuePair<string, RoomMemberState>> members)
    {
        Clear();
        foreach (var member in members)
        {
            _members.Add(member.Key);
            _memberStates[member.Key] = member.Value;
        }
    }

    public Dictionary<string, RoomMemberState>? CloneMemberStates()
    {
        if (_memberStates.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, RoomMemberState>(_memberStates, StringComparer.Ordinal);
    }

    public void Touch(string accountId, bool isOnline, bool? isBot = null)
    {
        var now = DateTime.UtcNow.Ticks;
        _memberStates.TryGetValue(accountId, out var previousState);
        _memberStates[accountId] = new RoomMemberState(
            isOnline,
            now,
            isOnline ? 0L : now,
            isBot ?? previousState?.IsBot ?? false);
    }

    public void MarkOffline(string accountId)
    {
        var now = DateTime.UtcNow.Ticks;
        _memberStates.TryGetValue(accountId, out var previousState);
        _memberStates[accountId] = new RoomMemberState(false, now, now, previousState?.IsBot ?? false);
    }

    public IReadOnlyList<string> CollectExpiredOfflineMembers(RoomSummary summary, long nowTicks)
    {
        var offlineTimeoutSeconds = ReadIntTag(summary, RoomTagKeys.OfflineTimeoutSeconds, 0);
        if (offlineTimeoutSeconds <= 0 || _memberStates.Count == 0)
        {
            return Array.Empty<string>();
        }

        var timeoutTicks = TimeSpan.FromSeconds(offlineTimeoutSeconds).Ticks;
        var expired = new List<string>();

        foreach (var kv in _memberStates)
        {
            if (kv.Value.IsOnline || kv.Value.OfflineSinceTicks <= 0)
            {
                continue;
            }

            if (nowTicks - kv.Value.OfflineSinceTicks >= timeoutTicks)
            {
                expired.Add(kv.Key);
            }
        }

        return expired;
    }

    public void RemoveMembersAndStates(IEnumerable<string> accountIds)
    {
        foreach (var accountId in accountIds)
        {
            RemoveMemberAndState(accountId);
        }
    }

    internal void SetMemberStateForTests(string accountId, RoomMemberState state)
    {
        _memberStates[accountId] = state;
    }

    private static int ReadIntTag(RoomSummary summary, string key, int fallback)
    {
        if (summary.Tags != null && summary.Tags.TryGetValue(key, out var value) && int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
