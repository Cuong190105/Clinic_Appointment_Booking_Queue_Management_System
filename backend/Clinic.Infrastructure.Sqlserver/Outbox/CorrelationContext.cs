using Clinic.Application.Interfaces;

namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class CorrelationContext : ICorrelationContext
{
    public Guid? CorrelationId { get; private set; }

    public void Set(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation ID must not be empty.", nameof(correlationId));
        }

        CorrelationId = correlationId;
    }
}
