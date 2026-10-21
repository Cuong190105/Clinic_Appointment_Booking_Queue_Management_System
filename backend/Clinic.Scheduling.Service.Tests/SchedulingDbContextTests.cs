using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Scheduling.Service.Tests;

public class SchedulingDbContextTests
{
    private static SchedulingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SchedulingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SchedulingDbContext(options);
    }

    [Fact]
    public async Task CanAddAndRetrieveSpecialty()
    {
        using var context = CreateDbContext();

        var specialty = new Specialty
        {
            Id = Guid.NewGuid(),
            Name = "Cardiology",
            Description = "Heart specialist",
            EstablishedDate = new DateOnly(2020, 1, 1)
        };

        context.Specialties.Add(specialty);
        await context.SaveChangesAsync();

        var retrieved = await context.Specialties.FindAsync(specialty.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Cardiology", retrieved.Name);
    }

    [Fact]
    public async Task CanAddDoctorWithEmployeeAndWorkSchedule()
    {
        using var context = CreateDbContext();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            IdentityUserId = Guid.NewGuid(),
            FullName = "Dr. John Doe",
            PhoneNumber = "0901234567",
            Email = "john.doe@clinic.local",
            Gender = "Male",
            HireDate = new DateOnly(2022, 5, 10),
            Status = EmployeeStatus.Active
        };

        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            LicenseNumber = "LIC-9999",
            Qualification = "MD, PhD",
            ExperienceYears = 10,
            Biography = "Experienced doctor",
            Status = DoctorStatus.Active
        };

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Date = new DateOnly(2026, 11, 15),
            ShiftStart = new TimeOnly(8, 0),
            ShiftEnd = new TimeOnly(12, 0),
            RecommendedCapacity = 16,
            Status = WorkScheduleStatus.Active
        };

        context.Employees.Add(employee);
        context.Doctors.Add(doctor);
        context.WorkSchedules.Add(schedule);
        await context.SaveChangesAsync();

        var retrievedSchedule = await context.WorkSchedules
            .Include(ws => ws.Doctor)
            .ThenInclude(d => d.Employee)
            .FirstOrDefaultAsync(ws => ws.Id == schedule.Id);

        Assert.NotNull(retrievedSchedule);
        Assert.Equal(doctor.Id, retrievedSchedule.DoctorId);
        Assert.NotNull(retrievedSchedule.Doctor);
        Assert.Equal("LIC-9999", retrievedSchedule.Doctor.LicenseNumber);
        Assert.Equal("Dr. John Doe", retrievedSchedule.Doctor.Employee.FullName);
    }

    [Fact]
    public async Task CanAddAndRetrieveSlotReservation()
    {
        using var context = CreateDbContext();

        var reservation = new SlotReservation
        {
            Id = Guid.NewGuid(),
            WorkScheduleId = Guid.NewGuid(),
            TimeSlot = new TimeOnly(8, 30),
            PatientId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            IdempotencyKey = "key-test-12345"
        };

        context.SlotReservations.Add(reservation);
        await context.SaveChangesAsync();

        var retrieved = await context.SlotReservations.FirstOrDefaultAsync(r => r.IdempotencyKey == "key-test-12345");
        Assert.NotNull(retrieved);
        Assert.Equal(reservation.Id, retrieved.Id);
        Assert.Equal(ReservationStatus.Reserved, retrieved.Status);
    }

    [Fact]
    public async Task CanAddAndRetrieveOutboxMessage()
    {
        using var context = CreateDbContext();

        var outboxMessage = new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "WorkScheduleCreated",
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "Clinic.Scheduling.Service",
            Payload = "{\"scheduleId\":\"123\"}",
            RoutingKey = "schedule.created",
            Status = OutboxMessageStatus.Pending
        };

        context.OutboxMessages.Add(outboxMessage);
        await context.SaveChangesAsync();

        var retrieved = await context.OutboxMessages.FirstOrDefaultAsync(m => m.EventId == outboxMessage.EventId);
        Assert.NotNull(retrieved);
        Assert.Equal("WorkScheduleCreated", retrieved.EventType);
        Assert.Equal(OutboxMessageStatus.Pending, retrieved.Status);
    }
}

