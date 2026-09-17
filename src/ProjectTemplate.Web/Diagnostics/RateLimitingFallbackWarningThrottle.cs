namespace ProjectTemplate.Web.Diagnostics;

/// <summary>
/// Limits how often the rate-limiting unknown-client fallback warning is written, while counting the
/// occurrences that were suppressed between warnings.
/// </summary>
/// <remarks>
/// The fallback path runs on every affected request. Without throttling, a deployment where
/// <c>RemoteIpAddress</c> is never available would write one warning per request.
/// </remarks>
internal sealed class RateLimitingFallbackWarningThrottle
{
    /// <summary>
    /// The default minimum interval between fallback warnings.
    /// </summary>
    internal static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider;
    private readonly long _intervalTimestampTicks;
    private long _nextWarningTimestamp = long.MinValue;
    private long _suppressedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingFallbackWarningThrottle"/> class.
    /// </summary>
    /// <param name="timeProvider">The time source used to measure the warning interval.</param>
    /// <param name="interval">The minimum interval between warnings. Must be greater than zero.</param>
    internal RateLimitingFallbackWarningThrottle(TimeProvider timeProvider, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        _timeProvider = timeProvider;
        _intervalTimestampTicks = checked((long)(interval.TotalSeconds * timeProvider.TimestampFrequency));
    }

    /// <summary>
    /// Determines whether a fallback warning may be written now.
    /// </summary>
    /// <param name="suppressedCount">
    /// When this method returns <see langword="true"/>, the number of warnings suppressed since the previous
    /// warning; otherwise zero.
    /// </param>
    /// <returns><see langword="true"/> when the caller should write the warning; otherwise <see langword="false"/>.</returns>
    internal bool TryAcquire(out long suppressedCount)
    {
        long now = _timeProvider.GetTimestamp();
        long nextWarningTimestamp = Interlocked.Read(ref _nextWarningTimestamp);

        if (now >= nextWarningTimestamp &&
            Interlocked.CompareExchange(
                ref _nextWarningTimestamp,
                now + _intervalTimestampTicks,
                nextWarningTimestamp) == nextWarningTimestamp)
        {
            suppressedCount = Interlocked.Exchange(ref _suppressedCount, 0);
            return true;
        }

        _ = Interlocked.Increment(ref _suppressedCount);
        suppressedCount = 0;
        return false;
    }
}
