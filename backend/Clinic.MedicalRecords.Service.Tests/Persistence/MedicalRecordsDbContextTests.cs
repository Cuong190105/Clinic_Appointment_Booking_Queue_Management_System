using System;
using System.Threading.Tasks;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Persistence;

public class MedicalRecordsDbContextTests
{
    private static MedicalRecordsDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MedicalRecordsDbContext(options);
    }

    [Fact]
    public async Task Can_Save_And_Retrieve_Patient_And_MedicalReport_With_Amendments()
    {
        using var db = CreateInMemoryContext();

        var patientId = Guid.NewGuid();
        var patient = new Patient(
            patientId,
            Guid.NewGuid(),
            "Tran Thi B",
            "0988776655",
            "thib@example.com",
            new DateOnly(1985, 3, 15),
            "Female",
            "Danang",
            "DN555666",
            "0911223344");

        db.Patients.Add(patient);

        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var report = new MedicalReport(
            appointmentId,
            patientId,
            doctorId,
            "Dr. Le",
            "Dermatology",
            new DateOnly(2026, 10, 26),
            "Skin rash",
            "Contact dermatitis",
            "Hydrocortisone cream",
            "Apply twice daily");

        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        // Finalize report
        report.FinalizeReport();
        await db.SaveChangesAsync();

        // Add amendment
        var amendment = new MedicalReportAmendment(
            report.Id,
            appointmentId,
            1,
            "Patient also reported mild itching",
            report.Symptoms,
            report.Diagnosis,
            report.Prescription + ", Loratadine 10mg",
            report.Notes,
            doctorId,
            "Dr. Le");

        db.MedicalReportAmendments.Add(amendment);

        // Add audit
        var audit = new MedicalAccessAudit(
            Guid.NewGuid(),
            "Doctor",
            doctorId,
            null,
            appointmentId,
            patientId,
            report.Id,
            "CreateAmendment",
            "Success",
            "127.0.0.1",
            Guid.NewGuid());

        db.MedicalAccessAudits.Add(audit);
        await db.SaveChangesAsync();

        // Retrieve and assert
        var retrievedReport = await db.MedicalReports
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);

        Assert.NotNull(retrievedReport);
        Assert.Equal("Finalized", retrievedReport.Status);
        Assert.Single(retrievedReport.Amendments);
        Assert.Equal(1, retrievedReport.Amendments[0].AmendmentNumber);
        Assert.Equal("Hydrocortisone cream, Loratadine 10mg", retrievedReport.Amendments[0].Prescription);

        var retrievedAudit = await db.MedicalAccessAudits
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
        Assert.NotNull(retrievedAudit);
        Assert.Equal("CreateAmendment", retrievedAudit.Action);
        Assert.Equal("Success", retrievedAudit.Outcome);
    }

    [Fact]
    public async Task EncounterContext_CanBeCreated_And_Completed()
    {
        using var db = CreateInMemoryContext();

        var appointmentId = Guid.NewGuid();
        var encounter = new EncounterContext(
            appointmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Le",
            new DateOnly(2026, 10, 26),
            DateTimeOffset.UtcNow);

        db.EncounterContexts.Add(encounter);
        await db.SaveChangesAsync();

        encounter.MarkCompleted(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var retrieved = await db.EncounterContexts.FindAsync(appointmentId);
        Assert.NotNull(retrieved);
        Assert.Equal("Completed", retrieved.Status);
        Assert.NotNull(retrieved.CompletedAt);
    }
}
