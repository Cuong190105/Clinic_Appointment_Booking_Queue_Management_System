namespace Clinic.Scheduling.Service.Services;

using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;

public interface ISchedulingOutboxWriter
{
    void Add<TPayload>(EventEnvelope<TPayload> envelope, string routingKey);
}

public class SchedulingOutboxWriter : ISchedulingOutboxWriter
{
    private readonly SchedulingDbContext _dbContext;

    public SchedulingOutboxWriter(SchedulingDbContext dbContext)
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

