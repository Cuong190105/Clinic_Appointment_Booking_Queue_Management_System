namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class RabbitMqOptions
{
    // Docker Compose overrides this with RabbitMq__Host=rabbitmq.
    // appsettings.json keeps localhost for running the API directly on the host.
    public string Host { get; set; } = "rabbitmq";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    public string Exchange { get; set; } = "clinic.events";

    public string DeadLetterQueue { get; set; } = "q.outbox.publish.dlq";

    public string DeadLetterRoutingKey { get; set; } = "outbox.publish-failed";

    public int PublisherBatchSize { get; set; } = 50;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int MaxPublishRetries { get; set; } = 3;
}
