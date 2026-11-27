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
    DateOnly ScheduleDate,
    Guid? SpecialtyId = null
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
    string Status,
    Guid? SpecialtyId = null
);

public record WalkInScheduleCandidate(Guid Id, Guid DoctorId, string DoctorName, string? SpecialtyName, DateOnly Date, TimeOnly ShiftStart, TimeOnly ShiftEnd, string Status, Guid? SpecialtyId = null);

public interface ISchedulingClient
{
    Task<ScheduleReservationResult?> ReserveSlotAsync(Guid scheduleId, TimeOnly timeSlot, Guid patientId, string idempotencyKey, CancellationToken ct = default);
    Task<bool> ConfirmReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default);
    Task<bool> ReleaseSlotByAppointmentAsync(Guid appointmentId, Guid scheduleId, TimeOnly timeSlot, Guid patientId, Guid cancelledBy, Guid? reservationId = null, CancellationToken ct = default);
    Task<ScheduleInfoResult?> GetScheduleAsync(Guid scheduleId, CancellationToken ct = default);
    Task<IReadOnlyList<WalkInScheduleCandidate>> GetWalkInCandidatesAsync(DateOnly date, Guid specialtyId, Guid? doctorId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetDoctorIdsBySpecialtyAsync(Guid specialtyId, CancellationToken ct = default);
}

