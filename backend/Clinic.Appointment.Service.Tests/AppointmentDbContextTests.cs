using AppointmentEntity = Clinic.Appointment.Service.Entities.Appointment;
using Clinic.Appointment.Service.Entities;
using Clinic.Appointment.Service.Enums;
using Clinic.Appointment.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinic.Appointment.Service.Tests;

public class AppointmentDbContextTests
{
    private static AppointmentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppointmentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppointmentDbContext(options);
    }

    [Fact]
    public async Task CanAddAndRetrieveAppointment()
    {
        using var context = CreateDbContext();

        var appointment = new AppointmentEntity(
            patientId: Guid.NewGuid(),
            patientName: "Nguyen Van A",
            patientPhone: "0901234567",
            patientGender: "Male",
            patientDateOfBirth: new DateOnly(1990, 1, 1),
            workScheduleId: Guid.NewGuid(),
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. House",
            specialtyName: "General",
            scheduleDate: new DateOnly(2026, 11, 1),
            timeSlot: new TimeOnly(8, 0),
            reason: "Checkup",
            createdByUserId: Guid.NewGuid(),
            isWalkIn: false);

        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var retrieved = await context.Appointments.FindAsync(appointment.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Nguyen Van A", retrieved.PatientName);
        Assert.Equal("Dr. House", retrieved.DoctorName);
        Assert.Equal(AppointmentStatus.Pending, retrieved.Status);
    }

    [Fact]
    public async Task CanAddAndRetrievePatientProjection()
    {
        using var context = CreateDbContext();

        var patient = new PatientProjection
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            IdentityUserId = Guid.NewGuid(),
            FullName = "Tran Thi B",
            PhoneNumber = "0912345678",
            Gender = "Female",
            DateOfBirth = new DateOnly(1995, 5, 20),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.PatientProjections.Add(patient);
        await context.SaveChangesAsync();

        var retrieved = await context.PatientProjections.FindAsync(patient.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Tran Thi B", retrieved.FullName);
        Assert.Equal("Female", retrieved.Gender);
    }

    [Fact]
    public async Task CanAddAndRetrieveAppointmentWithSnapshots()
    {
        using var context = CreateDbContext();

        var appointment = new AppointmentEntity(
            patientId: Guid.NewGuid(),
            patientName: "Le Van C",
            patientPhone: "0987654321",
            patientGender: "Male",
            patientDateOfBirth: new DateOnly(1985, 3, 15),
            workScheduleId: Guid.NewGuid(),
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. Watson",
            specialtyName: "Cardiology",
            scheduleDate: new DateOnly(2026, 11, 10),
            timeSlot: new TimeOnly(9, 0),
            reason: "Chest pain",
            createdByUserId: Guid.NewGuid(),
            isWalkIn: false);

        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        // Reschedule to trigger a snapshot
        appointment.Reschedule(
            newScheduleId: Guid.NewGuid(),
            newDoctorId: Guid.NewGuid(),
            newDoctorName: "Dr. Sherlock",
            newSpecialtyName: "Neurology",
            newDate: new DateOnly(2026, 11, 12),
            newTimeSlot: new TimeOnly(14, 0),
            newReason: "Rescheduled",
            updatedByUserId: Guid.NewGuid(),
            updaterName: "Admin");

        await context.SaveChangesAsync();

        var retrieved = await context.Appointments
            .Include(a => a.Snapshots)
            .FirstOrDefaultAsync(a => a.Id == appointment.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("Dr. Sherlock", retrieved.DoctorName);
        Assert.Single(retrieved.Snapshots);

        var snapshot = retrieved.Snapshots.First();
        Assert.Equal("Dr. Watson", snapshot.OldDoctorName);
        Assert.Equal("Cardiology", snapshot.OldSpecialtyName);
        Assert.Equal(new DateOnly(2026, 11, 10), snapshot.OldScheduleDate);
    }

    [Fact]
    public async Task CanAddAndRetrieveOutboxMessage()
    {
        using var context = CreateDbContext();

        var msg = new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "AppointmentCreated",
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "Clinic.Appointment.Service",
            Payload = "{\"test\": true}",
            RoutingKey = "appointment.created",
            Status = OutboxMessageStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.OutboxMessages.Add(msg);
        await context.SaveChangesAsync();

        var retrieved = await context.OutboxMessages.FirstOrDefaultAsync(m => m.EventId == msg.EventId);
        Assert.NotNull(retrieved);
        Assert.Equal("AppointmentCreated", retrieved.EventType);
        Assert.Equal(OutboxMessageStatus.Pending, retrieved.Status);
    }
}
