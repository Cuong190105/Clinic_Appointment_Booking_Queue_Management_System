namespace Clinic.Contracts.Events;

public sealed record ExamCompleted(
    Guid VisitId,
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset CompletedAt
);
