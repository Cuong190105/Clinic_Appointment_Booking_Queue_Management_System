namespace Clinic.Appointment.Service.Entities;

using Clinic.Appointment.Service.Enums;

public class Appointment
{
    private const int CancelLimitHours = 24;

    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? PatientGender { get; set; }
    public DateOnly? PatientDateOfBirth { get; set; }

    public Guid WorkScheduleId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? SpecialtyName { get; set; }
    public DateOnly ScheduleDate { get; set; }
    public TimeOnly TimeSlot { get; set; }
    public string Reason { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public bool IsWalkIn { get; set; } = false;
    public string? IdempotencyKey { get; set; }

    public Guid UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    public ICollection<AppointmentSnapshot> Snapshots { get; set; } = new List<AppointmentSnapshot>();

    public Appointment() { }

    public Appointment(
        Guid patientId,
        string patientName,
        string? patientPhone,
        string? patientGender,
        DateOnly? patientDateOfBirth,
        Guid workScheduleId,
        Guid doctorId,
        string doctorName,
        string? specialtyName,
        DateOnly scheduleDate,
        TimeOnly timeSlot,
        string reason,
        Guid createdByUserId,
        bool isWalkIn = false)
    {
        Id = Guid.NewGuid();
        PatientId = patientId;
        PatientName = patientName ?? string.Empty;
        PatientPhone = patientPhone;
        PatientGender = patientGender;
        PatientDateOfBirth = patientDateOfBirth;

        WorkScheduleId = workScheduleId;
        DoctorId = doctorId;
        DoctorName = doctorName ?? string.Empty;
        SpecialtyName = specialtyName;
        ScheduleDate = scheduleDate;
        TimeSlot = timeSlot;
        Reason = reason ?? string.Empty;

        IsWalkIn = isWalkIn;
        Status = isWalkIn ? AppointmentStatus.Confirmed : AppointmentStatus.Pending;
        UpdatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Confirm(Guid confirmedByUserId, string? updaterName = null)
    {
        if (Status != AppointmentStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot confirm appointment with status '{Status}'. Only Pending appointments can be confirmed.");
        }
        Snapshots.Add(new AppointmentSnapshot(this, updaterName, confirmedByUserId));
        Status = AppointmentStatus.Confirmed;
        UpdatedByUserId = confirmedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CheckIn(DateTime checkInTime)
    {
        if (Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot check in an appointment with status '{Status}'. Appointment must be confirmed first.");
        }
        var checkInDateUtc = DateOnly.FromDateTime(checkInTime);
        var checkInDateVn = DateOnly.FromDateTime(checkInTime.AddHours(7));
        if (ScheduleDate != checkInDateUtc && ScheduleDate != checkInDateVn)
        {
            throw new InvalidOperationException(
                $"Cannot check in: appointment is scheduled for {ScheduleDate:yyyy-MM-dd}, not today ({checkInDateVn:yyyy-MM-dd}).");
        }
        Status = AppointmentStatus.CheckedIn;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(Guid completedByUserId, string? updaterName = null)
    {
        if (Status != AppointmentStatus.CheckedIn)
        {
            throw new InvalidOperationException($"Cannot complete appointment with status '{Status}'. Appointment must be CheckedIn first.");
        }
        Snapshots.Add(new AppointmentSnapshot(this, updaterName, completedByUserId));
        Status = AppointmentStatus.Completed;
        UpdatedByUserId = completedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(Guid cancelledByUserId, bool isAdminOrReceptionist = false, string? updaterName = null)
    {
        if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
        {
            throw new InvalidOperationException($"{Status} appointments cannot be cancelled.");
        }

        if (!isAdminOrReceptionist)
        {
            var appointmentDateTimeUtc = ScheduleDate.ToDateTime(TimeSlot, DateTimeKind.Utc);
            if (DateTime.UtcNow.AddHours(CancelLimitHours) > appointmentDateTimeUtc)
            {
                throw new InvalidOperationException($"Appointments can only be cancelled at least {CancelLimitHours} hours before the scheduled time.");
            }
        }

        Snapshots.Add(new AppointmentSnapshot(this, updaterName, cancelledByUserId));
        Status = AppointmentStatus.Cancelled;
        UpdatedByUserId = cancelledByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reschedule(
        Guid newScheduleId,
        Guid newDoctorId,
        string newDoctorName,
        string? newSpecialtyName,
        DateOnly newDate,
        TimeOnly newTimeSlot,
        string newReason,
        Guid updatedByUserId,
        string? updaterName = null)
    {
        if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
        {
            throw new InvalidOperationException($"{Status} appointments cannot be updated.");
        }

        Snapshots.Add(new AppointmentSnapshot(this, updaterName, updatedByUserId));

        WorkScheduleId = newScheduleId;
        DoctorId = newDoctorId;
        DoctorName = newDoctorName;
        SpecialtyName = newSpecialtyName;
        ScheduleDate = newDate;
        TimeSlot = newTimeSlot;
        Reason = newReason;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}

