namespace Clinic.Notification.Service.Entities;

public sealed class DeliveryOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public Guid? NotificationId { get; set; }

    public Guid RecipientId { get; set; }

    public RecipientType RecipientType { get; set; }

    public NotificationChannel Channel { get; set; }

    public NotificationType NotificationType { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RecipientAddress { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string Content { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public int AttemptsCount { get; set; }

    public int MaxAttempts { get; set; } = 3;

    public DateTimeOffset? NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DeliveredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ScheduledFor { get; set; }

    public Guid? CorrelationId { get; set; }

    // Navigation
    public NotificationItem? Notification { get; set; }

    public ICollection<DeliveryAttempt> Attempts { get; set; } = new List<DeliveryAttempt>();
}
