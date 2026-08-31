using AbilityKit.Orleans.Contracts.Rooms;

namespace AbilityKit.Orleans.Grains.Rooms;

internal enum RoomLifecycleState
{
    Lobby = 0,
    Loading = 1,
    Starting = 2,
    InBattle = 3,
    Closing = 4,
    Closed = 5,
    Expired = 6
}

internal readonly record struct RoomLifecycleSnapshot(
    RoomLifecycleState State,
    bool IsOpenForLobbyActions,
    bool IsJoinable,
    bool HasBattle,
    bool ShouldRemoveFromDirectory);

internal static class RoomLifecyclePolicy
{
    public static RoomLifecycleSnapshot Evaluate(RoomPhase phase, string? battleId)
    {
        var state = phase switch
        {
            RoomPhase.Lobby => RoomLifecycleState.Lobby,
            RoomPhase.Loading => RoomLifecycleState.Loading,
            RoomPhase.Starting => RoomLifecycleState.Starting,
            RoomPhase.InBattle => RoomLifecycleState.InBattle,
            RoomPhase.Closing => RoomLifecycleState.Closing,
            RoomPhase.Closed => RoomLifecycleState.Closed,
            RoomPhase.Expired => RoomLifecycleState.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
        var hasBattle = !string.IsNullOrWhiteSpace(battleId);
        return new RoomLifecycleSnapshot(
            state,
            IsOpenForLobbyActions: state == RoomLifecycleState.Lobby,
            IsJoinable: state is RoomLifecycleState.Lobby or RoomLifecycleState.InBattle,
            HasBattle: hasBattle,
            ShouldRemoveFromDirectory: state is RoomLifecycleState.Closing or RoomLifecycleState.Closed or RoomLifecycleState.Expired);
    }

    public static RoomLifecycleSnapshot Evaluate(bool closed, string? battleId, int memberCount)
    {
        var phase = (closed, string.IsNullOrWhiteSpace(battleId), memberCount) switch
        {
            (false, true, _) => RoomPhase.Lobby,
            (false, false, _) => RoomPhase.Starting,
            (true, false, _) => RoomPhase.InBattle,
            (true, true, > 0) => RoomPhase.Closing,
            (true, true, 0) => RoomPhase.Closed
        };
        return Evaluate(phase, battleId);
    }
}
