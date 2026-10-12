using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Queue.Queries
{
    public class GetQueueByDoctorTests
    {
        [Fact]
        public async Task Handle_UserHasQueueViewPermission_ShouldReturnAnyDoctorsQueue()
        {
            // Arrange: Receptionist/Admin có "queue.view" -> xem được hàng đợi của bác sĩ bất kỳ,
            // kể cả khi không phải bác sĩ đó (DoctorId của current user khác/null).
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            currentUser.GrantPermission("queue.view");
            var handler = new GetQueueByDoctorHandler(queueTicketRepository, currentUser);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            var result = await handler.Handle(new GetQueueByDoctorQuery(doctor.Id), CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal(queueTicket.Id, result[0].Id);
            Assert.Equal(queueTicket.QueueNumber, result[0].QueueNumber);
        }

        [Fact]
        public async Task Handle_DoctorViewingOwnQueue_ShouldSucceed()
        {
            // Arrange: Doctor không có "queue.view" nhưng DoctorId khớp với bác sĩ được truy vấn.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            var handler = new GetQueueByDoctorHandler(queueTicketRepository, currentUser);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var queueTicket = TestDataFactory.CreateQueueTicket(appointment, queueNumber: 1);
            await queueTicketRepository.AddAsync(queueTicket);

            // Act
            var result = await handler.Handle(new GetQueueByDoctorQuery(doctor.Id), CancellationToken.None);

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public async Task Handle_DoctorViewingAnotherDoctorsQueue_ShouldThrowForbiddenException()
        {
            // Arrange: Doctor A cố xem hàng đợi của Doctor B, không có "queue.view" -> bị chặn.
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { DoctorId = Guid.NewGuid() };
            var handler = new GetQueueByDoctorHandler(queueTicketRepository, currentUser);

            var otherDoctorId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new GetQueueByDoctorQuery(otherDoctorId), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ResultsShouldBeOrderedByPriorityThenQueueNumber()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            currentUser.GrantPermission("queue.view");
            var handler = new GetQueueByDoctorHandler(queueTicketRepository, currentUser);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));

            var ticket1 = TestDataFactory.CreateQueueTicket(
                TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws), queueNumber: 1);
            var ticket2Priority = TestDataFactory.CreateQueueTicket(
                TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws), queueNumber: 2, priority: true);
            var ticket3 = TestDataFactory.CreateQueueTicket(
                TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws), queueNumber: 3);

            await queueTicketRepository.AddAsync(ticket1);
            await queueTicketRepository.AddAsync(ticket2Priority);
            await queueTicketRepository.AddAsync(ticket3);

            // Act
            var result = await handler.Handle(new GetQueueByDoctorQuery(doctor.Id), CancellationToken.None);

            // Assert: vé ưu tiên (số 2) phải đứng đầu, dù số thứ tự không nhỏ nhất.
            Assert.Equal(3, result.Count);
            Assert.Equal(ticket2Priority.Id, result[0].Id);
            Assert.Equal(ticket1.Id, result[1].Id);
            Assert.Equal(ticket3.Id, result[2].Id);
        }
    }
}