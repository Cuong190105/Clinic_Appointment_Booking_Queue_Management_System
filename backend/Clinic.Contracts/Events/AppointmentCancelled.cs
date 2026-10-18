namespace Clinic.Contracts.Events;

public sealed record AppointmentCancelled(
    Guid AppointmentId,
    Guid PatientId,
    Guid? CancelledByUserId,
    DateTimeOffset CancelledAt
);
