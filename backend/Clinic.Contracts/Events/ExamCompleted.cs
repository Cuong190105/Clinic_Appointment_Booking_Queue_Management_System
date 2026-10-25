namespace Clinic.Contracts.Events;

public sealed record ExamCompleted(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset CompletedAt
);
