using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class CheckInTests
    {
        [Fact]
        public async Task Handle_ConfirmedAppointment_ShouldCheckInAndCreateQueueTicket()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            await appointmentRepository.AddAsync(appointment);

            // Act
            await handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None);

            // Assert
            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
            Assert.NotNull(appointment.QueueTicket);
            Assert.Equal(1, appointment.QueueTicket!.QueueNumber);
            Assert.Single(queueTicketRepository.All);
        }

        [Fact]
        public async Task Handle_SecondPatientSameDoctorSameDay_ShouldGetIncrementingQueueNumber()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);
            var ws = TestDataFactory.CreateWorkSchedule(date: new Clinic.Domain.Common.TimeConverter().Today);
            var firstAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var secondAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            await appointmentRepository.AddAsync(firstAppointment);
            await appointmentRepository.AddAsync(secondAppointment);

            // Act
            await handler.Handle(new CheckInCommand(firstAppointment.Id), CancellationToken.None);
            await handler.Handle(new CheckInCommand(secondAppointment.Id), CancellationToken.None);

            // Assert
            Assert.Equal(1, firstAppointment.QueueTicket!.QueueNumber);
            Assert.Equal(2, secondAppointment.QueueTicket!.QueueNumber);
        }

        [Fact]
        public async Task Handle_DifferentDoctorsSameDay_ShouldEachStartFromOne()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            var appointmentForDoctorA = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var appointmentForDoctorB = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            await appointmentRepository.AddAsync(appointmentForDoctorA);
            await appointmentRepository.AddAsync(appointmentForDoctorB);

            // Act
            await handler.Handle(new CheckInCommand(appointmentForDoctorA.Id), CancellationToken.None);
            await handler.Handle(new CheckInCommand(appointmentForDoctorB.Id), CancellationToken.None);

            // Assert: m?i bác si có hàng d?i riêng, không dùng chung s? th? t?.
            Assert.Equal(1, appointmentForDoctorA.QueueTicket!.QueueNumber);
            Assert.Equal(1, appointmentForDoctorB.QueueTicket!.QueueNumber);
        }

        [Fact]
        public async Task Handle_AppointmentNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new CheckInCommand(Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PendingAppointment_ShouldThrowArgumentException()
        {
            // Arrange: appointment chua du?c Confirm (v?n ? Pending) -> chua du?c phép check-in.
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(today: true);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AppointmentScheduledForAnotherDay_ShouldThrowConflictException()
        {
            // Arrange: l?ch h?n dã Confirmed nhung TimeSlot là NGÀY MAI -> không du?c phép
            // check-in vào hàng d?i c?a ngày hôm nay.
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None));

            // Không du?c sinh QueueTicket khi check-in b? ch?n.
            Assert.Empty(queueTicketRepository.All);
        }

        [Fact]
        public async Task Handle_AlreadyCheckedInAppointment_ShouldThrowArgumentException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert: g?i check-in l?n 2 cho cùng 1 appointment ph?i b? ch?n.
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None));
        }
    }
}