namespace Clinic.MedicalRecords.Service.Entities;

public enum InboxMessageStatus
{
    Received,
    Processed,
    Failed
}

public class InboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ConsumerName { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public Guid? CorrelationId { get; set; }
    public InboxMessageStatus Status { get; set; } = InboxMessageStatus.Received;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
