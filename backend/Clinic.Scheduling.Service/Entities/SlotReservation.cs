namespace Clinic.Scheduling.Service.Entities;

using Clinic.Scheduling.Service.Enums;

public class SlotReservation
{
    public Guid Id { get; set; }
    public Guid WorkScheduleId { get; set; }
    public TimeOnly TimeSlot { get; set; }
    public Guid PatientId { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public DateTimeOffset ExpiresAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }

    // Navigation
    public WorkSchedule WorkSchedule { get; set; } = null!;

    public bool IsActive(DateTimeOffset utcNow)
    {
        if (Status == ReservationStatus.Confirmed)
            return true;
        if (Status == ReservationStatus.Reserved && ExpiresAt > utcNow)
            return true;
        return false;
    }

    public void Confirm()
    {
        if (Status != ReservationStatus.Reserved)
            throw new InvalidOperationException($"Cannot confirm reservation in '{Status}' state.");

        if (ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Status = ReservationStatus.Expired;
            throw new InvalidOperationException("Reservation has already expired.");
        }

        Status = ReservationStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }

    public void Release()
    {
        if (Status != ReservationStatus.Reserved && Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException($"Cannot release reservation in '{Status}' state.");

        Status = ReservationStatus.Released;
        ReleasedAt = DateTimeOffset.UtcNow;
    }

    public void Expire()
    {
        if (Status == ReservationStatus.Reserved)
        {
            Status = ReservationStatus.Expired;
        }
    }
}

