using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class CallNextQueueTests
    {
        [Fact]
        public async Task Handle_WaitingTicketExists_ShouldCallIt()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CallNextQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            var result = await handler.Handle(new CallNextQueueCommand(doctor.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.Called.ToString(), result.Status);
            Assert.NotNull(queueTicket.CalledAt);
            Assert.Equal(QueueStatus.Called, queueTicket.Status);
        }

        [Fact]
        public async Task Handle_PriorityTicketAndNormalTicketBothWaiting_ShouldCallPriorityFirst()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CallNextQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));

            var normalAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var normalTicket = TestDataFactory.CreateQueueTicket(normalAppointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(normalTicket);

            var priorityAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var priorityTicket = TestDataFactory.CreateQueueTicket(priorityAppointment, queueNumber: 2, priority: true);
            await queueTicketRepository.AddAsync(priorityTicket);

            // Act: dù s? th? t? l?n hon (2), vé uu tiên v?n ph?i du?c g?i tru?c vé s? 1.
            var result = await handler.Handle(new CallNextQueueCommand(doctor.Id), CancellationToken.None);

            // Assert
            Assert.Equal(priorityTicket.Id, result.Id);
            Assert.Equal(QueueStatus.Called, priorityTicket.Status);
            Assert.Equal(QueueStatus.Waiting, normalTicket.Status);
        }

        [Fact]
        public async Task Handle_NoWaitingTickets_ShouldThrowConflictException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CallNextQueueHandler(queueTicketRepository, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new CallNextQueueCommand(Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_TicketAlreadyCalled_ShouldNotBeCalledAgain()
        {
            // Arrange: vé dã Called không còn tính là "dang ch?" nên không du?c g?i l?n 2.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CallNextQueueHandler(queueTicketRepository, unitOfWork);

            var doctorId = Guid.NewGuid();
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new CallNextQueueCommand(doctorId), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DoctorHasActiveCalledTicket_ShouldAllowCallingNextTicketSequentially()
        {
            // Arrange: bác sĩ đang có 1 vé Called, nhưng được gọi lần lượt nhiều số mà không cần hoàn tất lượt trước
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CallNextQueueHandler(queueTicketRepository, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));

            var calledAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var calledTicket = TestDataFactory.CreateQueueTicket(calledAppointment, queueNumber: 1);
            calledTicket.Call();
            await queueTicketRepository.AddAsync(calledTicket);

            var waitingAppointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var waitingTicket = TestDataFactory.CreateQueueTicket(waitingAppointment, queueNumber: 2);
            await queueTicketRepository.AddAsync(waitingTicket);

            // Act: Bác sĩ gọi số tiếp theo
            var result = await handler.Handle(new CallNextQueueCommand(doctor.Id), CancellationToken.None);

            // Assert: Vé số 2 được gọi thành công mà không bị chặn
            Assert.Equal(2, result.QueueNumber);
            Assert.Equal(QueueStatus.Called, waitingTicket.Status);
        }
    }
}