namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class RabbitMqConsumerOptions
{
    public string QueueName { get; set; } = "q.queue-service.events";

    public string ConsumerName { get; set; } = "queue-service.appointment-checked-in";

    public string RetryRoutePrefix { get; set; } = "queue-service";

    public string DeadLetterRoutingKey { get; set; } = "q.queue-service.dlq";

    public ushort PrefetchCount { get; set; } = 1;

    public int MaxRetryAttempts { get; set; } = 3;
}
