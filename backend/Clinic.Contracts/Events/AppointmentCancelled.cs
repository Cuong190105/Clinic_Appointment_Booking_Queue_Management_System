using System.Text.Json.Serialization;

namespace Clinic.Contracts.Events;

public sealed record AppointmentCancelled
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public Guid? CancelledByUserId { get; init; }
    public DateTimeOffset CancelledAt { get; init; }
    public Guid WorkScheduleId { get; init; }
    public TimeOnly TimeSlot { get; init; }
    public bool IsRescheduleRelease { get; init; }
    public string? PatientName { get; init; }
    public string? PatientEmail { get; init; }
    public string? PatientPhone { get; init; }
    public Guid? IdentityUserId { get; init; }

    [JsonConstructor]
    public AppointmentCancelled()
    {
    }

    public AppointmentCancelled(
        Guid AppointmentId,
        Guid PatientId,
        Guid? CancelledByUserId,
        DateTimeOffset CancelledAt,
        Guid WorkScheduleId = default,
        TimeOnly TimeSlot = default,
        bool IsRescheduleRelease = false,
        string? PatientName = null,
        string? PatientEmail = null,
        string? PatientPhone = null,
        Guid? IdentityUserId = null)
    {
        this.AppointmentId = AppointmentId;
        this.PatientId = PatientId;
        this.CancelledByUserId = CancelledByUserId;
        this.CancelledAt = CancelledAt;
        this.WorkScheduleId = WorkScheduleId;
        this.TimeSlot = TimeSlot;
        this.IsRescheduleRelease = IsRescheduleRelease;
        this.PatientName = PatientName;
        this.PatientEmail = PatientEmail;
        this.PatientPhone = PatientPhone;
        this.IdentityUserId = IdentityUserId;
    }
}
