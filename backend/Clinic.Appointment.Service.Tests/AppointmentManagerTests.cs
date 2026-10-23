using Clinic.Appointment.Service.DTOs;
using Clinic.Appointment.Service.Entities;
using Clinic.Appointment.Service.Enums;
using AppointmentEntity = Clinic.Appointment.Service.Entities.Appointment;
using Clinic.Appointment.Service.Persistence;
using Clinic.Appointment.Service.Services;
using Clinic.Contracts.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.Appointment.Service.Tests;

public class AppointmentManagerTests
{
    private sealed class FakeSchedulingClient : ISchedulingClient
    {
        public ScheduleReservationResult? ReserveResult { get; set; }
        public bool ConfirmResult { get; set; } = true;
        public ScheduleInfoResult? ScheduleInfoResult { get; set; }
        public List<(Guid ScheduleId, Guid ReservationId)> ReleasedReservations { get; } = new();

        public Task<ScheduleReservationResult?> ReserveSlotAsync(Guid workScheduleId, TimeOnly timeSlot, Guid patientId, string idempotencyKey, CancellationToken ct = default)
        {
            return Task.FromResult(ReserveResult);
        }

        public Task<bool> ConfirmReservationAsync(Guid workScheduleId, Guid reservationId, CancellationToken ct = default)
        {
            return Task.FromResult(ConfirmResult);
        }

        public Task<bool> ReleaseReservationAsync(Guid workScheduleId, Guid reservationId, CancellationToken ct = default)
        {
            ReleasedReservations.Add((workScheduleId, reservationId));
            return Task.FromResult(true);
        }

        public Task<bool> ReleaseSlotByAppointmentAsync(Guid appointmentId, Guid scheduleId, TimeOnly timeSlot, Guid patientId, Guid cancelledBy, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<ScheduleInfoResult?> GetScheduleAsync(Guid workScheduleId, CancellationToken ct = default)
        {
            return Task.FromResult(ScheduleInfoResult);
        }
    }

    private static (AppointmentDbContext db, FakeSchedulingClient client, IAppointmentManager manager) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppointmentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new AppointmentDbContext(options);
        var client = new FakeSchedulingClient();
        var outbox = new AppointmentOutboxWriter(db);
        var manager = new AppointmentManager(db, client, outbox, NullLogger<AppointmentManager>.Instance);

        return (db, client, manager);
    }

    [Fact]
    public async Task CreateAppointmentAsync_OnlineBooking_HappyPath_SagaCompletesAndPublishesOutboxEvents()
    {
        var (db, client, manager) = CreateTestSetup();
        var scheduleId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 1);
        var slot = new TimeOnly(9, 0);

        client.ReserveResult = new ScheduleReservationResult(
            reservationId,
            scheduleId,
            slot,
            patientId,
            "Reserved",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "key-1",
            doctorId,
            "Dr. House",
            "General",
            date);
        client.ConfirmResult = true;

        var request = new CreateAppointmentRequest(
            scheduleId,
            slot,
            "Routine checkup",
            patientId,
            IsWalkIn: false,
            PatientName: "Nguyen Van A",
            PatientPhone: "0901234567",
            PatientGender: "Male",
            PatientDateOfBirth: new DateOnly(1990, 1, 1));

        var (success, errorMessage, result, statusCode) = await manager.CreateAppointmentAsync(
            request,
            currentUserId: patientId,
            tokenPatientId: patientId,
            isStaff: false,
            idempotencyKey: "test-idemp-1");

        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Confirmed, result.Status); // Confirmed in online saga
        Assert.Equal("Dr. House", result.DoctorName);

        // Verify DB
        var app = await db.Appointments.FindAsync(result.Id);
        Assert.NotNull(app);
        Assert.Equal(AppointmentStatus.Confirmed, app.Status);

        // Verify Outbox: AppointmentCreated and AppointmentConfirmed
        var outboxMessages = await db.OutboxMessages.ToListAsync();
        Assert.Equal(2, outboxMessages.Count);
        Assert.Contains(outboxMessages, m => m.EventType == EventTypes.AppointmentCreated);
        Assert.Contains(outboxMessages, m => m.EventType == EventTypes.AppointmentConfirmed);
    }

    [Fact]
    public async Task CreateAppointmentAsync_OnlineBooking_WhenSlotUnavailable_ReturnsConflict()
    {
        var (db, client, manager) = CreateTestSetup();
        var scheduleId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        client.ReserveResult = null; // Full or unavailable

        var request = new CreateAppointmentRequest(
            scheduleId,
            new TimeOnly(10, 0),
            "Fever",
            patientId);

        var (success, errorMessage, result, statusCode) = await manager.CreateAppointmentAsync(
            request,
            currentUserId: patientId,
            tokenPatientId: patientId,
            isStaff: false,
            idempotencyKey: "test-idemp-fail");

        Assert.False(success);
        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Null(result);
        Assert.Empty(db.Appointments);
    }

    [Fact]
    public async Task CreateAppointmentAsync_OnlineBooking_WhenConfirmReservationFails_TriggersCompensationRelease()
    {
        var (db, client, manager) = CreateTestSetup();
        var scheduleId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        client.ReserveResult = new ScheduleReservationResult(
            reservationId,
            scheduleId,
            new TimeOnly(11, 0),
            patientId,
            "Reserved",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "key-2",
            Guid.NewGuid(),
            "Dr. Strange",
            "Magic",
            new DateOnly(2026, 11, 2));
        client.ConfirmResult = false; // Confirm fails in Scheduling Service

        var request = new CreateAppointmentRequest(
            scheduleId,
            new TimeOnly(11, 0),
            "Headache",
            patientId);

        var (success, errorMessage, result, statusCode) = await manager.CreateAppointmentAsync(
            request,
            currentUserId: patientId,
            tokenPatientId: patientId,
            isStaff: false,
            idempotencyKey: "test-idemp-compensate");

        Assert.False(success);
        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        // Compensation verified:
        Assert.Single(client.ReleasedReservations);
        Assert.Equal((scheduleId, reservationId), client.ReleasedReservations.First());
    }

    [Fact]
    public async Task CreateAppointmentAsync_WhenSameIdempotencyKeyRetried_ReturnsExistingAppointmentWithoutReservingNewSlot()
    {
        var (db, client, manager) = CreateTestSetup();
        var scheduleId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        client.ReserveResult = new ScheduleReservationResult(
            reservationId,
            scheduleId,
            new TimeOnly(10, 0),
            patientId,
            "Reserved",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "idemp-key-repeat",
            Guid.NewGuid(),
            "Dr. House",
            "General",
            new DateOnly(2026, 11, 1));
        client.ConfirmResult = true;

        var request = new CreateAppointmentRequest(
            scheduleId,
            new TimeOnly(10, 0),
            "Fever",
            patientId);

        // 1st request
        var (success1, _, result1, statusCode1) = await manager.CreateAppointmentAsync(
            request,
            currentUserId: patientId,
            tokenPatientId: patientId,
            isStaff: false,
            idempotencyKey: "idemp-key-repeat");

        Assert.True(success1);
        Assert.Equal(StatusCodes.Status201Created, statusCode1);
        Assert.NotNull(result1);

        // Remove reserve result so any subsequent reserve attempt would fail
        client.ReserveResult = null;

        // 2nd request with exact same idempotency key
        var (success2, _, result2, statusCode2) = await manager.CreateAppointmentAsync(
            request,
            currentUserId: patientId,
            tokenPatientId: patientId,
            isStaff: false,
            idempotencyKey: "idemp-key-repeat");

        Assert.True(success2);
        Assert.Equal(StatusCodes.Status200OK, statusCode2);
        Assert.NotNull(result2);
        Assert.Equal(result1.Id, result2.Id);

        // Only 1 appointment in DB
        Assert.Single(db.Appointments);
    }

    [Fact]
    public async Task CreateAppointmentAsync_WalkIn_BypassesReservationAndConfirmedDirectly()
    {
        var (db, client, manager) = CreateTestSetup();
        var scheduleId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var receptionistId = Guid.NewGuid();

        client.ScheduleInfoResult = new ScheduleInfoResult(
            scheduleId,
            doctorId,
            "Dr. Watson",
            "General",
            new DateOnly(2026, 11, 3),
            new TimeOnly(8, 0),
            new TimeOnly(12, 0),
            20,
            "Active");

        var request = new CreateAppointmentRequest(
            scheduleId,
            new TimeOnly(8, 30),
            "Urgent walk-in",
            patientId,
            IsWalkIn: true,
            PatientName: "Le Thi WalkIn",
            PatientPhone: "0999888777");

        var (success, errorMessage, result, statusCode) = await manager.CreateAppointmentAsync(
            request,
            currentUserId: receptionistId,
            tokenPatientId: null,
            isStaff: true,
            idempotencyKey: "walkin-1");

        Assert.True(success);
        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.NotNull(result);
        Assert.True(result.IsWalkIn);
        Assert.Equal(AppointmentStatus.Confirmed, result.Status);

        // Verify Outbox: AppointmentCreated published with IsWalkIn = true
        var outboxMessages = await db.OutboxMessages.ToListAsync();
        Assert.Single(outboxMessages);
        Assert.Equal(EventTypes.AppointmentCreated, outboxMessages.First().EventType);
    }

    [Fact]
    public async Task ConfirmAppointmentAsync_WhenPending_TransitionsToConfirmedAndPublishesOutbox()
    {
        var (db, client, manager) = CreateTestSetup();
        var app = new AppointmentEntity(
            Guid.NewGuid(), "Patient", "090", "Male", null,
            Guid.NewGuid(), Guid.NewGuid(), "Dr. A", "General",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), new TimeOnly(9, 0), "Reason",
            Guid.NewGuid(), isWalkIn: false);
        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        var receptionistId = Guid.NewGuid();
        var (success, errorMessage, statusCode) = await manager.ConfirmAppointmentAsync(
            app.Id, receptionistId, "Receptionist User");

        Assert.True(success);
        Assert.Equal(StatusCodes.Status204NoContent, statusCode);

        var updated = await db.Appointments.FindAsync(app.Id);
        Assert.NotNull(updated);
        Assert.Equal(AppointmentStatus.Confirmed, updated.Status);

        var outbox = await db.OutboxMessages.ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(EventTypes.AppointmentConfirmed, outbox.First().EventType);
    }

    [Fact]
    public async Task CheckInAppointmentAsync_WhenConfirmed_TransitionsToCheckedInAndPublishesOutbox()
    {
        var (db, client, manager) = CreateTestSetup();
        var app = new AppointmentEntity(
            Guid.NewGuid(), "Patient", "090", "Male", null,
            Guid.NewGuid(), Guid.NewGuid(), "Dr. A", "General",
            DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(9, 0), "Reason",
            Guid.NewGuid(), isWalkIn: false);
        app.Confirm(Guid.NewGuid());
        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        var (success, errorMessage, statusCode) = await manager.CheckInAppointmentAsync(
            app.Id, Guid.NewGuid(), "checkin-key-1");

        Assert.True(success);
        Assert.Equal(StatusCodes.Status204NoContent, statusCode);

        var updated = await db.Appointments.FindAsync(app.Id);
        Assert.NotNull(updated);
        Assert.Equal(AppointmentStatus.CheckedIn, updated.Status);
        Assert.NotNull(updated.UpdatedAt);

        var outbox = await db.OutboxMessages.ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(EventTypes.AppointmentCheckedIn, outbox.First().EventType);
    }

    [Fact]
    public async Task CancelAppointmentAsync_WhenActive_TransitionsToCancelledAndPublishesOutbox()
    {
        var (db, client, manager) = CreateTestSetup();
        var patientId = Guid.NewGuid();
        var app = new AppointmentEntity(
            patientId, "Patient", "090", "Male", null,
            Guid.NewGuid(), Guid.NewGuid(), "Dr. A", "General",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), new TimeOnly(9, 0), "Reason",
            patientId, isWalkIn: false);
        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        var (success, errorMessage, statusCode) = await manager.CancelAppointmentAsync(
            app.Id, patientId, isAdminOrReceptionist: false, updaterName: "Patient");

        Assert.True(success);
        Assert.Equal(StatusCodes.Status204NoContent, statusCode);

        var updated = await db.Appointments.FindAsync(app.Id);
        Assert.NotNull(updated);
        Assert.Equal(AppointmentStatus.Cancelled, updated.Status);

        var outbox = await db.OutboxMessages.ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(EventTypes.AppointmentCancelled, outbox.First().EventType);
    }

    [Fact]
    public async Task RescheduleAppointmentAsync_HappyPath_SagaCompletesWithSnapshotAndOutboxEvents()
    {
        var (db, client, manager) = CreateTestSetup();
        var patientId = Guid.NewGuid();
        var oldScheduleId = Guid.NewGuid();
        var oldDoctorId = Guid.NewGuid();
        var oldDate = new DateOnly(2026, 11, 1);
        var oldSlot = new TimeOnly(9, 0);

        var app = new AppointmentEntity(
            patientId, "Patient", "090", "Male", null,
            oldScheduleId, oldDoctorId, "Dr. Old", "General",
            oldDate, oldSlot, "Old reason",
            patientId, isWalkIn: false);
        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        var newScheduleId = Guid.NewGuid();
        var newDoctorId = Guid.NewGuid();
        var newReservationId = Guid.NewGuid();
        var newDate = new DateOnly(2026, 11, 5);
        var newSlot = new TimeOnly(14, 0);

        client.ReserveResult = new ScheduleReservationResult(
            newReservationId,
            newScheduleId,
            newSlot,
            patientId,
            "Reserved",
            DateTimeOffset.UtcNow.AddMinutes(5),
            "resched-key-1",
            newDoctorId,
            "Dr. New",
            "Specialty",
            newDate);
        client.ConfirmResult = true;

        var request = new UpdateAppointmentRequest(newScheduleId, newSlot, "Rescheduled reason");

        var (success, errorMessage, result, statusCode) = await manager.RescheduleAppointmentAsync(
            app.Id, request, patientId, "Patient", "resched-key-1");

        Assert.True(success);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.NotNull(result);
        Assert.Equal("Dr. New", result.DoctorName);
        Assert.Equal(newDate, result.Date);

        // Verify Snapshot created for traceback
        var changelogResult = await manager.GetAppointmentChangelogAsync(app.Id);
        Assert.True(changelogResult.Success);
        Assert.Equal(2, changelogResult.Result!.Changelog.Count);
        var entry = changelogResult.Result.Changelog.Last();
        Assert.Equal("Dr. Old", entry.DoctorName);
        Assert.Equal(oldDate, entry.Date);
        Assert.Equal(oldSlot, entry.TimeSlot);

        // Verify Outbox: AppointmentCancelled emitted to release old slot in Scheduling
        var outbox = await db.OutboxMessages.ToListAsync();
        Assert.Single(outbox);
        Assert.Equal(EventTypes.AppointmentCancelled, outbox.First().EventType);
    }

    [Fact]
    public async Task ProcessExamCompletedAsync_UpdatesStatusToCompleted()
    {
        var (db, client, manager) = CreateTestSetup();
        var app = new AppointmentEntity(
            Guid.NewGuid(), "Patient", "090", "Male", null,
            Guid.NewGuid(), Guid.NewGuid(), "Dr. A", "General",
            DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(9, 0), "Reason",
            Guid.NewGuid(), isWalkIn: false);
        app.Confirm(Guid.NewGuid());
        app.CheckIn(DateTime.UtcNow);
        db.Appointments.Add(app);
        await db.SaveChangesAsync();

        var evt = new ExamCompleted(
            Guid.NewGuid(),
            Guid.NewGuid(),
            app.Id,
            app.DoctorId,
            DateTimeOffset.UtcNow);

        var handled = await manager.ProcessExamCompletedAsync(evt);
        Assert.True(handled);

        var updated = await db.Appointments.FindAsync(app.Id);
        Assert.NotNull(updated);
        Assert.Equal(AppointmentStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task ProcessUserProfileUpdatedAsync_UpdatesPatientProjection()
    {
        var (db, client, manager) = CreateTestSetup();
        var personId = Guid.NewGuid();

        var evt = new UserProfileUpdated(
            personId,
            "Updated Full Name",
            "0988776655",
            "user@test.local",
            "Female");

        var handled = await manager.ProcessUserProfileUpdatedAsync(evt);
        Assert.True(handled);

        var projection = await db.PatientProjections.FirstOrDefaultAsync(p => p.PersonId == personId);
        Assert.NotNull(projection);
        Assert.Equal("Updated Full Name", projection.FullName);
        Assert.Equal("0988776655", projection.PhoneNumber);
        Assert.Equal("Female", projection.Gender);
    }
}
