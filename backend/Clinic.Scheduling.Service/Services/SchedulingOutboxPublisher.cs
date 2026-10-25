namespace Clinic.Scheduling.Service.Services;

using System.Text;
using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

public class SchedulingOutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SchedulingOutboxPublisher> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public SchedulingOutboxPublisher(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<SchedulingOutboxPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.GetValue<bool>("SkipBackgroundWorkers"))
        {
            _logger.LogInformation("SchedulingOutboxPublisher is disabled by SkipBackgroundWorkers.");
            return;
        }

        _logger.LogInformation("SchedulingOutboxPublisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during Scheduling outbox publishing cycle.");
                await DisconnectAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                continue;
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

        await DisconnectAsync();
        _logger.LogInformation("SchedulingOutboxPublisher stopped.");
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return;
        }

        await DisconnectAsync();

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
            ClientProvidedName = "clinic-scheduling-outbox-publisher"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        const string exchangeName = "clinic.events";
        await _channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.ExchangeDeclareAsync(
            exchange: "clinic.dead-letter",
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: "q.scheduling-service.outbox-dlq",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: "q.scheduling-service.outbox-dlq",
            exchange: "clinic.dead-letter",
            routingKey: "q.scheduling-service.outbox-dlq",
            cancellationToken: ct);
    }

    public async Task<int> PublishPendingOutboxMessagesAsync(CancellationToken ct = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var now = DateTimeOffset.UtcNow;
        var leaseTimeout = now.AddMinutes(-5);

        var candidateIds = await db.OutboxMessages
            .Where(m => (m.Status == OutboxMessageStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) ||
                        (m.Status == OutboxMessageStatus.Failed && m.RetryCount <= 5 && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) ||
                        (m.Status == OutboxMessageStatus.Publishing && m.PublishingStartedAt != null && m.PublishingStartedAt <= leaseTimeout) ||
                        (m.Status == OutboxMessageStatus.DeadLetterPending && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) ||
                        (m.Status == OutboxMessageStatus.DeadLetterPublishing && m.PublishingStartedAt != null && m.PublishingStartedAt <= leaseTimeout))
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(20)
            .ToListAsync(ct);

        if (candidateIds.Count == 0)
        {
            return 0;
        }

        await EnsureConnectedAsync(ct);

        const string exchangeName = "clinic.events";
        var processedCount = 0;

        foreach (var id in candidateIds)
        {
            if (db.Database.IsRelational())
            {
                var claimed = await db.OutboxMessages
                    .Where(m => m.Id == id &&
                                ((m.Status == OutboxMessageStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) ||
                                 (m.Status == OutboxMessageStatus.Failed && m.RetryCount <= 5 && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) ||
                                 (m.Status == OutboxMessageStatus.Publishing && m.PublishingStartedAt != null && m.PublishingStartedAt <= leaseTimeout) ||
                                 (m.Status == OutboxMessageStatus.DeadLetterPending && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) ||
                                 (m.Status == OutboxMessageStatus.DeadLetterPublishing && m.PublishingStartedAt != null && m.PublishingStartedAt <= leaseTimeout)))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, x =>
                            x.Status == OutboxMessageStatus.DeadLetterPending || x.Status == OutboxMessageStatus.DeadLetterPublishing ||
                            (x.Status == OutboxMessageStatus.Failed && x.RetryCount >= 5)
                                ? OutboxMessageStatus.DeadLetterPublishing
                                : OutboxMessageStatus.Publishing)
                        .SetProperty(x => x.PublishingStartedAt, now)
                        .SetProperty(x => x.LastAttemptAt, now), ct);

                if (claimed == 0)
                {
                    continue;
                }
            }
            else
            {
                var target = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
                if (target == null) continue;
                target.Status = target.Status == OutboxMessageStatus.DeadLetterPending || target.Status == OutboxMessageStatus.DeadLetterPublishing || (target.Status == OutboxMessageStatus.Failed && target.RetryCount >= 5)
                    ? OutboxMessageStatus.DeadLetterPublishing
                    : OutboxMessageStatus.Publishing;
                target.PublishingStartedAt = now;
                target.LastAttemptAt = now;
                await db.SaveChangesAsync(ct);
            }

            var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (message == null)
            {
                continue;
            }

            try
            {
                var isDeadLetter = message.Status == OutboxMessageStatus.DeadLetterPublishing;
                var routingKey = string.IsNullOrWhiteSpace(message.RoutingKey)
                    ? message.EventType.ToLowerInvariant()
                    : message.RoutingKey;

                var bodyBytes = Encoding.UTF8.GetBytes(message.Payload);

                var props = new BasicProperties
                {
                    DeliveryMode = DeliveryModes.Persistent,
                    ContentType = "application/json",
                    MessageId = message.EventId.ToString(),
                    CorrelationId = message.CorrelationId.ToString(),
                    Type = message.EventType,
                    Headers = new Dictionary<string, object?>
                    {
                        ["causation-id"] = message.CausationId?.ToString() ?? string.Empty,
                        ["schema-version"] = message.SchemaVersion,
                        ["x-retry-count"] = message.RetryCount,
                        ["x-dead-letter-reason"] = message.LastError ?? string.Empty,
                        ["traceparent"] = message.TraceParent ?? string.Empty,
                        ["tracestate"] = message.TraceState ?? string.Empty
                    }
                };

                await _channel!.BasicPublishAsync(
                    exchange: isDeadLetter ? "clinic.dead-letter" : exchangeName,
                    routingKey: isDeadLetter ? "q.scheduling-service.outbox-dlq" : routingKey,
                    mandatory: true,
                    basicProperties: props,
                    body: bodyBytes,
                    cancellationToken: ct);

                message.Status = isDeadLetter ? OutboxMessageStatus.DeadLettered : OutboxMessageStatus.Sent;
                if (!isDeadLetter) message.PublishedAt = DateTimeOffset.UtcNow;
                message.PublishingStartedAt = null;
                message.LastError = null;
                _logger.LogInformation("Successfully published outbox event {EventType} ({EventId})", message.EventType, message.EventId);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish outbox event {EventType} ({EventId})", message.EventType, message.EventId);
                var wasDeadLetterAttempt = message.Status == OutboxMessageStatus.DeadLetterPublishing;
                if (!wasDeadLetterAttempt) message.RetryCount++;
                message.PublishingStartedAt = null;
                message.LastError = ex.Message;
                if (wasDeadLetterAttempt)
                {
                    message.Status = OutboxMessageStatus.DeadLetterPending;
                    message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
                }
                else if (message.RetryCount >= 5)
                {
                    message.Status = OutboxMessageStatus.DeadLetterPending;
                    message.NextAttemptAt = null;
                }
                else
                {
                    message.Status = OutboxMessageStatus.Failed;
                    message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, message.RetryCount) * 2));
                }
            }

            await db.SaveChangesAsync(ct);
        }

        return processedCount;
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
