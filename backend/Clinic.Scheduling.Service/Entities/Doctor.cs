namespace Clinic.Scheduling.Service.Entities;

using Clinic.Scheduling.Service.Enums;

public class Doctor
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string? Biography { get; set; }
    public DoctorStatus Status { get; set; } = DoctorStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<WorkHistory> WorkHistories { get; set; } = new List<WorkHistory>();
    public ICollection<ShiftRequest> ShiftRequests { get; set; } = new List<ShiftRequest>();

    public void UpdateStatus(DoctorStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateInfo(string qualification, int experienceYears, string? biography)
    {
        Qualification = qualification;
        ExperienceYears = experienceYears;
        Biography = biography;
        UpdatedAt = DateTime.UtcNow;
    }
}

