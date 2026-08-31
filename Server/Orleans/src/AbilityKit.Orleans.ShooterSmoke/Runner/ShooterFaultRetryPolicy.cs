internal static class ShooterFaultRetryPolicy
{
    public static TimeSpan GetBackoff(int attempt, TimeSpan initialDelay, TimeSpan maximumDelay)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        var initialMs = Math.Max(0d, initialDelay.TotalMilliseconds);
        var maximumMs = Math.Max(initialMs, maximumDelay.TotalMilliseconds);
        var multiplier = Math.Pow(2d, Math.Min(attempt - 1, 20));
        return TimeSpan.FromMilliseconds(Math.Min(maximumMs, initialMs * multiplier));
    }

    public static async Task<T> ExecuteAsync<T>(
        Func<int, Task<T>> operation,
        int recoverableFailureCount,
        TimeSpan initialDelay,
        TimeSpan maximumDelay,
        CancellationToken cancellationToken = default,
        Func<Exception, bool>? isRecoverable = null,
        Action<int, Exception, TimeSpan>? onRetry = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfNegative(recoverableFailureCount);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(attempt).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                attempt <= recoverableFailureCount &&
                !cancellationToken.IsCancellationRequested &&
                (isRecoverable?.Invoke(ex) ?? true))
            {
                var delay = GetBackoff(attempt, initialDelay, maximumDelay);
                onRetry?.Invoke(attempt, ex, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
