using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.MedicalRecords.Service.Services;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.MedicalRecords.Service.Services;

public sealed class MedicalRecordsEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MedicalRecordsEventConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public MedicalRecordsEventConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MedicalRecordsEventConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MedicalRecordsEventConsumer started.");

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
                _logger.LogWarning("MedicalRecordsEventConsumer connection/loop warning: {Message}", ex.Message);
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
        _logger.LogInformation("MedicalRecordsEventConsumer stopped.");
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
            ClientProvidedName = "clinic-medical-records-event-consumer"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        const string exchangeName = "clinic.events";
        const string queueName = "clinic.medical-records.events.v2";
        const string deadLetterExchange = "clinic.events.dlx";
        const string deadLetterQueue = "clinic.medical-records.events.dead";

        await _channel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await _channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await _channel.QueueBindAsync(deadLetterQueue, deadLetterExchange, queueName, cancellationToken: ct);

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
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = queueName
            },
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "identity.user-profile-updated",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "queue.exam-started",
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "queue.exam-completed",
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                await using var scope = _serviceProvider.CreateAsyncScope();
                var manager = scope.ServiceProvider.GetRequiredService<IMedicalRecordsManager>();
                var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
                Guid? eventId = Guid.TryParse(args.BasicProperties.MessageId, out var messageId) ? messageId : null;
                string eventType = "Unknown";
                int schemaVersion = 1;
                Guid? correlationId = null;
                EventEnvelope<UserProfileUpdated>? userEnvelope = null;
                EventEnvelope<ExamStarted>? startedEnvelope = null;
                EventEnvelope<ExamCompleted>? completedEnvelope = null;

                if (args.RoutingKey == "identity.user-profile-updated" || json.Contains(EventTypes.UserProfileUpdated))
                {
                    UserProfileUpdated? payload = null;
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<EventEnvelope<UserProfileUpdated>>(
                            json,
                            EventJsonSerializerOptions.Default);
                        userEnvelope = envelope;
                        if (envelope is not null) eventId = envelope.EventId;
                        payload = envelope?.Payload;
                    }
                    catch { }

                    payload ??= JsonSerializer.Deserialize<UserProfileUpdated>(json, EventJsonSerializerOptions.Default);

                    if (payload != null)
                    {
                        if (payload.PersonId == Guid.Empty || string.IsNullOrWhiteSpace(payload.FullName))
                            throw new FormatException("UserProfileUpdated event is missing identity fields.");
                        eventType = EventTypes.UserProfileUpdated;
                        await EnsureNotProcessedAsync(db, eventId, ct);
                        await manager.HandleUserProfileUpdatedAsync(payload, ct);
                        _logger.LogInformation("Processed UserProfileUpdated event for PersonId={PersonId}", payload.PersonId);
                    }
                }
                else if (args.RoutingKey == "queue.exam-started" || json.Contains(EventTypes.ExamStarted))
                {
                    ExamStarted? payload = null;
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<EventEnvelope<ExamStarted>>(
                            json,
                            EventJsonSerializerOptions.Default);
                        startedEnvelope = envelope;
                        if (envelope is not null) eventId = envelope.EventId;
                        payload = envelope?.Payload;
                    }
                    catch { }

                    payload ??= JsonSerializer.Deserialize<ExamStarted>(json, EventJsonSerializerOptions.Default);

                    if (payload != null)
                    {
                        if (payload.AppointmentId == Guid.Empty || payload.PatientId == Guid.Empty || payload.DoctorId == Guid.Empty)
                            throw new FormatException("ExamStarted event is missing required encounter identifiers.");
                        eventType = EventTypes.ExamStarted;
                        await EnsureNotProcessedAsync(db, eventId, ct);
                        await manager.HandleExamStartedAsync(payload, ct);
                        _logger.LogInformation("Processed ExamStarted event for AppointmentId={AppointmentId}", payload.AppointmentId);
                    }
                }
                else if (args.RoutingKey == "queue.exam-completed" || json.Contains(EventTypes.ExamCompleted))
                {
                    ExamCompleted? payload = null;
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<EventEnvelope<ExamCompleted>>(
                            json,
                            EventJsonSerializerOptions.Default);
                        completedEnvelope = envelope;
                        if (envelope is not null) eventId = envelope.EventId;
                        payload = envelope?.Payload;
                    }
                    catch { }

                    payload ??= JsonSerializer.Deserialize<ExamCompleted>(json, EventJsonSerializerOptions.Default);

                    if (payload != null)
                    {
                        if (payload.AppointmentId == Guid.Empty || payload.DoctorId == Guid.Empty)
                            throw new FormatException("ExamCompleted event is missing required identifiers.");
                        eventType = EventTypes.ExamCompleted;
                        await EnsureNotProcessedAsync(db, eventId, ct);
                        await manager.HandleExamCompletedAsync(payload, ct);
                        _logger.LogInformation("Processed ExamCompleted event for AppointmentId={AppointmentId}", payload.AppointmentId);
                    }
                }

                if (eventId.HasValue)
                {
                    object? envelope = userEnvelope ?? (object?)startedEnvelope ?? completedEnvelope;
                    if (envelope is not null)
                    {
                        var metadata = envelope switch
                        {
                            EventEnvelope<UserProfileUpdated> e => (e.EventId, e.EventType, e.SchemaVersion, (Guid?)e.CorrelationId),
                            EventEnvelope<ExamStarted> e => (e.EventId, e.EventType, e.SchemaVersion, (Guid?)e.CorrelationId),
                            EventEnvelope<ExamCompleted> e => (e.EventId, e.EventType, e.SchemaVersion, (Guid?)e.CorrelationId),
                            _ => (eventId.Value, eventType, schemaVersion, correlationId)
                        };
                        db.InboxMessages.Add(new InboxMessage
                        {
                            ConsumerName = "medical-records",
                            EventId = metadata.Item1,
                            EventType = metadata.Item2,
                            SchemaVersion = metadata.Item3,
                            CorrelationId = metadata.Item4,
                            Status = InboxMessageStatus.Processed,
                            ProcessedAt = DateTimeOffset.UtcNow
                        });
                        await db.SaveChangesAsync(ct);
                    }
                }
                else if (eventType == "Unknown")
                {
                    throw new FormatException("Unsupported or malformed integration event payload.");
                }

                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, ct);
            }
            catch (DuplicateIntegrationEventException)
            {
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event from {RoutingKey}", args.RoutingKey);
                try
                {
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, ct);
                }
                catch { }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("MedicalRecordsEventConsumer connected and subscribed to {QueueName}", queueName);

        var tcs = new TaskCompletionSource();
        using (ct.Register(() => tcs.TrySetResult()))
        {
            await tcs.Task;
        }
    }

    private static async Task EnsureNotProcessedAsync(MedicalRecordsDbContext db, Guid? eventId, CancellationToken ct)
    {
        if (eventId.HasValue && await db.InboxMessages.AnyAsync(
                m => m.ConsumerName == "medical-records" && m.EventId == eventId.Value && m.Status == InboxMessageStatus.Processed, ct))
            throw new DuplicateIntegrationEventException();
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
            _logger.LogDebug(ex, "Error closing RabbitMQ connection/channel.");
        }
    }
}

internal sealed class DuplicateIntegrationEventException : Exception { }
