namespace Clinic.Scheduling.Service.Entities;

using Clinic.Scheduling.Service.Enums;

public class WorkSchedule
{
    public const int SlotIntervalMinutes = 15;

    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
    public int RecommendedCapacity { get; set; }
    public WorkScheduleStatus Status { get; set; } = WorkScheduleStatus.Active;
    public string? CancellationReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Doctor Doctor { get; set; } = null!;
    public ICollection<SlotReservation> SlotReservations { get; set; } = new List<SlotReservation>();

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));

        Status = WorkScheduleStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateShift(DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int recommendedCapacity)
    {
        if (shiftEnd <= shiftStart)
            throw new InvalidOperationException("Shift end must be after shift start.");
        if (recommendedCapacity <= 0)
            throw new InvalidOperationException("Recommended capacity must be greater than zero.");

        Date = date;
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        RecommendedCapacity = recommendedCapacity;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsOverlapping(DateOnly date, TimeOnly start, TimeOnly end)
    {
        if (Date != date || Status == WorkScheduleStatus.Cancelled || IsDeleted)
            return false;

        return ShiftStart < end && start < ShiftEnd;
    }
}

