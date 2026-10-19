using Clinic.Contracts.Events;

namespace Clinic.Application.Interfaces;

public interface IOutboxWriter
{
    void Add<TPayload>(
        EventEnvelope<TPayload> envelope,
        string routingKey);
}