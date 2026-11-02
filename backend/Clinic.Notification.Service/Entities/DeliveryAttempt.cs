namespace Clinic.Notification.Service.Entities;

public sealed class DeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeliveryOutboxMessageId { get; set; }

    public int AttemptNumber { get; set; }

    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;

    public DeliveryStatus Status { get; set; }

    public string? ProviderResponse { get; set; }

    public string? ProviderReference { get; set; }

    public Guid? CorrelationId { get; set; }

    /// <summary>Identity subject of the administrator who explicitly requested a replay.</summary>
    public Guid? OperatorId { get; set; }

    // Navigation
    public DeliveryOutboxMessage? DeliveryOutboxMessage { get; set; }
}

