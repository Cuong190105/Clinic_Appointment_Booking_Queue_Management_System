namespace Clinic.Contracts.Events;

public sealed record AppointmentCheckedIn(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid VisitId,
    DateTimeOffset CheckedInAt,
    bool Priority = false,
    string? PatientName = null,
    string? DoctorName = null
);
