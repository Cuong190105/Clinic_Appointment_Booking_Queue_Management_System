using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class ConfirmAppointmentTests
    {
        [Fact]
        public async Task Handle_PendingAppointment_ShouldConfirm()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var notiService = new FakeNotificationService();
            var handler = new ConfirmAppointmentCommandHandler(appointmentRepository, notiQueue, notiService, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment();
            await appointmentRepository.AddAsync(appointment);

            // Act
            await handler.Handle(new ConfirmAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

            // Assert
            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        }

        [Fact]
        public async Task Handle_AppointmentNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var notiService = new FakeNotificationService();
            var handler = new ConfirmAppointmentCommandHandler(appointmentRepository, notiQueue, notiService, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new ConfirmAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AlreadyConfirmedAppointment_ShouldThrowArgumentException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var notiService = new FakeNotificationService();
            var handler = new ConfirmAppointmentCommandHandler(appointmentRepository, notiQueue, notiService, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new ConfirmAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_CancelledAppointment_ShouldThrowArgumentException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();
            var notiService = new FakeNotificationService();
            var handler = new ConfirmAppointmentCommandHandler(appointmentRepository, notiQueue, notiService, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment();
            appointment.Cancel(Guid.NewGuid());
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new ConfirmAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None));
        }
    }
}