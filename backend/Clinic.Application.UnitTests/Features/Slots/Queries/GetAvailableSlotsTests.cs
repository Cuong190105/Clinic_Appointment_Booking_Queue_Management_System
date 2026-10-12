using Clinic.Application.Features.Slots.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Slots.Queries
{
    public class GetAvailableSlotsTests
    {
        [Fact]
        public async Task Handle_FilterByDoctor_ShouldReturnMatchingSlots()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var handler = new GetAvailableSlotsQueryHandler(workScheduleRepository);

            var doctor1 = TestDataFactory.CreateDoctor();
            var doctor2 = TestDataFactory.CreateDoctor();

            var schedule1 = TestDataFactory.CreateWorkSchedule(doctor: doctor1);
            var schedule2 = TestDataFactory.CreateWorkSchedule(doctor: doctor2);

            workScheduleRepository.Add(schedule1);
            workScheduleRepository.Add(schedule2);

            var query = new GetAvailableSlotsQuery(DoctorId: doctor1.Id);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(doctor1.Id, result[0].DoctorId);
            Assert.Equal(schedule1.Id, result[0].WorkScheduleId);
        }

        [Fact]
        public async Task Handle_FilterByDateRange_ShouldReturnMatchingSlots()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var handler = new GetAvailableSlotsQueryHandler(workScheduleRepository);

            var doctor = TestDataFactory.CreateDoctor();

            var scheduleDay1 = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
            var scheduleDay5 = TestDataFactory.CreateWorkSchedule(doctor: doctor, date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)));

            workScheduleRepository.Add(scheduleDay1);
            workScheduleRepository.Add(scheduleDay5);

            var query = new GetAvailableSlotsQuery(
                FromDate: DateTime.UtcNow.AddDays(1).Date,
                ToDate: DateTime.UtcNow.AddDays(2).Date.AddHours(23));

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(scheduleDay1.Id, result[0].WorkScheduleId);
        }

        [Fact]
        public async Task Handle_CalculatesRemainingCapacityCorrectly()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var handler = new GetAvailableSlotsQueryHandler(workScheduleRepository);

            var workSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 5);
            // 2 active appointments + 1 cancelled appointment
            var active1 = TestDataFactory.CreateAppointment(workSchedule: workSchedule);
            var active2 = TestDataFactory.CreateAppointment(workSchedule: workSchedule);
            var cancelled = TestDataFactory.CreateAppointment(workSchedule: workSchedule);
            cancelled.Cancel(Guid.NewGuid());

            workSchedule.Appointments.Add(active1);
            workSchedule.Appointments.Add(active2);
            workSchedule.Appointments.Add(cancelled);

            workScheduleRepository.Add(workSchedule);

            var query = new GetAvailableSlotsQuery(DoctorId: workSchedule.DoctorId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            // Limit (5) - Active (2) = RemainingCapacity (3)
            Assert.Equal(3, result[0].RemainingCapacity);
        }

        [Fact]
        public async Task Handle_FullyBookedSlots_ShouldBeExcluded()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var handler = new GetAvailableSlotsQueryHandler(workScheduleRepository);

            var fullSchedule = TestDataFactory.CreateWorkSchedule(patientLimit: 1);
            var activeApp = TestDataFactory.CreateAppointment(workSchedule: fullSchedule);
            fullSchedule.Appointments.Add(activeApp);

            workScheduleRepository.Add(fullSchedule);

            var query = new GetAvailableSlotsQuery(DoctorId: fullSchedule.DoctorId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
