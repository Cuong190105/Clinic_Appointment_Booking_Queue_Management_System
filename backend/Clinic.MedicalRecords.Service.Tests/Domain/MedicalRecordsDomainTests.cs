using System;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Domain;

public class MedicalRecordsDomainTests
{
    [Fact]
    public void Patient_CanBeCreated_And_Updated()
    {
        var patientId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        var dob = new DateOnly(1990, 5, 20);

        var patient = new Entities.Patient(
            patientId,
            identityUserId,
            "Nguyen Van A",
            "0912345678",
            "vana@example.com",
            dob,
            "Male",
            "123 Le Loi, Hanoi",
            "DN123456789",
            "0987654321");

        Assert.Equal(patientId, patient.Id);
        Assert.Equal(identityUserId, patient.IdentityUserId);
        Assert.Equal("Nguyen Van A", patient.FullName);
        Assert.Equal("0912345678", patient.PhoneNumber);
        Assert.Equal("vana@example.com", patient.Email);
        Assert.Equal(dob, patient.DateOfBirth);
        Assert.Equal("Male", patient.Gender);
        Assert.Equal("123 Le Loi, Hanoi", patient.Address);
        Assert.Equal("DN123456789", patient.InsuranceNumber);
        Assert.Equal("0987654321", patient.EmergencyContact);
        Assert.False(patient.IsDeleted);

        patient.UpdateProfile(
            "vana_new@example.com",
            "456 Tran Phu, Hanoi",
            "DN987654321",
            "0911223344",
            "Male");

        Assert.Equal("vana_new@example.com", patient.Email);
        Assert.Equal("456 Tran Phu, Hanoi", patient.Address);
        Assert.Equal("DN987654321", patient.InsuranceNumber);
        Assert.Equal("0911223344", patient.EmergencyContact);
        Assert.NotNull(patient.UpdatedAt);
    }

    [Fact]
    public void MedicalReport_InitialStatus_IsDraft()
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        var report = new Entities.MedicalReport(
            appointmentId,
            patientId,
            doctorId,
            "Dr. Smith",
            "Cardiology",
            new DateOnly(2026, 10, 26),
            "Fever, dry cough",
            "Upper respiratory infection",
            "Paracetamol 500mg",
            "Rest and drink water");

        Assert.Equal(appointmentId, report.AppointmentId);
        Assert.Equal(patientId, report.PatientId);
        Assert.Equal(doctorId, report.DoctorId);
        Assert.Equal("Dr. Smith", report.DoctorName);
        Assert.Equal("Cardiology", report.SpecialtyName);
        Assert.Equal("Draft", report.Status);
        Assert.NotNull(report.ExamStartTime);
        Assert.Null(report.ExamEndTime);
        Assert.False(report.IsDeleted);
    }

    [Fact]
    public void MedicalReport_UpdateDraft_ModifiesContent()
    {
        var report = new Entities.MedicalReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Smith",
            "General",
            new DateOnly(2026, 10, 26),
            "Headache",
            "Tension headache",
            "Aspirin",
            "Follow up in 3 days");

        report.UpdateDraft("Severe headache", "Migraine", "Sumatriptan", "Avoid stress");

        Assert.Equal("Severe headache", report.Symptoms);
        Assert.Equal("Migraine", report.Diagnosis);
        Assert.Equal("Sumatriptan", report.Prescription);
        Assert.Equal("Avoid stress", report.Notes);
        Assert.NotNull(report.UpdatedAt);
    }

    [Fact]
    public void MedicalReport_Finalize_MakesReportImmutable()
    {
        var report = new Entities.MedicalReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Smith",
            "General",
            new DateOnly(2026, 10, 26),
            "Cough",
            "Bronchitis",
            "Amoxicillin",
            "Complete 7-day course");

        var finalizedAt = DateTimeOffset.UtcNow;
        report.FinalizeReport(finalizedAt);

        Assert.Equal("Finalized", report.Status);
        Assert.Equal(finalizedAt, report.ExamEndTime);

        // Attempting to modify directly must throw InvalidOperationException
        var ex = Assert.Throws<InvalidOperationException>(() =>
            report.UpdateDraft("New cough", "Pneumonia", "Azithromycin", "Admit"));

        Assert.Contains("finalized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MedicalReport_Finalize_IsIdempotent()
    {
        var report = new Entities.MedicalReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Smith",
            "General",
            new DateOnly(2026, 10, 26),
            "Cough",
            "Bronchitis",
            "Amoxicillin",
            "Notes");

        var firstTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        report.FinalizeReport(firstTime);

        // Calling finalize again does not throw or change the finalized timestamp
        report.FinalizeReport(DateTimeOffset.UtcNow);

        Assert.Equal("Finalized", report.Status);
        Assert.Equal(firstTime, report.ExamEndTime);
    }

    [Fact]
    public void Amendment_AppendOnly_RecordsAuthorAndReason()
    {
        var reportId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var authorDoctorId = Guid.NewGuid();

        var amendment = new Entities.MedicalReportAmendment(
            reportId,
            appointmentId,
            amendmentNumber: 1,
            reason: "Patient reported allergy to penicillin; switched to erythromycin",
            symptoms: "Cough",
            diagnosis: "Bronchitis",
            prescription: "Erythromycin 250mg",
            notes: "Avoid all beta-lactams",
            authorDoctorId: authorDoctorId,
            authorName: "Dr. Smith");

        Assert.Equal(reportId, amendment.MedicalReportId);
        Assert.Equal(appointmentId, amendment.AppointmentId);
        Assert.Equal(1, amendment.AmendmentNumber);
        Assert.Contains("allergy", amendment.Reason);
        Assert.Equal(authorDoctorId, amendment.AuthorDoctorId);
        Assert.Equal("Dr. Smith", amendment.AuthorName);
        Assert.True(amendment.CreatedAt <= DateTimeOffset.UtcNow);
    }
}
