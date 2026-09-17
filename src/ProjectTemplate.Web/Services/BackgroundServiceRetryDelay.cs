namespace ProjectTemplate.Web.Services;

/// <summary>
/// Calculates the delay before a background service retries after consecutive failures.
/// </summary>
/// <remarks>
/// A fixed poll interval keeps a failing dependency under constant load and fills the log with one entry per cycle.
/// After a failure the delay doubles for each additional consecutive failure, up to a configured maximum, and returns
/// to the normal interval as soon as a cycle succeeds.
/// </remarks>
internal static class BackgroundServiceRetryDelay
{
    /// <summary>
    /// Calculates the delay before the next cycle.
    /// </summary>
    /// <param name="interval">The configured interval between successful cycles.</param>
    /// <param name="maximumRetryDelay">The maximum delay after consecutive failures.</param>
    /// <param name="consecutiveFailureCount">The number of consecutive failed cycles; zero after a success.</param>
    /// <returns>
    /// <paramref name="interval"/> when the previous cycle succeeded; otherwise the interval doubled once per
    /// additional consecutive failure, capped at <paramref name="maximumRetryDelay"/>.
    /// </returns>
    internal static TimeSpan Calculate(
        TimeSpan interval,
        TimeSpan maximumRetryDelay,
        int consecutiveFailureCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(consecutiveFailureCount);

        if (consecutiveFailureCount == 0)
        {
            return interval;
        }

        TimeSpan cap = maximumRetryDelay > interval ? maximumRetryDelay : interval;

        // Cap the exponent before multiplying so a long-running outage cannot overflow the calculation.
        int exponent = Math.Min(consecutiveFailureCount - 1, 16);
        double delayTicks = interval.Ticks * Math.Pow(2, exponent);

        return delayTicks >= cap.Ticks ? cap : TimeSpan.FromTicks((long)delayTicks);
    }
}
