namespace Clinic.Application.Interfaces;

/// <summary>
/// Request-scoped metadata shared by all integration events created during one workflow.
/// HTTP middleware can later populate this from X-Correlation-Id and tracing headers.
/// </summary>
public interface IEventContext
{
    Guid CorrelationId { get; }

    Guid? CausationId { get; }
}
