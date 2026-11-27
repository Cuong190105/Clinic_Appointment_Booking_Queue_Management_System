namespace Clinic.StaffScheduling.Service.Services;

using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class SchedulingEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SchedulingEventConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public SchedulingEventConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<SchedulingEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(_configuration["SkipBackgroundWorkers"], "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("SchedulingEventConsumer skipped because SkipBackgroundWorkers=true.");
            return;
        }

        _logger.LogInformation("SchedulingEventConsumer started.");

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
                _logger.LogWarning("SchedulingEventConsumer connection/loop warning: {Message}", ex.Message);
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
        _logger.LogInformation("SchedulingEventConsumer stopped.");
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
            ClientProvidedName = "clinic-scheduling-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken: ct);

        const string exchangeName = SchedulingRetryPolicy.ExchangeName;
        const string queueName = SchedulingRetryPolicy.MainQueueName;
        const string deadLetterExchange = SchedulingRetryPolicy.DeadLetterExchange;
        const string deadLetterQueue = SchedulingRetryPolicy.DeadLetterQueueName;

        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

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
            routingKey: queueName,
            cancellationToken: ct);

        await _channel.ExchangeDeclareAsync(
            exchange: SchedulingRetryPolicy.RetryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        foreach (var (retryQueueName, delayMilliseconds) in SchedulingRetryPolicy.Queues)
        {
            await _channel.QueueDeclareAsync(
                queue: retryQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = delayMilliseconds,
                    ["x-dead-letter-exchange"] = exchangeName,
                    ["x-dead-letter-routing-key"] = SchedulingRetryPolicy.RetryRoutingKey
                },
                cancellationToken: ct);

            await _channel.QueueBindAsync(
                queue: retryQueueName,
                exchange: SchedulingRetryPolicy.RetryExchange,
                routingKey: retryQueueName,
                cancellationToken: ct);
        }

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = queueName
            },
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "appointment.cancelled",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "identity.user-profile-updated",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: SchedulingRetryPolicy.RetryRoutingKey,
            cancellationToken: ct);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            var effectiveRoutingKey = SchedulingRetryPolicy.GetEffectiveRoutingKey(
                args.RoutingKey,
                args.BasicProperties.Headers);

            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                await using var scope = _serviceProvider.CreateAsyncScope();
                var manager = scope.ServiceProvider.GetRequiredService<ISchedulingManager>();

                if (effectiveRoutingKey == "appointment.cancelled" || json.Contains(EventTypes.AppointmentCancelled))
                {
                    var payload = DeserializePayload<AppointmentCancelled>(json)
                        ?? throw new JsonException("Message is not a valid AppointmentCancelled envelope or payload.");

                    if (payload.WorkScheduleId != Guid.Empty)
                    {
                        await manager.ReleaseSlotByAppointmentAsync(
                            payload.WorkScheduleId,
                            payload.TimeSlot,
                            payload.PatientId,
                            ct);
                        _logger.LogInformation(
                            "Processed AppointmentCancelled event for AppointmentId={AppointmentId}, Schedule={ScheduleId}, Slot={TimeSlot}",
                            payload.AppointmentId,
                            payload.WorkScheduleId,
                            payload.TimeSlot);
                    }
                }
                else if (effectiveRoutingKey == "identity.user-profile-updated" || json.Contains(EventTypes.UserProfileUpdated))
                {
                    var payload = DeserializePayload<UserProfileUpdated>(json)
                        ?? throw new JsonException("Message is not a valid UserProfileUpdated envelope or payload.");

                    await manager.HandleUserProfileUpdatedAsync(payload, ct);
                }
                else
                {
                    throw new JsonException($"Unsupported scheduling event routing key '{effectiveRoutingKey}'.");
                }

                if (_channel is { IsOpen: true })
                {
                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                    return;

                var retryCount = SchedulingRetryPolicy.GetRetryCount(args.BasicProperties.Headers);
                if (_channel is not { IsOpen: true })
                    return;

                if (SchedulingRetryPolicy.ShouldDeadLetter(ex, retryCount))
                {
                    _logger.LogError(
                        ex,
                        "Scheduling event permanently failed; dead-lettering RoutingKey={RoutingKey} after {RetryCount} retries",
                        effectiveRoutingKey,
                        retryCount);
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                    return;
                }

                try
                {
                    var nextRetryCount = retryCount + 1;
                    var retryProperties = SchedulingRetryPolicy.CreateRetryProperties(
                        args.BasicProperties,
                        effectiveRoutingKey,
                        nextRetryCount);

                    await _channel.BasicPublishAsync(
                        exchange: SchedulingRetryPolicy.RetryExchange,
                        routingKey: SchedulingRetryPolicy.GetRetryQueueName(retryCount),
                        mandatory: true,
                        basicProperties: retryProperties,
                        body: args.Body,
                        cancellationToken: ct);

                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                    _logger.LogWarning(
                        ex,
                        "Scheduling event transiently failed (RoutingKey={RoutingKey}); scheduled retry {RetryCount}/{MaximumRetries}",
                        effectiveRoutingKey,
                        nextRetryCount,
                        SchedulingRetryPolicy.MaximumRetries);
                }
                catch (Exception retryException)
                {
                    _logger.LogError(
                        retryException,
                        "Could not schedule scheduling event retry; leaving source message unacked for broker redelivery.");
                    if (_channel.IsOpen)
                    {
                        await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
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

    private static TPayload? DeserializePayload<TPayload>(string json)
        where TPayload : class
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope<TPayload>>(
                json,
                EventJsonSerializerOptions.Default);
            if (envelope?.Payload is not null)
                return envelope.Payload;
        }
        catch (JsonException)
        {
            // Fallback to direct deserialization
        }

        return JsonSerializer.Deserialize<TPayload>(
            json,
            EventJsonSerializerOptions.Default);
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
