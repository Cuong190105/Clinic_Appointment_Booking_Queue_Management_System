namespace Clinic.Queue.Service.Entities;

using Clinic.Queue.Service.Enums;

public class QueueTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateOnly QueueDate { get; set; }
    public int QueueNumber { get; set; }
    public string? PatientName { get; set; }
    public string? DoctorName { get; set; }
    public bool Priority { get; set; }
    public QueueStatus Status { get; set; } = QueueStatus.Waiting;
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
    public DateTime? ExamStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public void Call()
    {
        if (Status != QueueStatus.Waiting && Status != QueueStatus.Called)
        {
            throw new InvalidOperationException($"Cannot call patient from status '{Status}'. Must be Waiting or Called.");
        }
        Status = QueueStatus.Called;
        CalledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartExam()
    {
        if (Status != QueueStatus.Called && Status != QueueStatus.Waiting)
        {
            throw new InvalidOperationException($"Cannot start examination from status '{Status}'. Must be Called or Waiting.");
        }
        Status = QueueStatus.InProgress;
        ExamStartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteExam()
    {
        if (Status != QueueStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot complete examination from status '{Status}'. Must be InProgress.");
        }
        Status = QueueStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        if (Status != QueueStatus.Waiting && Status != QueueStatus.Called)
        {
            throw new InvalidOperationException($"Cannot skip patient from status '{Status}'. Must be Waiting or Called.");
        }
        Status = QueueStatus.Skipped;
        SkippedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != QueueStatus.Waiting && Status != QueueStatus.Called)
        {
            throw new InvalidOperationException($"Cannot cancel a queue ticket with status '{Status}'. Must be Waiting or Called.");
        }
        Status = QueueStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPriority(bool priority)
    {
        Priority = priority;
        UpdatedAt = DateTime.UtcNow;
    }
}
