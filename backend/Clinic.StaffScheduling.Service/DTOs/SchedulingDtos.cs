using Clinic.StaffScheduling.Service.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clinic.StaffScheduling.Service.DTOs;

public class FlexibleGenderConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var val = reader.GetInt32();
            return val switch
            {
                0 => "Male",
                1 => "Female",
                2 => "Other",
                _ => "Unspecified"
            };
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (int.TryParse(str, out var num))
            {
                return num switch
                {
                    0 => "Male",
                    1 => "Female",
                    2 => "Other",
                    _ => "Unspecified"
                };
            }
            return str;
        }
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

public class FlexibleDoctorStatusConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var val = reader.GetInt32();
            return val switch
            {
                0 => "Active",
                1 => "Inactive",
                2 => "OnLeave",
                _ => "Active"
            };
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (int.TryParse(str, out var num))
            {
                return num switch
                {
                    0 => "Active",
                    1 => "Inactive",
                    2 => "OnLeave",
                    _ => "Active"
                };
            }
            return string.IsNullOrWhiteSpace(str) ? "Active" : str;
        }
        return "Active";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ?? "Active");
    }
}

public record AvailableSlotDto(
    Guid ScheduleId,
    Guid DoctorId,
    string DoctorName,
    string? SpecialtyName,
    DateOnly Date,
    TimeOnly TimeSlot,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    int RecommendedCapacity,
    int RemainingCapacity
);

public record AvailabilityQuery(
    Guid? DoctorId,
    Guid? SpecialtyId,
    DateOnly? FromDate,
    DateOnly? ToDate
);

public record CreateReservationRequest(
    TimeOnly TimeSlot,
    Guid PatientId
);

public record ReservationResponse(
    Guid Id,
    Guid ScheduleId,
    TimeOnly TimeSlot,
    Guid PatientId,
    string Status,
    DateTimeOffset ExpiresAt,
    string IdempotencyKey,
    Guid? DoctorId = null,
    string? DoctorName = null,
    string? SpecialtyName = null,
    DateOnly? ScheduleDate = null
);

public record ShiftSuggestionApprovalRequest(
    string? Note
);

public record ShiftSuggestionRejectionRequest(
    string? Reason
);

public record DoctorDto(
    Guid Id,
    [property: JsonIgnore] Guid EmployeeId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    string LicenseNumber,
    string Qualification,
    int ExperienceYears,
    string? Biography,
    string Status,
    string? CurrentSpecialty,
    [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender = null,
    Guid? SpecialtyId = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    DateOnly? HireDate = null
);

public record SpecialtyDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly EstablishedDate
);

public record WorkScheduleDto(
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    int RecommendedCapacity,
    string Status,
    string? CancellationReason
);

public record ScheduleDetailDto(
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    string? SpecialtyName,
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    int RecommendedCapacity,
    string Status,
    string? CancellationReason
);

public record WalkInScheduleDto(
    Guid Id, Guid DoctorId, string DoctorName, string? SpecialtyName,
    DateOnly Date, TimeOnly ShiftStart, TimeOnly ShiftEnd, string Status);

public record UpdateScheduleRequest(
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    int RecommendedCapacity
);

public record CancelScheduleRequest(
    string Reason
);

public record CreateEmployeeRequest(
    Guid? PersonId = null,
    Guid? IdentityUserId = null,
    string FullName = "",
    string? PhoneNumber = null,
    string? Email = null,
    [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender = null,
    DateOnly? HireDate = null,
    EmployeeStatus Status = EmployeeStatus.Active,
    Guid? ManagerId = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    Guid? Id = null,
    string? Username = null,
    string? Password = null,
    string[]? Roles = null);

public record CreateEmployeeFromUserRequest(
    Guid UserId,
    DateOnly HireDate,
    string[]? Roles = null);

public record AdminUpdateEmployeeRequest(
    string FullName,
    string? PhoneNumber,
    string? Email,
    [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender,
    DateOnly? DateOfBirth,
    string? Address,
    List<string>? Roles = null,
    Guid? ManagerId = null);

public record UpdateEmployeeRequest(
    string FullName,
    string? PhoneNumber,
    string? Email,
    [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender,
    DateOnly? DateOfBirth,
    string? Address,
    Guid? ManagerId = null);

public record CreateDoctorRequest(
    Guid? EmployeeId = null,
    Guid? PersonId = null,
    Guid? IdentityUserId = null,
    string FullName = "",
    string? PhoneNumber = null,
    string? Email = null,
    [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender = null,
    DateOnly? HireDate = null,
    string LicenseNumber = "",
    string Qualification = "",
    string? Biography = null,
    int ExperienceYears = 0,
    [property: JsonConverter(typeof(FlexibleDoctorStatusConverter))] string Status = "Active",
    Guid? SpecialtyId = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    Guid? Id = null,
    string? Username = null,
    string? Password = null);

public record CreateDoctorFromUserRequest(
    Guid UserId,
    DateOnly HireDate,
    string LicenseNumber,
    string Qualification,
    string? Biography,
    int ExperienceYears,
    [property: JsonConverter(typeof(FlexibleDoctorStatusConverter))] string Status,
    Guid SpecialtyId);

public record UpdateDoctorRequest(
    string FullName,
    string? PhoneNumber,
    string? Email,
    [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender,
    DateOnly? DateOfBirth,
    string? Address,
    string LicenseNumber,
    string Qualification,
    int ExperienceYears,
    string? Biography,
    Guid? SpecialtyId = null);

public record CreateSpecialtyRequest(string Name, string? Description, DateOnly EstablishedDate);
public record UpdateSpecialtyRequest(string Name, string? Description, DateOnly EstablishedDate);
public record CreateShiftRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int RecommendedCapacity);
public record UpdateShiftLegacyRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int RecommendedCapacityPerSlot);
public record CreateShiftSuggestionRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int RecommendedCapacity, string? Reason);
public record UpdateShiftSuggestionRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int RecommendedCapacityPerSlot, string? Reason);

public record IdentityProvisionResult(Guid UserId, Guid PersonId, Guid? PatientId, bool IsActive, bool Created);
public record IdentityRegistrationPatientIds(Guid PersonId, Guid PatientId);
public record IdentityPatientProvisionRequest(Guid PatientId, Guid PersonId, Guid IdentityUserId,
    string FullName, string? PhoneNumber, string? Email, DateOnly DateOfBirth, string Gender, string? Address);
