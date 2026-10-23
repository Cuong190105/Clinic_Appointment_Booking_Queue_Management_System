using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Features.MedicalReports.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.MedicalReports.Commands
{
    public class UpdateDraftMedicalReportTests
    {
        [Fact]
        public async Task Handle_DraftReport_UpdatesSuccessfully()
        {
            var repo = new FakeMedicalReportRepository();
            var uow = new FakeUnitOfWork();
            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.GrantRole("Doctor");

            var ws = TestDataFactory.CreateWorkSchedule(doctor: doctor);
            var appointment = TestDataFactory.CreateAppointment(workSchedule: ws);
            var report = new MedicalReport(appointment.Id, patient.Id, "Sốt", "Cảm cúm", "Paracetamol", "Nghỉ ngơi")
            {
                Appointment = appointment,
                Patient = patient
            };
            await repo.AddAsync(report);

            var handler = new UpdateDraftMedicalReportHandler(repo, currentUser, uow);
            var result = await handler.Handle(new UpdateDraftMedicalReportCommand(
                ReportId: report.Id,
                Symptoms: "Sốt 39 độ",
                Diagnosis: "Cảm cúm A",
                Prescription: "Tamiflu",
                Notes: "Cách ly",
                IsFinalize: false
            ), CancellationToken.None);

            Assert.Equal("Cảm cúm A", result.Diagnosis);
            Assert.Equal(MedicalReportStatus.Draft, result.Status);
        }

        [Fact]
        public async Task Handle_FinalizedReport_ThrowsInvalidOperationException()
        {
            var repo = new FakeMedicalReportRepository();
            var uow = new FakeUnitOfWork();
            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.GrantRole("Doctor");

            var ws = TestDataFactory.CreateWorkSchedule(doctor: doctor);
            var appointment = TestDataFactory.CreateAppointment(workSchedule: ws);
            var report = new MedicalReport(appointment.Id, patient.Id, "Sốt", "Cảm cúm", "Paracetamol", "Nghỉ ngơi")
            {
                Appointment = appointment,
                Patient = patient
            };
            report.FinalizeReport();
            await repo.AddAsync(report);

            var handler = new UpdateDraftMedicalReportHandler(repo, currentUser, uow);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new UpdateDraftMedicalReportCommand(
                    ReportId: report.Id,
                    Symptoms: "Sốt 39 độ",
                    Diagnosis: "Cảm cúm A",
                    Prescription: "Tamiflu",
                    Notes: "Cách ly"
                ), CancellationToken.None));
        }
    }
}

