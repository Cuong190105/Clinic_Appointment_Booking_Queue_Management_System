using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Features.MedicalReports.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Commands;

public sealed class SaveMedicalReportTests
{
    [Fact]
    public async Task Handle_NewDraft_StoresAppointmentSnapshot()
    {
        var reports = new FakeMedicalReportRepository();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var snapshot = CreateAppointmentSnapshot(patientId, doctorId);
        var handler = new SaveMedicalReportHandler(reports, new FakeCurrentUser { DoctorId = doctorId }, new FakeUnitOfWork());
        var command = new SaveMedicalReportCommand(snapshot.Id, "Symptoms", "Diagnosis", "Rx", "Notes", false, snapshot, "InProgress", DateTime.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(snapshot.Id, result.AppointmentId);
        Assert.Equal(patientId, result.PatientId);
        Assert.Equal(doctorId, result.DoctorId);
        Assert.Equal("Dr Snapshot", result.DoctorName);
        Assert.Equal(snapshot.Date, result.ExamDate);
        Assert.Equal(MedicalReportStatus.Draft, result.Status);
        var saved = await reports.GetByAppointmentIdAsync(snapshot.Id);
        Assert.Equal(doctorId, saved!.DoctorId);
    }

    [Fact]
    public async Task Handle_ExistingDraft_UpdatesWithoutChangingOriginalSnapshot()
    {
        var reports = new FakeMedicalReportRepository();
        var snapshot = CreateAppointmentSnapshot();
        var report = CreateReport(snapshot);
        await reports.AddAsync(report);
        var handler = new SaveMedicalReportHandler(reports, new FakeCurrentUser { DoctorId = snapshot.DoctorId }, new FakeUnitOfWork());

        var result = await handler.Handle(new SaveMedicalReportCommand(
            snapshot.Id, "Updated symptoms", "Updated diagnosis", "Updated Rx", "Updated notes", false,
            snapshot, "InProgress"), CancellationToken.None);

        Assert.Equal(report.Id, result.Id);
        Assert.Equal("Updated diagnosis", result.Diagnosis);
        Assert.Equal(snapshot.DoctorId, report.DoctorId);
        Assert.Equal(MedicalReportStatus.Draft, result.Status);
    }

    [Fact]
    public async Task Handle_Finalize_FinalizesReport()
    {
        var reports = new FakeMedicalReportRepository();
        var snapshot = CreateAppointmentSnapshot();
        var handler = new SaveMedicalReportHandler(reports, new FakeCurrentUser { DoctorId = snapshot.DoctorId }, new FakeUnitOfWork());

        var result = await handler.Handle(new SaveMedicalReportCommand(
            snapshot.Id, "Symptoms", "Diagnosis", "Rx", null, true, snapshot, "InProgress"), CancellationToken.None);

        Assert.Equal(MedicalReportStatus.Finalized, result.Status);
        Assert.NotNull(result.ExamEndTime);
    }

    [Fact]
    public async Task Handle_QueueTicketNotInProgress_RejectsWriting()
    {
        var reports = new FakeMedicalReportRepository();
        var snapshot = CreateAppointmentSnapshot();
        var handler = new SaveMedicalReportHandler(reports, new FakeCurrentUser { DoctorId = snapshot.DoctorId }, new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new SaveMedicalReportCommand(snapshot.Id, "Symptoms", "Diagnosis", "Rx", null, false, snapshot, "Waiting"),
            CancellationToken.None));

        Assert.Contains("Examination must be in progress", exception.Message);
    }

    [Fact]
    public async Task Handle_FinalizedReport_RejectsUpdate()
    {
        var reports = new FakeMedicalReportRepository();
        var snapshot = CreateAppointmentSnapshot();
        var report = CreateReport(snapshot);
        report.FinalizeReport();
        await reports.AddAsync(report);
        var handler = new SaveMedicalReportHandler(reports, new FakeCurrentUser { DoctorId = snapshot.DoctorId }, new FakeUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new SaveMedicalReportCommand(snapshot.Id, "Changed", "Changed", "Changed", null, false, snapshot, "InProgress"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnauthorizedDoctor_RejectsWrite()
    {
        var reports = new FakeMedicalReportRepository();
        var snapshot = CreateAppointmentSnapshot();
        var handler = new SaveMedicalReportHandler(reports, new FakeCurrentUser { DoctorId = Guid.NewGuid() }, new FakeUnitOfWork());

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new SaveMedicalReportCommand(snapshot.Id, "Symptoms", "Diagnosis", "Rx", null, false, snapshot, "InProgress"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingAppointmentSnapshot_ThrowsNotFound()
    {
        var handler = new SaveMedicalReportHandler(new FakeMedicalReportRepository(), new FakeCurrentUser(), new FakeUnitOfWork());
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SaveMedicalReportCommand(Guid.NewGuid(), "Symptoms", "Diagnosis", "Rx", null),
            CancellationToken.None));
    }

    private static AppointmentReportSnapshot CreateAppointmentSnapshot(Guid? patientId = null, Guid? doctorId = null) => new(
        Guid.NewGuid(), patientId ?? Guid.NewGuid(), "Patient Snapshot", doctorId ?? Guid.NewGuid(),
        "Dr Snapshot", "Cardiology", DateOnly.FromDateTime(DateTime.Today), "CheckedIn");

    private static MedicalReport CreateReport(AppointmentReportSnapshot snapshot) => new(
        snapshot.Id, snapshot.PatientId, null, "Diagnosis", "Rx", "Notes")
    {
        DoctorId = snapshot.DoctorId,
        DoctorName = snapshot.DoctorName,
        SpecialtyName = snapshot.SpecialtyName,
        ScheduleDate = snapshot.Date
    };
}
