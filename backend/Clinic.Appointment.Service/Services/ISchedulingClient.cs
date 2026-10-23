namespace Clinic.Appointment.Service.Services;

public record ScheduleReservationResult(
    Guid Id,
    Guid ScheduleId,
    TimeOnly TimeSlot,
    Guid PatientId,
    string Status,
    DateTimeOffset ExpiresAt,
    string IdempotencyKey,
    Guid DoctorId,
    string? DoctorName,
    string? SpecialtyName,
    DateOnly ScheduleDate
);

public record ScheduleInfoResult(
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    string? SpecialtyName,
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    int RecommendedCapacity,
    string Status
);

public interface ISchedulingClient
{
    Task<ScheduleReservationResult?> ReserveSlotAsync(Guid scheduleId, TimeOnly timeSlot, Guid patientId, string idempotencyKey, CancellationToken ct = default);
    Task<bool> ConfirmReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default);
    Task<ScheduleInfoResult?> GetScheduleAsync(Guid scheduleId, CancellationToken ct = default);
}

