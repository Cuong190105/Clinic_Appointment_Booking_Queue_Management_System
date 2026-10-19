namespace Clinic.Infrastructure.Sqlserver.Outbox;

public static class OutboxPublishPolicy
{
    public static bool ShouldDeadLetter(int retryCount, int maxRetries) =>
        retryCount >= maxRetries;

    public static DateTimeOffset? GetNextAttemptAt(
        int retryCount,
        int maxRetries,
        DateTimeOffset now)
    {
        if (retryCount >= maxRetries)
        {
            return null;
        }

        var delay = retryCount switch
        {
            1 => TimeSpan.FromSeconds(10),
            2 => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(5)
        };

        return now.Add(delay);
    }
}
