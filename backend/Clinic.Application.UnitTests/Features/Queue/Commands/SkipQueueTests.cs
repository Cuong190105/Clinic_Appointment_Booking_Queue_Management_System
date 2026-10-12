using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
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

            // Assert: vé Waiting bị skip thì bác sĩ chưa hề bận với vé này -> không có
            // hành động "gọi số tiếp theo" nào được tự động thực hiện thay lễ tân.
            Assert.Equal(QueueStatus.Skipped, queueTicket.Status);
            Assert.Null(result.NextCalledTicket);
        }

        [Fact]
        public async Task Handle_CalledTicket_ShouldSkipAndAutoCallNextWaiting()
        {
            // Arrange: bệnh nhân được gọi nhưng không có mặt -> lễ tân bỏ qua lượt. Vé Called
            // đang giữ chỗ bác sĩ, nên sau khi skip, hệ thống phải tự động gọi vé Waiting kế tiếp
            // để "giải phóng bác sĩ và gọi bệnh nhân tiếp theo" đúng như nghiệp vụ yêu cầu.
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
            // Arrange: vé Called bị skip nhưng hàng đợi hiện không còn ai Waiting -> bác sĩ
            // được giải phóng nhưng không có ai để tự động gọi, vẫn phải skip thành công.
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
            // Arrange: vé đang được bác sĩ khám dở -> không được phép "bỏ qua",
            // phải dùng Complete() để kết thúc lượt khám.
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
    }
}