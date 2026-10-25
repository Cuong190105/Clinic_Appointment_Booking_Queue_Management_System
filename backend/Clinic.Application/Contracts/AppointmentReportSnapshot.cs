namespace Clinic.Application.Contracts;

public sealed record AppointmentReportSnapshot(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    string? SpecialtyName,
    DateOnly Date,
    string Status);
