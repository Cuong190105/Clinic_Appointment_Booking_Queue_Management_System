namespace Clinic.Scheduling.Service.Entities;

public class Specialty
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EstablishedDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<WorkHistory> WorkHistories { get; set; } = new List<WorkHistory>();
}

