using System.Globalization;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Clinic.Appointment.Service.Services;

public static class AppointmentRetryPolicy
{
    public const int MaximumRetries = 3;
    public const string ExchangeName = "clinic.events";
    public const string MainQueueName = "clinic.appointment.events.v2";
    public const string DeadLetterExchange = "clinic.events.dlx";
    public const string DeadLetterQueueName = "clinic.appointment.events.dead";
    public const string RetryExchange = "clinic.appointment.retry";
    public const string RetryRoutingKey = "appointment.retry";
    public const string RetryCountHeader = "x-appointment-retry-count";
    public const string OriginalRoutingKeyHeader = "x-original-routing-key";

    private static readonly (string QueueName, int DelayMilliseconds)[] RetryQueues =
    [
        ("clinic.appointment.retry.5s", 5_000),
        ("clinic.appointment.retry.30s", 30_000),
        ("clinic.appointment.retry.2m", 120_000)
    ];

    public static IReadOnlyList<(string QueueName, int DelayMilliseconds)> Queues => RetryQueues;

    public static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out var value) || value is null)
            return 0;

        try
        {
            return value is byte[] bytes
                ? int.Parse(Encoding.UTF8.GetString(bytes), CultureInfo.InvariantCulture)
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    public static bool ShouldDeadLetter(Exception exception, int retryCount)
        => exception is JsonException or FormatException || retryCount >= MaximumRetries;

    public static string GetRetryQueueName(int retryCount)
        => RetryQueues[Math.Clamp(retryCount, 0, RetryQueues.Length - 1)].QueueName;

    public static string GetEffectiveRoutingKey(string deliveryRoutingKey, IDictionary<string, object?>? headers)
    {
        if (!string.Equals(deliveryRoutingKey, RetryRoutingKey, StringComparison.Ordinal))
            return deliveryRoutingKey;

        if (headers is null || !headers.TryGetValue(OriginalRoutingKeyHeader, out var value) || value is null)
            return deliveryRoutingKey;

        var resolved = value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value.ToString()
        };

        return string.IsNullOrWhiteSpace(resolved) ? deliveryRoutingKey : resolved;
    }

    public static BasicProperties CreateRetryProperties(
        IReadOnlyBasicProperties source,
        string originalRoutingKey,
        int nextRetryCount)
    {
        var headers = new Dictionary<string, object?>(source.Headers ?? new Dictionary<string, object?>())
        {
            [RetryCountHeader] = nextRetryCount,
            [OriginalRoutingKeyHeader] = originalRoutingKey
        };

        return new BasicProperties
        {
            Persistent = true,
            ContentType = source.ContentType,
            MessageId = source.MessageId,
            CorrelationId = source.CorrelationId,
            Type = source.Type,
            Headers = headers
        };
    }
}
