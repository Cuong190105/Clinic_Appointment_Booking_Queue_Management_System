namespace Clinic.MedicalRecords.Service.Entities;

public enum MedicalRecordStatus { Draft, Finalized }

public sealed class MedicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid QueueTicketId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecialtyName { get; set; } = string.Empty;
    public DateOnly ExamDate { get; set; }
    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ExamStartTime { get; set; }
    public DateTimeOffset? ExamEndTime { get; set; }
    public MedicalRecordStatus Status { get; set; }
    public long WorkflowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InboxMessage
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class MedicalAuditEntry
{
    public long Id { get; set; }
    public Guid? RecordId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Rebuildable, service-owned projection used by patient and clinician history queries.
/// Clinical source data remains in <see cref="MedicalRecord"/>.
/// </summary>
public sealed class MedicalHistoryEntry
{
    public Guid MedicalRecordId { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecialtyName { get; set; } = string.Empty;
    public DateOnly ExamDate { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }
    public long SourceVersion { get; set; }
    public DateTimeOffset ProjectedAt { get; set; } = DateTimeOffset.UtcNow;
}
