using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.MedicalReports.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Commands;

public sealed class FinalizeMedicalReportTests
{
    [Fact]
    public async Task Handle_ValidReport_FinalizesReportFromSnapshot()
    {
        var repository = new FakeMedicalReportRepository();
        var doctorId = Guid.NewGuid();
        var report = CreateReport(doctorId);
        await repository.AddAsync(report);
        var handler = new FinalizeMedicalReportHandler(repository, new FakeCurrentUser { DoctorId = doctorId }, new FakeUnitOfWork());

        var result = await handler.Handle(new FinalizeMedicalReportCommand(report.Id), CancellationToken.None);

        Assert.Equal(MedicalReportStatus.Finalized, result.Status);
        Assert.Equal(doctorId, result.DoctorId);
        Assert.Equal("Dr Snapshot", result.DoctorName);
        Assert.NotNull(result.ExamEndTime);
    }

    [Fact]
    public async Task Handle_UnauthorizedDoctor_RejectsFinalize()
    {
        var repository = new FakeMedicalReportRepository();
        var report = CreateReport(Guid.NewGuid());
        await repository.AddAsync(report);
        var handler = new FinalizeMedicalReportHandler(repository, new FakeCurrentUser { DoctorId = Guid.NewGuid() }, new FakeUnitOfWork());

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new FinalizeMedicalReportCommand(report.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReportNotFound_ThrowsNotFoundException()
    {
        var handler = new FinalizeMedicalReportHandler(new FakeMedicalReportRepository(), new FakeCurrentUser(), new FakeUnitOfWork());
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new FinalizeMedicalReportCommand(Guid.NewGuid()), CancellationToken.None));
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
