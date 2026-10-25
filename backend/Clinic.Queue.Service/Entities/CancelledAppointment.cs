namespace Clinic.Queue.Service.Entities;

/// <summary>
/// Tombstone used to prevent a delayed AppointmentCheckedIn event from creating a queue ticket
/// after the appointment cancellation event has already been processed.
/// </summary>
public sealed class CancelledAppointment
{
    public Guid AppointmentId { get; set; }
    public DateTimeOffset CancelledAt { get; set; }
}
