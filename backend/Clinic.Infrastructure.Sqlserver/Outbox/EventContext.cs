using Clinic.Application.Interfaces;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class EventContext : IEventContext
{
    public Guid CorrelationId { get; }

    public EventContext(ICorrelationContext correlationContext)
    {
        CorrelationId = correlationContext.CorrelationId ?? Guid.NewGuid();
    }

    public Guid? CausationId { get; init; }
}
