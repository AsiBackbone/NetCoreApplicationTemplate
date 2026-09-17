using ProjectTemplate.Web.Services;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Verifies the retry delay applied by the audit background services after consecutive failures.
/// </summary>
public sealed class BackgroundServiceRetryDelayTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _maximumRetryDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Verifies that a successful cycle keeps the configured interval.
    /// </summary>
    [Fact]
    public void Calculate_NoFailures_ReturnsInterval()
    {
        Assert.Equal(_interval, BackgroundServiceRetryDelay.Calculate(_interval, _maximumRetryDelay, 0));
    }

    /// <summary>
    /// Verifies that the delay doubles for each additional consecutive failure.
    /// </summary>
    /// <param name="consecutiveFailureCount">The number of consecutive failed cycles.</param>
    /// <param name="expectedSeconds">The expected delay in seconds.</param>
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    public void Calculate_ConsecutiveFailures_DoublesDelay(int consecutiveFailureCount, double expectedSeconds)
    {
        TimeSpan delay = BackgroundServiceRetryDelay.Calculate(
            _interval,
            _maximumRetryDelay,
            consecutiveFailureCount);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    /// <summary>
    /// Verifies that the delay is capped at the configured maximum.
    /// </summary>
    [Fact]
    public void Calculate_LongOutage_IsCappedAtMaximumRetryDelay()
    {
        Assert.Equal(
            _maximumRetryDelay,
            BackgroundServiceRetryDelay.Calculate(_interval, _maximumRetryDelay, 20));
        Assert.Equal(
            _maximumRetryDelay,
            BackgroundServiceRetryDelay.Calculate(_interval, _maximumRetryDelay, int.MaxValue));
    }

    /// <summary>
    /// Verifies that a maximum below the interval never shortens the configured interval.
    /// </summary>
    [Fact]
    public void Calculate_MaximumBelowInterval_UsesInterval()
    {
        Assert.Equal(
            _interval,
            BackgroundServiceRetryDelay.Calculate(_interval, TimeSpan.FromSeconds(1), 5));
    }

    /// <summary>
    /// Verifies that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void Calculate_InvalidArguments_Throw()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BackgroundServiceRetryDelay.Calculate(TimeSpan.Zero, _maximumRetryDelay, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BackgroundServiceRetryDelay.Calculate(_interval, _maximumRetryDelay, -1));
    }
}
