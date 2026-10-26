namespace Clinic.Notification.Service.Entities;

public sealed class InboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public string Status { get; set; } = "Pending";

    public string? Error { get; set; }
}
