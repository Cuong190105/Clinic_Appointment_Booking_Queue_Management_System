namespace Clinic.Appointment.Service.Services;

using System.Text;
using Clinic.Appointment.Service.Enums;
using Clinic.Appointment.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public class AppointmentOutboxPublisher : BackgroundService
{
    private const string ExchangeName = "clinic.events";
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentOutboxPublisher> _logger;
    private readonly IAppointmentWorkSignal? _workSignal;

    private IConnection? _connection;
    private IChannel? _channel;

    public AppointmentOutboxPublisher(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AppointmentOutboxPublisher> logger,
        IAppointmentWorkSignal? workSignal = null)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _workSignal = workSignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentOutboxPublisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                published = await PublishPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during outbox publishing cycle.");
                await DisconnectAsync();
            }

            if (published >= 20)
            {
                continue;
            }

            try
            {
                var rawDelayMs = _configuration.GetValue<int?>("Outbox:PollingIntervalMs") ?? 1000;
                var delayMs = Math.Clamp(rawDelayMs, 100, 60_000);
                var delay = TimeSpan.FromMilliseconds(delayMs);

                if (_workSignal != null)
                {
                    await _workSignal.WaitForOutboxAsync(delay, stoppingToken);
                }
                else
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        await DisconnectAsync();
        _logger.LogInformation("AppointmentOutboxPublisher stopped.");
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return;
        }

        await DisconnectAsync();

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
            ClientProvidedName = "clinic-appointment-outbox-publisher"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);
    }

    private async Task<int> PublishPendingOutboxMessagesAsync(CancellationToken ct)
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
            return 0;
        }

        try
        {
            await EnsureConnectedAsync(ct);

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

                await _channel!.BasicPublishAsync(
                    exchange: ExchangeName,
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
            return pendingMessages.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to publish outbox messages to RabbitMQ: {Message}", ex.Message);
            await DisconnectAsync();
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
