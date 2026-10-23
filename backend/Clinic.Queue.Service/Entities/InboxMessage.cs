namespace Clinic.Queue.Service.Entities;

using Clinic.Queue.Service.Enums;

public class InboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public Guid CorrelationId { get; set; }
    public InboxMessageStatus Status { get; set; } = InboxMessageStatus.Processing;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
