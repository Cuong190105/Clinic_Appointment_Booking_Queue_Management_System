using Clinic.Contracts.Events;

namespace Clinic.MedicalRecords.Service.Entities;

public class EncounterContext
{
    public Guid AppointmentId { get; set; }
    public Guid QueueTicketId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public DateOnly? QueueDate { get; set; }

    public string Status { get; set; } = "Started"; // "Started", "Completed"
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public EncounterContext() { }

    public EncounterContext(
        Guid appointmentId,
        Guid queueTicketId,
        Guid patientId,
        Guid doctorId,
        string? doctorName,
        DateOnly? queueDate,
        DateTimeOffset startedAt)
    {
        AppointmentId = appointmentId;
        QueueTicketId = queueTicketId;
        PatientId = patientId;
        DoctorId = doctorId;
        DoctorName = doctorName;
        QueueDate = queueDate;
        StartedAt = startedAt;
        Status = "Started";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        if (Status == "Completed") return;
        Status = "Completed";
        CompletedAt = completedAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkStarted(ExamStarted evt)
    {
        if (Status == "Completed")
        {
            // Keep the terminal state, but fill fields omitted by an earlier ExamCompleted event.
            QueueTicketId = evt.QueueTicketId;
            PatientId = evt.PatientId;
            DoctorId = evt.DoctorId;
            DoctorName = evt.DoctorName;
            QueueDate = evt.QueueDate;
            StartedAt = evt.StartedAt;
            UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }
        QueueTicketId = evt.QueueTicketId;
        PatientId = evt.PatientId;
        DoctorId = evt.DoctorId;
        DoctorName = evt.DoctorName;
        QueueDate = evt.QueueDate;
        StartedAt = evt.StartedAt;
        Status = "Started";
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
