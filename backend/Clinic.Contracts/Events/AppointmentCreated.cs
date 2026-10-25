namespace Clinic.Contracts.Events;

public sealed record AppointmentCreated(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid WorkScheduleId,
    DateOnly ScheduleDate,
    TimeOnly TimeSlot,
    string Reason,
    bool IsWalkIn,
    DateTimeOffset CreatedAt
);

