namespace Clinic.Contracts.Events;

public sealed record ExamStarted(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset StartedAt,
    Guid PatientId = default,
    string? PatientName = null,
    string? DoctorName = null
);

public sealed record PatientCalled(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    int QueueNumber,
    DateTimeOffset CalledAt,
    Guid PatientId = default
);

public sealed record PatientSkipped(
    Guid QueueTicketId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset SkippedAt
);
