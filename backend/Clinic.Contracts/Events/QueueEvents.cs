using System.Text.Json.Serialization;

namespace Clinic.Contracts.Events;

public sealed record ExamStarted
{
    public Guid QueueTicketId { get; init; }
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public string? DoctorName { get; init; }
    public DateOnly? QueueDate { get; init; }
    public DateTimeOffset StartedAt { get; init; }

    [JsonConstructor]
    public ExamStarted(
        Guid queueTicketId,
        Guid appointmentId,
        Guid patientId,
        Guid doctorId,
        string? doctorName,
        DateOnly? queueDate,
        DateTimeOffset startedAt)
    {
        QueueTicketId = queueTicketId;
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        DoctorName = doctorName;
        QueueDate = queueDate;
        StartedAt = startedAt;
    }

    public ExamStarted(Guid queueTicketId, Guid appointmentId, Guid doctorId, DateTimeOffset startedAt)
        : this(queueTicketId, appointmentId, Guid.Empty, doctorId, null, null, startedAt)
    {
    }
}

public sealed record PatientCalled(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    int QueueNumber,
    DateTimeOffset CalledAt
);

public sealed record PatientSkipped(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset SkippedAt
);
