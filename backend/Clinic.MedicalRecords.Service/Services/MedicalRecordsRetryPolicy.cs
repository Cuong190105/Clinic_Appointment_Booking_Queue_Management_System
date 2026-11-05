using System.Globalization;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Clinic.MedicalRecords.Service.Services;

internal static class MedicalRecordsRetryPolicy
{
    internal const int MaximumRetries = 3;
    internal const string RetryExchange = "clinic.medical-records.retry";
    internal const string RetryRoutingKey = "medical-records.retry";

    private static readonly (string QueueName, int DelayMilliseconds)[] RetryQueues =
    [
        ("clinic.medical-records.retry.10s", 10_000),
        ("clinic.medical-records.retry.1m", 60_000),
        ("clinic.medical-records.retry.5m", 300_000)
    ];

    internal static IReadOnlyList<(string QueueName, int DelayMilliseconds)> Queues => RetryQueues;

    internal static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-medical-records-retry-count", out var value) || value is null)
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

    internal static bool ShouldDeadLetter(Exception exception, int retryCount)
        => exception is JsonException or FormatException || retryCount >= MaximumRetries;

    internal static string GetRetryQueueName(int retryCount)
        => RetryQueues[Math.Clamp(retryCount, 0, RetryQueues.Length - 1)].QueueName;

    internal static BasicProperties CreateRetryProperties(
        IReadOnlyBasicProperties source,
        int retryCount)
    {
        var headers = new Dictionary<string, object?>(source.Headers ?? new Dictionary<string, object?>())
        {
            ["x-medical-records-retry-count"] = retryCount
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
