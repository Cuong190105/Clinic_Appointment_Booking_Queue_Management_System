namespace Clinic.Contracts.Events;

public sealed record AppointmentConfirmed(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid WorkScheduleId,
    DateOnly ScheduleDate,
    TimeOnly TimeSlot,
    Guid ConfirmedByUserId,
    DateTimeOffset ConfirmedAt,
    string? PatientName = null,
    string? PatientEmail = null,
    string? PatientPhone = null,
    Guid? IdentityUserId = null
);
