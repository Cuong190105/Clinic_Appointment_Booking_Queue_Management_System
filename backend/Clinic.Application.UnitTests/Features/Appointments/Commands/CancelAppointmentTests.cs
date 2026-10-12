using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class CancelAppointmentTests
    {
        [Fact]
        public async Task Handle_PendingAppointment_ShouldCancel()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment();
            currentUser.PatientId = appointment.PatientId;
            currentUser.UserId = appointment.Patient.Person.User.Id;
            await appointmentRepository.AddAsync(appointment);

            // Act
            await handler.Handle(new CancelAppointmentCommand(appointment.Id), CancellationToken.None);

            // Assert
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }

        // Cannot cancel an appointment within 24 hours before the appointment time. So these tests are commented out because they are not applicable in the current context.

        /// <summary>
        /// Bug gốc: hủy 1 Appointment đã CheckedIn (vé đang Waiting) khiến Appointment=Cancelled
        /// nhưng QueueTicket vẫn Waiting -> lễ tân vẫn gọi phải bệnh nhân "ảo". Sau fix, handler
        /// phải cascade-hủy QueueTicket cùng lúc.
        /// </summary>
        //[Fact]
        //public async Task Handle_CheckedInAppointmentWithWaitingTicket_ShouldCancelAppointmentAndQueueTicket()
        //{
        //    // Arrange
        //    var appointmentRepository = new FakeAppointmentRepository();
        //    var queueTicketRepository = new FakeQueueTicketRepository();
        //    var currentUser = new FakeCurrentUser();
        //    var unitOfWork = new FakeUnitOfWork();
        //    var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

        //    var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
        //    var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
        //    currentUser.PatientId = appointment.PatientId;
        //    currentUser.UserId = appointment.Patient.Person.User.Id;
        //    await appointmentRepository.AddAsync(appointment);
        //    await queueTicketRepository.AddAsync(queueTicket);

        //    // Act
        //    await handler.Handle(new CancelAppointmentCommand(appointment.Id), CancellationToken.None);

        //    // Assert: không còn "bệnh nhân ảo" nào ở trạng thái Waiting/Called/InProgress nữa.
        //    Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        //    Assert.Equal(QueueStatus.Cancelled, queueTicket.Status);
        //}

        //[Fact]
        //public async Task Handle_CheckedInAppointmentWithCalledTicket_ShouldCancelAppointmentAndQueueTicket()
        //{
        //    // Arrange
        //    var appointmentRepository = new FakeAppointmentRepository();
        //    var queueTicketRepository = new FakeQueueTicketRepository();
        //    var currentUser = new FakeCurrentUser();
        //    var unitOfWork = new FakeUnitOfWork();
        //    var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

        //    var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
        //    var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
        //    queueTicket.Call();
        //    currentUser.UserId = appointment.Patient.Person.User.Id;
        //    currentUser.PatientId = appointment.PatientId;
        //    await appointmentRepository.AddAsync(appointment);
        //    await queueTicketRepository.AddAsync(queueTicket);

        //    // Act
        //    await handler.Handle(new CancelAppointmentCommand(appointment.Id), CancellationToken.None);

        //    // Assert
        //    Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        //    Assert.Equal(QueueStatus.Cancelled, queueTicket.Status);
        //}

        /// <summary>Bác sĩ đang khám dở (InProgress) -> phải chặn hủy, không được cascade.</summary>
        //[Fact]
        //public async Task Handle_CheckedInAppointmentWithInProgressTicket_ShouldThrowArgumentException()
        //{
        //    // Arrange
        //    var appointmentRepository = new FakeAppointmentRepository();
        //    var queueTicketRepository = new FakeQueueTicketRepository();
        //    var currentUser = new FakeCurrentUser();
        //    var unitOfWork = new FakeUnitOfWork();
        //    var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

        //    var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
        //    var queueTicket = TestDataFactory.CreateQueueTicket(appointment);
        //    queueTicket.Call();
        //    queueTicket.StartExam();
        //    currentUser.UserId = appointment.Patient.Person.User.Id;
        //    currentUser.PatientId = appointment.PatientId;
        //    await appointmentRepository.AddAsync(appointment);
        //    await queueTicketRepository.AddAsync(queueTicket);

        //    // Act & Assert
        //    await Assert.ThrowsAsync<ArgumentException>(() =>
        //        handler.Handle(new CancelAppointmentCommand(appointment.Id), CancellationToken.None));

        //    Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
        //    Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
        //}

        [Fact]
        public async Task Handle_AppointmentNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);
            currentUser.UserId = Guid.NewGuid();
            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new CancelAppointmentCommand(Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_OtherPatientWithoutAnyScope_ShouldThrowForbiddenException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment();
            currentUser.PatientId = Guid.NewGuid(); // khác với appointment.PatientId
            currentUser.UserId = Guid.NewGuid();
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new CancelAppointmentCommand(appointment.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ReceptionistWithAnyScope_ShouldCancelAppointmentOfAnyPatient()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            currentUser.UserId = Guid.NewGuid();
            currentUser.GrantPermission("appointment.edit.any");
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment();
            await appointmentRepository.AddAsync(appointment);

            // Act
            await handler.Handle(new CancelAppointmentCommand(appointment.Id), CancellationToken.None);

            // Assert
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }
    }
}