using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class StartExamTests
    {
        [Fact]
        public async Task Handle_DoctorStartingOwnQueueTicket_ShouldSucceed()
        {
            // Arrange: Doctor không có "queue.start-exam.any" nhưng DoctorId khớp với
            // bác sĩ sở hữu vé (đã được gọi số - Called) -> được phép bắt đầu khám.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            var unitOfWork = new FakeUnitOfWork();
            var handler = new StartExamHandler(queueTicketRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            await handler.Handle(new StartExamCommand(queueTicket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
        }

        [Fact]
        public async Task Handle_UserHasAnyScope_ShouldStartExamOnAnotherDoctorsQueue()
        {
            // Arrange: Admin/Receptionist có "queue.start-exam.any" -> được thao tác trên
            // hàng đợi của bất kỳ bác sĩ nào, kể cả khi không phải bác sĩ đó.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            currentUser.GrantPermission("queue.start-exam.any");
            var unitOfWork = new FakeUnitOfWork();
            var handler = new StartExamHandler(queueTicketRepository, currentUser, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            await handler.Handle(new StartExamCommand(queueTicket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
        }

        [Fact]
        public async Task Handle_DoctorStartingAnotherDoctorsQueueTicket_ShouldThrowForbiddenException()
        {
            // Arrange: Doctor A cố bắt đầu khám cho vé thuộc hàng đợi của Doctor B,
            // không có "queue.start-exam.any" -> phải bị chặn.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var handler = new StartExamHandler(queueTicketRepository, currentUser, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new StartExamCommand(queueTicket.Id), CancellationToken.None));

            // Trạng thái vé không được đổi khi bị chặn.
            Assert.Equal(QueueStatus.Called, queueTicket.Status);
        }

        [Fact]
        public async Task Handle_QueueTicketNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var handler = new StartExamHandler(queueTicketRepository, currentUser, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new StartExamCommand(Guid.NewGuid()), CancellationToken.None));
        }
    }
}