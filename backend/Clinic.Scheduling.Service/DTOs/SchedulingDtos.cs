namespace Clinic.Scheduling.Service.DTOs;

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
    string IdempotencyKey
);

public record ShiftSuggestionApprovalRequest(
    string? Note
);

public record ShiftSuggestionRejectionRequest(
    string? Reason
);

public record DoctorDto(
    Guid Id,
    Guid EmployeeId,
    string FullName,
    string? PhoneNumber,
    string? Email,
    string LicenseNumber,
    string Qualification,
    int ExperienceYears,
    string? Biography,
    string Status,
    string? SpecialtyName
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

public record UpdateScheduleRequest(
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    int RecommendedCapacity
);

public record CancelScheduleRequest(
    string Reason
);
