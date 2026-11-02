namespace Clinic.Notification.Service.Services.Delivery;

public static class DeliveryBackoffStrategy
{
    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(10);

    public static DateTimeOffset CalculateNextAttemptAt(
        int attemptNumber,
        DateTimeOffset fromTime,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        var b = baseDelay ?? DefaultBaseDelay;
        var m = maxDelay ?? DefaultMaxDelay;

        // Bounded exponential: base * 2^(attempt - 1)
        var factor = Math.Pow(2, Math.Max(0, attemptNumber - 1));
        var rawDelayMilliseconds = b.TotalMilliseconds * factor;

        var clampedDelay = TimeSpan.FromMilliseconds(Math.Min(rawDelayMilliseconds, m.TotalMilliseconds));
        return fromTime.Add(clampedDelay);
    }
}

