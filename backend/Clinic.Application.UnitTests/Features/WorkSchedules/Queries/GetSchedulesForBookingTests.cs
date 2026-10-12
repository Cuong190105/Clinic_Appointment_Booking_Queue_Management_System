using Clinic.Application.Features.WorkSchedules.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;


namespace Clinic.Application.UnitTests.Features.WorkSchedules.Queries
{
    public class GetSchedulesForBookingTests
    {
        [Fact]
        public async Task TestGetSchedulesForBooking()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorSchedulesForBookingQueryHandler(workScheduleRepository, doctorRepository);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2026, 9, 5, 7, 0, 0, DateTimeKind.Unspecified);
            var schedule1 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), 5);
            var schedule2 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1).AddHours(6)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(2)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(4)), 5);
            await doctorRepository.AddAsync(doctor);
            doctor.AddWorkSchedule(schedule1);
            doctor.AddWorkSchedule(schedule2);
            await workScheduleRepository.AddWorkScheduleAsync(schedule1);
            await workScheduleRepository.AddWorkScheduleAsync(schedule2);
            // Act
            var query = new GetDoctorSchedulesForBookingQuery(doctor.Id, DateOnly.FromDateTime(now.AddDays(1)));
            var result = await handler.Handle(query, CancellationToken.None);
            // Assert
            // 2 schedules, each with 2 hours and 4 slots/hour (15-minute intervals)
            Assert.Equal(2, result.Count);
            foreach (var item in result)
            {
                Assert.Equal(8, item.TimeSlot.Count); // Each schedule has 2 hours * 4 slots/hour = 8 slots
            }
        }

        [Fact]
        public async Task TestGetNonExistingDoctorSchedulesForBooking()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorSchedulesForBookingQueryHandler(workScheduleRepository, doctorRepository);
            var now = new DateTime(2026, 9, 5, 7, 0, 0, DateTimeKind.Unspecified);
            // Act
            var query = new GetDoctorSchedulesForBookingQuery(Guid.NewGuid(), DateOnly.FromDateTime(now.AddDays(1)));
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
        }

        [Fact]
        public async Task TestGetDoctorPastSchedulesForBooking()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var doctorRepository = new FakeDoctorRepository();
            var handler = new GetDoctorSchedulesForBookingQueryHandler(workScheduleRepository, doctorRepository);
            var doctor = TestDataFactory.CreateDoctor();
            DateTime now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var schedule1 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(2)), 5);
            var schedule2 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1).AddHours(6)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(2)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(4)), 5);
            await doctorRepository.AddAsync(doctor);
            doctor.AddWorkSchedule(schedule1);
            doctor.AddWorkSchedule(schedule2);
            await workScheduleRepository.AddWorkScheduleAsync(schedule1);
            await workScheduleRepository.AddWorkScheduleAsync(schedule2);
            // Act
            var query = new GetDoctorSchedulesForBookingQuery(doctor.Id, DateOnly.FromDateTime(now.AddDays(-10)));
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(query, CancellationToken.None));
        }
    }
}
