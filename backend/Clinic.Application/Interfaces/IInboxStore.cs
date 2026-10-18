using Clinic.Contracts.Events;

namespace Clinic.Application.Interfaces;

public interface IInboxStore
{
    Task<InboxDecision> TryBeginProcessingAsync<TPayload>(
        EventEnvelope<TPayload> envelope,
        string consumerName,
        CancellationToken cancellationToken);

    void MarkProcessed(
        Guid eventId,
        string consumerName,
        DateTimeOffset processedAt);

    void MarkFailed(
        Guid eventId,
        string consumerName,
        string error);
}

public enum InboxDecision
{
    New = 0,
    AlreadyProcessed = 1,
    CurrentlyProcessing = 2
}
