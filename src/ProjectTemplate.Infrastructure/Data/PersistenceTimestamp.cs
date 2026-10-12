namespace ProjectTemplate.Infrastructure.Data;

internal static class PersistenceTimestamp
{
    internal const int Precision = 3;

    private const long _ticksPerMillisecond = TimeSpan.TicksPerMillisecond;

    internal static DateTime UtcNow()
    {
        return NormalizeUtc(DateTime.UtcNow);
    }

    internal static DateTime NormalizeUtc(DateTime value)
    {
        // Local values are converted; Utc values pass through SpecifyKind unchanged; Unspecified values are treated
        // as UTC. A conditional replaces the switch because IDE0072 requires a switch to name all three kinds, which
        // leaves its discard arm unreachable.
        DateTime utcValue = value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        long normalizedTicks = utcValue.Ticks - (utcValue.Ticks % _ticksPerMillisecond);

        return new DateTime(normalizedTicks, DateTimeKind.Utc);
    }

    internal static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        DateTimeOffset utcValue = value.ToUniversalTime();
        long normalizedTicks = utcValue.Ticks - (utcValue.Ticks % _ticksPerMillisecond);

        return new DateTimeOffset(normalizedTicks, TimeSpan.Zero);
    }
}
