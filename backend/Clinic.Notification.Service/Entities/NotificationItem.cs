namespace Clinic.Notification.Service.Entities;

public sealed class NotificationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientId { get; set; }

    public RecipientType RecipientType { get; set; }

    public NotificationType NotificationType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public string? ContactName { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public Guid? AppointmentId { get; set; }

    public Guid? QueueTicketId { get; set; }

    public Guid? CorrelationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ScheduledFor { get; set; }

    // Navigation
    public ICollection<DeliveryOutboxMessage> Deliveries { get; set; } = new List<DeliveryOutboxMessage>();
}

