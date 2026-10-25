using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Queries;

public sealed class GetMyMedicalHistoryTests
{
    [Fact]
    public async Task Handle_PatientWithFinalizedReports_ReturnsOnlyOwnHistory()
    {
        var repository = new FakeMedicalReportRepository();
        var patient = TestDataFactory.CreatePatient();
        var user = new FakeCurrentUser { PatientId = patient.Id };
        var handler = new GetMyMedicalHistoryHandler(repository, user);
        var own = CreateReport(patient.Id, MedicalReportStatus.Finalized, "Cough", "Common cold");
        var other = CreateReport(Guid.NewGuid(), MedicalReportStatus.Finalized, "Pain", "Migraine");
        own.Patient = patient;
        await repository.AddAsync(own);
        await repository.AddAsync(other);

        var result = await handler.Handle(new GetMyMedicalHistoryQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(own.Id, result[0].Id);
        Assert.Equal("Cough", result[0].Symptoms);
        Assert.Equal("Common cold", result[0].Diagnosis);
    }

    [Fact]
    public async Task Handle_DraftReports_AreExcluded()
    {
        var repository = new FakeMedicalReportRepository();
        var patient = TestDataFactory.CreatePatient();
        var handler = new GetMyMedicalHistoryHandler(repository, new FakeCurrentUser { PatientId = patient.Id });
        await repository.AddAsync(CreateReport(patient.Id, MedicalReportStatus.Draft, "Cough", "Pending"));

        var result = await handler.Handle(new GetMyMedicalHistoryQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_UserWithoutPatientId_ThrowsForbiddenException()
    {
        var handler = new GetMyMedicalHistoryHandler(new FakeMedicalReportRepository(), new FakeCurrentUser());
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new GetMyMedicalHistoryQuery(), CancellationToken.None));
    }

    private static MedicalReport CreateReport(Guid patientId, MedicalReportStatus status, string symptoms, string diagnosis) => new(
        Guid.NewGuid(), Guid.NewGuid(), symptoms, diagnosis, "Treatment", null)
    {
        PatientId = patientId,
        DoctorId = Guid.NewGuid(),
        DoctorName = "Dr Snapshot",
        SpecialtyName = "Cardiology",
        ScheduleDate = DateOnly.FromDateTime(DateTime.Today),
        Status = status
    };
}
