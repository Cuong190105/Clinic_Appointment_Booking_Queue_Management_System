namespace Clinic.Contracts.Events;

public sealed record NotificationRequested(
    Guid RecipientId,
    string TemplateKey,
    string Title,
    string Body,
    string[] Channels,
    string? Email = null,
    string? PhoneNumber = null,
    DateTimeOffset? ScheduledAt = null);
