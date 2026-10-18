using RabbitMQ.Client;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public static class RabbitMqRetryPolicy
{
    public static int GetAttempt(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null ||
            !properties.Headers.TryGetValue("x-retry-attempt", out var value))
        {
            return 0;
        }

        return value switch
        {
            byte[] bytes when int.TryParse(
                System.Text.Encoding.UTF8.GetString(bytes), out var attempt) => attempt,
            int attempt => attempt,
            long attempt => checked((int)attempt),
            _ => 0
        };
    }

    public static string GetRetryExchange(int nextAttempt) => nextAttempt switch
    {
        1 => "clinic.retry.10s",
        2 => "clinic.retry.1m",
        3 => "clinic.retry.5m",
        _ => throw new ArgumentOutOfRangeException(nameof(nextAttempt))
    };
}
