namespace Clinic.API.Models;

public sealed record IdentityRegistrationPatientIds(Guid PersonId, Guid PatientId);

public sealed record IdentityPatientProvisionRequest(
    Guid PatientId,
    Guid PersonId,
    Guid IdentityUserId,
    string FullName,
    string PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    string Gender,
    string Address);
