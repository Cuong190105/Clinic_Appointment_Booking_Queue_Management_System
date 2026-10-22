using Clinic.Scheduling.Service.DTOs;
using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Clinic.Scheduling.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Scheduling.Service.Tests;

public class SchedulingManagerTests
{
    private static (SchedulingDbContext db, ISchedulingManager manager) CreateTestManager()
    {
        var options = new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new SchedulingDbContext(options);
        var outboxWriter = new SchedulingOutboxWriter(db);
        var manager = new SchedulingManager(db, outboxWriter, NullLogger<SchedulingManager>.Instance);

        return (db, manager);
    }

    private static Doctor CreateTestDoctor(Guid doctorId, string fullName = "Dr. Test")
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            FullName = fullName,
            HireDate = new DateOnly(2020, 1, 1),
            Status = EmployeeStatus.Active
        };
        return new Doctor
        {
            Id = doctorId,
            EmployeeId = employee.Id,
            Employee = employee,
            LicenseNumber = $"LIC-{doctorId.ToString()[..4]}",
            Qualification = "MD",
            ExperienceYears = 5,
            Status = DoctorStatus.Active
        };
    }

    [Fact]
    public async Task GetAvailability_WhenUnderCapacity_ReturnsAvailable15MinuteSlots()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId, "Dr. Alice");
        var scheduleDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = scheduleDate,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(9, 0), // 4 slots: 08:00, 08:15, 08:30, 08:45
            RecommendedCapacity = 4,
            Status = WorkScheduleStatus.Active
        };

        db.Doctors.Add(doctor);
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var query = new AvailabilityQuery(doctorId, null, scheduleDate, scheduleDate);
        var slots = await manager.GetAvailabilityAsync(query);

        Assert.Equal(4, slots.Count);
        Assert.Equal(new TimeOnly(8, 0), slots[0].TimeSlot);
        Assert.Equal(new TimeOnly(8, 15), slots[1].TimeSlot);
        Assert.Equal(new TimeOnly(8, 30), slots[2].TimeSlot);
        Assert.Equal(new TimeOnly(8, 45), slots[3].TimeSlot);
    }

    [Fact]
    public async Task GetAvailability_WhenAtOrOverCapacity_HidesEntireShiftNoSlotsReturned()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId);
        var scheduleDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = scheduleDate,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(10, 0), // 8 slots generated
            RecommendedCapacity = 2, // Low capacity
            Status = WorkScheduleStatus.Active
        };

        // Add 2 confirmed reservations -> OnlineCount reaches RecommendedCapacity (2)
        var res1 = new SlotReservation
        {
            Id = Guid.NewGuid(),
            WorkScheduleId = schedule.Id,
            TimeSlot = new TimeOnly(8, 0),
            PatientId = Guid.NewGuid(),
            Status = ReservationStatus.Confirmed,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            IdempotencyKey = "key-1"
        };
        var res2 = new SlotReservation
        {
            Id = Guid.NewGuid(),
            WorkScheduleId = schedule.Id,
            TimeSlot = new TimeOnly(8, 15),
            PatientId = Guid.NewGuid(),
            Status = ReservationStatus.Confirmed,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            IdempotencyKey = "key-2"
        };

        db.Doctors.Add(doctor);
        db.WorkSchedules.Add(schedule);
        db.SlotReservations.AddRange(res1, res2);
        await db.SaveChangesAsync();

        var query = new AvailabilityQuery(doctorId, null, scheduleDate, scheduleDate);
        var slots = await manager.GetAvailabilityAsync(query);

        // Entire shift should be hidden even though 6 other time slots are empty!
        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetAvailability_ExcludesAlreadyReservedOrConfirmedSlot()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId);
        var scheduleDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = scheduleDate,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(9, 0), // 4 slots: 08:00, 08:15, 08:30, 08:45
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };

        // Reserve 08:15
        var reservation = new SlotReservation
        {
            Id = Guid.NewGuid(),
            WorkScheduleId = schedule.Id,
            TimeSlot = new TimeOnly(8, 15),
            PatientId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            IdempotencyKey = "key-reserved"
        };

        db.Doctors.Add(doctor);
        db.WorkSchedules.Add(schedule);
        db.SlotReservations.Add(reservation);
        await db.SaveChangesAsync();

        var query = new AvailabilityQuery(doctorId, null, scheduleDate, scheduleDate);
        var slots = await manager.GetAvailabilityAsync(query);

        // 3 slots should be returned: 08:00, 08:30, 08:45 (08:15 excluded)
        Assert.Equal(3, slots.Count);
        Assert.DoesNotContain(slots, s => s.TimeSlot == new TimeOnly(8, 15));
    }

    [Fact]
    public async Task GetAvailability_ExcludesSlotsInPastOrLessThan30MinutesAway()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId);
        var localNow = DateTime.UtcNow.AddHours(7);
        var today = DateOnly.FromDateTime(localNow);
        var currentTime = TimeOnly.FromDateTime(localNow);

        if (currentTime.Hour < 2 || currentTime.Hour > 20) return;

        var shiftStart = new TimeOnly(currentTime.Hour - 1, 0);
        var shiftEnd = new TimeOnly(currentTime.Hour + 2, 0);

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = today,
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            RecommendedCapacity = 20,
            Status = WorkScheduleStatus.Active
        };

        db.Doctors.Add(doctor);
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var query = new AvailabilityQuery(doctorId, null, today, today);
        var slots = await manager.GetAvailabilityAsync(query);

        var minTime = currentTime.AddMinutes(30);
        foreach (var slot in slots)
        {
            Assert.True(slot.TimeSlot >= minTime);
        }
    }

    [Fact]
    public async Task ReserveSlot_WhenSlotIsLessThan30MinutesInFuture_ThrowsInvalidOperationException()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var localNow = DateTime.UtcNow.AddHours(7);
        var today = DateOnly.FromDateTime(localNow);
        var currentTime = TimeOnly.FromDateTime(localNow);

        if (currentTime.Hour < 2 || currentTime.Hour > 21) return;

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Date = today,
            ShiftStart = new TimeOnly(currentTime.Hour - 1, 0),
            ShiftEnd = new TimeOnly(currentTime.Hour + 2, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };

        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var pastSlot = currentTime.AddMinutes(-15);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ReserveSlotAsync(schedule.Id, pastSlot, Guid.NewGuid(), "key-past"));

        Assert.Contains("30 phút", ex.Message);
    }

    [Fact]
    public async Task ReserveSlot_WhenValid_ReturnsReservedWith10MinuteTimeout()
    {
        var (db, manager) = CreateTestManager();

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };

        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var patientId = Guid.NewGuid();
        var idempotencyKey = "unique-key-100";

        var response = await manager.ReserveSlotAsync(schedule.Id, new TimeOnly(8, 30), patientId, idempotencyKey);

        Assert.NotNull(response);
        Assert.Equal("Reserved", response.Status);
        Assert.Equal(schedule.Id, response.ScheduleId);
        Assert.Equal(new TimeOnly(8, 30), response.TimeSlot);
        Assert.Equal(idempotencyKey, response.IdempotencyKey);
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(9));
    }

    [Fact]
    public async Task ReserveSlot_DuplicateIdempotencyKey_ReturnsExistingReservation()
    {
        var (db, manager) = CreateTestManager();

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };

        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var patientId = Guid.NewGuid();
        var idempotencyKey = "same-idempotency-key";

        var first = await manager.ReserveSlotAsync(schedule.Id, new TimeOnly(8, 30), patientId, idempotencyKey);
        var second = await manager.ReserveSlotAsync(schedule.Id, new TimeOnly(8, 30), patientId, idempotencyKey);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);

        // Only 1 record in database
        var totalReservations = await db.SlotReservations.CountAsync();
        Assert.Equal(1, totalReservations);
    }

    [Fact]
    public async Task ConfirmAndReleaseReservation_FlowTests()
    {
        var (db, manager) = CreateTestManager();

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var reserved = await manager.ReserveSlotAsync(schedule.Id, new TimeOnly(8, 30), Guid.NewGuid(), "key-confirm");

        // Confirm
        var confirmedSuccess = await manager.ConfirmReservationAsync(schedule.Id, reserved.Id);
        Assert.True(confirmedSuccess);

        var dbRecord = await db.SlotReservations.FindAsync(reserved.Id);
        Assert.Equal(ReservationStatus.Confirmed, dbRecord!.Status);

        // Release
        var releaseSuccess = await manager.ReleaseReservationAsync(schedule.Id, reserved.Id);
        Assert.True(releaseSuccess);

        dbRecord = await db.SlotReservations.FindAsync(reserved.Id);
        Assert.Equal(ReservationStatus.Released, dbRecord!.Status);
    }

    [Fact]
    public async Task ApproveShiftRequest_DetectsConflict_AndThrows()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var date = new DateOnly(2026, 12, 1);

        // Existing schedule from 08:00 to 11:30
        var existingSchedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Date = date,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(11, 30),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };
        db.WorkSchedules.Add(existingSchedule);

        // Shift request from 10:00 to 13:00 (overlaps with existing!)
        var request = new ShiftRequest
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Date = date,
            ShiftStart = new TimeOnly(10, 0),
            ShiftEnd = new TimeOnly(13, 0),
            RecommendedCapacity = 10,
            Status = ShiftRequestStatus.Pending
        };
        db.ShiftRequests.Add(request);
        await db.SaveChangesAsync();

        // Act & Assert: must throw conflict
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ApproveShiftRequestAsync(request.Id, null));

        Assert.Contains("trùng thời gian", ex.Message);
    }

    [Fact]
    public async Task ApproveShiftRequest_WhenNoConflict_CreatesScheduleAndOutboxEvent()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var date = new DateOnly(2026, 12, 1);

        var request = new ShiftRequest
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Date = date,
            ShiftStart = new TimeOnly(14, 0),
            ShiftEnd = new TimeOnly(18, 0),
            RecommendedCapacity = 12,
            Status = ShiftRequestStatus.Pending
        };
        db.ShiftRequests.Add(request);
        await db.SaveChangesAsync();

        var schedule = await manager.ApproveShiftRequestAsync(request.Id, "Approved by Lead");

        Assert.NotNull(schedule);
        Assert.Equal(WorkScheduleStatus.Active, schedule.Status);

        // Verify outbox message was created in same transaction
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "WorkScheduleCreated");
        Assert.NotNull(outbox);
        Assert.Equal("Clinic.Scheduling.Service", outbox.Producer);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task UpdateSchedule_EmitsWorkScheduleUpdatedOutboxEvent()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId);
        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = new DateOnly(2026, 12, 5),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };
        db.Doctors.Add(doctor);
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var updated = await manager.UpdateScheduleAsync(
            schedule.Id,
            new DateOnly(2026, 12, 5),
            new TimeOnly(8, 30),
            new TimeOnly(12, 30),
            12);

        Assert.Equal(new TimeOnly(8, 30), updated.ShiftStart);
        Assert.Equal(12, updated.RecommendedCapacity);

        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "WorkScheduleUpdated");
        Assert.NotNull(outbox);
        Assert.Equal("schedule.updated", outbox.RoutingKey);
        Assert.Equal("Clinic.Scheduling.Service", outbox.Producer);
    }

    [Fact]
    public async Task UpdateSchedule_WhenOverlapsWithExisting_ThrowsConflict()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId);
        var date = new DateOnly(2026, 12, 6);

        var schedule1 = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = date,
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };
        var schedule2 = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = date,
            ShiftStart = new TimeOnly(13, 0),
            ShiftEnd = new TimeOnly(17, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };
        db.Doctors.Add(doctor);
        db.WorkSchedules.AddRange(schedule1, schedule2);
        await db.SaveChangesAsync();

        // Updating schedule2 to 11:00 - 15:00 overlaps with schedule1 (8:00 - 12:00)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.UpdateScheduleAsync(schedule2.Id, date, new TimeOnly(11, 0), new TimeOnly(15, 0), 10));

        Assert.Contains("overlaps", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelSchedule_EmitsWorkScheduleCancelledOutboxEvent()
    {
        var (db, manager) = CreateTestManager();

        var doctorId = Guid.NewGuid();
        var doctor = CreateTestDoctor(doctorId);
        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Doctor = doctor,
            Date = new DateOnly(2026, 12, 7),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 10,
            Status = WorkScheduleStatus.Active
        };
        db.Doctors.Add(doctor);
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var success = await manager.CancelScheduleAsync(schedule.Id, "Doctor sick leave");

        Assert.True(success);
        Assert.Equal(WorkScheduleStatus.Cancelled, schedule.Status);

        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "WorkScheduleCancelled");
        Assert.NotNull(outbox);
        Assert.Equal("schedule.cancelled", outbox.RoutingKey);
        Assert.Contains("Doctor sick leave", outbox.Payload);
    }

    [Fact]
    public async Task HandleUserProfileUpdated_UpdatesMatchingEmployeeProjections()
    {
        var (db, manager) = CreateTestManager();

        var personId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            FullName = "Old Name",
            PhoneNumber = "0900000001",
            Email = "old@example.com",
            Gender = "Female",
            HireDate = new DateOnly(2021, 1, 1),
            Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var evt = new Clinic.Contracts.Events.UserProfileUpdated(
            personId,
            "New Full Name",
            "0988888888",
            "new@example.com",
            "Male"
        );

        var updatedCount = await manager.HandleUserProfileUpdatedAsync(evt);

        Assert.Equal(1, updatedCount);
        var reloaded = await db.Employees.FindAsync(employee.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("New Full Name", reloaded.FullName);
        Assert.Equal("0988888888", reloaded.PhoneNumber);
        Assert.Equal("new@example.com", reloaded.Email);
        Assert.Equal("Male", reloaded.Gender);
    }
}

