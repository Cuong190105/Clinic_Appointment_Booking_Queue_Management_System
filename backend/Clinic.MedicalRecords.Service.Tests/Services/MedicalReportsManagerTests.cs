using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Services;

public class MedicalReportsManagerTests
{
    private static MedicalRecordsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new MedicalRecordsDbContext(options);
    }

    [Fact]
    public async Task SaveReportAsync_CreatesDraft_WhenNotFinalized()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patient = new Patient(Guid.NewGuid(), null, "Patient One", "0901112233", "p1@test.com", new DateOnly(1990, 1, 1), "Male", "HCM", null, null);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        var request = new SaveMedicalReportRequest(
            AppointmentId: appointmentId,
            PatientId: patient.Id,
            DoctorId: doctorId,
            DoctorName: "Dr. Strange",
            SpecialtyName: "Cardiology",
            ScheduleDate: new DateOnly(2026, 10, 26),
            Symptoms: "Chest pain",
            Diagnosis: "Mild arrhythmia",
            Prescription: "Aspirin 81mg",
            Notes: "Rest well",
            IsFinalize: false
        );

        var result = await manager.SaveReportAsync(request, Guid.NewGuid(), "Doctor", doctorId, "Dr. Strange");

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(appointmentId, result.AppointmentId);
        Assert.Equal("Draft", result.Status);
        Assert.Equal("Patient One", result.PatientName);
        Assert.Null(result.ExamEndTime);

        var inDb = await db.MedicalReports.FindAsync(result.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Draft", inDb.Status);
    }

    [Fact]
    public async Task UpdateReportDraftAsync_UpdatesContent_WhenDraft()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patient = new Patient(Guid.NewGuid(), null, "Patient Two", null, null, new DateOnly(1995, 2, 2), "Female", "Danang", null, null);
        db.Patients.Add(patient);
        var report = new MedicalReport(
            appointmentId: Guid.NewGuid(),
            patientId: patient.Id,
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. Who",
            specialtyName: "General",
            scheduleDate: new DateOnly(2026, 10, 26),
            symptoms: "Headache",
            diagnosis: "Initial migraine",
            prescription: "Paracetamol",
            notes: "Note 1"
        );
        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        var updateReq = new UpdateMedicalReportRequest(
            Symptoms: "Severe headache and nausea",
            Diagnosis: "Acute migraine",
            Prescription: "Sumatriptan 50mg",
            Notes: "Avoid bright light",
            IsFinalize: false
        );

        var updated = await manager.UpdateReportDraftAsync(report.Id, updateReq, Guid.NewGuid(), "Doctor");

        Assert.Equal("Severe headache and nausea", updated.Symptoms);
        Assert.Equal("Acute migraine", updated.Diagnosis);
        Assert.Equal("Draft", updated.Status);
    }

    [Fact]
    public async Task UpdateReportDraftAsync_ThrowsInvalidOperationException_WhenFinalized()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patient = new Patient(Guid.NewGuid(), null, "Patient House", null, null, new DateOnly(1975, 1, 1), "Male", "Princeton", null, null);
        db.Patients.Add(patient);

        var report = new MedicalReport(
            appointmentId: Guid.NewGuid(),
            patientId: patient.Id,
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. House",
            specialtyName: "Diagnostic",
            scheduleDate: new DateOnly(2026, 10, 26),
            symptoms: "Lupus symptoms",
            diagnosis: "Not lupus",
            prescription: "None",
            notes: "Clinic note"
        );
        report.FinalizeReport();
        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        var updateReq = new UpdateMedicalReportRequest("New symptoms", "New diag", "New rx", "New note", false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.UpdateReportDraftAsync(report.Id, updateReq, Guid.NewGuid(), "Doctor"));
    }

    [Fact]
    public async Task FinalizeReportAsync_SetsStatusFinalized_AndIsIdempotent()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patient = new Patient(Guid.NewGuid(), null, "Patient Three", null, null, new DateOnly(1980, 5, 5), "Male", "Hue", null, null);
        db.Patients.Add(patient);
        var report = new MedicalReport(
            appointmentId: Guid.NewGuid(),
            patientId: patient.Id,
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. Watson",
            specialtyName: "General",
            scheduleDate: new DateOnly(2026, 10, 26),
            symptoms: "Cough",
            diagnosis: "Bronchitis",
            prescription: "Amoxicillin",
            notes: "Rest"
        );
        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        var finalized1 = await manager.FinalizeReportAsync(report.Id, Guid.NewGuid(), "Doctor");
        Assert.Equal("Finalized", finalized1.Status);
        Assert.NotNull(finalized1.ExamEndTime);

        var finalized2 = await manager.FinalizeReportAsync(report.Id, Guid.NewGuid(), "Doctor");
        Assert.Equal("Finalized", finalized2.Status);
        Assert.Equal(finalized1.ExamEndTime, finalized2.ExamEndTime);
    }

    [Fact]
    public async Task CreateAmendmentAsync_AppendsAmendment_WhenFinalized()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patient = new Patient(Guid.NewGuid(), null, "Patient Jekyll", null, null, new DateOnly(1978, 8, 8), "Male", "London", null, null);
        db.Patients.Add(patient);

        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        var report = new MedicalReport(
            appointmentId: appointmentId,
            patientId: patient.Id,
            doctorId: doctorId,
            doctorName: "Dr. Jekyll",
            specialtyName: "Psychiatry",
            scheduleDate: new DateOnly(2026, 10, 26),
            symptoms: "Mood swings",
            diagnosis: "Initial assessment",
            prescription: "None",
            notes: "Follow up"
        );
        report.FinalizeReport();
        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        var amendmentReq = new CreateAmendmentRequest(
            Reason: "Add updated lab results received post-examination",
            Symptoms: null,
            Diagnosis: "Bipolar disorder confirmed",
            Prescription: "Lithium 300mg",
            Notes: "Lab potassium level normal"
        );

        var amendment = await manager.CreateAmendmentAsync(
            appointmentId,
            amendmentReq,
            Guid.NewGuid(),
            "Doctor",
            doctorId,
            "Dr. Jekyll"
        );

        Assert.NotNull(amendment);
        Assert.Equal(1, amendment.AmendmentNumber);
        Assert.Equal(amendmentReq.Reason, amendment.Reason);
        Assert.Equal(appointmentId, amendment.AppointmentId);

        var fetchedReport = await manager.GetReportByAppointmentIdAsync(appointmentId);
        Assert.NotNull(fetchedReport);
        Assert.Single(fetchedReport.Amendments);
        Assert.Equal("Add updated lab results received post-examination", fetchedReport.Amendments[0].Reason);
    }

    [Fact]
    public async Task CreateAmendmentAsync_Fails_WhenReportIsDraft()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var appointmentId = Guid.NewGuid();
        var report = new MedicalReport(
            appointmentId: appointmentId,
            patientId: Guid.NewGuid(),
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. Jekyll",
            specialtyName: "Psychiatry",
            scheduleDate: new DateOnly(2026, 10, 26),
            symptoms: "Draft",
            diagnosis: "Draft",
            prescription: "Draft",
            notes: "Draft"
        );
        db.MedicalReports.Add(report);
        await db.SaveChangesAsync();

        var amendmentReq = new CreateAmendmentRequest("Reason", null, null, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.CreateAmendmentAsync(appointmentId, amendmentReq, Guid.NewGuid(), "Doctor", Guid.NewGuid(), "Dr. Jekyll"));
    }

    [Fact]
    public async Task GetPatientMedicalHistoryAsync_ReturnsOnlyReportsForTargetPatient()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patientA = new Patient(Guid.NewGuid(), null, "Patient A", null, null, new DateOnly(1990, 1, 1), "Male", "HCM", null, null);
        var patientB = new Patient(Guid.NewGuid(), null, "Patient B", null, null, new DateOnly(1992, 2, 2), "Female", "HN", null, null);
        db.Patients.AddRange(patientA, patientB);

        var reportA1 = new MedicalReport(Guid.NewGuid(), patientA.Id, Guid.NewGuid(), "Dr A", "Cardio", new DateOnly(2026, 9, 1), "s1", "d1", "rx1", "n1");
        reportA1.FinalizeReport();
        var reportA2 = new MedicalReport(Guid.NewGuid(), patientA.Id, Guid.NewGuid(), "Dr B", "Neuro", new DateOnly(2026, 10, 1), "s2", "d2", "rx2", "n2");
        reportA2.FinalizeReport();
        var reportB1 = new MedicalReport(Guid.NewGuid(), patientB.Id, Guid.NewGuid(), "Dr C", "Derm", new DateOnly(2026, 10, 15), "s3", "d3", "rx3", "n3");
        reportB1.FinalizeReport();

        db.MedicalReports.AddRange(reportA1, reportA2, reportB1);
        await db.SaveChangesAsync();

        var historyA = await manager.GetPatientMedicalHistoryAsync(patientA.Id);
        Assert.Equal(2, historyA.Count);
        Assert.All(historyA, r => Assert.Equal(patientA.Id, r.PatientId));
    }
}
