namespace Clinic.Infrastructure.Sqlserver.Inbox;

public sealed class InboxMessage
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public required string ConsumerName { get; set; }

    public required string EventType { get; set; }

    public int SchemaVersion { get; set; }

    public Guid CorrelationId { get; set; }

    public InboxMessageStatus Status { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessingStartedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }
}

public enum InboxMessageStatus
{
    Processing = 0,
    Processed = 1,
    Failed = 2
}
