using System.Text.Json.Serialization;
using Clinic.Contracts.Serialization;

namespace Clinic.MedicalRecords.Service.DTOs;

public record CreatePatientRequest(
    string FullName,
    string? PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    [property: JsonConverter(typeof(GenderStringJsonConverter))] string Gender,
    string Address,
    string? InsuranceNumber = null,
    string? EmergencyContact = null,
    Guid? IdentityUserId = null
);

public record ProvisionIdentityPatientRequest(
    Guid PatientId,
    Guid PersonId,
    Guid IdentityUserId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    string Gender,
    string Address
);

public record IdentityRegistrationPatientIds(Guid PersonId, Guid PatientId);

public record UpdatePatientRequest(
    string? Email,
    [property: JsonConverter(typeof(GenderStringJsonConverter))] string Gender,
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
