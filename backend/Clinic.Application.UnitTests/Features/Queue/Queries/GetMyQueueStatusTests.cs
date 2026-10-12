using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Queue.Queries
{
    public class GetMyQueueStatusTests
    {
        [Fact]
        public async Task Handle_PatientWithActiveTicket_ShouldCalculatePositionAndEstimatedWait()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new GetMyQueueStatusHandler(queueTicketRepository, currentUser);

            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            currentUser.PatientId = patient.Id;

            // Doctor's work schedule
            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));

            // Ticket 1: Other patient (Waiting)
            var app1 = TestDataFactory.CreateAppointment(workSchedule: workSchedule);
            var ticket1 = TestDataFactory.CreateQueueTicket(app1, queueNumber: 1);
            await queueTicketRepository.AddAsync(ticket1);

            // Ticket 2: Other patient (Called)
            var app2 = TestDataFactory.CreateAppointment(workSchedule: workSchedule);
            var ticket2 = TestDataFactory.CreateQueueTicket(app2, queueNumber: 2);
            ticket2.Call();
            await queueTicketRepository.AddAsync(ticket2);

            // Ticket 3: Current patient (Waiting)
            var myApp = TestDataFactory.CreateAppointment(workSchedule: workSchedule, patient: patient);
            var myTicket = TestDataFactory.CreateQueueTicket(myApp, queueNumber: 3);
            await queueTicketRepository.AddAsync(myTicket);

            // Act
            var result = await handler.Handle(new GetMyQueueStatusQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(myTicket.Id, result[0].QueueTicketId);
            Assert.Equal(3, result[0].QueueNumber);
            // 2 people ahead -> PositionInQueue is 3, EstimatedWaitMinutes is 2 * 10 = 20
            Assert.Equal(3, result[0].PositionInQueue);
            Assert.Equal(20, result[0].EstimatedWaitMinutes);
        }

        [Fact]
        public async Task Handle_PriorityPatientAhead_ShouldOrderBeforeNormalPatient()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser();
            var handler = new GetMyQueueStatusHandler(queueTicketRepository, currentUser);

            var doctor = TestDataFactory.CreateDoctor();
            var patient = TestDataFactory.CreatePatient();
            currentUser.PatientId = patient.Id;

            var workSchedule = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.Today));

            // Ticket 1: Normal patient (QueueNumber 1)
            var app1 = TestDataFactory.CreateAppointment(workSchedule: workSchedule);
            var ticket1 = TestDataFactory.CreateQueueTicket(app1, queueNumber: 1, priority: false);
            await queueTicketRepository.AddAsync(ticket1);

            // Ticket 2: My Ticket (QueueNumber 2, Priority = true)
            var myApp = TestDataFactory.CreateAppointment(workSchedule: workSchedule, patient: patient);
            var myTicket = TestDataFactory.CreateQueueTicket(myApp, queueNumber: 2, priority: true);
            await queueTicketRepository.AddAsync(myTicket);

            // Act
            var result = await handler.Handle(new GetMyQueueStatusQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            // Since Priority = true, myTicket is ordered FIRST in the queue
            Assert.Equal(1, result[0].PositionInQueue);
            Assert.Equal(0, result[0].EstimatedWaitMinutes);
        }

        [Fact]
        public async Task Handle_PatientWithoutActiveTicketsToday_ShouldReturnEmptyList()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { PatientId = Guid.NewGuid() };
            var handler = new GetMyQueueStatusHandler(queueTicketRepository, currentUser);

            // Act
            var result = await handler.Handle(new GetMyQueueStatusQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_UserNotPatient_ShouldThrowForbiddenException()
        {
            // Arrange
            var queueTicketRepository = new FakeQueueTicketRepository();
            var currentUser = new FakeCurrentUser { PatientId = null };
            var handler = new GetMyQueueStatusHandler(queueTicketRepository, currentUser);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new GetMyQueueStatusQuery(), CancellationToken.None));
        }
    }
}
