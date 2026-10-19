using System.Text;
using System.Diagnostics;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxPublisher> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConnectedAsync(stoppingToken);
                await PublishBatchAsync(stoppingToken);
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publisher iteration failed");
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

        await _channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: "clinic.dead-letter",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(
            queue: _options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(
            queue: _options.DeadLetterQueue,
            exchange: "clinic.dead-letter",
            routingKey: _options.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTimeOffset.UtcNow;

        var candidateIds = await db.OutboxMessages
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending &&
                 (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                (x.Status == OutboxMessageStatus.Failed &&
                 (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                (x.Status == OutboxMessageStatus.DeadLetterPending &&
                 (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                (x.Status == OutboxMessageStatus.Publishing &&
                 x.PublishingStartedAt != null &&
                 x.PublishingStartedAt <= now.AddMinutes(-5)) ||
                (x.Status == OutboxMessageStatus.DeadLetterPublishing &&
                 x.PublishingStartedAt != null &&
                 x.PublishingStartedAt <= now.AddMinutes(-5)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(_options.PublisherBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var id in candidateIds)
        {
            if (!await TryClaimAsync(db, id, now, cancellationToken))
            {
                continue;
            }

            var message = await db.OutboxMessages
                .SingleAsync(x => x.Id == id, cancellationToken);

            await PublishOneAsync(db, message, cancellationToken);
        }
    }

    private async Task<bool> TryClaimAsync(
        ApplicationDbContext db,
        long id,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updated = await db.OutboxMessages
            .Where(x =>
                x.Id == id &&
                ((x.Status == OutboxMessageStatus.Pending &&
                  (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                 (x.Status == OutboxMessageStatus.Failed &&
                  (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                 (x.Status == OutboxMessageStatus.DeadLetterPending &&
                  (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                 (x.Status == OutboxMessageStatus.Publishing &&
                  x.PublishingStartedAt != null &&
                  x.PublishingStartedAt <= now.AddMinutes(-5)) ||
                 (x.Status == OutboxMessageStatus.DeadLetterPublishing &&
                  x.PublishingStartedAt != null &&
                  x.PublishingStartedAt <= now.AddMinutes(-5))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, x =>
                    x.Status == OutboxMessageStatus.DeadLetterPending ||
                    x.Status == OutboxMessageStatus.DeadLetterPublishing ||
                    (x.Status == OutboxMessageStatus.Failed &&
                     x.RetryCount >= _options.MaxPublishRetries)
                        ? OutboxMessageStatus.DeadLetterPublishing
                        : OutboxMessageStatus.Publishing)
                .SetProperty(x => x.PublishingStartedAt, now)
                .SetProperty(x => x.LastAttemptAt, now), cancellationToken);

        return updated == 1;
    }

    private async Task PublishOneAsync(
        ApplicationDbContext db,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (_channel is null || !_channel.IsOpen)
        {
            throw new InvalidOperationException(
                "RabbitMQ channel is not initialized.");
        }

        try
        {
            var deadLetter = message.Status == OutboxMessageStatus.DeadLetterPublishing;
            var parentContext = ActivityContext.TryParse(
                message.TraceParent,
                message.TraceState,
                out var parsedParent)
                ? parsedParent
                : default;
            using var activity = MessagingTelemetry.ActivitySource.StartActivity(
                "messaging.publish",
                ActivityKind.Producer,
                parentContext);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", deadLetter ? _options.DeadLetterQueue : message.RoutingKey);
            activity?.SetTag("messaging.message.id", message.EventId);
            activity?.SetTag("event.type", message.EventType);
            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventId"] = message.EventId,
                ["EventType"] = message.EventType,
                ["CorrelationId"] = message.CorrelationId
            });
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.EventId.ToString(),
                Type = message.EventType,
                CorrelationId = message.CorrelationId.ToString(),
                Headers = new Dictionary<string, object?>
                {
                    ["causation-id"] = message.CausationId?.ToString() ?? string.Empty,
                    ["schema-version"] = message.SchemaVersion,
                    ["x-retry-count"] = message.RetryCount,
                    ["x-error"] = message.LastError ?? string.Empty,
                    ["traceparent"] = activity?.Id ?? message.TraceParent ?? string.Empty,
                    ["tracestate"] = activity?.TraceStateString ?? message.TraceState ?? string.Empty
                }
            };

            await _channel.BasicPublishAsync(
                exchange: deadLetter ? "clinic.dead-letter" : _options.Exchange,
                routingKey: deadLetter ? _options.DeadLetterRoutingKey : message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: cancellationToken);

            message.Status = deadLetter ? OutboxMessageStatus.DeadLettered : OutboxMessageStatus.Sent;
            if (!deadLetter)
            {
                message.PublishedAt = DateTimeOffset.UtcNow;
            }
            message.PublishingStartedAt = null;
            message.NextAttemptAt = null;
            if (!deadLetter)
            {
                message.LastError = null;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (deadLetter)
            {
                MessagingTelemetry.OutboxDeadLettered.Add(1);
            }
            else
            {
                MessagingTelemetry.OutboxPublished.Add(1);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessagingTelemetry.OutboxPublishFailures.Add(1);
            var wasDeadLetterAttempt = message.Status == OutboxMessageStatus.DeadLetterPublishing;
            if (!wasDeadLetterAttempt)
            {
                message.RetryCount++;
            }
            message.LastError = ex.Message;
            message.PublishingStartedAt = null;
            if (wasDeadLetterAttempt)
            {
                message.Status = OutboxMessageStatus.DeadLetterPending;
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
            }
            else if (OutboxPublishPolicy.ShouldDeadLetter(message.RetryCount, _options.MaxPublishRetries))
            {
                message.Status = OutboxMessageStatus.DeadLetterPending;
                message.NextAttemptAt = null;
            }
            else
            {
                message.Status = OutboxMessageStatus.Failed;
                message.NextAttemptAt = OutboxPublishPolicy.GetNextAttemptAt(
                    message.RetryCount,
                    _options.MaxPublishRetries,
                    DateTimeOffset.UtcNow);
            }

            await db.SaveChangesAsync(cancellationToken);
            if (message.Status == OutboxMessageStatus.Failed)
            {
                MessagingTelemetry.OutboxPublishRetries.Add(1);
            }

            _logger.LogError(
                ex,
                "Failed to publish outbox event {EventId}; attempt {RetryCount}",
                message.EventId,
                message.RetryCount);
        }
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
