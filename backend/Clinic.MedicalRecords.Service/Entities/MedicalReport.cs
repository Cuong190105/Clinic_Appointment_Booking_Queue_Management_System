namespace Clinic.MedicalRecords.Service.Entities;

public class MedicalReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public string? SpecialtyName { get; set; }
    public DateOnly? ScheduleDate { get; set; }

    public DateTimeOffset? ExamStartTime { get; set; }
    public DateTimeOffset? ExamEndTime { get; set; }

    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft"; // "Draft", "Finalized"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public List<MedicalReportAmendment> Amendments { get; set; } = new();

    public MedicalReport() { }

    public MedicalReport(
        Guid appointmentId,
        Guid patientId,
        Guid doctorId,
        string? doctorName,
        string? specialtyName,
        DateOnly? scheduleDate,
        string? symptoms,
        string? diagnosis,
        string? prescription,
        string? notes,
        DateTimeOffset? examStartTime = null)
    {
        Id = Guid.NewGuid();
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        DoctorName = doctorName;
        SpecialtyName = specialtyName;
        ScheduleDate = scheduleDate;
        Symptoms = symptoms;
        Diagnosis = diagnosis;
        Prescription = prescription;
        Notes = notes;
        ExamStartTime = examStartTime ?? DateTimeOffset.UtcNow;
        Status = "Draft";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDraft(string? symptoms, string? diagnosis, string? prescription, string? notes)
    {
        if (Status.Equals("Finalized", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot update a finalized medical report directly. Use amendment instead.");
        }

        Symptoms = symptoms;
        Diagnosis = diagnosis;
        Prescription = prescription;
        Notes = notes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void FinalizeReport(DateTimeOffset? examEndTime = null)
    {
        if (Status.Equals("Finalized", StringComparison.OrdinalIgnoreCase))
            return; // Idempotent

        Status = "Finalized";
        ExamEndTime = examEndTime ?? DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
