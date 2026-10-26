namespace Clinic.MedicalRecords.Service.DTOs;

public record CreatePatientRequest(
    string FullName,
    string? PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    string? InsuranceNumber = null,
    string? EmergencyContact = null,
    Guid? IdentityUserId = null
);

public record UpdatePatientRequest(
    string? Email,
    string Gender,
    string Address,
    string? InsuranceNumber = null,
    string? EmergencyContact = null
);

public record PatientDto(
    Guid Id,
    string FullName,
    string? PhoneNumber,
    string? Email,
    string Address,
    DateOnly DateOfBirth,
    string Gender,
    string? InsuranceNumber,
    string? EmergencyContact,
    Guid? IdentityUserId = null
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages
);
