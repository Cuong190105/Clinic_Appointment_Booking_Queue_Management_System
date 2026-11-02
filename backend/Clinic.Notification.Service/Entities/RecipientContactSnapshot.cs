namespace Clinic.Notification.Service.Entities;

public sealed record RecipientContactSnapshot(
    string? FullName,
    string? Email,
    string? PhoneNumber
);

