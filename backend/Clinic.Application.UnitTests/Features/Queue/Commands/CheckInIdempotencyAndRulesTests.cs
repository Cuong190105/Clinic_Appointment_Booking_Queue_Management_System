using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
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
    public class CheckInIdempotencyAndRulesTests
    {
        [Fact]
        public async Task Handle_CheckInWrongDate_ShouldThrowConflictException()
        {
            // Arrange: appointment is scheduled for tomorrow
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: false);
            await appointmentRepository.AddAsync(appointment);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None));

            Assert.Contains("không phải hôm nay", ex.Message);
            Assert.Empty(queueTicketRepository.All);
        }

        [Fact]
        public async Task Handle_RetryCheckInOnAlreadyCheckedInAppointment_ShouldThrowAndNeverDuplicateTicket()
        {
            // Arrange: appointment is scheduled for today and confirmed
            var appointmentRepository = new FakeAppointmentRepository();
            var queueTicketRepository = new FakeQueueTicketRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CheckInHandler(appointmentRepository, queueTicketRepository, unitOfWork);

            var appointment = TestDataFactory.CreateAppointment(confirmed: true, today: true);
            await appointmentRepository.AddAsync(appointment);

            // Act 1: First check-in succeeds
            await handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None);
            Assert.Single(queueTicketRepository.All);
            Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);

            // Act 2: Retry check-in request (e.g. network retry)
            await Assert.ThrowsAnyAsync<Exception>(() =>
                handler.Handle(new CheckInCommand(appointment.Id), CancellationToken.None));

            // Assert: Exactly 1 queue ticket exists, never duplicated
            Assert.Single(queueTicketRepository.All);
        }

        [Fact]
        public async Task CancelAppointment_Within24Hours_PatientCannotCancel()
        {
            // Arrange: shift starts in future but < 24h cancellation limit
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();

            var patient = TestDataFactory.CreatePatient();
            var doctor = TestDataFactory.CreateDoctor();

            var timeConverter = new Clinic.Domain.Common.TimeConverter();
            DateOnly date = default;
            TimeOnly shiftStart = default;
            TimeOnly shiftEnd = default;
            TimeOnly slot = default;

            for (int hours = 2; hours <= 20; hours++)
            {
                var candidateUtc = DateTime.UtcNow.AddHours(hours);
                var candidateLocal = timeConverter.ConvertFromUtc(candidateUtc);
                if (candidateLocal.Hour >= 2 && candidateLocal.Hour <= 20)
                {
                    date = DateOnly.FromDateTime(candidateLocal);
                    shiftStart = new TimeOnly(candidateLocal.Hour - 1, 0);
                    shiftEnd = new TimeOnly(candidateLocal.Hour + 2, 0);
                    slot = new TimeOnly(candidateLocal.Hour, 0);
                    break;
                }
            }

            var ws = new WorkSchedule(doctor, date, shiftStart, shiftEnd, 10);
            var appt = new Appointment(patient, ws, slot, "Checkup", patient.Person.User.Id, false);
            appt.Confirm(patient.Person.User.Id);

            await appointmentRepository.AddAsync(appt);

            var currentUser = new FakeCurrentUser { UserId = patient.Person.User.Id, PatientId = patient.Id };
            var handler = new CancelAppointmentCommandHandler(appointmentRepository, currentUser, unitOfWork);

            var command = new CancelAppointmentCommand(appt.Id);

            // Act & Assert: Patient cancellation blocked within 24 hours of shift
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("cancelled at least 24 hours", ex.Message);
        }
    }
}
