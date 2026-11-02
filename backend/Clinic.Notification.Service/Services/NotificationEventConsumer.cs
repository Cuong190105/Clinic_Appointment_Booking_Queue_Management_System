using System.Text;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clinic.Notification.Service.Services;

public sealed class NotificationEventConsumer(IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationEventConsumer> logger) : BackgroundService
{
    private IConnection? connection; private IChannel? channel; private const string QueueName = "q.notification.business-events";
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (config.GetValue<bool>("SkipBackgroundWorkers")) return;
        var factory = new ConnectionFactory { HostName = config["RabbitMq:Host"] ?? "rabbitmq", Port = config.GetValue("RabbitMq:Port", 5672),
            UserName = config["RabbitMq:Username"] ?? "clinic", Password = config["RabbitMq:Password"] ?? "clinic-dev-password", VirtualHost = config["RabbitMq:VirtualHost"] ?? "/", AutomaticRecoveryEnabled = true };
        while (!ct.IsCancellationRequested)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(ct); channel = await connection.CreateChannelAsync(cancellationToken: ct);
                await channel.ExchangeDeclareAsync("clinic.events", ExchangeType.Topic, true, false, cancellationToken: ct);
                await channel.ExchangeDeclareAsync("clinic.dead-letter", ExchangeType.Direct, true, false, cancellationToken: ct);
                await channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "clinic.dead-letter", ["x-dead-letter-routing-key"] = "q.notification.dlq" }, cancellationToken: ct);
                await channel.QueueDeclareAsync("q.notification.dlq", true, false, false, cancellationToken: ct);
                foreach (var key in new[] { "notification.requested", "appointment.created", "appointment.confirmed", "appointment.cancelled", "queue.patient-called" }) await channel.QueueBindAsync(QueueName, "clinic.events", key, cancellationToken: ct);
                await channel.QueueBindAsync("q.notification.dlq", "clinic.dead-letter", "q.notification.dlq", cancellationToken: ct);
                await channel.BasicQosAsync(0, 12, false, ct);
                var consumer = new AsyncEventingBasicConsumer(channel); consumer.ReceivedAsync += async (_, a) => await ConsumeAsync(a, ct);
                await channel.BasicConsumeAsync(QueueName, false, consumer, ct); await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Notification broker disconnected; reconnecting"); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }

    private async Task ConsumeAsync(BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.Span); var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            NotificationRequested request; Guid eventId; Guid correlationId;
            if (args.RoutingKey == "notification.requested")
            {
                var e = JsonSerializer.Deserialize<EventEnvelope<NotificationRequested>>(json, opts) ?? throw new JsonException(); request = e.Payload; eventId = e.EventId; correlationId = e.CorrelationId;
            }
            else if (args.RoutingKey == "appointment.cancelled")
            {
                var e = JsonSerializer.Deserialize<EventEnvelope<AppointmentCancelled>>(json, opts) ?? throw new JsonException(); eventId = e.EventId; correlationId = e.CorrelationId;
                request = new(e.Payload.PatientId, "appointment-cancelled", "Lịch hẹn đã hủy", "Lịch hẹn của bạn đã được hủy.", ["in-app"]);
            }
            else if (args.RoutingKey == "queue.patient-called")
            {
                var e = JsonSerializer.Deserialize<EventEnvelope<PatientCalled>>(json, opts) ?? throw new JsonException(); eventId = e.EventId; correlationId = e.CorrelationId;
                request = new(e.Payload.PatientId, "patient-called", "Đến lượt khám", $"Số thứ tự {e.Payload.QueueNumber} đã được gọi.", ["in-app"]);
            }
            else if (args.RoutingKey == "appointment.confirmed")
            {
                var e = JsonSerializer.Deserialize<EventEnvelope<AppointmentConfirmed>>(json, opts) ?? throw new JsonException(); eventId = e.EventId; correlationId = e.CorrelationId;
                request = new(e.Payload.PatientId, "appointment-confirmed", "Lịch hẹn đã xác nhận", $"Lịch hẹn ngày {e.Payload.ScheduleDate:dd/MM/yyyy} đã được xác nhận.", ["in-app"]);
            }
            else
            {
                var e = JsonSerializer.Deserialize<EventEnvelope<AppointmentCreated>>(json, opts) ?? throw new JsonException(); eventId = e.EventId; correlationId = e.CorrelationId;
                request = new(e.Payload.PatientId, "appointment-created", "Đặt lịch thành công", $"Lịch hẹn ngày {e.Payload.ScheduleDate:dd/MM/yyyy} đã được tiếp nhận.", ["in-app"]);
            }
            await using var scope = scopes.CreateAsyncScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<NotificationIngestionService>();
            var handled = await ingestion.PersistAsync(eventId, correlationId, request, ct);
            if (handled) NotificationTelemetry.Events.Add(1);
            else NotificationTelemetry.Duplicates.Add(1);
            await channel!.BasicAckAsync(args.DeliveryTag, false, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification event rejected; message body is intentionally omitted");
            await channel!.BasicNackAsync(args.DeliveryTag, false, false, ct);
        }
    }

}
