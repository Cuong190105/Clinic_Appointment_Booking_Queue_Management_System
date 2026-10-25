using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;

namespace Clinic.Scheduling.Service.Tests;

public class DomainModelTests
{
    [Fact]
    public void ShiftRequest_Approve_WhenPending_ShouldCreateWorkScheduleAndSetApproved()
    {
        // Arrange
        var doctorId = Guid.NewGuid();
        var request = new ShiftRequest
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Date = new DateOnly(2026, 11, 1),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 15,
            Status = ShiftRequestStatus.Pending
        };

        // Act
        var schedule = request.Approve();

        // Assert
        Assert.Equal(ShiftRequestStatus.Approved, request.Status);
        Assert.NotNull(schedule);
        Assert.Equal(doctorId, schedule.DoctorId);
        Assert.Equal(request.Date, schedule.Date);
        Assert.Equal(request.ShiftStart, schedule.ShiftStart);
        Assert.Equal(request.ShiftEnd, schedule.ShiftEnd);
        Assert.Equal(request.RecommendedCapacity, schedule.RecommendedCapacity);
        Assert.Equal(WorkScheduleStatus.Active, schedule.Status);
    }

    [Fact]
    public void ShiftRequest_Approve_WhenNotPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var request = new ShiftRequest
        {
            Status = ShiftRequestStatus.Rejected
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => request.Approve());
    }

    [Fact]
    public void ShiftRequest_Reject_WhenPending_ShouldSetRejectedWithReason()
    {
        // Arrange
        var request = new ShiftRequest
        {
            Status = ShiftRequestStatus.Pending
        };

        // Act
        request.Reject("Doctor unavailable");

        // Assert
        Assert.Equal(ShiftRequestStatus.Rejected, request.Status);
        Assert.Equal("Doctor unavailable", request.Reason);
    }

    [Fact]
    public void SlotReservation_Confirm_WhenReservedAndNotExpired_ShouldSucceed()
    {
        // Arrange
        var reservation = new SlotReservation
        {
            Id = Guid.NewGuid(),
            WorkScheduleId = Guid.NewGuid(),
            TimeSlot = new TimeOnly(9, 0),
            PatientId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act
        reservation.Confirm();

        // Assert
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.NotNull(reservation.ConfirmedAt);
    }

    [Fact]
    public void SlotReservation_Confirm_WhenExpired_ShouldSetExpiredAndThrow()
    {
        // Arrange
        var reservation = new SlotReservation
        {
            Id = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), // already expired
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => reservation.Confirm());
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void SlotReservation_Release_WhenReserved_ShouldSucceed()
    {
        // Arrange
        var reservation = new SlotReservation
        {
            Id = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        // Act
        reservation.Release();

        // Assert
        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.NotNull(reservation.ReleasedAt);
    }

    [Fact]
    public void SlotReservation_IsActive_ShouldAccuratelyDetermineState()
    {
        var now = DateTimeOffset.UtcNow;

        var reservedActive = new SlotReservation
        {
            Status = ReservationStatus.Reserved,
            ExpiresAt = now.AddMinutes(5)
        };
        var reservedExpired = new SlotReservation
        {
            Status = ReservationStatus.Reserved,
            ExpiresAt = now.AddMinutes(-1)
        };
        var confirmed = new SlotReservation
        {
            Status = ReservationStatus.Confirmed,
            ExpiresAt = now.AddMinutes(-5) // Confirmed is always active regardless of ExpiresAt
        };
        var released = new SlotReservation
        {
            Status = ReservationStatus.Released,
            ExpiresAt = now.AddMinutes(5)
        };

        Assert.True(reservedActive.IsActive(now));
        Assert.False(reservedExpired.IsActive(now));
        Assert.True(confirmed.IsActive(now));
        Assert.False(released.IsActive(now));
    }

    [Fact]
    public void WorkSchedule_IsOverlapping_ShouldDetectOverlapsCorrectly()
    {
        var date = new DateOnly(2026, 11, 1);
        var schedule = new WorkSchedule
        {
            Date = date,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            Status = WorkScheduleStatus.Active
        };

        // Overlapping cases
        Assert.True(schedule.IsOverlapping(date, new TimeOnly(7, 30), new TimeOnly(8, 30))); // Start earlier, ends inside
        Assert.True(schedule.IsOverlapping(date, new TimeOnly(11, 30), new TimeOnly(13, 0))); // Starts inside, ends later
        Assert.True(schedule.IsOverlapping(date, new TimeOnly(9, 0), new TimeOnly(11, 0))); // Inside
        Assert.True(schedule.IsOverlapping(date, new TimeOnly(7, 0), new TimeOnly(13, 0))); // Encloses

        // Non-overlapping cases
        Assert.False(schedule.IsOverlapping(date, new TimeOnly(6, 0), new TimeOnly(8, 0))); // Ends right when schedule starts
        Assert.False(schedule.IsOverlapping(date, new TimeOnly(12, 0), new TimeOnly(14, 0))); // Starts right when schedule ends
        Assert.False(schedule.IsOverlapping(new DateOnly(2026, 11, 2), new TimeOnly(9, 0), new TimeOnly(11, 0))); // Different date
    }

    [Fact]
    public void Employee_UpdateProfileProjection_ShouldUpdateProperties()
    {
        var employee = new Employee
        {
            PersonId = Guid.NewGuid(),
            FullName = "Old Name",
            PhoneNumber = "0901000000",
            Email = "old@clinic.local",
            Gender = "Male"
        };

        employee.UpdateProfileProjection("New Name", "0909999999", "new@clinic.local", "Female");

        Assert.Equal("New Name", employee.FullName);
        Assert.Equal("0909999999", employee.PhoneNumber);
        Assert.Equal("new@clinic.local", employee.Email);
        Assert.Equal("Female", employee.Gender);
        Assert.NotNull(employee.UpdatedAt);
    }
}

