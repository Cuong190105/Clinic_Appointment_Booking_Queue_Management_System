using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.Notification.Service.Messaging;

public sealed class NotificationEventHostedConsumer : BackgroundService
{
    private readonly INotificationEventProcessor _processor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationEventHostedConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationEventHostedConsumer(
        INotificationEventProcessor processor,
        IConfiguration configuration,
        ILogger<NotificationEventHostedConsumer> logger)
    {
        _processor = processor;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationEventHostedConsumer started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("NotificationEventHostedConsumer connection error: {Message}. Reconnecting in 5s...", ex.Message);
                await DisconnectAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        await DisconnectAsync();
        _logger.LogInformation("NotificationEventHostedConsumer stopped.");
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
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
            ClientProvidedName = "clinic-notification-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        const string exchangeName = "clinic.events";
        const string queueName = "clinic.notification.events";

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        // Bind relevant business events
        var routingKeys = new[]
        {
            "appointment.confirmed",
            "appointment.cancelled",
            "appointment.rescheduled",
            "queue.patient-called",
            "queue.*",
            "notification.*"
        };

        foreach (var rk in routingKeys)
        {
            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: rk,
                cancellationToken: ct);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(args.Body.ToArray());
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement.Clone();

                var eventId = Guid.Empty;
                if (!string.IsNullOrEmpty(args.BasicProperties.MessageId) && Guid.TryParse(args.BasicProperties.MessageId, out var parsedMid))
                {
                    eventId = parsedMid;
                }
                else if (root.TryGetProperty("eventId", out var eidProp) && eidProp.TryGetGuid(out var parsedEid))
                {
                    eventId = parsedEid;
                }
                else if (root.TryGetProperty("id", out var idProp) && idProp.TryGetGuid(out var parsedId))
                {
                    eventId = parsedId;
                }
                else
                {
                    eventId = Guid.NewGuid();
                }

                // Determine event type
                string eventType = args.RoutingKey;
                if (root.TryGetProperty("eventType", out var etProp) && !string.IsNullOrWhiteSpace(etProp.GetString()))
                {
                    eventType = etProp.GetString()!;
                }
                else
                {
                    // Map common routing keys to EventTypes constants
                    eventType = args.RoutingKey.ToLowerInvariant() switch
                    {
                        "appointment.confirmed" => EventTypes.AppointmentConfirmed,
                        "appointment.cancelled" => EventTypes.AppointmentCancelled,
                        "queue.patient-called" => EventTypes.PatientCalled,
                        _ => args.RoutingKey
                    };
                }

                Guid? correlationId = null;
                if (root.TryGetProperty("correlationId", out var corrProp) && corrProp.TryGetGuid(out var parsedCorr))
                {
                    correlationId = parsedCorr;
                }

                await _processor.ProcessEventAsync(eventId, eventType, root, correlationId, ct);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle RabbitMQ message with routing key '{RoutingKey}'", args.RoutingKey);
                // Nack with requeue=false to avoid poison pill loops
                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("Subscribed to queue '{QueueName}' on exchange '{ExchangeName}'.", queueName, exchangeName);

        // Keep running until canceled
        var tcs = new TaskCompletionSource();
        using var registration = ct.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }

    private async Task DisconnectAsync()
    {
        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                _channel.Dispose();
                _channel = null;
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
                _connection = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception while disconnecting RabbitMQ channel/connection.");
        }
    }
}

