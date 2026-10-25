using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Queries;

public sealed class GetPatientMedicalHistoryForDoctorTests
{
    [Fact]
    public async Task Handle_DoctorWithReportForPatient_ReturnsFinalizedReportsOnly()
    {
        var reports = new FakeMedicalReportRepository();
        var doctorId = Guid.NewGuid();
        var patient = TestDataFactory.CreatePatient();
        var currentUser = new FakeCurrentUser { DoctorId = doctorId, UserId = Guid.NewGuid() };
        var finalized = CreateReport(patient.Id, doctorId, MedicalReportStatus.Finalized, "Flu");
        var draft = CreateReport(patient.Id, doctorId, MedicalReportStatus.Draft, "Bronchitis");
        await reports.AddAsync(finalized);
        await reports.AddAsync(draft);
        var handler = new GetPatientMedicalHistoryForDoctorHandler(reports, currentUser);

        var result = await handler.Handle(new GetPatientMedicalHistoryForDoctorQuery(patient.Id), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Flu", result[0].Diagnosis);
    }

    [Fact]
    public async Task Handle_DoctorWithoutRelatedReport_ThrowsForbiddenException()
    {
        var patient = TestDataFactory.CreatePatient();
        var handler = new GetPatientMedicalHistoryForDoctorHandler(
            new FakeMedicalReportRepository(), new FakeCurrentUser { DoctorId = Guid.NewGuid() });

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetPatientMedicalHistoryForDoctorQuery(patient.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdminMayViewAnyPatient()
    {
        var reports = new FakeMedicalReportRepository();
        var patient = TestDataFactory.CreatePatient();
        await reports.AddAsync(CreateReport(patient.Id, Guid.NewGuid(), MedicalReportStatus.Finalized, "Migraine"));
        var currentUser = new FakeCurrentUser();
        currentUser.GrantPermission("patient-history.view.any");
        var handler = new GetPatientMedicalHistoryForDoctorHandler(reports, currentUser);

        var result = await handler.Handle(new GetPatientMedicalHistoryForDoctorQuery(patient.Id), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Migraine", result[0].Diagnosis);
    }

    private static MedicalReport CreateReport(Guid patientId, Guid doctorId, MedicalReportStatus status, string diagnosis) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Symptoms", diagnosis, "Treatment", null)
    {
        PatientId = patientId,
        DoctorId = doctorId,
        DoctorName = "Dr Snapshot",
        SpecialtyName = "Cardiology",
        ScheduleDate = DateOnly.FromDateTime(DateTime.Today),
        Status = status
    };
}
