namespace Clinic.Contracts.Events;

public sealed record ExamStarted(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset StartedAt
);

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
