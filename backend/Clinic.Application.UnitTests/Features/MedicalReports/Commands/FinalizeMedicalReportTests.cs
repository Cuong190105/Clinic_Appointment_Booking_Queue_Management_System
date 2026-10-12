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
        public async Task Handle_ValidReport_ShouldFinalizeReportAndCompleteTicketAndAppointment()
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

            var report = new MedicalReport(ticket.Id, "Chest pain", "Angina", "Nitroglycerin", "Rest")
            {
                QueueTicket = ticket
            };
            await medicalReportRepository.AddAsync(report);

            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            // Act
            var result = await handler.Handle(new FinalizeMedicalReportCommand(report.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MedicalReportStatus.Finalized, result.Status);
            Assert.NotNull(result.ExamEndTime);
            Assert.Equal(QueueStatus.Completed, ticket.Status);
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        }

        [Fact]
        public async Task Handle_TicketNotInProgress_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            // Ticket is still in Waiting status
            var ticket = TestDataFactory.CreateQueueTicket(appointment);

            var report = new MedicalReport(ticket.Id, "Chest pain", "Angina", "Nitroglycerin", "Rest")
            {
                QueueTicket = ticket
            };
            await medicalReportRepository.AddAsync(report);

            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new FinalizeMedicalReportCommand(report.Id), CancellationToken.None));
            Assert.Contains("Examination must be in progress", ex.Message);
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

            var report = new MedicalReport(ticket.Id, "Chest pain", "Angina", "Nitroglycerin", "Rest")
            {
                QueueTicket = ticket
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
