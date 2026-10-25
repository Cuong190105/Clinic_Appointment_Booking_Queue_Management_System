namespace Clinic.Appointment.Service.Services;

using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Appointment.Service.Entities;
using Clinic.Appointment.Service.Enums;
using Clinic.Appointment.Service.Persistence;

public interface IAppointmentOutboxWriter
{
    void Add<TPayload>(EventEnvelope<TPayload> envelope, string routingKey);
}

public class AppointmentOutboxWriter : IAppointmentOutboxWriter
{
    private readonly AppointmentDbContext _dbContext;

    public AppointmentOutboxWriter(AppointmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add<TPayload>(EventEnvelope<TPayload> envelope, string routingKey)
    {
        var payloadJson = JsonSerializer.Serialize(envelope.Payload, EventJsonSerializerOptions.Default);

        var message = new OutboxMessage
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            Producer = envelope.Producer,
            Payload = payloadJson,
            RoutingKey = routingKey,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.OutboxMessages.Add(message);
    }
}

