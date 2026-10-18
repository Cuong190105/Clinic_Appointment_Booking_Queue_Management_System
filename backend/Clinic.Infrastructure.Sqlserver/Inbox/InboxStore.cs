using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Inbox;

public sealed class InboxStore : IInboxStore
{
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private readonly ApplicationDbContext _dbContext;

    public InboxStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InboxDecision> TryBeginProcessingAsync<TPayload>(
        EventEnvelope<TPayload> envelope,
        string consumerName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        var existing = await _dbContext.InboxMessages
            .SingleOrDefaultAsync(
                x => x.EventId == envelope.EventId &&
                     x.ConsumerName == consumerName,
                cancellationToken);

        if (existing is null)
        {
            _dbContext.InboxMessages.Add(new InboxMessage
            {
                EventId = envelope.EventId,
                ConsumerName = consumerName,
                EventType = envelope.EventType,
                SchemaVersion = envelope.SchemaVersion,
                CorrelationId = envelope.CorrelationId,
                Status = InboxMessageStatus.Processing,
                ReceivedAt = DateTimeOffset.UtcNow,
                ProcessingStartedAt = DateTimeOffset.UtcNow
            });

            return InboxDecision.New;
        }

        if (existing.Status == InboxMessageStatus.Processed)
        {
            return InboxDecision.AlreadyProcessed;
        }

        var isLeaseExpired = existing.ProcessingStartedAt is null ||
            existing.ProcessingStartedAt <= DateTimeOffset.UtcNow - ProcessingLease;

        if (existing.Status == InboxMessageStatus.Processing && !isLeaseExpired)
        {
            return InboxDecision.CurrentlyProcessing;
        }

        existing.Status = InboxMessageStatus.Processing;
        existing.ProcessingStartedAt = DateTimeOffset.UtcNow;
        existing.ProcessedAt = null;
        existing.LastError = null;
        existing.RetryCount++;

        return InboxDecision.New;
    }

    public void MarkProcessed(
        Guid eventId,
        string consumerName,
        DateTimeOffset processedAt)
    {
        var inbox = GetTracked(eventId, consumerName);
        inbox.Status = InboxMessageStatus.Processed;
        inbox.ProcessedAt = processedAt;
        inbox.ProcessingStartedAt = null;
        inbox.LastError = null;
    }

    public void MarkFailed(
        Guid eventId,
        string consumerName,
        string error)
    {
        var inbox = GetTracked(eventId, consumerName);
        inbox.Status = InboxMessageStatus.Failed;
        inbox.ProcessingStartedAt = null;
        inbox.RetryCount++;
        inbox.LastError = error;
    }

    private InboxMessage GetTracked(Guid eventId, string consumerName)
    {
        var inbox = _dbContext.InboxMessages.Local
            .SingleOrDefault(x =>
                x.EventId == eventId &&
                x.ConsumerName == consumerName);

        return inbox ?? throw new InvalidOperationException(
            $"Inbox message {eventId} for consumer '{consumerName}' was not started.");
    }
}
