namespace AbilityKit.Game.Cooking.FlowAcceptance;

// Independent cleanup deadline. It owns this session only, never processes from other runs.
public sealed class FlowResourceScope(IFlowSession session)
{
    private Task<CleanupResult>? close;
    public Task<CleanupResult> CloseAsync(int timeoutMs) => close ??= CloseOnceAsync(timeoutMs);

    private async Task<CleanupResult> CloseOnceAsync(int timeoutMs)
    {
        using var cleanup = new CancellationTokenSource(Math.Max(1, timeoutMs));
        try
        {
            // Sessions must honor this token and finish their owned work before returning.
            return await session.CloseAsync(timeoutMs, cleanup.Token);
        }
        catch (Exception e)
        {
            return new(CleanupState.Incomplete, Array.Empty<ResourceOutcome>(), new[] { e.GetType().Name });
        }
    }
}
