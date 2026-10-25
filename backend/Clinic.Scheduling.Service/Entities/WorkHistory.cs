namespace Clinic.Scheduling.Service.Entities;

public class WorkHistory
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public Guid SpecialtyId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Doctor Doctor { get; set; } = null!;
    public Specialty Specialty { get; set; } = null!;

    public void EndHistory(DateOnly endDate)
    {
        if (endDate < StartDate)
            throw new InvalidOperationException("End date cannot be earlier than start date.");

        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }
}

