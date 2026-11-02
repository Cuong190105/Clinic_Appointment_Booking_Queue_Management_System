namespace Clinic.MedicalRecords.Service.Entities;

public class MedicalReportAmendment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicalReportId { get; set; }
    public MedicalReport? MedicalReport { get; set; }
    public Guid AppointmentId { get; set; }

    public int AmendmentNumber { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }

    public Guid AuthorDoctorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MedicalReportAmendment() { }

    public MedicalReportAmendment(
        Guid medicalReportId,
        Guid appointmentId,
        int amendmentNumber,
        string reason,
        string? symptoms,
        string? diagnosis,
        string? prescription,
        string? notes,
        Guid authorDoctorId,
        string authorName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Amendment reason is required.", nameof(reason));

        Id = Guid.NewGuid();
        MedicalReportId = medicalReportId;
        AppointmentId = appointmentId;
        AmendmentNumber = amendmentNumber;
        Reason = reason;
        Symptoms = symptoms;
        Diagnosis = diagnosis;
        Prescription = prescription;
        Notes = notes;
        AuthorDoctorId = authorDoctorId;
        AuthorName = authorName;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
