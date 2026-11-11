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

        [Fact]
        public async Task Handle_ConcurrentRequestsWithSameIdempotencyKey_ShouldAllSucceedAndReturnIdenticalAppointment()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var notiQueue = new FakeNotificationQueue();

            var patient = TestDataFactory.CreatePatient();
            await patientRepository.AddAsync(patient);

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 10);
            workScheduleRepository.Add(workSchedule);

            var idempotencyKey = "concurrent-idemp-uuid-" + Guid.NewGuid();
            var timeSlot = workSchedule.ShiftStart.AddMinutes(15);
            var command = new CreateAppointmentCommand(
                patient.Person.User.Id,
                workSchedule.Id,
                timeSlot,
                "Khám đồng thời cùng key",
                false,
                patient.Id,
                idempotencyKey
            );

            // Act: 10 parallel tasks sending the exact same idempotency key concurrently
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
            {
                var unitOfWork = new FakeUnitOfWork();
                var handler = new CreateAppointmentCommandHandler(
                    workScheduleRepository,
                    patientRepository,
                    appointmentRepository,
                    unitOfWork,
                    notiQueue
                );
                return await handler.Handle(command, CancellationToken.None);
            }));

            var results = await Task.WhenAll(tasks);

            // Assert: All 10 tasks succeed and return the exact same appointment ID
            Assert.Equal(10, results.Length);
            var firstId = results[0].Id;
            Assert.All(results, r => Assert.Equal(firstId, r.Id));
            Assert.Single(workSchedule.Appointments);
        }

        [Fact]
        public async Task Handle_ConcurrentRequestsForSameSlot_ShouldAllowOnlyOneWinnerAndRejectOthers()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var notiQueue = new FakeNotificationQueue();

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 10);
            workScheduleRepository.Add(workSchedule);

            var targetSlot = workSchedule.ShiftStart.AddMinutes(30);

            // Create 10 different patients competing for the same slot
            var patients = new List<Patient>();
            for (int i = 0; i < 10; i++)
            {
                var p = TestDataFactory.CreatePatient();
                await patientRepository.AddAsync(p);
                patients.Add(p);
            }

            int successCount = 0;
            int conflictCount = 0;
            var barrier = new System.Threading.ManualResetEventSlim(false);

            // Act: 10 parallel tasks simultaneously attempt to book the exact same slot
            var tasks = patients.Select(p => Task.Run(async () =>
            {
                var unitOfWork = new FakeUnitOfWork();
                var handler = new CreateAppointmentCommandHandler(
                    workScheduleRepository,
                    patientRepository,
                    appointmentRepository,
                    unitOfWork,
                    notiQueue
                );

                var cmd = new CreateAppointmentCommand(
                    p.Person.User.Id,
                    workSchedule.Id,
                    targetSlot,
                    "Cạnh tranh slot",
                    false,
                    p.Id,
                    Guid.NewGuid().ToString()
                );

                barrier.Wait();
                try
                {
                    await handler.Handle(cmd, CancellationToken.None);
                    Interlocked.Increment(ref successCount);
                }
                catch (ConflictException)
                {
                    Interlocked.Increment(ref conflictCount);
                }
            })).ToArray();

            barrier.Set();
            await Task.WhenAll(tasks);

            // Assert: Exactly 1 winner gets the slot, the other 9 receive ConflictException (409)
            Assert.Equal(1, successCount);
            Assert.Equal(9, conflictCount);
            Assert.Single(workSchedule.Appointments);
        }

        [Fact]
        public async Task CancelAppointment_WhenScheduleWasFull_ShouldRevertScheduleToActiveAndAllowNewBooking()
        {
            // Arrange: Schedule with RecommendedCapacity = 2
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

            // Book 2 online slots -> Schedule becomes Full
            var cmd1 = new CreateAppointmentCommand(p1.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart, "Book 1", false, p1.Id);
            var cmd2 = new CreateAppointmentCommand(p2.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart.AddMinutes(15), "Book 2", false, p2.Id);
            var res1 = await handler.Handle(cmd1, CancellationToken.None);
            await handler.Handle(cmd2, CancellationToken.None);

            Assert.Equal(WorkScheduleStatus.Full, workSchedule.Status);

            // Attempting to book a 3rd online slot fails with ConflictException
            var cmd3 = new CreateAppointmentCommand(p3.Person.User.Id, workSchedule.Id, workSchedule.ShiftStart.AddMinutes(30), "Book 3", false, p3.Id);
            await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd3, CancellationToken.None));

            // Act: Cancel appointment 1 (using AdminCancel to bypass 24h limit)
            var appt1 = workSchedule.Appointments.First(a => a.Id == res1.Id);
            appt1.AdminCancel(Guid.NewGuid());

            // Assert: Schedule status automatically reverted to Active
            Assert.Equal(WorkScheduleStatus.Active, workSchedule.Status);

            // Now 3rd booking succeeds!
            var res3 = await handler.Handle(cmd3, CancellationToken.None);
            Assert.NotNull(res3);
            Assert.Equal(WorkScheduleStatus.Full, workSchedule.Status);
        }
    }
}
