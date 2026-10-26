namespace Clinic.Contracts.Events;

public sealed record UserProfileUpdated(
    Guid PersonId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    string? Gender,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    Guid? PatientId = null,
    Guid? IdentityUserId = null
);
