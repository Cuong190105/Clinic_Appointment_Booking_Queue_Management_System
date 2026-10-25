namespace Clinic.Scheduling.Service.Entities;

using Clinic.Scheduling.Service.Enums;

public class OutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset OccurredAt { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
    public string Producer { get; set; } = "Clinic.Scheduling.Service";
    public string Payload { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? PublishingStartedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}

