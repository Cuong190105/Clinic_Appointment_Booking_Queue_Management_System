namespace Clinic.Application.Interfaces;

public interface ICorrelationContext
{
    Guid? CorrelationId { get; }

    void Set(Guid correlationId);
}
