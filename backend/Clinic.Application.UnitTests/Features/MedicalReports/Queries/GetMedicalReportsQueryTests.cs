using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Queries;

public sealed class GetMedicalReportsQueryTests
{
    [Fact]
    public async Task Handle_SearchFilter_UsesReportSnapshot()
    {
        var repository = new FakeMedicalReportRepository();
        var user = new FakeCurrentUser { DoctorId = Guid.NewGuid() };
        user.GrantPermission("medical-report.view.any");
        var matching = CreateReport("Pharyngitis", "Dr Snapshot");
        var other = CreateReport("Migraine", "Dr Other");
        await repository.AddAsync(matching);
        await repository.AddAsync(other);
        var handler = new GetMedicalReportsQueryHandler(repository, user);

        var result = await handler.Handle(new GetMedicalReportsQuery(Search: "Pharyngitis"), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Pharyngitis", result.Items[0].Diagnosis);
        Assert.Equal("Dr Snapshot", result.Items[0].DoctorName);
    }

    private static MedicalReport CreateReport(string diagnosis, string doctorName) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Symptoms", diagnosis, "Treatment", null)
    {
        DoctorId = Guid.NewGuid(),
        DoctorName = doctorName,
        SpecialtyName = "Cardiology",
        ScheduleDate = DateOnly.FromDateTime(DateTime.Today)
    };
}
