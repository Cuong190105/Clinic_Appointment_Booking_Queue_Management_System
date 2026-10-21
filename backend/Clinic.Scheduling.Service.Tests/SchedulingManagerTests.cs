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
}

