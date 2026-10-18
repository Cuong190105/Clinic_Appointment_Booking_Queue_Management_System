using RabbitMQ.Client;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public static class RabbitMqTopology
{
    public static async Task DeclareQueueServiceAsync(
        IChannel channel,
        RabbitMqOptions options,
        RabbitMqConsumerOptions consumerOptions,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        foreach (var retryExchange in new[] { "clinic.retry.10s", "clinic.retry.1m", "clinic.retry.5m" })
        {
            await channel.ExchangeDeclareAsync(
                retryExchange,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        await channel.ExchangeDeclareAsync(
            "clinic.dead-letter",
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            consumerOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "clinic.dead-letter",
                ["x-dead-letter-routing-key"] = consumerOptions.DeadLetterRoutingKey
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            consumerOptions.QueueName,
            options.Exchange,
            "appointment.checked-in",
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            consumerOptions.QueueName,
            options.Exchange,
            $"{consumerOptions.RetryRoutePrefix}.#",
            cancellationToken: cancellationToken);

        var retryQueues = new Dictionary<string, int>
        {
            ["clinic.retry.10s"] = 10_000,
            ["clinic.retry.1m"] = 60_000,
            ["clinic.retry.5m"] = 300_000
        };

        foreach (var retry in retryQueues)
        {
            var queueName = $"q.queue-service.retry.{retry.Key[("clinic.retry.").Length..]}";

            await channel.QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = retry.Value,
                    ["x-dead-letter-exchange"] = options.Exchange,
                    // Keep this explicit and stable. Existing queues in local
                    // RabbitMQ were created with an empty routing key.
                    ["x-dead-letter-routing-key"] = string.Empty
                },
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queueName,
                retry.Key,
                $"{consumerOptions.RetryRoutePrefix}.#",
                cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(
            consumerOptions.DeadLetterRoutingKey,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            consumerOptions.DeadLetterRoutingKey,
            "clinic.dead-letter",
            consumerOptions.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }
}
