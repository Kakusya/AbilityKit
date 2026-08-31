namespace AbilityKit.Orleans.Grains.Battle;

internal sealed class StateSyncBaselineRefreshState
{
    public bool IsPending { get; private set; }

    public bool IsInFlight { get; private set; }

    public int CoalescedRequestCount { get; private set; }

    public void Request()
    {
        if (IsPending || IsInFlight)
        {
            CoalescedRequestCount++;
        }

        IsPending = true;
    }

    public bool TryBegin()
    {
        if (!IsPending || IsInFlight)
        {
            return false;
        }

        IsPending = false;
        IsInFlight = true;
        return true;
    }

    public void Complete(bool succeeded)
    {
        if (!IsInFlight)
        {
            return;
        }

        IsInFlight = false;
        if (!succeeded)
        {
            IsPending = true;
        }
    }

    public void Clear()
    {
        IsPending = false;
        IsInFlight = false;
        CoalescedRequestCount = 0;
    }
}
