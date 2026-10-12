using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class CompleteExamTests
    {
        [Fact]
        public async Task Handle_DoctorCompletingOwnQueueTicket_ShouldCompleteTicketAndAppointment()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CompleteExamHandler(queueTicketRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            currentUser.UserId = appointment.WorkSchedule.Doctor.Employee.Person.User.Id;
            currentUser.DoctorId = appointment.WorkSchedule.Doctor.Id;
            currentUser.GrantPermission("queue.complete-exam.own");
            queueTicket.Call();
            queueTicket.StartExam();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            await handler.Handle(new CompleteExamCommand(queueTicket.Id), CancellationToken.None);

            // Assert: cả QueueTicket lẫn Appointment đều phải chuyển sang Completed.
            Assert.Equal(QueueStatus.Completed, queueTicket.Status);
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        }

        [Fact]
        public async Task Handle_UserHasAnyScope_ShouldCompleteExamOnAnotherDoctorsQueue()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            currentUser.GrantPermission("queue.complete-exam.any");
            currentUser.UserId = Guid.NewGuid();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CompleteExamHandler(queueTicketRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            queueTicket.StartExam();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            await handler.Handle(new CompleteExamCommand(queueTicket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.Completed, queueTicket.Status);
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        }

        [Fact]
        public async Task Handle_DoctorCompletingAnotherDoctorsQueueTicket_ShouldThrowForbiddenException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CompleteExamHandler(queueTicketRepository, currentUser, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(checkedIn: true);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            queueTicket.Call();
            queueTicket.StartExam();
            await queueTicketRepository.AddAsync(queueTicket);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new CompleteExamCommand(queueTicket.Id), CancellationToken.None));

            // Trạng thái không được đổi khi bị chặn.
            Assert.Equal(QueueStatus.InProgress, queueTicket.Status);
            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
        }

        [Fact]
        public async Task Handle_QueueTicketNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid() };
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CompleteExamHandler(queueTicketRepository, currentUser, unitOfWork);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new CompleteExamCommand(Guid.NewGuid()), CancellationToken.None));
        }
    }
}