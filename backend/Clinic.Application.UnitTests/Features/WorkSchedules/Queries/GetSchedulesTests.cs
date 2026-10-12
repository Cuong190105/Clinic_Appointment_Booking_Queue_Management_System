using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.Features.WorkSchedules.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Queries
{
    public class GetSchedulesTests
    {
        [Fact]
        public async Task TestGetSchedules()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var handler = new GetDoctorSchedulesHandler(doctorRepository, workScheduleRepository);
            var doctor = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);
            var now = DateTime.UtcNow;
            var schedule1 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5);
            var schedule2 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(10)), TimeOnly.FromDateTime(now.AddDays(10)), TimeOnly.FromDateTime(now.AddDays(10).AddHours(1)), 5);
            doctor.AddWorkSchedule(schedule1);
            doctor.AddWorkSchedule(schedule2);
            await workScheduleRepository.AddWorkScheduleAsync(schedule1);
            await workScheduleRepository.AddWorkScheduleAsync(schedule2);
            var query = new GetDoctorSchedulesQuery(doctor.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)));
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.Schedules.Count());

            var query2 = new GetDoctorSchedulesQuery(doctor.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)));
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.Single(result2.Schedules);

            var query3 = new GetDoctorSchedulesQuery(doctor.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(11)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)));
            var result3 = await handler.Handle(query3, CancellationToken.None);
            Assert.Empty(result3.Schedules);
            // Test invalid date range
            var query4 = new GetDoctorSchedulesQuery(doctor.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)));
            await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(query4, CancellationToken.None));
        }

        [Fact]
        public async Task TestGetSchedulesForNonExistingDoctor()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var handler = new GetDoctorSchedulesHandler(doctorRepository, workScheduleRepository);
            var query = new GetDoctorSchedulesQuery(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)));
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(query, CancellationToken.None));
        }
    }
}
