namespace Clinic.Contracts.Events;

public sealed record WorkScheduleCreated(
    Guid WorkScheduleId,
    Guid DoctorId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int PatientLimit
);

public sealed record WorkScheduleUpdated(
    Guid WorkScheduleId,
    Guid DoctorId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int PatientLimit
);

public sealed record WorkScheduleCancelled(
    Guid WorkScheduleId,
    Guid DoctorId,
    string Reason,
    DateTimeOffset CancelledAt
);
