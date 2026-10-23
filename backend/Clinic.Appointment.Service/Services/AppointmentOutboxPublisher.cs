namespace Clinic.Appointment.Service.Services;

using System.Text;
using Clinic.Appointment.Service.Enums;
using Clinic.Appointment.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public class AppointmentOutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentOutboxPublisher> _logger;

    public AppointmentOutboxPublisher(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AppointmentOutboxPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentOutboxPublisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during outbox publishing cycle.");
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

        _logger.LogInformation("AppointmentOutboxPublisher stopped.");
    }

    private async Task PublishPendingOutboxMessagesAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        var host = _configuration["RabbitMq:Host"] ?? _configuration["RabbitMQ__Host"] ?? "rabbitmq";
        var portStr = _configuration["RabbitMq:Port"] ?? _configuration["RabbitMQ__Port"] ?? "5672";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 5672;

        var username = _configuration["RabbitMq:Username"] ?? _configuration["RabbitMQ__Username"] ?? "clinic";
        var password = _configuration["RabbitMq:Password"] ?? _configuration["RabbitMQ__Password"] ?? "clinic-dev-password";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            ClientProvidedName = "clinic-appointment-outbox-publisher"
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

                message.Status = OutboxMessageStatus.Sent;
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.LastAttemptAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Published {Count} outbox messages to RabbitMQ.", pendingMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to publish outbox messages to RabbitMQ ({Host}:{Port}): {Message}", host, port, ex.Message);
        }
        finally
        {
            if (channel != null) await channel.DisposeAsync();
            if (connection != null) await connection.DisposeAsync();
        }
    }
}
