using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Infrastructure.Sqlserver.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class AppointmentCheckedInConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly RabbitMqConsumerOptions _consumerOptions;
    private readonly ILogger<AppointmentCheckedInConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public AppointmentCheckedInConsumer(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> rabbitOptions,
        IOptions<RabbitMqConsumerOptions> consumerOptions,
        ILogger<AppointmentCheckedInConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _rabbitOptions = rabbitOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            catch (Exception exception)
            {
                _logger.LogError(exception, "AppointmentCheckedIn consumer iteration failed");
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
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        if (_channel is null)
        {
            throw new InvalidOperationException("RabbitMQ consumer channel is not initialized.");
        }

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _consumerOptions.PrefetchCount,
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleMessageAsync;

        await _channel.BasicConsumeAsync(
            queue: _consumerOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        using var activity = MessagingTelemetry.StartConsumerActivity(
            ReadHeader(args.BasicProperties, "traceparent"),
            ReadHeader(args.BasicProperties, "tracestate"));
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", _consumerOptions.QueueName);

        if (_channel is null || !_channel.IsOpen)
        {
            return;
        }

        EventEnvelope<AppointmentCheckedIn>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope<AppointmentCheckedIn>>(
                args.Body.Span,
                EventJsonSerializerOptions.Default);

            if (envelope is null ||
                envelope.EventType != EventTypes.AppointmentCheckedIn ||
                envelope.EventId == Guid.Empty)
            {
                throw new JsonException("Invalid AppointmentCheckedIn envelope.");
            }

            activity?.SetTag("messaging.message.id", envelope.EventId);
            activity?.SetTag("event.type", envelope.EventType);
            activity?.SetTag("clinic.correlation_id", envelope.CorrelationId);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            await PublishDeadLetterAsync(args, exception.Message, CancellationToken.None);
            await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
            return;
        }

        try
        {
            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventId"] = envelope.EventId,
                ["EventType"] = envelope.EventType,
                ["CorrelationId"] = envelope.CorrelationId,
                ["Consumer"] = _consumerOptions.ConsumerName
            });
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Clinic.Infrastructure.Sqlserver.Persistence.ApplicationDbContext>();
            var inboxStore = scope.ServiceProvider.GetRequiredService<Clinic.Application.Interfaces.IInboxStore>();
            var processor = new AppointmentCheckedInProcessor(
                dbContext,
                inboxStore,
                _consumerOptions.ConsumerName);

            var result = await processor.ProcessAsync(envelope, CancellationToken.None);

            if (result == AppointmentCheckedInProcessingResult.CurrentlyProcessing)
            {
                await PublishRetryOrDeadLetterAsync(args, "Inbox lease is currently owned by another consumer", CancellationToken.None);
            }

            if (result == AppointmentCheckedInProcessingResult.AlreadyProcessed)
            {
                MessagingTelemetry.ConsumerDuplicates.Add(1);
            }
            else if (result == AppointmentCheckedInProcessingResult.Processed)
            {
                MessagingTelemetry.ConsumerProcessed.Add(1);
                MessagingTelemetry.ConsumerLag.Record(
                    Math.Max(0, (DateTimeOffset.UtcNow - envelope.OccurredAt).TotalSeconds));
            }

            await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
            _logger.LogInformation(
                "Processed AppointmentCheckedIn {EventId} with result {Result}",
                envelope.EventId,
                result);
        }
        catch (Exception exception)
        {
            MessagingTelemetry.ConsumerFailures.Add(1);
            _logger.LogError(
                exception,
                "Failed to process AppointmentCheckedIn {EventId}",
                envelope.EventId);

            await RecordFailureAsync(envelope, exception.Message);
            await PublishRetryOrDeadLetterAsync(args, exception.Message, CancellationToken.None);
            await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
    }

    private static string? ReadHeader(IReadOnlyBasicProperties properties, string name)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> bytes => Encoding.UTF8.GetString(bytes.Span),
            _ => null
        };
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return;
        }

        await DisconnectAsync();

        _connection = await _connectionFactory
            .Create()
            .CreateConnectionAsync(cancellationToken);

        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await RabbitMqTopology.DeclareQueueServiceAsync(
            _channel,
            _rabbitOptions,
            _consumerOptions,
            cancellationToken);
    }

    private async Task RecordFailureAsync(
        EventEnvelope<AppointmentCheckedIn> envelope,
        string error)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Clinic.Infrastructure.Sqlserver.Persistence.ApplicationDbContext>();
            var inboxStore = scope.ServiceProvider.GetRequiredService<Clinic.Application.Interfaces.IInboxStore>();

            var decision = await inboxStore.TryBeginProcessingAsync(
                envelope,
                _consumerOptions.ConsumerName,
                CancellationToken.None);

            if (decision != Clinic.Application.Interfaces.InboxDecision.AlreadyProcessed)
            {
                inboxStore.MarkFailed(envelope.EventId, _consumerOptions.ConsumerName, error);
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception failureException)
        {
            _logger.LogError(
                failureException,
                "Could not persist Inbox failure for {EventId}",
                envelope.EventId);
        }
    }

    private async Task PublishRetryOrDeadLetterAsync(
        BasicDeliverEventArgs args,
        string error,
        CancellationToken cancellationToken)
    {
        if (_channel is null || !_channel.IsOpen)
        {
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");
        }

        var attempt = RabbitMqRetryPolicy.GetAttempt(args.BasicProperties) + 1;
        if (attempt > _consumerOptions.MaxRetryAttempts)
        {
            await PublishDeadLetterAsync(args, error, cancellationToken);
            MessagingTelemetry.ConsumerDeadLettered.Add(1);
            return;
        }

        var properties = CreateFailureProperties(args.BasicProperties, attempt, error);
        await _channel.BasicPublishAsync(
            exchange: RabbitMqRetryPolicy.GetRetryExchange(attempt),
            routingKey: $"{_consumerOptions.RetryRoutePrefix}.appointment.checked-in",
            mandatory: true,
            basicProperties: properties,
            body: args.Body,
            cancellationToken: cancellationToken);
        MessagingTelemetry.ConsumerRetries.Add(1);
    }

    private async Task PublishDeadLetterAsync(
        BasicDeliverEventArgs args,
        string error,
        CancellationToken cancellationToken)
    {
        if (_channel is null || !_channel.IsOpen)
        {
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");
        }

        var properties = CreateFailureProperties(
            args.BasicProperties,
            RabbitMqRetryPolicy.GetAttempt(args.BasicProperties),
            error);

        await _channel.BasicPublishAsync(
            exchange: "clinic.dead-letter",
            routingKey: _consumerOptions.DeadLetterRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: args.Body,
            cancellationToken: cancellationToken);
    }

    private static BasicProperties CreateFailureProperties(
        IReadOnlyBasicProperties source,
        int attempt,
        string error)
    {
        var headers = source.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(source.Headers);

        headers["x-retry-attempt"] = attempt;
        headers["x-error"] = error;

        return new BasicProperties
        {
            Persistent = true,
            ContentType = source.ContentType ?? "application/json",
            MessageId = source.MessageId,
            Type = source.Type,
            CorrelationId = source.CorrelationId,
            Headers = headers
        };
    }

    private async Task DisconnectAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
