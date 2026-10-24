namespace Clinic.Contracts.Events;

public sealed record AppointmentCheckedIn(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset CheckedInAt,
    bool Priority = false,
    string? PatientName = null,
    string? DoctorName = null
);
