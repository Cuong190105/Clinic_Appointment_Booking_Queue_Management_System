namespace Clinic.Queue.Service.Services;

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
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        const string exchangeName = "clinic.events";
        const string queueName = "clinic.queue.events";

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

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "appointment.checked-in",
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

                if (envelope != null && envelope.Payload != null)
                {
                    await queueManager.ProcessAppointmentCheckedInAsync(envelope, ct);
                    _logger.LogInformation("Successfully handled AppointmentCheckedIn for AppointmentId={AppointmentId}", envelope.Payload.AppointmentId);
                }

                if (_channel is { IsOpen: true })
                {
                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queue event message with RoutingKey={RoutingKey}", args.RoutingKey);
                if (_channel is { IsOpen: true })
                {
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
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

