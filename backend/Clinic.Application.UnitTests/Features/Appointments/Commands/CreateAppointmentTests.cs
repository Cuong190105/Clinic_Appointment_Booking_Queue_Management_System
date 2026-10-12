using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class CreateAppointmentTests2
    {
        [Fact]
        public async Task Handle_ValidRequest_ShouldCreateAppointmentAndReturnId()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var notiQueue = new FakeNotificationQueue();
            var appointmentRepository = new FakeAppointmentRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);

            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            workScheduleRepository.Add(workSchedule);

            var timeSlot = workSchedule.ShiftStart.AddMinutes(30);
            var command = new CreateAppointmentCommand(patient.Person.User.Id, workSchedule.Id, timeSlot, "Routine checkup", false);

            // Act
            var appointment = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotEqual(Guid.Empty, appointment.Id);
            Assert.True(unitOfWork.WasCommitted);
            var created = workSchedule.Appointments.FirstOrDefault(a => a.Id == appointment.Id);
            Assert.NotNull(created);
            Assert.Equal(patient.Id, created.PatientId);
            Assert.Equal(workSchedule.Id, created.WorkScheduleId);
            Assert.Equal(timeSlot, created.TimeSlot);
            Assert.Equal(AppointmentStatus.Pending, created.Status);
        }

        [Fact]
        public async Task Handle_UserNotPatient_ShouldThrowException()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var currentUser = new FakeCurrentUser { PatientId = null };
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);

            var workSchedule = TestDataFactory.CreateWorkSchedule();
            workScheduleRepository.Add(workSchedule);

            var command = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "Reason", false, null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
            Assert.False(unitOfWork.WasCommitted);

            var command2 = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "Reason", false, Guid.NewGuid());
        }

        [Fact]
        public async Task Handle_WorkScheduleNotFound_ShouldThrowNotFoundExceptionAndRollback()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var appointmentRepository = new FakeAppointmentRepository();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);

            var nonExistentScheduleId = Guid.NewGuid();
            var command = new CreateAppointmentCommand(Guid.NewGuid(), nonExistentScheduleId, new TimeOnly(10, 0), "Reason", false, patient.Id);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
            Assert.True(unitOfWork.WasRolledBack);
        }

        [Fact]
        public async Task Handle_WorkScheduleNotActive_ShouldThrowException()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var appointmentRepository = new FakeAppointmentRepository();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            workSchedule.Cancel("Doctor on leave");
            workScheduleRepository.Add(workSchedule);

            var command = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "Reason", true, patient.Id);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.True(unitOfWork.WasRolledBack);
        }

        [Fact]
        public async Task Handle_TimeSlotOutsideShift_ShouldThrowArgumentException()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var appointmentRepository = new FakeAppointmentRepository();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);
            var workSchedule = TestDataFactory.CreateWorkSchedule();
            workScheduleRepository.Add(workSchedule);

            // Slot time before shift starts
            var invalidTimeSlot = workSchedule.ShiftStart.AddHours(-1);
            var command = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, invalidTimeSlot, "Reason", false, patient.Id);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.Handle(command, CancellationToken.None));
            Assert.True(unitOfWork.WasRolledBack);
        }

        [Fact]
        public async Task Handle_SlotFullyBooked_ShouldThrowConflictException()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var appointmentRepository = new FakeAppointmentRepository();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 2);
            workScheduleRepository.Add(workSchedule);
            var patient1 = TestDataFactory.CreatePatient();
            var patient2 = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient1);
            await patientRepository.AddAsync(patient2);

            // Fill capacity
            var app1 = new Appointment(patient1, workSchedule, workSchedule.ShiftStart, "Reason", patient1.Person.User.Id, false);
            var app2 = new Appointment(patient2, workSchedule, workSchedule.ShiftStart, "Reason", Guid.NewGuid(), true);
            workSchedule.AddAppointment(app1);
            workSchedule.AddAppointment(app2);

            var command = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "Reason", false, patient1.Id);

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
            Assert.True(unitOfWork.WasRolledBack);
        }

        [Fact]
        public async Task Handle_CancelledAppointmentsDoNotCountTowardsLimit_ShouldSucceed()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var appointmentRepository = new FakeAppointmentRepository();
            var handler = new CreateAppointmentCommandHandler(workScheduleRepository, patientRepository, appointmentRepository, unitOfWork, notiQueue);
            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);
            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 1);
            workScheduleRepository.Add(workSchedule);

            // One cancelled appointment
            var cancelledApp = new Appointment(patient, workSchedule, workSchedule.ShiftStart, "Cancelled", Guid.NewGuid(), false);
            cancelledApp.Cancel(patient.Person.User.Id);
            workSchedule.Appointments.Add(cancelledApp);

            var command = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "After cancellation", false, patient.Id);

            // Act
            var appointment = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotEqual(Guid.Empty, appointment.Id);
            Assert.True(unitOfWork.WasCommitted);
        }
    }
}
