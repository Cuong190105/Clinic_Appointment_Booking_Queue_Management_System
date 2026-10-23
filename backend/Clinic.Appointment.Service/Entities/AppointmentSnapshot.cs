namespace Clinic.Appointment.Service.Entities;

using Clinic.Appointment.Service.Enums;

public class AppointmentSnapshot
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid OldWorkScheduleId { get; set; }
    public Guid OldDoctorId { get; set; }
    public string OldDoctorName { get; set; } = string.Empty;
    public string? OldSpecialtyName { get; set; }
    public DateOnly OldScheduleDate { get; set; }
    public TimeOnly TimeSlot { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public string? UpdatedByUserName { get; set; }

    // Navigation property
    public Appointment Appointment { get; set; } = null!;

    public AppointmentSnapshot() { }

    public AppointmentSnapshot(Appointment appointment, string? updaterName = null, Guid? updatedByUserId = null)
    {
        Appointment = appointment;
        AppointmentId = appointment.Id;
        OldWorkScheduleId = appointment.WorkScheduleId;
        OldDoctorId = appointment.DoctorId;
        OldDoctorName = appointment.DoctorName;
        OldSpecialtyName = appointment.SpecialtyName;
        OldScheduleDate = appointment.ScheduleDate;
        TimeSlot = appointment.TimeSlot;
        Reason = appointment.Reason;
        Status = appointment.Status;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId ?? appointment.UpdatedByUserId;
        UpdatedByUserName = updaterName;
    }
}
