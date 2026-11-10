using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class SkipQueueTests
    {
        [Fact]
        public async Task Handle_WaitingTicket_ShouldSkip()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SkipQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            var result = await handler.Handle(new SkipQueueCommand(queueTicket.Id), CancellationToken.None);

            // Assert: vé Waiting b? skip thì bác si chua h? b?n v?i vé này -> không có
            // hành d?ng "g?i s? ti?p theo" nào du?c t? d?ng th?c hi?n thay l? tân.
            Assert.Equal(QueueStatus.Skipped, queueTicket.Status);
            Assert.Null(result.NextCalledTicket);
        }

        [Fact]
        public async Task Handle_CalledTicket_ShouldSkipAndAutoCallNextWaiting()
        {
            // Arrange: b?nh nhân du?c g?i nhung không có m?t -> l? tân b? qua lu?t. Vé Called
            // dang gi? ch? bác si, nên sau khi skip, h? th?ng ph?i t? d?ng g?i vé Waiting k? ti?p
            // d? "gi?i phóng bác si và g?i b?nh nhân ti?p theo" dúng nhu nghi?p v? yêu c?u.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SkipQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment1 = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var noShowTicket = TestDataFactory.CreateQueueTicket(appointment1, queueNumber: 1);
            noShowTicket.Call();
            await queueTicketRepository.AddAsync(noShowTicket);

            var appointment2 = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var nextWaitingTicket = TestDataFactory.CreateQueueTicket(appointment2, queueNumber: 2);
            await queueTicketRepository.AddAsync(nextWaitingTicket);

            // Act
            var result = await handler.Handle(new SkipQueueCommand(noShowTicket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.Skipped, noShowTicket.Status);
            Assert.Equal(QueueStatus.Called, nextWaitingTicket.Status);
            Assert.NotNull(result.NextCalledTicket);
            Assert.Equal(nextWaitingTicket.Id, result.NextCalledTicket!.Id);
        }

        [Fact]
        public async Task Handle_CalledTicket_ShouldSkipWithoutAutoCall_WhenNoOneWaiting()
        {
            // Arrange: vé Called b? skip nhung hàng d?i hi?n không còn ai Waiting -> bác si
            // du?c gi?i phóng nhung không có ai d? t? d?ng g?i, v?n ph?i skip thành công.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SkipQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            var result = await handler.Handle(new SkipQueueCommand(queueTicket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.Skipped, queueTicket.Status);
            Assert.Null(result.NextCalledTicket);
        }

        [Fact]
        public async Task Handle_QueueTicketNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SkipQueueHandler(queueTicketRepository, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new SkipQueueCommand(Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InProgressTicket_ShouldThrowArgumentException()
        {
            // Arrange: vé dang du?c bác si khám d? -> không du?c phép "b? qua",
            // ph?i dùng Complete() d? k?t thúc lu?t khám.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SkipQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            queueTicket.StartExam();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new SkipQueueCommand(queueTicket.Id), CancellationToken.None));

            Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
        }

        [Fact]
        public async Task Handle_AlreadySkippedTicket_ShouldThrowArgumentException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SkipQueueHandler(queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Skip();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new SkipQueueCommand(queueTicket.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DoctorSkipCalledTicket_OwnTicket_ShouldSucceed()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            ticket.Call();
            await queueTicketRepository.AddAsync(ticket);

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.AddRole("Doctor");
            var handler = new SkipQueueHandler(queueTicketRepository, currentUser, unitOfWork);

            // Act
            var result = await handler.Handle(new SkipQueueCommand(ticket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.Skipped, ticket.Status);
        }

        [Fact]
        public async Task Handle_DoctorSkipWaitingTicket_ShouldThrowConflictException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(ticket);

            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.AddRole("Doctor");
            var handler = new SkipQueueHandler(queueTicketRepository, currentUser, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new SkipQueueCommand(ticket.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DoctorSkipOtherDoctorTicket_ShouldThrowForbiddenException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();

            var doctor = TestDataFactory.CreateDoctor();
            var otherDoctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            ticket.Call();
            await queueTicketRepository.AddAsync(ticket);

            var currentUser = new FakeCurrentUser { DoctorId = otherDoctor.Id };
            currentUser.AddRole("Doctor");
            var handler = new SkipQueueHandler(queueTicketRepository, currentUser, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new SkipQueueCommand(ticket.Id), CancellationToken.None));
        }
    }
}