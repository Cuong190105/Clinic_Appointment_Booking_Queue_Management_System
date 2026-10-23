namespace Clinic.Queue.Service.Entities;

public class QueueCounter
{
    public Guid DoctorId { get; set; }
    public DateOnly QueueDate { get; set; }
    public int LastNumber { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
