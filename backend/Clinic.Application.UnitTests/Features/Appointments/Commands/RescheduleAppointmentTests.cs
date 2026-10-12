// Remove this test, as the feature is removed. Instead use UpdateAppointment


//using Clinic.Application.Common.Exceptions;
//using Clinic.Application.Features.Appointments.Commands;
//using Clinic.Application.UnitTests.Common;
//using Clinic.Domain.Common.Exceptions;
//using Clinic.Domain.Entities;
//using Clinic.Domain.Enums;

//namespace Clinic.Application.UnitTests.Features.Appointments.Commands
//{
//    public class RescheduleAppointmentTests
//    {
//        [Fact]
//        public async Task Handle_ValidRequest_ShouldRescheduleAppointment()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser();
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings { RescheduleMinNoticeHours = 2 };
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            var patientId = Guid.NewGuid();
//            currentUser.PatientId = patientId;

//            // Original appointment 24 hours in the future
//            var originalAppointment = TestDataFactory.CreateAppointment(patientId: patientId, timeSlot: DateTime.UtcNow.AddHours(24));
//            await appointmentRepository.AddAsync(originalAppointment);

//            // New work schedule in 2 days
//            var newSchedule = TestDataFactory.CreateWorkSchedule(shiftStart: DateTime.UtcNow.AddDays(2).Date.AddHours(9), shiftEnd: DateTime.UtcNow.AddDays(2).Date.AddHours(12));
//            workScheduleRepository.Add(newSchedule);

//            var newTimeSlot = newSchedule.ShiftStart.AddMinutes(30);
//            var command = new RescheduleAppointmentCommand(originalAppointment.Id, newSchedule.Id, newTimeSlot);

//            // Act
//            await handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.True(unitOfWork.WasCommitted);
//            Assert.Equal(newSchedule.Id, originalAppointment.WorkScheduleId);
//            Assert.Equal(newTimeSlot, originalAppointment.TimeSlot);
//        }

//        [Fact]
//        public async Task Handle_AppointmentNotFound_ShouldThrowNotFoundException()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings();
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            var command = new RescheduleAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(1));

//            // Act & Assert
//            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
//        }

//        [Fact]
//        public async Task Handle_TimeSlotInPast_ShouldThrowArgumentException()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser();
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings();
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            var appointment = TestDataFactory.CreateAppointment();
//            currentUser.PatientId = appointment.PatientId;
//            await appointmentRepository.AddAsync(appointment);

//            var command = new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), DateTime.UtcNow.AddHours(-1));

//            // Act & Assert
//            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
//        }

//        [Fact]
//        public async Task Handle_UnauthorizedPatient_ShouldThrowForbiddenException()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() }; // Different from appointment patient
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings();
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            var appointment = TestDataFactory.CreateAppointment();
//            await appointmentRepository.AddAsync(appointment);

//            var command = new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), DateTime.UtcNow.AddDays(1));

//            // Act & Assert
//            await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
//        }

//        [Fact]
//        public async Task Handle_NoticeHoursViolation_ShouldThrowConflictException()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser();
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings { RescheduleMinNoticeHours = 4 };
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            // Appointment is in 2 hours, but minimum notice is 4 hours
//            var appointment = TestDataFactory.CreateAppointment(timeSlot: DateTime.UtcNow.AddHours(2));
//            currentUser.PatientId = appointment.PatientId;
//            await appointmentRepository.AddAsync(appointment);

//            var command = new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), DateTime.UtcNow.AddDays(1));

//            // Act & Assert
//            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
//        }

//        [Fact]
//        public async Task Handle_StaffWithUpdatePermission_ShouldBypassNoticeHours()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser();
//            currentUser.GrantPermission("appointment.update"); // Staff permission
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings { RescheduleMinNoticeHours = 24 };
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            // Appointment is in 1 hour (less than 24h notice)
//            var appointment = TestDataFactory.CreateAppointment(timeSlot: DateTime.UtcNow.AddHours(1));
//            await appointmentRepository.AddAsync(appointment);

//            var newSchedule = TestDataFactory.CreateWorkSchedule();
//            workScheduleRepository.Add(newSchedule);

//            var newTimeSlot = newSchedule.ShiftStart.AddMinutes(30);
//            var command = new RescheduleAppointmentCommand(appointment.Id, newSchedule.Id, newTimeSlot);

//            // Act
//            await handler.Handle(command, CancellationToken.None);

//            // Assert
//            Assert.True(unitOfWork.WasCommitted);
//            Assert.Equal(newSchedule.Id, appointment.WorkScheduleId);
//            Assert.Equal(newTimeSlot, appointment.TimeSlot);
//        }

//        [Fact]
//        public async Task Handle_NewWorkScheduleNotFound_ShouldThrowNotFoundExceptionAndRollback()
//        {
//            // Arrange
//            var appointmentRepository = new FakeAppointmentRepository();
//            var workScheduleRepository = new FakeWorkScheduleRepository();
//            var currentUser = new FakeCurrentUser();
//            var unitOfWork = new FakeUnitOfWork();
//            var policySettings = new FakeAppointmentPolicySettings();
//            var handler = new RescheduleAppointmentHandler(appointmentRepository, workScheduleRepository, currentUser, unitOfWork, policySettings);

//            var appointment = TestDataFactory.CreateAppointment(timeSlot: DateTime.UtcNow.AddDays(1));
//            currentUser.PatientId = appointment.PatientId;
//            await appointmentRepository.AddAsync(appointment);

//            var nonExistentScheduleId = Guid.NewGuid();
//            var command = new RescheduleAppointmentCommand(appointment.Id, nonExistentScheduleId, DateTime.UtcNow.AddDays(2));

//            // Act & Assert
//            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
//            Assert.True(unitOfWork.WasRolledBack);
//        }
//    }
//}
