using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.Queue.Commands
{
    public class QueueRoleMatrixTests
    {
        [Fact]
        public async Task Receptionist_CannotStartExam_ShouldThrowForbiddenException()
        {
            // Arrange
            var queueRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var currentUser = new FakeCurrentUser();
            currentUser.AddRole("Receptionist");

            var handler = new StartExamHandler(queueRepo, currentUser, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appt = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appt);
            ticket.Call();
            await queueRepo.AddAsync(ticket);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new StartExamCommand(ticket.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Receptionist_CannotCompleteExam_ShouldThrowForbiddenException()
        {
            // Arrange
            var queueRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var currentUser = new FakeCurrentUser();
            currentUser.AddRole("Receptionist");

            var handler = new CompleteExamHandler(queueRepo, currentUser, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appt = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appt);
            ticket.Call();
            ticket.StartExam();
            await queueRepo.AddAsync(ticket);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new CompleteExamCommand(ticket.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Doctor_CannotSkipWaitingTicket_ShouldThrowConflictException()
        {
            // Arrange
            var queueRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var doctor = TestDataFactory.CreateDoctor();
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.AddRole("Doctor");

            var handler = new SkipQueueHandler(queueRepo, currentUser, unitOfWork);

            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appt = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appt); // Status is Waiting
            await queueRepo.AddAsync(ticket);

            // Act & Assert: Doctor can only skip ticket that is Called, not Waiting
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new SkipQueueCommand(ticket.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Doctor_CanSkipCalledTicket_ShouldSucceed()
        {
            // Arrange
            var queueRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var doctor = TestDataFactory.CreateDoctor();
            var currentUser = new FakeCurrentUser { DoctorId = doctor.Id };
            currentUser.AddRole("Doctor");

            var handler = new SkipQueueHandler(queueRepo, currentUser, unitOfWork);

            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appt = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appt);
            ticket.Call(); // Status is Called
            await queueRepo.AddAsync(ticket);

            // Act: Doctor skips the called ticket
            var result = await handler.Handle(new SkipQueueCommand(ticket.Id), CancellationToken.None);

            // Assert
            Assert.Equal("Skipped", result.SkippedTicket.Status);
            Assert.Equal(QueueStatus.Skipped, ticket.Status);
        }

        [Fact]
        public async Task Receptionist_CanRetrySkippedTicket_ShouldTransitionBackToWaiting()
        {
            // Arrange
            var queueRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var currentUser = new FakeCurrentUser();
            currentUser.AddRole("Receptionist");

            var handler = new RetrySkippedQueueHandler(queueRepo, currentUser, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));
            var appt = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket = TestDataFactory.CreateQueueTicket(appt);
            ticket.Call();
            ticket.Skip(); // Ticket is Skipped
            await queueRepo.AddAsync(ticket);

            // Act: Receptionist manually retries the skipped ticket
            var result = await handler.Handle(new RetrySkippedQueueCommand(ticket.Id), CancellationToken.None);

            // Assert
            Assert.Equal(QueueStatus.Waiting, ticket.Status);
            Assert.Equal("Waiting", result.Status);
        }

        [Fact]
        public async Task Doctor_CanCallNextSequentially_WithoutCompletingPreviousTicket()
        {
            // Arrange
            var queueRepo = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CallNextQueueHandler(queueRepo, unitOfWork);

            var doctor = TestDataFactory.CreateDoctor();
            var ws = TestDataFactory.CreateWorkSchedule(doctor, date: DateOnly.FromDateTime(DateTime.Today));

            var appt1 = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket1 = TestDataFactory.CreateQueueTicket(appt1, queueNumber: 1);
            ticket1.Call(); // Ticket 1 is Called
            await queueRepo.AddAsync(ticket1);

            var appt2 = TestDataFactory.CreateAppointment(confirmed: true, workSchedule: ws);
            var ticket2 = TestDataFactory.CreateQueueTicket(appt2, queueNumber: 2);
            await queueRepo.AddAsync(ticket2);

            // Act: Doctor calls next without completing ticket 1 first
            var result = await handler.Handle(new CallNextQueueCommand(doctor.Id), CancellationToken.None);

            // Assert: Sequential calling is permitted
            Assert.Equal(2, result.QueueNumber);
            Assert.Equal(QueueStatus.Called, ticket2.Status);
        }
    }
}
