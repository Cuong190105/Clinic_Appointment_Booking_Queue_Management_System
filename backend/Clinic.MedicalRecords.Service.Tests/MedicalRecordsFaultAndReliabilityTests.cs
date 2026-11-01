using System;
using System.Threading.Tasks;
using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests;

public class MedicalRecordsFaultAndReliabilityTests
{
    [Fact]
    public async Task DuplicateExamStartedEvents_AreHandledIdempotently_WithoutErrorOrDuplicateContext()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new MedicalRecordsDbContext(options);
        var logger = NullLogger<MedicalRecordsManager>.Instance;
        var manager = new MedicalRecordsManager(db, logger);

        var queueTicketId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        var evt1 = new ExamStarted(
            queueTicketId,
            appointmentId,
            patientId,
            doctorId,
            "Dr. Test",
            new DateOnly(2026, 10, 26),
            DateTimeOffset.UtcNow);

        // First delivery
        await manager.HandleExamStartedAsync(evt1);
        var countFirst = await db.EncounterContexts.CountAsync(c => c.AppointmentId == appointmentId);
        Assert.Equal(1, countFirst);

        // Duplicate delivery (re-delivery / retry)
        await manager.HandleExamStartedAsync(evt1);
        var countSecond = await db.EncounterContexts.CountAsync(c => c.AppointmentId == appointmentId);
        Assert.Equal(1, countSecond);

        var context = await db.EncounterContexts.FindAsync(appointmentId);
        Assert.NotNull(context);
        Assert.Equal("Started", context.Status);
    }

    [Fact]
    public async Task ExamCompleted_DoesNotFinalizeDraftMedicalReport()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new MedicalRecordsDbContext(options);
        var logger = NullLogger<MedicalRecordsManager>.Instance;
        var manager = new MedicalRecordsManager(db, logger);

        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        // Seed Patient
        db.Patients.Add(new Patient(patientId, null, "Patient One", "0901234567", null, new DateOnly(1995, 1, 1), "Female", null, null, null));
        await db.SaveChangesAsync();

        await manager.HandleExamStartedAsync(new ExamStarted(Guid.NewGuid(), appointmentId, patientId, doctorId,
            "Dr. Doctor", new DateOnly(2026, 10, 26), DateTimeOffset.UtcNow));

        // Doctor saves Draft report
        var draft = await manager.SaveReportAsync(
            new SaveMedicalReportRequest(
                appointmentId,
                patientId,
                doctorId,
                "Dr. Doctor",
                "Internal Medicine",
                new DateOnly(2026, 10, 26),
                "Fever and headache",
                "Viral infection",
                "Paracetamol 500mg",
                "Rest and fluids",
                IsFinalize: false),
            currentUserId: doctorId,
            currentRole: "Doctor",
            currentDoctorId: doctorId,
            currentDoctorName: "Dr. Doctor");

        Assert.Equal("Draft", draft.Status);

        // Queue service publishes ExamCompleted event
        var completedEvent = new ExamCompleted(
            Guid.NewGuid(),
            appointmentId,
            doctorId,
            DateTimeOffset.UtcNow);

        await manager.HandleExamCompletedAsync(completedEvent);

        // Verify report remains in Draft status! (Queue does not finalize Medical Report)
        var reportAfterExamCompleted = await manager.GetReportByAppointmentIdAsync(appointmentId);
        Assert.NotNull(reportAfterExamCompleted);
        Assert.Equal("Draft", reportAfterExamCompleted.Status);

        // Only explicit doctor finalize transitions to Finalized
        var finalized = await manager.FinalizeReportAsync(draft.Id, currentUserId: doctorId, currentRole: "Doctor", currentDoctorId: doctorId);
        Assert.Equal("Finalized", finalized.Status);
    }

    [Fact]
    public async Task OutOfOrderExamCompleted_HandlesGracefully_WithoutCrashing()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new MedicalRecordsDbContext(options);
        var logger = NullLogger<MedicalRecordsManager>.Instance;
        var manager = new MedicalRecordsManager(db, logger);

        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        // ExamCompleted arrives before any ExamStarted or Draft
        var completedEvent = new ExamCompleted(
            Guid.NewGuid(),
            appointmentId,
            doctorId,
            DateTimeOffset.UtcNow);

        var exception = await Record.ExceptionAsync(() => manager.HandleExamCompletedAsync(completedEvent));
        Assert.Null(exception);

        // A completion arriving first creates a terminal tombstone, so a later start cannot regress it.
        var context = await db.EncounterContexts.FindAsync(appointmentId);
        Assert.NotNull(context);
        Assert.Equal("Completed", context.Status);

        await manager.HandleExamStartedAsync(new ExamStarted(Guid.NewGuid(), appointmentId, Guid.NewGuid(), doctorId,
            "Dr. Late", DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
        context = await db.EncounterContexts.FindAsync(appointmentId);
        Assert.Equal("Completed", context!.Status);
        Assert.Equal(doctorId, context.DoctorId);
    }

    [Fact]
    public void EventContracts_ContainNoPHI()
    {
        // Assert ExamStarted and ExamCompleted properties do not contain PHI
        var examStartedProps = typeof(ExamStarted).GetProperties();
        var examCompletedProps = typeof(ExamCompleted).GetProperties();

        string[] phiKeywords = ["symptom", "diagnosis", "prescription", "note", "medicalrecord", "treatment"];

        foreach (var prop in examStartedProps)
        {
            var name = prop.Name.ToLowerInvariant();
            foreach (var phi in phiKeywords)
            {
                Assert.DoesNotContain(phi, name);
            }
        }

        foreach (var prop in examCompletedProps)
        {
            var name = prop.Name.ToLowerInvariant();
            foreach (var phi in phiKeywords)
            {
                Assert.DoesNotContain(phi, name);
            }
        }
    }
}
