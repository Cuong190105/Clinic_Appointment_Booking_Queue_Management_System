namespace Clinic.Scheduling.Service.Entities;

using Clinic.Scheduling.Service.Enums;

public class ShiftRequest
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
    public int RecommendedCapacity { get; set; }
    public string? Reason { get; set; }
    public ShiftRequestStatus Status { get; set; } = ShiftRequestStatus.Pending;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Doctor Doctor { get; set; } = null!;

    public WorkSchedule Approve()
    {
        if (Status != ShiftRequestStatus.Pending)
            throw new InvalidOperationException($"Cannot approve request with status '{Status}'.");

        Status = ShiftRequestStatus.Approved;
        UpdatedAt = DateTime.UtcNow;

        return new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = Date,
            ShiftStart = ShiftStart,
            ShiftEnd = ShiftEnd,
            RecommendedCapacity = RecommendedCapacity,
            Status = WorkScheduleStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Reject(string? reason = null)
    {
        if (Status != ShiftRequestStatus.Pending)
            throw new InvalidOperationException($"Cannot reject request with status '{Status}'.");

        Status = ShiftRequestStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason))
            Reason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != ShiftRequestStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel request with status '{Status}'.");

        Status = ShiftRequestStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}

