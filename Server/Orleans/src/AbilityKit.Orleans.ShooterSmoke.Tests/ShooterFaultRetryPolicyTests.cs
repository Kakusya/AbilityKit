using Xunit;

public sealed class ShooterFaultRetryPolicyTests
{
    [Theory]
    [InlineData(1, 25)]
    [InlineData(2, 50)]
    [InlineData(3, 100)]
    [InlineData(4, 100)]
    public void GetBackoffUsesBoundedExponentialDelay(int attempt, int expectedMilliseconds)
    {
        var delay = ShooterFaultRetryPolicy.GetBackoff(
            attempt,
            TimeSpan.FromMilliseconds(25),
            TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), delay);
    }

    [Fact]
    public async Task ExecuteAsyncRetriesConfiguredFailuresThenReturnsSuccess()
    {
        var attempts = new List<int>();
        var retries = new List<(int Attempt, string Message, TimeSpan Delay)>();

        var result = await ShooterFaultRetryPolicy.ExecuteAsync(
            attempt =>
            {
                attempts.Add(attempt);
                return attempt <= 3
                    ? Task.FromException<string>(new IOException($"failure-{attempt}"))
                    : Task.FromResult("recovered");
            },
            recoverableFailureCount: 3,
            initialDelay: TimeSpan.Zero,
            maximumDelay: TimeSpan.Zero,
            isRecoverable: static exception => exception is IOException,
            onRetry: (attempt, exception, delay) => retries.Add((attempt, exception.Message, delay)));

        Assert.Equal("recovered", result);
        Assert.Equal(new[] { 1, 2, 3, 4 }, attempts);
        Assert.Equal(new[] { 1, 2, 3 }, retries.Select(entry => entry.Attempt));
        Assert.All(retries, entry => Assert.Equal(TimeSpan.Zero, entry.Delay));
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotRetryNonRecoverableFailure()
    {
        var attempts = 0;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ShooterFaultRetryPolicy.ExecuteAsync<string>(
                _ =>
                {
                    attempts++;
                    return Task.FromException<string>(new InvalidOperationException("terminal"));
                },
                recoverableFailureCount: 3,
                initialDelay: TimeSpan.Zero,
                maximumDelay: TimeSpan.Zero,
                isRecoverable: static exception => exception is IOException));

        Assert.Equal("terminal", error.Message);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesCancellationWithoutRetry()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ShooterFaultRetryPolicy.ExecuteAsync<string>(
                _ =>
                {
                    attempts++;
                    return Task.FromCanceled<string>(cancellation.Token);
                },
                recoverableFailureCount: 3,
                initialDelay: TimeSpan.Zero,
                maximumDelay: TimeSpan.Zero,
                cancellationToken: cancellation.Token));

        Assert.Equal(1, attempts);
    }
}
