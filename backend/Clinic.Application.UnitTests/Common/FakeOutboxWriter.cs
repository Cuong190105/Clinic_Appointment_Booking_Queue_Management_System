using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;

namespace Clinic.Application.UnitTests.Common;

public sealed class FakeOutboxWriter : IOutboxWriter
{
    public void Add<TPayload>(EventEnvelope<TPayload> envelope, string routingKey)
    {
    }
}

