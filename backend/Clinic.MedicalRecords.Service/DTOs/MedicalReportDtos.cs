namespace Clinic.MedicalRecords.Service.DTOs;

public record SaveMedicalReportRequest(
    Guid AppointmentId,
    Guid? PatientId,
    Guid? DoctorId,
    string? DoctorName,
    string? SpecialtyName,
    DateOnly? ScheduleDate,
    string? Symptoms,
    string? Diagnosis,
    string? Prescription,
    string? Notes,
    bool IsFinalize = false
);

public record UpdateMedicalReportRequest(
    string? Symptoms,
    string? Diagnosis,
    string? Prescription,
    string? Notes,
    bool IsFinalize = false
);

public record CreateAmendmentRequest(
    string Reason,
    string? Symptoms,
    string? Diagnosis,
    string? Prescription,
    string? Notes
);

public record MedicalReportAmendmentDto(
    Guid Id,
    Guid MedicalReportId,
    Guid AppointmentId,
    int AmendmentNumber,
    string Reason,
    string? Symptoms,
    string? Diagnosis,
    string? Prescription,
    string? Notes,
    Guid AuthorDoctorId,
    string AuthorName,
    DateTimeOffset CreatedAt
);

public record MedicalReportDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    string SpecialtyName,
    DateOnly ExamDate,
    string? Symptoms,
    string? Diagnosis,
    string? Prescription,
    string? Notes,
    DateTimeOffset? ExamStartTime,
    DateTimeOffset? ExamEndTime,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MedicalReportAmendmentDto> Amendments
);
