namespace Clinic.Infrastructure.Sqlserver.Outbox;

public sealed class OutboxMessage
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public required string EventType { get; set; }

    public int SchemaVersion { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CorrelationId { get; set; }

    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }

    public Guid? CausationId { get; set; }

    public required string Producer { get; set; }

    public required string Payload { get; set; }

    public required string RoutingKey { get; set; }

    public OutboxMessageStatus Status { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? PublishingStartedAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Publishing = 1,
    Sent = 2,
    Failed = 3,
    DeadLetterPending = 4,
    DeadLettered = 5,
    DeadLetterPublishing = 6
}
