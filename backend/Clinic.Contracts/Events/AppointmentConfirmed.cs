namespace Clinic.Contracts.Events;

public sealed record AppointmentConfirmed(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid WorkScheduleId,
    DateOnly ScheduleDate,
    TimeOnly TimeSlot,
    Guid ConfirmedByUserId,
    DateTimeOffset ConfirmedAt
);

