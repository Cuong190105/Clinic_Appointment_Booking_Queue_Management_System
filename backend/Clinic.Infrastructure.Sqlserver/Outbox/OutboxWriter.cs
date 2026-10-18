using System.Text.Json;
using System.Diagnostics;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Infrastructure.Sqlserver.Persistence;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly ApplicationDbContext _dbContext;

    public OutboxWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add<TPayload>(
        EventEnvelope<TPayload> envelope,
        string routingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        if (envelope.EventId == Guid.Empty)
        {
            throw new ArgumentException("EventId must not be empty.", nameof(envelope));
        }

        if (envelope.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException("CorrelationId must not be empty.", nameof(envelope));
        }

        var payload = JsonSerializer.Serialize(
            envelope,
            EventJsonSerializerOptions.Default);

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            CorrelationId = envelope.CorrelationId,
            TraceParent = Activity.Current?.Id,
            TraceState = Activity.Current?.TraceStateString,
            CausationId = envelope.CausationId,
            Producer = envelope.Producer,
            Payload = payload,
            RoutingKey = routingKey,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
