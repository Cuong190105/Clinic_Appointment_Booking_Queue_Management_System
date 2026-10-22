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
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            await appointmentRepository.AddAsync(appointment);

            // Act
            await handler.Handle(new CheckInCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

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
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());
            var ws = TestDataFactory.CreateWorkSchedule(date: new Clinic.Domain.Common.TimeConverter().Today);
            var firstAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var secondAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            await appointmentRepository.AddAsync(firstAppointment);
            await appointmentRepository.AddAsync(secondAppointment);

            // Act
            await handler.Handle(new CheckInCommand(firstAppointment.Id, Guid.NewGuid()), CancellationToken.None);
            await handler.Handle(new CheckInCommand(secondAppointment.Id, Guid.NewGuid()), CancellationToken.None);

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
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());

            var appointmentForDoctorA = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var appointmentForDoctorB = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            await appointmentRepository.AddAsync(appointmentForDoctorA);
            await appointmentRepository.AddAsync(appointmentForDoctorB);

            // Act
            await handler.Handle(new CheckInCommand(appointmentForDoctorA.Id, Guid.NewGuid()), CancellationToken.None);
            await handler.Handle(new CheckInCommand(appointmentForDoctorB.Id, Guid.NewGuid()), CancellationToken.None);

            // Assert: mỗi bác sĩ có hàng đợi riêng, không dùng chung số thứ tự.
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
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new CheckInCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PendingAppointment_ShouldThrowArgumentException()
        {
            // Arrange: appointment chưa được Confirm (vẫn ở Pending) -> chưa được phép check-in.
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());

            var appointment = TestDataFactory.CreateAppointment();
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AppointmentScheduledForAnotherDay_ShouldThrowConflictException()
        {
            // Arrange: lịch hẹn đã Confirmed nhưng TimeSlot là NGÀY MAI -> không được phép
            // check-in vào hàng đợi của ngày hôm nay.
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());

            var appointment = TestDataFactory.CreateAppointment(confirmed: true);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None));

            // Không được sinh QueueTicket khi check-in bị chặn.
            Assert.Empty(queueTicketRepository.All);
        }

        [Fact]
        public async Task Handle_AlreadyCheckedInAppointment_ShouldThrowArgumentException()
        {
            // Arrange
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork, new FakeOutboxWriter(), new FakeInboxStore());
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, checkedIn: true);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert: gọi check-in lần 2 cho cùng 1 appointment phải bị chặn.
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_SameIdempotencyKeyTwice_ShouldCreateOnlyOneQueueTicket()
        {
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(
                appointmentRepository,
                queueTicketRepository,
                unitOfWork,
                new FakeOutboxWriter(),
                new FakeInboxStore());
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var idempotencyKey = Guid.NewGuid();
            await appointmentRepository.AddAsync(appointment);

            await handler.Handle(new CheckInCommand(appointment.Id, idempotencyKey), CancellationToken.None);
            await handler.Handle(new CheckInCommand(appointment.Id, idempotencyKey), CancellationToken.None);

            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
            Assert.Single(queueTicketRepository.All);
            Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        }

        [Fact]
        public async Task Handle_EmptyIdempotencyKey_ShouldRejectRequestBeforeChangingAppointment()
        {
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var handler = new CheckInHandler(
                appointmentRepository,
                queueTicketRepository,
                new FakeUnitOfWork(),
                new FakeOutboxWriter(),
                new FakeInboxStore());
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            await appointmentRepository.AddAsync(appointment);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id, Guid.Empty), CancellationToken.None));

            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
            Assert.Empty(queueTicketRepository.All);
        }
    }
}

