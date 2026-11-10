using Clinic.Application.Common.Exceptions;
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
    public class FinalizeMedicalReportTests
    {
        [Fact]
        public async Task Handle_ValidReport_ShouldFinalizeReport()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();

            var report = new MedicalReport(appointment.Id, "Chest pain", "Angina", "Nitroglycerin", "Rest")
            {
                QueueTicket = ticket,
                Appointment = appointment
            };
            await medicalReportRepository.AddAsync(report);

            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            // Act
            var result = await handler.Handle(new FinalizeMedicalReportCommand(report.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MedicalReportStatus.Finalized, result.Status);
            Assert.NotNull(result.ExamEndTime);
        }

        [Fact]
        public async Task Handle_AlreadyFinalizedReport_ShouldBeIdempotent()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var report = new MedicalReport(appointment.Id, "Chest pain", "Angina", "Nitroglycerin", "Rest")
            {
                Appointment = appointment
            };
            report.FinalizeReport();
            await medicalReportRepository.AddAsync(report);

            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            // Act
            var result = await handler.Handle(new FinalizeMedicalReportCommand(report.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MedicalReportStatus.Finalized, result.Status);
        }

        [Fact]
        public async Task Handle_UnauthorizedDoctor_ShouldThrowForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var otherDoctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = otherDoctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();

            var report = new MedicalReport(appointment.Id, "Chest pain", "Angina", "Nitroglycerin", "Rest")
            {
                QueueTicket = ticket,
                Appointment = appointment
            };
            await medicalReportRepository.AddAsync(report);

            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new FinalizeMedicalReportCommand(report.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ReportNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid(), UserId = Guid.NewGuid() };

            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new FinalizeMedicalReportCommand(Guid.NewGuid()), CancellationToken.None));
        }
    }
}
