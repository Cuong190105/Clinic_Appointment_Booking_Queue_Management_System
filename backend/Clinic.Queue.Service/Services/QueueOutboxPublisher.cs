namespace Clinic.Queue.Service.Services;

using System.Text;
using Clinic.Queue.Service.Enums;
using Clinic.Queue.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public class QueueOutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QueueOutboxPublisher> _logger;

    public QueueOutboxPublisher(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<QueueOutboxPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.GetValue<bool>("SkipBackgroundWorkers"))
        {
            _logger.LogInformation("QueueOutboxPublisher is disabled by SkipBackgroundWorkers.");
            return;
        }

        _logger.LogInformation("QueueOutboxPublisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during Queue outbox publishing cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("QueueOutboxPublisher stopped.");
    }

    private async Task PublishPendingOutboxMessagesAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        var host = _configuration["RabbitMq:Host"] ?? _configuration["RabbitMQ__Host"] ?? _configuration["RabbitMq:HostName"] ?? "rabbitmq";
        var portStr = _configuration["RabbitMq:Port"] ?? _configuration["RabbitMQ__Port"] ?? "5672";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 5672;

        var username = _configuration["RabbitMq:Username"] ?? _configuration["RabbitMQ__Username"] ?? _configuration["RabbitMq:UserName"] ?? "clinic";
        var password = _configuration["RabbitMq:Password"] ?? _configuration["RabbitMQ__Password"] ?? "clinic-dev-password";
        var virtualHost = _configuration["RabbitMq:VirtualHost"] ?? _configuration["RabbitMQ__VirtualHost"] ?? "/";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            VirtualHost = virtualHost,
            ClientProvidedName = "clinic-queue-outbox-publisher"
        };

        IConnection? connection = null;
        IChannel? channel = null;

        try
        {
            connection = await factory.CreateConnectionAsync(ct);
            channel = await connection.CreateChannelAsync(cancellationToken: ct);

            const string exchangeName = "clinic.events";
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct);

            foreach (var message in pendingMessages)
            {
                var routingKey = string.IsNullOrWhiteSpace(message.RoutingKey)
                    ? message.EventType.ToLowerInvariant()
                    : message.RoutingKey;

                var bodyBytes = Encoding.UTF8.GetBytes(message.Payload);

                var props = new BasicProperties
                {
                    DeliveryMode = DeliveryModes.Persistent,
                    ContentType = "application/json",
                    MessageId = message.EventId.ToString()
                };

                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: bodyBytes,
                    cancellationToken: ct);

                message.Status = OutboxMessageStatus.Published;
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.LastAttemptAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Published {Count} Queue outbox messages to RabbitMQ.", pendingMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to publish Queue outbox messages to RabbitMQ ({Host}:{Port}): {Message}", host, port, ex.Message);
        }
        finally
        {
            if (channel != null) await channel.DisposeAsync();
            if (connection != null) await connection.DisposeAsync();
        }
    }
}

