namespace Clinic.Notification.Service.Entities;

/// <summary>
/// Durable tombstone: a reminder for (AppointmentId, SlotKey) must never be sent.
/// Written when the appointment is cancelled or rescheduled away from the slot, so that
/// late or re-ordered "confirmed" events cannot resurrect the reminder.
/// </summary>
public sealed class ReminderSuppression
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    public string SlotKey { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public DateTimeOffset SuppressedAt { get; set; } = DateTimeOffset.UtcNow;
}
