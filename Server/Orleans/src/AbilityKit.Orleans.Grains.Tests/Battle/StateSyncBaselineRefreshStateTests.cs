using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class StateSyncBaselineRefreshStateTests
{
    [Fact]
    public void MultipleGapsCoalesceIntoOnePendingRequest()
    {
        var state = new StateSyncBaselineRefreshState();

        state.Request();
        state.Request();
        state.Request();

        Assert.True(state.IsPending);
        Assert.False(state.IsInFlight);
        Assert.Equal(2, state.CoalescedRequestCount);
        Assert.True(state.TryBegin());
        Assert.False(state.TryBegin());
    }

    [Fact]
    public void FailureRestoresPendingAndSuccessClearsIt()
    {
        var state = new StateSyncBaselineRefreshState();
        state.Request();
        Assert.True(state.TryBegin());

        state.Complete(succeeded: false);

        Assert.True(state.IsPending);
        Assert.False(state.IsInFlight);
        Assert.True(state.TryBegin());
        state.Request();
        state.Complete(succeeded: true);

        Assert.True(state.IsPending);
        Assert.False(state.IsInFlight);
        Assert.True(state.TryBegin());
        state.Complete(succeeded: true);
        Assert.False(state.IsPending);
    }

    [Fact]
    public void ClearResetsLifecycleState()
    {
        var state = new StateSyncBaselineRefreshState();
        state.Request();
        Assert.True(state.TryBegin());
        state.Request();

        state.Clear();

        Assert.False(state.IsPending);
        Assert.False(state.IsInFlight);
        Assert.Equal(0, state.CoalescedRequestCount);
        Assert.False(state.TryBegin());
    }
}
