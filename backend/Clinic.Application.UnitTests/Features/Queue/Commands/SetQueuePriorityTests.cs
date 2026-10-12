using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class SetQueuePriorityTests
    {
        [Fact]
        public async Task Handle_WaitingTicket_ShouldSetPriorityTrue()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SetQueuePriorityHandler(queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            await handler.Handle(new SetQueuePriorityCommand(queueTicket.Id, true), CancellationToken.None);

            // Assert
            Assert.True(queueTicket.Priority);
        }

        [Fact]
        public async Task Handle_PriorityTicket_ShouldUnsetPriority()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SetQueuePriorityHandler(queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1, priority: true);
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            await handler.Handle(new SetQueuePriorityCommand(queueTicket.Id, false), CancellationToken.None);

            // Assert
            Assert.False(queueTicket.Priority);
        }

        [Fact]
        public async Task Handle_QueueTicketNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SetQueuePriorityHandler(queueTicketRepository, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new SetQueuePriorityCommand(Guid.NewGuid(), true), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AlreadyCalledTicket_ShouldThrowArgumentException()
        {
            // Arrange: ưu tiên chỉ còn ý nghĩa khi vé còn đang chờ, không áp dụng sau khi đã gọi.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SetQueuePriorityHandler(queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(new SetQueuePriorityCommand(queueTicket.Id, true), CancellationToken.None));
        }
    }
}