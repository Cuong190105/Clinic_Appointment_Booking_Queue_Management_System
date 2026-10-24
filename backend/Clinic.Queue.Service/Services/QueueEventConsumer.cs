namespace Clinic.Queue.Service.Services;

using System.Text;
using System.Diagnostics;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Queue.Service.Common;
using Clinic.Queue.Service.Common.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class QueueEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QueueEventConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public QueueEventConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<QueueEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.GetValue<bool>("SkipBackgroundWorkers"))
        {
            _logger.LogInformation("QueueEventConsumer is disabled by SkipBackgroundWorkers.");
            return;
        }

        _logger.LogInformation("QueueEventConsumer started.");

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
                _logger.LogWarning("QueueEventConsumer connection/loop warning: {Message}", ex.Message);
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
        _logger.LogInformation("QueueEventConsumer stopped.");
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
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
            ClientProvidedName = "clinic-queue-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        const string exchangeName = "clinic.events";
        const string queueName = "q.queue-service.events";
        const string deadLetterExchange = "clinic.dead-letter";
        const string deadLetterQueue = "q.queue-service.dlq";
        const string deadLetterRoutingKey = "q.queue-service.dlq";
        const string retryQueue = "q.queue-service.retry";

        // Declare main exchange
        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // Declare DLX and DLQ
        await _channel.ExchangeDeclareAsync(
            exchange: deadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: deadLetterQueue,
            exchange: deadLetterExchange,
            routingKey: deadLetterRoutingKey,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: retryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = exchangeName,
                ["x-dead-letter-routing-key"] = "appointment.checked-in"
            },
            cancellationToken: ct);

        // Declare main queue with DLX arguments matching Queue design.md
        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = deadLetterExchange,
            ["x-dead-letter-routing-key"] = deadLetterRoutingKey
        };

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "appointment.checked-in",
            cancellationToken: ct);

        // Set prefetch count
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                await using var scope = _serviceProvider.CreateAsyncScope();
                var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

                EventEnvelope<AppointmentCheckedIn>? envelope = null;
                Guid eventId = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(args.BasicProperties.MessageId) && Guid.TryParse(args.BasicProperties.MessageId, out var parsedMessageId))
                {
                    eventId = parsedMessageId;
                }

                try
                {
                    envelope = JsonSerializer.Deserialize<EventEnvelope<AppointmentCheckedIn>>(
                        json,
                        EventJsonSerializerOptions.Default);
                }
                catch
                {
                    // Fallback to direct payload
                }

                if (envelope == null || envelope.Payload == null)
                {
                    var directPayload = JsonSerializer.Deserialize<AppointmentCheckedIn>(
                        json,
                        EventJsonSerializerOptions.Default);
                    if (directPayload != null)
                    {
                        envelope = new EventEnvelope<AppointmentCheckedIn>
                        {
                            EventId = eventId != Guid.Empty ? eventId : Guid.NewGuid(),
                            EventType = EventTypes.AppointmentCheckedIn,
                            SchemaVersion = 1,
                            OccurredAt = DateTimeOffset.UtcNow,
                            CorrelationId = Guid.NewGuid(),
                            Producer = "appointment-service",
                            Payload = directPayload
                        };
                    }
                }

                if (envelope == null || envelope.Payload == null)
                    throw new JsonException("Message is not a valid AppointmentCheckedIn envelope or payload.");
                if (!string.Equals(envelope.EventType, EventTypes.AppointmentCheckedIn, StringComparison.Ordinal))
                    throw new JsonException($"Unexpected event type '{envelope.EventType}'.");

                if (envelope.Payload != null)
                {
                    using var activity = QueueTelemetry.ActivitySource.StartActivity("messaging.consume", ActivityKind.Consumer);
                    activity?.SetTag("messaging.message.id", envelope.EventId);
                    activity?.SetTag("messaging.destination.name", queueName);
                    activity?.SetTag("event.type", envelope.EventType);
                    var startedAt = Stopwatch.GetTimestamp();
                    await queueManager.ProcessAppointmentCheckedInAsync(envelope, ct);
                    QueueTelemetry.ConsumerProcessed.Add(1);
                    QueueTelemetry.ConsumerLag.Record(Math.Max(0, (DateTimeOffset.UtcNow - envelope.OccurredAt).TotalSeconds));
                    QueueTelemetry.TicketProcessingTime.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
                    _logger.LogInformation("Successfully handled AppointmentCheckedIn for AppointmentId={AppointmentId}", envelope.Payload.AppointmentId);
                }
                if (_channel is { IsOpen: true })
                {
                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                var permanentFailure = ex is JsonException or QueueValidationException;
                QueueTelemetry.ConsumerFailures.Add(1);
                var retryCount = GetRetryCount(args.BasicProperties.Headers);
                if (_channel is { IsOpen: true })
                {
                    if (permanentFailure || retryCount >= 3)
                    {
                        QueueTelemetry.ConsumerDeadLettered.Add(1);
                        _logger.LogError(ex, "Queue event permanently failed (retryCount={RetryCount}); dead-lettering to DLQ", retryCount);
                        await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                    }
                    else
                    {
                        QueueTelemetry.ConsumerRetries.Add(1);
                        try
                        {
                            var retryProperties = new BasicProperties
                            {
                                Persistent = true,
                                ContentType = args.BasicProperties.ContentType,
                                MessageId = args.BasicProperties.MessageId,
                                CorrelationId = args.BasicProperties.CorrelationId,
                                Type = args.BasicProperties.Type,
                                Headers = new Dictionary<string, object?>(args.BasicProperties.Headers ?? new Dictionary<string, object?>())
                            };
                            retryProperties.Headers["x-queue-retry-count"] = retryCount + 1;
                            await _channel.BasicPublishAsync(
                                exchange: string.Empty,
                                routingKey: retryQueue,
                                mandatory: true,
                                basicProperties: retryProperties,
                                body: args.Body,
                                cancellationToken: ct);
                            await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                            _logger.LogWarning(ex, "Queue event transiently failed; scheduled retry {RetryCount}/3", retryCount + 1);
                        }
                        catch (Exception retryException)
                        {
                            _logger.LogError(retryException, "Could not schedule queue event retry; leaving source unacked for redelivery.");
                            if (_channel.IsOpen)
                                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
                        }
                    }
                }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers == null || !headers.TryGetValue("x-queue-retry-count", out var value) || value == null) return 0;
        try
        {
            return value is byte[] bytes
                ? int.Parse(Encoding.UTF8.GetString(bytes))
                : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private async Task DisconnectAsync()
    {
        if (_channel != null)
        {
            try { await _channel.CloseAsync(); } catch { }
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection != null)
        {
            try { await _connection.CloseAsync(); } catch { }
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
