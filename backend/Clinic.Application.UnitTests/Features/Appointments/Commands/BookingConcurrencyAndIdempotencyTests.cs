using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class BookingConcurrencyAndIdempotencyTests
    {
        [Fact]
        public async Task Handle_SameIdempotencyKeyTwice_ShouldReturnOriginalAppointmentWithoutDuplication()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();

            var handler = new CreateAppointmentCommandHandler(
                workScheduleRepository,
                patientRepository,
                appointmentRepository,
                unitOfWork,
                notiQueue
            );

            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 10);
            workScheduleRepository.Add(workSchedule);

            var idempotencyKey = "client-req-uuid-12345";
            var timeSlot = workSchedule.ShiftStart.AddMinutes(15);
            var command = new CreateAppointmentCommand(
                patient.Person.User.Id,
                workSchedule.Id,
                timeSlot,
                "Khám định kỳ",
                false,
                patient.Id,
                idempotencyKey
            );

            // Act: First request
            var firstResult = await handler.Handle(command, CancellationToken.None);

            // Act: Retry request with the exact same idempotency key
            var secondResult = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(firstResult.Id, secondResult.Id);
            Assert.Equal(1, workSchedule.Appointments.Count);
        }

        [Fact]
        public async Task Handle_OnlineBookingExceedingRecommendedCapacity_ShouldThrowConflictException()
        {
            // Arrange: RecommendedCapacity = 2
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();

            var handler = new CreateAppointmentCommandHandler(
                workScheduleRepository,
                patientRepository,
                appointmentRepository,
                unitOfWork,
                notiQueue
            );

            var p1 = TestDataFactory.CreatePatient();
            var p2 = TestDataFactory.CreatePatient();
            var p3 = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(p1);
            await patientRepository.AddAsync(p2);
            await patientRepository.AddAsync(p3);

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 2);
            workScheduleRepository.Add(workSchedule);

            // Book 2 online slots
            var cmd1 = new CreateAppointmentCommand(p1.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart, "Book 1", false, p1.Id);
            var cmd2 = new CreateAppointmentCommand(p2.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart.AddMinutes(15), "Book 2", false, p2.Id);
            await handler.Handle(cmd1, CancellationToken.None);
            await handler.Handle(cmd2, CancellationToken.None);

            // Act & Assert: 3rd online booking exceeds recommended capacity
            var cmd3 = new CreateAppointmentCommand(p3.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "Book 3", false, p3.Id);
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd3, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WalkInBookingCanExceedRecommendedCapacity_AndDoesNotHoldOnlineSlot()
        {
            // Arrange: RecommendedCapacity = 1
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var unitOfWork = new FakeUnitOfWork();
            var notiQueue = new FakeNotificationQueue();

            var handler = new CreateAppointmentCommandHandler(
                workScheduleRepository,
                patientRepository,
                appointmentRepository,
                unitOfWork,
                notiQueue
            );

            var p1 = TestDataFactory.CreatePatient();
            var p2 = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(p1);
            await patientRepository.AddAsync(p2);

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 1);
            workScheduleRepository.Add(workSchedule);

            // 1 walk-in appointment by receptionist
            var walkInCmd = new CreateAppointmentCommand(Guid.NewGuid(), workSchedule.Id, workSchedule.ShiftStart, "Walk-in", true, p1.Id);
            var walkInResult = await handler.Handle(walkInCmd, CancellationToken.None);

            // Online slot is still available because walk-in does not occupy online capacity
            var onlineCmd = new CreateAppointmentCommand(p2.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart.AddMinutes(15), "Online book", false, p2.Id);
            var onlineResult = await handler.Handle(onlineCmd, CancellationToken.None);

            // Assert
            Assert.NotNull(walkInResult);
            Assert.NotNull(onlineResult);
            Assert.Equal(2, workSchedule.Appointments.Count);
        }
    }
}
