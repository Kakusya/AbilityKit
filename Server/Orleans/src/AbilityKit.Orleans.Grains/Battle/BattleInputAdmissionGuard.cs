using AbilityKit.Orleans.Contracts.Battle;

namespace AbilityKit.Orleans.Grains.Battle;

internal enum BattleInputGuardStatus
{
    Accepted,
    AcceptedLegacy,
    RejectedDuplicate,
    RejectedTooOld,
    RejectedRateLimited,
    RejectedCapacity
}

internal readonly record struct BattleInputGuardResult(BattleInputGuardStatus Status)
{
    public bool Accepted => Status is BattleInputGuardStatus.Accepted or BattleInputGuardStatus.AcceptedLegacy;

    public string StatusCode => Status switch
    {
        BattleInputGuardStatus.RejectedDuplicate => BattleResultStatusCodes.RejectedDuplicateSequence,
        BattleInputGuardStatus.RejectedTooOld => BattleResultStatusCodes.RejectedSequenceTooOld,
        BattleInputGuardStatus.RejectedRateLimited => BattleResultStatusCodes.RejectedRateLimited,
        BattleInputGuardStatus.RejectedCapacity => BattleResultStatusCodes.RejectedRateLimited,
        _ => string.Empty
    };
}

internal sealed class BattleInputAdmissionGuard
{
    private readonly BattleInputSecurityOptions _options;
    private readonly Dictionary<uint, PlayerState> _players = new();

    public BattleInputAdmissionGuard(BattleInputSecurityOptions? options = null)
    {
        _options = (options ?? new BattleInputSecurityOptions()).Snapshot();
        var failures = BattleInputSecurityOptions.GetValidationFailures(_options);
        if (failures.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", failures), nameof(options));
        }
    }

    internal int TrackedPlayerCount => _players.Count;

    internal int GetTrackedSequenceCount(uint playerId) =>
        _players.TryGetValue(playerId, out var state) ? state.Sequences.Count : 0;

    public BattleInputGuardResult Check(uint playerId, ulong sequence, long nowTicks)
    {
        if (!_players.TryGetValue(playerId, out var state))
        {
            if (_players.Count >= _options.MaxBattleTrackedPlayers)
            {
                return new BattleInputGuardResult(BattleInputGuardStatus.RejectedCapacity);
            }

            state = new PlayerState(_options.BurstInputs, nowTicks);
            _players.Add(playerId, state);
        }

        if (sequence != 0)
        {
            if (state.Sequences.Contains(sequence))
            {
                return new BattleInputGuardResult(BattleInputGuardStatus.RejectedDuplicate);
            }

            if (state.HighestSequence > 0
                && sequence < state.HighestSequence
                && state.HighestSequence - sequence >= (ulong)_options.ReplayWindowSize)
            {
                return new BattleInputGuardResult(BattleInputGuardStatus.RejectedTooOld);
            }
        }

        if (!TryConsumeToken(state, nowTicks))
        {
            return new BattleInputGuardResult(BattleInputGuardStatus.RejectedRateLimited);
        }

        return new BattleInputGuardResult(
            sequence == 0 ? BattleInputGuardStatus.AcceptedLegacy : BattleInputGuardStatus.Accepted);
    }

    public void RecordAccepted(uint playerId, ulong sequence)
    {
        if (sequence == 0 || !_players.TryGetValue(playerId, out var state))
        {
            return;
        }

        state.Sequences.Add(sequence);
        if (sequence > state.HighestSequence)
        {
            state.HighestSequence = sequence;
        }

        var minimum = state.HighestSequence >= (ulong)_options.ReplayWindowSize
            ? state.HighestSequence - (ulong)_options.ReplayWindowSize + 1
            : 1;
        state.Sequences.RemoveWhere(value => value < minimum);
    }

    public void Clear() => _players.Clear();

    private bool TryConsumeToken(PlayerState state, long nowTicks)
    {
        var elapsedTicks = Math.Max(0, nowTicks - state.LastRefillTicks);
        state.LastRefillTicks = nowTicks;
        state.Tokens = Math.Min(
            _options.BurstInputs,
            state.Tokens + elapsedTicks * (double)_options.InputsPerSecond / TimeSpan.TicksPerSecond);
        if (state.Tokens < 1d)
        {
            return false;
        }

        state.Tokens -= 1d;
        return true;
    }

    private sealed class PlayerState
    {
        public PlayerState(double tokens, long nowTicks)
        {
            Tokens = tokens;
            LastRefillTicks = nowTicks;
        }

        public HashSet<ulong> Sequences { get; } = new();
        public ulong HighestSequence { get; set; }
        public double Tokens { get; set; }
        public long LastRefillTicks { get; set; }
    }
}
