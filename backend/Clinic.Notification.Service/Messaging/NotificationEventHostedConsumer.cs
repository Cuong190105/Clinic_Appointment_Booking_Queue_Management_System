using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.Notification.Service.Messaging;

/// <summary>
/// Durable RabbitMQ consumer for business events.
/// - Bounded prefetch; ack only after the DB transaction commits (at-least-once, idempotent via inbox).
/// - Transient failures: bounded retry through a TTL retry queue.
/// - Poison / malformed messages (invalid JSON, missing message id, retries exhausted) are quarantined
///   into a durable DLQ as a <b>sanitized</b> record (reason + hashes, never the payload), so they are
///   neither lost silently nor looped forever, and no PHI/secret reaches the DLQ.
/// </summary>
public sealed class NotificationEventHostedConsumer : BackgroundService
{
    public const string ExchangeName = "clinic.events";
    public const string QueueName = "clinic.notification.events.v2";
    public const string LegacyQueueName = "clinic.notification.events";
    public const string RetryQueueName = "clinic.notification.events.retry";
    public const string DeadLetterExchange = "clinic.notification.dlx";
    public const string DeadLetterQueue = "clinic.notification.events.dlq";
    public const string DeadLetterRoutingKey = "dead";
    public const int MaxRetries = 3;
    public const int RetryDelayMs = 15_000;
    public const ushort Prefetch = 10;
    private const string RetryHeader = "x-retry-count";

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
        var password = _configuration["RabbitMq:Password"] ?? _configuration["RabbitMQ__Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("RabbitMQ password is not configured.");

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
        var channel = _channel;

        await DeclareTopologyAsync(channel, ct);
        await RetireLegacyQueueAsync(ct);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: Prefetch, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) => HandleAsync(channel, args, ct);

        await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);

        _logger.LogInformation("Subscribed to queue '{QueueName}' on exchange '{ExchangeName}'.", QueueName, ExchangeName);

        // Keep running until canceled or the connection drops.
        var tcs = new TaskCompletionSource();
        using var registration = ct.Register(() => tcs.TrySetResult());
        _connection.ConnectionShutdownAsync += (_, _) =>
        {
            tcs.TrySetException(new InvalidOperationException("RabbitMQ connection lost."));
            return Task.CompletedTask;
        };
        await tcs.Task;
    }

    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        // Dead-letter exchange + durable DLQ (sanitized quarantine records land here).
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey, cancellationToken: ct);

        // Retry queue: message waits RetryDelayMs, then is dead-lettered back to the main queue via the default exchange.
        await channel.QueueDeclareAsync(
            RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = RetryDelayMs,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = QueueName
            },
            cancellationToken: ct);

        // Main queue: anything rejected without requeue is routed to the DLX (safety net).
        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
            },
            cancellationToken: ct);

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
            await channel.QueueBindAsync(QueueName, ExchangeName, rk, cancellationToken: ct);
        }
    }

    /// <summary>
    /// The first version of the queue was declared without DLX arguments (RabbitMQ forbids changing them),
    /// so it was replaced by <see cref="QueueName"/>. Remove the old queue only if it is empty and idle.
    /// </summary>
    private async Task RetireLegacyQueueAsync(CancellationToken ct)
    {
        if (_connection == null) return;
        try
        {
            await using var temp = await _connection.CreateChannelAsync(cancellationToken: ct);
            await temp.QueueDeleteAsync(LegacyQueueName, ifUnused: true, ifEmpty: true, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Legacy queue '{Queue}' not removed: {Message}", LegacyQueueName, ex.Message);
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var body = args.Body.ToArray();

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(body);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                await QuarantineAsync(channel, args, body, "invalid-json", ct);
                return;
            }

            // The producer's outbox sets MessageId = EventId (or EventId inside the envelope);
            // without a stable id dedupe is impossible, so the message is quarantined instead of being given a random id.
            var eventId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(args.BasicProperties.MessageId) &&
                Guid.TryParse(args.BasicProperties.MessageId, out var parsedMid))
            {
                eventId = parsedMid;
            }
            else if (root.ValueKind == JsonValueKind.Object &&
                     ((root.TryGetProperty("eventId", out var eidProp) && eidProp.ValueKind == JsonValueKind.String && eidProp.TryGetGuid(out var parsedEid)) ||
                      (root.TryGetProperty("EventId", out var eidPropPascal) && eidPropPascal.ValueKind == JsonValueKind.String && eidPropPascal.TryGetGuid(out parsedEid))))
            {
                eventId = parsedEid;
            }

            if (eventId == Guid.Empty)
            {
                await QuarantineAsync(channel, args, body, "missing-or-invalid-message-id", ct);
                return;
            }

            string? envelopeEventType = null;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("eventType", out var etProp) && etProp.ValueKind == JsonValueKind.String)
                    envelopeEventType = etProp.GetString();
                else if (root.TryGetProperty("EventType", out var etPropPascal) && etPropPascal.ValueKind == JsonValueKind.String)
                    envelopeEventType = etPropPascal.GetString();
            }

            var eventType = !string.IsNullOrWhiteSpace(envelopeEventType)
                ? envelopeEventType!
                : args.RoutingKey.ToLowerInvariant() switch
                {
                    "appointment.confirmed" => EventTypes.AppointmentConfirmed,
                    "appointment.cancelled" => EventTypes.AppointmentCancelled,
                    "queue.patient-called" => EventTypes.PatientCalled,
                    _ => args.RoutingKey
                };

            Guid? correlationId = null;
            if (root.ValueKind == JsonValueKind.Object &&
                ((root.TryGetProperty("correlationId", out var corrProp) && corrProp.ValueKind == JsonValueKind.String && corrProp.TryGetGuid(out var parsedCorr)) ||
                 (root.TryGetProperty("CorrelationId", out var corrPropPascal) && corrPropPascal.ValueKind == JsonValueKind.String && corrPropPascal.TryGetGuid(out parsedCorr))))
            {
                correlationId = parsedCorr;
            }

            await _processor.ProcessEventAsync(eventId, eventType, root, correlationId, ct);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to handle RabbitMQ message with routing key '{RoutingKey}'", args.RoutingKey);
            await RetryOrQuarantineAsync(channel, args, ct);
        }
    }

    private async Task RetryOrQuarantineAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var retries = GetRetryCount(args.BasicProperties.Headers);
            var body = args.Body.ToArray();

            if (retries >= MaxRetries)
            {
                await QuarantineAsync(channel, args, body, "processing-failed-retries-exhausted", ct);
                return;
            }

            var headers = new Dictionary<string, object?>(args.BasicProperties.Headers ?? new Dictionary<string, object?>())
            {
                [RetryHeader] = retries + 1
            };
            var props = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                ContentType = args.BasicProperties.ContentType,
                MessageId = args.BasicProperties.MessageId,
                Headers = headers
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: RetryQueueName,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Last resort: reject without requeue so the broker routes it to the DLX instead of looping or losing it.
            _logger.LogError(ex, "Retry/quarantine publish failed for routing key '{RoutingKey}'; rejecting to DLX.", args.RoutingKey);
            try
            {
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
            catch (Exception nackEx)
            {
                _logger.LogDebug(nackEx, "Nack failed (channel closed); broker will redeliver.");
            }
        }
    }

    /// <summary>Publishes a payload-free quarantine record to the DLQ and acknowledges the original message.</summary>
    private async Task QuarantineAsync(IChannel channel, BasicDeliverEventArgs args, byte[] body, string reason, CancellationToken ct)
    {
        var record = new
        {
            reason,
            routingKey = args.RoutingKey,
            messageId = args.BasicProperties.MessageId,
            sizeBytes = body.Length,
            bodySha256 = Convert.ToHexString(SHA256.HashData(body)),
            retryCount = GetRetryCount(args.BasicProperties.Headers),
            quarantinedAt = DateTimeOffset.UtcNow
        };

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            Headers = new Dictionary<string, object?> { ["x-quarantine-reason"] = reason }
        };

        await channel.BasicPublishAsync(
            exchange: DeadLetterExchange,
            routingKey: DeadLetterRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record)),
            cancellationToken: ct);
        await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);

        _logger.LogWarning(
            "Quarantined message (reason={Reason}, routingKey={RoutingKey}, messageId={MessageId}); payload not retained.",
            reason, args.RoutingKey, args.BasicProperties.MessageId);
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers != null && headers.TryGetValue(RetryHeader, out var value) && value != null)
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] b when int.TryParse(Encoding.UTF8.GetString(b), out var parsed) => parsed,
                _ => 0
            };
        }
        return 0;
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
