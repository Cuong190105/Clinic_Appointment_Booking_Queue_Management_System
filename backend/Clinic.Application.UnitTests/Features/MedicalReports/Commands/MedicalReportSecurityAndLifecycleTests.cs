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
    public class MedicalReportSecurityAndLifecycleTests
    {
        [Fact]
        public async Task Handle_DoctorTriesToSaveAnotherDoctorsReport_ThrowsForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();

            var assignedDoctor = TestDataFactory.CreateDoctor();
            var unauthorizedDoctor = TestDataFactory.CreateDoctor();

            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: assignedDoctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            await queueTicketRepository.AddAsync(ticket);

            // Current user is unauthorized doctor
            var currentUser = new FakeCurrentUser { DoctorId = unauthorizedDoctor.Id, UserId = Guid.NewGuid() };

            var handler = new SaveMedicalReportHandler(
                medicalReportRepository,
                appointmentRepository,
                queueTicketRepository,
                currentUser,
                unitOfWork
            );

            var command = new SaveMedicalReportCommand(
                AppointmentId: appointment.Id,
                Symptoms: "Fever",
                Diagnosis: "Flu",
                Prescription: "Rest",
                Notes: "Drink water",
                IsFinalize: false
            );

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DoctorTriesToFinalizeAnotherDoctorsReport_ThrowsForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var assignedDoctor = TestDataFactory.CreateDoctor();
            var unauthorizedDoctor = TestDataFactory.CreateDoctor();

            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: assignedDoctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);

            var report = new MedicalReport(appointment.Id, "Headache", "Migraine", "Painkiller", "Rest");
            report.Appointment = appointment;
            await medicalReportRepository.AddAsync(report);

            var currentUser = new FakeCurrentUser { DoctorId = unauthorizedDoctor.Id, UserId = Guid.NewGuid() };
            var handler = new FinalizeMedicalReportHandler(medicalReportRepository, currentUser, unitOfWork);

            var command = new FinalizeMedicalReportCommand(report.Id);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DoctorTriesToAmendAnotherDoctorsReport_ThrowsForbiddenException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var assignedDoctor = TestDataFactory.CreateDoctor();
            var unauthorizedDoctor = TestDataFactory.CreateDoctor();

            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: assignedDoctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);

            var report = new MedicalReport(appointment.Id, "Headache", "Migraine", "Painkiller", "Rest");
            report.Appointment = appointment;
            report.FinalizeReport();
            await medicalReportRepository.AddAsync(report);

            var currentUser = new FakeCurrentUser { DoctorId = unauthorizedDoctor.Id, UserId = Guid.NewGuid() };
            var handler = new CreateAmendmentHandler(medicalReportRepository, currentUser, unitOfWork);

            var command = new CreateAmendmentCommand(report.Id, "Correction", "Updated dosage to 200mg");

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DoctorSavesDraft_DoesNotAlterQueueTicketOrAppointmentCompletion()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            var ticket = TestDataFactory.CreateQueueTicket(appointment);
            ticket.Call();
            ticket.StartExam(); // Status: InProgress
            await queueTicketRepository.AddAsync(ticket);

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };
            var handler = new SaveMedicalReportHandler(medicalReportRepository, appointmentRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                AppointmentId: appointment.Id,
                Symptoms: "Cough",
                Diagnosis: "Bronchitis draft",
                Prescription: "Syrup",
                Notes: "Ongoing observation",
                IsFinalize: false
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MedicalReportStatus.Draft, result.Status);

            // Ticket and appointment must remain in their examination state, not auto-completed!
            Assert.Equal(QueueStatus.InProgress, ticket.Status);
            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
            Assert.Single(medicalReportRepository.Audits);
            Assert.Equal("CreateDraft", medicalReportRepository.Audits[0].Action);
        }

        [Fact]
        public async Task Handle_SaveDraftOnFinalizedReport_ThrowsInvalidOperationException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            var report = new MedicalReport(appointment.Id, "Fever", "Flu", "Paracetamol", "Rest");
            report.Appointment = appointment;
            report.FinalizeReport(); // Already finalized!
            await medicalReportRepository.AddAsync(report);

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };
            var handler = new SaveMedicalReportHandler(medicalReportRepository, appointmentRepository, queueTicketRepository, currentUser, unitOfWork);

            var command = new SaveMedicalReportCommand(
                AppointmentId: appointment.Id,
                Symptoms: "New Symptoms",
                Diagnosis: "Changed Diagnosis",
                Prescription: "Changed Prescription",
                Notes: "Direct edit attempt",
                IsFinalize: false
            );

            // Act & Assert - direct edit must be blocked on finalized records
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot update a finalized medical report", ex.Message);
        }

        [Fact]
        public async Task Handle_CreateAmendmentOnFinalizedReport_SucceedsWithAuditAndDetails()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);

            var report = new MedicalReport(appointment.Id, "Stomach ache", "Gastritis", "Antacid", "Avoid spicy foods");
            report.Appointment = appointment;
            report.FinalizeReport();
            await medicalReportRepository.AddAsync(report);

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };
            var handler = new CreateAmendmentHandler(medicalReportRepository, currentUser, unitOfWork);

            var command = new CreateAmendmentCommand(
                MedicalReportId: report.Id,
                Reason: "Patient reported allergy to Antacid",
                Content: "Switched to Omeprazole 20mg once daily before breakfast."
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Patient reported allergy to Antacid", result.Reason);
            Assert.Equal("Switched to Omeprazole 20mg once daily before breakfast.", result.Content);
            Assert.Equal(doctor.Id, result.CreatedByDoctorId);

            Assert.Single(medicalReportRepository.Amendments);
            Assert.Equal("Patient reported allergy to Antacid", medicalReportRepository.Amendments[0].Reason);

            Assert.Single(medicalReportRepository.Audits);
            Assert.Equal("AddAmendment", medicalReportRepository.Audits[0].Action);
        }

        [Fact]
        public async Task Handle_CreateAmendmentOnDraftReport_ThrowsInvalidOperationException()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);

            var report = new MedicalReport(appointment.Id, "Stomach ache", "Gastritis", "Antacid", "Avoid spicy foods");
            report.Appointment = appointment;
            // Report is still in Draft!
            await medicalReportRepository.AddAsync(report);

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id, UserId = Guid.NewGuid() };
            var handler = new CreateAmendmentHandler(medicalReportRepository, currentUser, unitOfWork);

            var command = new CreateAmendmentCommand(
                MedicalReportId: report.Id,
                Reason: "Correction",
                Content: "Content"
            );

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Amendments can only be added to finalized medical reports", ex.Message);
        }

        [Fact]
        public async Task Handle_AdminOrElevatedPermission_CanSaveAndFinalizeAndAmendAnyReport()
        {
            // Arrange
            var medicalReportRepository = new FakeMedicalReportRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var assignedDoctor = TestDataFactory.CreateDoctor();
            var otherDoctor = TestDataFactory.CreateDoctor();

            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: assignedDoctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(workSchedule: workSchedule, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            // User is Admin or has medical-report.view.any
            var adminUser = new FakeCurrentUser
            {
                DoctorId = otherDoctor.Id,
                UserId = Guid.NewGuid()
            };
            adminUser.AddRole("Admin");
            adminUser.GrantPermission("medical-report.view.any");
            adminUser.GrantPermission("queue.complete-exam.any");

            var saveHandler = new SaveMedicalReportHandler(medicalReportRepository, appointmentRepository, queueTicketRepository, adminUser, unitOfWork);
            var finalizeHandler = new FinalizeMedicalReportHandler(medicalReportRepository, adminUser, unitOfWork);
            var amendHandler = new CreateAmendmentHandler(medicalReportRepository, adminUser, unitOfWork);

            // 1. Admin creates Draft
            var saveResult = await saveHandler.Handle(new SaveMedicalReportCommand(
                AppointmentId: appointment.Id,
                Symptoms: "Chest pain",
                Diagnosis: "Angina Draft",
                Prescription: "Nitroglycerin",
                Notes: "Urgent",
                IsFinalize: false
            ), CancellationToken.None);
            Assert.NotNull(saveResult);

            // 2. Admin finalizes
            var finalizeResult = await finalizeHandler.Handle(new FinalizeMedicalReportCommand(saveResult.Id), CancellationToken.None);
            Assert.Equal(MedicalReportStatus.Finalized, finalizeResult.Status);

            // 3. Admin amends
            var amendResult = await amendHandler.Handle(new CreateAmendmentCommand(
                saveResult.Id,
                "Supervisory note",
                "Reviewed and confirmed"
            ), CancellationToken.None);
            Assert.NotNull(amendResult);
            Assert.Equal("Supervisory note", amendResult.Reason);
        }
    }
}
