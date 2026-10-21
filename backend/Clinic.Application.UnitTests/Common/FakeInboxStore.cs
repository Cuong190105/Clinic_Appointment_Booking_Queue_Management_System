using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;

namespace Clinic.Application.UnitTests.Common;

public sealed class FakeInboxStore : IInboxStore
{
    private readonly HashSet<(Guid EventId, string ConsumerName)> _processedMessages = [];

    public Task<InboxDecision> TryBeginProcessingAsync<TPayload>(
        EventEnvelope<TPayload> envelope,
        string consumerName,
        CancellationToken cancellationToken)
    {
        var key = (envelope.EventId, consumerName);
        if (_processedMessages.Contains(key))
        {
            return Task.FromResult(InboxDecision.AlreadyProcessed);
        }

        return Task.FromResult(InboxDecision.New);
    }

    public void MarkProcessed(Guid eventId, string consumerName, DateTimeOffset processedAt)
    {
        _processedMessages.Add((eventId, consumerName));
    }

    public void MarkFailed(Guid eventId, string consumerName, string error)
    {
    }
}

