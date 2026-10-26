namespace Clinic.MedicalRecords.Service.Entities;

public class MedicalAccessAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public Guid? ActorDoctorId { get; set; }
    public Guid? ActorPatientId { get; set; }

    public Guid? AppointmentId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? MedicalReportId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = "Success"; // "Success", "Denied"
    public string? IpAddress { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CorrelationId { get; set; }

    public MedicalAccessAudit() { }

    public MedicalAccessAudit(
        Guid actorUserId,
        string actorRole,
        Guid? actorDoctorId,
        Guid? actorPatientId,
        Guid? appointmentId,
        Guid? patientId,
        Guid? medicalReportId,
        string action,
        string outcome = "Success",
        string? ipAddress = null,
        Guid? correlationId = null)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        ActorDoctorId = actorDoctorId;
        ActorPatientId = actorPatientId;
        AppointmentId = appointmentId;
        PatientId = patientId;
        MedicalReportId = medicalReportId;
        Action = action;
        Outcome = outcome;
        IpAddress = ipAddress;
        Timestamp = DateTimeOffset.UtcNow;
        CorrelationId = correlationId;
    }
}
