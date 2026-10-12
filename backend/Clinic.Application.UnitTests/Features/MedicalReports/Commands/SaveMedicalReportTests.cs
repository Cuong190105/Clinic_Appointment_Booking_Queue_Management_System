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
    public class SaveMedicalReportTests
    {
        [Fact]
        public async Task Handle_NewDraft_ShouldCreateSuccessfully()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();
            await queueTicketRepository.AddAsync(ticket);

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                ticket.Id,
                Symptoms: "Sore throat, mild fever",
                Diagnosis: "Pharyngitis",
                Prescription: "Paracetamol 500mg, Vitamin C",
                Notes: "Drink warm water",
                IsFinalize: false
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ticket.Id, result.QueueTicketId);
            Assert.Equal("Sore throat, mild fever", result.Symptoms);
            Assert.Equal("Pharyngitis", result.Diagnosis);
            Assert.Equal("Paracetamol 500mg, Vitamin C", result.Prescription);
            Assert.Equal(MedicalReportStatus.Draft, result.Status);

            var savedReport = await medicalReportRepository.GetByQueueTicketIdAsync(ticket.Id);
            Assert.NotNull(savedReport);
            Assert.Equal("Pharyngitis", savedReport.Diagnosis);
        }

        [Fact]
        public async Task Handle_ExistingDraft_ShouldUpdateSuccessfully()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();
            await queueTicketRepository.AddAsync(ticket);

            var existingReport = new MedicalReport(ticket.Id, "Old symptoms", "Old diag", "Old rx", "Old notes");
            await medicalReportRepository.AddAsync(existingReport);

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                ticket.Id,
                Symptoms: "Updated symptoms",
                Diagnosis: "Updated diag",
                Prescription: "Updated rx",
                Notes: "Updated notes",
                IsFinalize: false
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingReport.Id, result.Id);
            Assert.Equal("Updated symptoms", result.Symptoms);
            Assert.Equal("Updated diag", result.Diagnosis);
            Assert.Equal(MedicalReportStatus.Draft, result.Status);

            var updatedReport = await medicalReportRepository.GetByQueueTicketIdAsync(ticket.Id);
            Assert.NotNull(updatedReport);
            Assert.Equal("Updated diag", updatedReport.Diagnosis);
        }

        [Fact]
        public async Task Handle_Finalize_ShouldCompleteTicketAndAppointment()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();
            await queueTicketRepository.AddAsync(ticket);

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                ticket.Id,
                Symptoms: "Chest pain",
                Diagnosis: "Angina",
                Prescription: "Nitroglycerin 0.4mg",
                Notes: "Rest required",
                IsFinalize: true
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

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
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            // Ticket is in Waiting status, not InProgress
            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            await queueTicketRepository.AddAsync(ticket);

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                ticket.Id,
                Symptoms: "Symptoms",
                Diagnosis: "Diagnosis",
                Prescription: "Rx",
                Notes: null,
                IsFinalize: false
            );

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(command, CancellationToken.None));
            Assert.Contains("Examination must be in progress", ex.Message);
        }

        [Fact]
        public async Task Handle_UpdateFinalizedReport_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();
            await queueTicketRepository.AddAsync(ticket);

            var finalizedReport = new MedicalReport(ticket.Id, "Initial", "Initial", "Initial", null);
            finalizedReport.FinalizeReport();
            await medicalReportRepository.AddAsync(finalizedReport);

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                ticket.Id,
                Symptoms: "Try edit",
                Diagnosis: "Try edit",
                Prescription: "Try edit",
                Notes: null,
                IsFinalize: false
            );

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_UnauthorizedDoctor_ShouldThrowForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var otherDoctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            var currentUser = new FakeCurrentUser { DoctorId = otherDoctor.Id, UserId = Guid.NewGuid() };

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam();
            await queueTicketRepository.AddAsync(ticket);

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(ticket.Id, "Symptoms", "Diag", "Rx", null);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_TicketNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid(), UserId = Guid.NewGuid() };

            var handler = new SaveMedicalReportHandler(medicalReportRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(Guid.NewGuid(), "Symptoms", "Diag", "Rx", null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(command, CancellationToken.None));
        }
    }
}
