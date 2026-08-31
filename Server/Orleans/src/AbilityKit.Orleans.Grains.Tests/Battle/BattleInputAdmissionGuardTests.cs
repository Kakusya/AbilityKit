using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class BattleInputAdmissionGuardTests
{
    [Fact]
    public void Check_RejectsDuplicateOnlyAfterAcceptanceIsRecorded()
    {
        var guard = CreateGuard();
        Assert.Equal(BattleInputGuardStatus.Accepted, guard.Check(1, 7, 0).Status);
        Assert.Equal(BattleInputGuardStatus.Accepted, guard.Check(1, 7, 0).Status);

        guard.RecordAccepted(1, 7);
        var duplicate = guard.Check(1, 7, 0);

        Assert.Equal(BattleInputGuardStatus.RejectedDuplicate, duplicate.Status);
        Assert.Equal(BattleResultStatusCodes.RejectedDuplicateSequence, duplicate.StatusCode);
    }

    [Fact]
    public void Check_AllowsOutOfOrderInsideWindowAndRejectsBoundary()
    {
        var guard = CreateGuard();
        Assert.True(guard.Check(1, 20, 0).Accepted);
        guard.RecordAccepted(1, 20);
        Assert.Equal(BattleInputGuardStatus.Accepted, guard.Check(1, 19, 0).Status);

        var tooOld = guard.Check(1, 12, 0);

        Assert.Equal(BattleInputGuardStatus.RejectedTooOld, tooOld.Status);
        Assert.Equal(BattleResultStatusCodes.RejectedSequenceTooOld, tooOld.StatusCode);
    }

    [Fact]
    public void Check_SequenceZeroUsesLegacyPathWithoutReplayState()
    {
        var guard = CreateGuard();

        Assert.Equal(BattleInputGuardStatus.AcceptedLegacy, guard.Check(1, 0, 0).Status);
        Assert.Equal(BattleInputGuardStatus.AcceptedLegacy, guard.Check(1, 0, 0).Status);
        Assert.Equal(0, guard.GetTrackedSequenceCount(1));
    }

    [Fact]
    public void Check_RateLimitsBurstAndRefillsTokens()
    {
        var guard = CreateGuard(burstInputs: 2, inputsPerSecond: 2);
        Assert.True(guard.Check(1, 1, 0).Accepted);
        Assert.True(guard.Check(1, 2, 0).Accepted);

        var limited = guard.Check(1, 3, 0);

        Assert.Equal(BattleInputGuardStatus.RejectedRateLimited, limited.Status);
        Assert.Equal(BattleResultStatusCodes.RejectedRateLimited, limited.StatusCode);
        Assert.True(guard.Check(1, 3, TimeSpan.TicksPerSecond / 2).Accepted);
    }

    [Fact]
    public void Check_DuplicateDoesNotConsumeRateLimitToken()
    {
        var guard = CreateGuard(burstInputs: 2, inputsPerSecond: 1);
        Assert.True(guard.Check(1, 1, 0).Accepted);
        guard.RecordAccepted(1, 1);

        for (var index = 0; index < 10; index++)
        {
            Assert.Equal(BattleInputGuardStatus.RejectedDuplicate, guard.Check(1, 1, 0).Status);
        }

        Assert.True(guard.Check(1, 2, 0).Accepted);
        Assert.Equal(BattleInputGuardStatus.RejectedRateLimited, guard.Check(1, 3, 0).Status);
    }

    [Fact]
    public void RecordAccepted_BoundsRetainedReplayWindow()
    {
        var guard = CreateGuard(replayWindowSize: 8);
        for (ulong sequence = 1; sequence <= 64; sequence++)
        {
            Assert.True(guard.Check(1, sequence, (long)sequence * TimeSpan.TicksPerSecond).Accepted);
            guard.RecordAccepted(1, sequence);
        }

        Assert.Equal(8, guard.GetTrackedSequenceCount(1));
        Assert.Equal(
            BattleInputGuardStatus.RejectedTooOld,
            guard.Check(1, 56, 65 * TimeSpan.TicksPerSecond).Status);
    }

    [Fact]
    public void Check_BoundsPlayersAndClearReleasesState()
    {
        var guard = CreateGuard(maxTrackedPlayers: 2);
        Assert.True(guard.Check(1, 1, 0).Accepted);
        Assert.True(guard.Check(2, 1, 0).Accepted);

        var overCapacity = guard.Check(3, 1, 0);

        Assert.Equal(BattleInputGuardStatus.RejectedCapacity, overCapacity.Status);
        Assert.Equal(BattleResultStatusCodes.RejectedRateLimited, overCapacity.StatusCode);
        Assert.Equal(2, guard.TrackedPlayerCount);
        guard.Clear();
        Assert.Equal(0, guard.TrackedPlayerCount);
        Assert.True(guard.Check(3, 1, 0).Accepted);
    }

    private static BattleInputAdmissionGuard CreateGuard(
        int replayWindowSize = 8,
        int inputsPerSecond = 100,
        int burstInputs = 100,
        int maxTrackedPlayers = 4) =>
        new(new BattleInputSecurityOptions
        {
            ReplayWindowSize = replayWindowSize,
            InputsPerSecond = inputsPerSecond,
            BurstInputs = burstInputs,
            MaxBattleTrackedPlayers = maxTrackedPlayers
        });
}
