namespace Clinic.Contracts.Events;

public sealed record UserProfileUpdated(
    Guid PersonId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    string? Gender
);
