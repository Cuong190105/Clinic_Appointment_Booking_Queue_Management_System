namespace Clinic.Notification.Service.Entities;

public enum DeliveryStatus { Pending, Processing, Sent, RetryScheduled, DeadLettered }

public sealed class NotificationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid RecipientId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<NotificationDelivery> Deliveries { get; set; } = [];
}

public sealed class NotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationMessageId { get; set; }
    public NotificationMessage Message { get; set; } = null!;
    public string Channel { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    /// <summary>Also acts as a processing lease deadline while Status is Processing.</summary>
    public DateTimeOffset? ProcessingLeaseUntil { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? LastErrorCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<DeliveryAttempt> Attempts { get; set; } = [];
}

public sealed class DeliveryAttempt
{
    public long Id { get; set; }
    public Guid NotificationDeliveryId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public bool Succeeded { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? ErrorCode { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class InboxMessage { public Guid EventId { get; set; } public DateTimeOffset ProcessedAt { get; set; } }
