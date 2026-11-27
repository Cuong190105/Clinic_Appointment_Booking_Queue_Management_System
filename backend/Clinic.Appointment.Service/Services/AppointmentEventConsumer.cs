namespace Clinic.Appointment.Service.Services;

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

public sealed class AppointmentEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentEventConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public AppointmentEventConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AppointmentEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(_configuration["SkipBackgroundWorkers"], "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("AppointmentEventConsumer skipped because SkipBackgroundWorkers=true.");
            return;
        }

        _logger.LogInformation("AppointmentEventConsumer started.");

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
                _logger.LogWarning("AppointmentEventConsumer connection/loop warning: {Message}", ex.Message);
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
        _logger.LogInformation("AppointmentEventConsumer stopped.");
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
            ClientProvidedName = "clinic-appointment-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken: ct);

        const string exchangeName = AppointmentRetryPolicy.ExchangeName;
        const string queueName = AppointmentRetryPolicy.MainQueueName;
        const string deadLetterExchange = AppointmentRetryPolicy.DeadLetterExchange;
        const string deadLetterQueue = AppointmentRetryPolicy.DeadLetterQueueName;

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
            exchange: AppointmentRetryPolicy.RetryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        foreach (var (retryQueueName, delayMilliseconds) in AppointmentRetryPolicy.Queues)
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
                    ["x-dead-letter-routing-key"] = AppointmentRetryPolicy.RetryRoutingKey
                },
                cancellationToken: ct);

            await _channel.QueueBindAsync(
                queue: retryQueueName,
                exchange: AppointmentRetryPolicy.RetryExchange,
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
            routingKey: "queue.exam-completed",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "identity.user-profile-updated",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: AppointmentRetryPolicy.RetryRoutingKey,
            cancellationToken: ct);

        await TryCleanupLegacyQueueAsync(
            legacyQueue: "clinic.appointment.events",
            exchangeName: exchangeName,
            routingKeys: ["queue.exam-completed", "identity.user-profile-updated"],
            ct);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            var effectiveRoutingKey = AppointmentRetryPolicy.GetEffectiveRoutingKey(
                args.RoutingKey,
                args.BasicProperties.Headers);

            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                await using var scope = _serviceProvider.CreateAsyncScope();
                var manager = scope.ServiceProvider.GetRequiredService<IAppointmentManager>();

                if (effectiveRoutingKey == "queue.exam-completed" || json.Contains(EventTypes.ExamCompleted))
                {
                    var payload = DeserializePayload<ExamCompleted>(json)
                        ?? throw new JsonException("Message is not a valid ExamCompleted envelope or payload.");

                    await manager.ProcessExamCompletedAsync(payload, ct);
                    _logger.LogInformation("Processed ExamCompleted event for AppointmentId={AppointmentId}", payload.AppointmentId);
                }
                else if (effectiveRoutingKey == "identity.user-profile-updated" || json.Contains(EventTypes.UserProfileUpdated))
                {
                    var payload = DeserializePayload<UserProfileUpdated>(json)
                        ?? throw new JsonException("Message is not a valid UserProfileUpdated envelope or payload.");

                    await manager.ProcessUserProfileUpdatedAsync(payload, ct);
                    _logger.LogInformation("Processed UserProfileUpdated event for PersonId={PersonId}", payload.PersonId);
                }
                else
                {
                    throw new JsonException($"Unsupported appointment event routing key '{effectiveRoutingKey}'.");
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

                var retryCount = AppointmentRetryPolicy.GetRetryCount(args.BasicProperties.Headers);
                if (_channel is not { IsOpen: true })
                    return;

                if (AppointmentRetryPolicy.ShouldDeadLetter(ex, retryCount))
                {
                    _logger.LogError(
                        ex,
                        "Appointment event permanently failed; dead-lettering RoutingKey={RoutingKey} after {RetryCount} retries",
                        effectiveRoutingKey,
                        retryCount);
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                    return;
                }

                try
                {
                    var nextRetryCount = retryCount + 1;
                    var retryProperties = AppointmentRetryPolicy.CreateRetryProperties(
                        args.BasicProperties,
                        effectiveRoutingKey,
                        nextRetryCount);

                    await _channel.BasicPublishAsync(
                        exchange: AppointmentRetryPolicy.RetryExchange,
                        routingKey: AppointmentRetryPolicy.GetRetryQueueName(retryCount),
                        mandatory: true,
                        basicProperties: retryProperties,
                        body: args.Body,
                        cancellationToken: ct);

                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                    _logger.LogWarning(
                        ex,
                        "Appointment event transiently failed (RoutingKey={RoutingKey}); scheduled retry {RetryCount}/{MaximumRetries}",
                        effectiveRoutingKey,
                        nextRetryCount,
                        AppointmentRetryPolicy.MaximumRetries);
                }
                catch (Exception retryException)
                {
                    _logger.LogError(
                        retryException,
                        "Could not schedule appointment event retry; leaving source message unacked for broker redelivery.");
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

    private async Task TryCleanupLegacyQueueAsync(string legacyQueue, string exchangeName, string[] routingKeys, CancellationToken ct)
    {
        if (_connection == null) return;

        try
        {
            await using var unbindChannel = await _connection.CreateChannelAsync(cancellationToken: ct);
            foreach (var key in routingKeys)
            {
                await unbindChannel.QueueUnbindAsync(queue: legacyQueue, exchange: exchangeName, routingKey: key, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Legacy queue {Queue} unbind skipped.", legacyQueue);
        }

        try
        {
            await using var deleteChannel = await _connection.CreateChannelAsync(cancellationToken: ct);
            await deleteChannel.QueueDeleteAsync(legacyQueue, ifUnused: true, ifEmpty: true, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Legacy queue {Queue} delete skipped.", legacyQueue);
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
