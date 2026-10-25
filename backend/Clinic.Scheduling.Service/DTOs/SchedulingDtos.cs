using Clinic.Scheduling.Service.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clinic.Scheduling.Service.DTOs;

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
    Guid DoctorId = default,
    string? DoctorName = null,
    string? SpecialtyName = null,
    DateOnly ScheduleDate = default
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

public record CreateEmployeeRequest(Guid PersonId, Guid IdentityUserId, string FullName, string? PhoneNumber,
    string? Email, [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender, DateOnly HireDate, EmployeeStatus Status, Guid? ManagerId = null,
    DateOnly? DateOfBirth = null, string? Address = null, Guid? Id = null);

public record UpdateEmployeeRequest(string FullName, string? PhoneNumber, string? Email, [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender,
    DateOnly? DateOfBirth, string? Address, Guid? ManagerId = null);

public record CreateDoctorRequest(Guid? EmployeeId, Guid PersonId, Guid IdentityUserId, string FullName,
    string? PhoneNumber, string? Email, [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender, DateOnly HireDate, string LicenseNumber,
    string Qualification, string? Biography, int ExperienceYears, string Status, Guid SpecialtyId,
    DateOnly? DateOfBirth = null, string? Address = null, Guid? Id = null);

public record UpdateDoctorRequest(string FullName, string? PhoneNumber, string? Email, [property: JsonConverter(typeof(FlexibleGenderConverter))] string? Gender,
    DateOnly? DateOfBirth, string? Address, string LicenseNumber, string Qualification,
    int ExperienceYears, string? Biography, Guid? SpecialtyId = null);

public record CreateSpecialtyRequest(string Name, string? Description, DateOnly EstablishedDate);
public record UpdateSpecialtyRequest(string Name, string? Description, DateOnly EstablishedDate);
public record CreateShiftRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int RecommendedCapacity);
public record UpdateShiftLegacyRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int RecommendedCapacityPerSlot);
public record CreateShiftSuggestionRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int RecommendedCapacity, string? Reason);
public record UpdateShiftSuggestionRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    int RecommendedCapacityPerSlot, string? Reason);
