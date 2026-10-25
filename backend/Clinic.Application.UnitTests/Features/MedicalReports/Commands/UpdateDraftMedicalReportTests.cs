using Clinic.Application.Features.MedicalReports.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Commands;

public sealed class UpdateDraftMedicalReportTests
{
    [Fact]
    public async Task Handle_DraftReport_UpdatesSuccessfully()
    {
        var repository = new FakeMedicalReportRepository();
        var doctorId = Guid.NewGuid();
        var report = CreateReport(doctorId);
        await repository.AddAsync(report);
        var currentUser = new FakeCurrentUser { DoctorId = doctorId };
        currentUser.GrantRole("Doctor");
        var handler = new UpdateDraftMedicalReportHandler(repository, currentUser, new FakeUnitOfWork());

        var result = await handler.Handle(new UpdateDraftMedicalReportCommand(
            report.Id, "Updated symptoms", "Updated diagnosis", "Updated prescription", "Updated notes"), CancellationToken.None);

        Assert.Equal("Updated diagnosis", result.Diagnosis);
        Assert.Equal(doctorId, result.DoctorId);
        Assert.Equal(MedicalReportStatus.Draft, result.Status);
    }

    [Fact]
    public async Task Handle_FinalizedReport_ThrowsInvalidOperationException()
    {
        var repository = new FakeMedicalReportRepository();
        var report = CreateReport(Guid.NewGuid());
        report.FinalizeReport();
        await repository.AddAsync(report);
        var handler = new UpdateDraftMedicalReportHandler(repository, new FakeCurrentUser { DoctorId = report.DoctorId }, new FakeUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateDraftMedicalReportCommand(report.Id, "Updated", "Updated", "Updated", "Updated"), CancellationToken.None));
    }

    private static MedicalReport CreateReport(Guid doctorId) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Symptoms", "Diagnosis", "Prescription", "Notes")
    {
        DoctorId = doctorId,
        DoctorName = "Dr Snapshot",
        SpecialtyName = "Cardiology",
        ScheduleDate = DateOnly.FromDateTime(DateTime.Today)
    };
}
