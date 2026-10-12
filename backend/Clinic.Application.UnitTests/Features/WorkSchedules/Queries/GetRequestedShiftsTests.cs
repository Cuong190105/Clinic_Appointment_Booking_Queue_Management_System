using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.Features.WorkSchedules.Queries;
using Clinic.Application.Interfaces;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Queries
{
    public class GetRequestedShiftsTests
    {
        [Fact]
        public async Task TestGetRequestedShifts()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetDoctorRequestedShiftsHandler(doctorRepository, workScheduleRepository, userRepository);
            var doctor = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);
            var now = DateTime.UtcNow;
            var schedule1 = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            var schedule2 = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(10)), TimeOnly.FromDateTime(now.AddDays(10)), TimeOnly.FromDateTime(now.AddDays(10).AddHours(1)), 5, "");
            doctor.AddShiftRequest(schedule1);
            doctor.AddShiftRequest(schedule2);
            var admin = TestDataFactory.CreateEmployee();
            await userRepository.AddAsync(admin.Person.User);
            await workScheduleRepository.AddShiftRequestAsync(schedule1);
            await workScheduleRepository.AddShiftRequestAsync(schedule2);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            await userRepository.AddAsync(admin.Person.User);
            var query = new GetDoctorRequestedShiftsQuery(doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now), DateOnly.FromDateTime(now.AddDays(15)));
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.Schedules.Count());

            var query2 = new GetDoctorRequestedShiftsQuery(doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(2)), DateOnly.FromDateTime(now.AddDays(15)));
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.Single(result2.Schedules);

            var query3 = new GetDoctorRequestedShiftsQuery(doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(11)), DateOnly.FromDateTime(now.AddDays(15)));
            var result3 = await handler.Handle(query3, CancellationToken.None);
            Assert.Empty(result3.Schedules);

            var query4 = new GetDoctorRequestedShiftsQuery(doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(-1)), DateOnly.FromDateTime(now.AddDays(40)));
            await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(query4, CancellationToken.None));

            // Admin gets schedules
            var query5 = new GetDoctorRequestedShiftsQuery(admin.Person.User.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), doctor.Id);
            var result5 = await handler.Handle(query5, CancellationToken.None);
            Assert.Equal(2, result5.Schedules.Count());
        }

        [Fact]
        public async Task TestGetRequestedShiftsForNonExistingDoctor()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetDoctorRequestedShiftsHandler(doctorRepository, workScheduleRepository, userRepository);
            var admin = TestDataFactory.CreateEmployee();
            await userRepository.AddAsync(admin.Person.User);
            var now = DateTime.UtcNow;
            var query = new GetDoctorRequestedShiftsQuery(admin.Person.User.Id, DateOnly.FromDateTime(now.AddMinutes(-1)), DateOnly.FromDateTime(now.AddDays(15)), Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(query, CancellationToken.None));
        }

        [Fact]
        public async Task TestUnauthorizedGetRequestedShifts()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetDoctorRequestedShiftsHandler(doctorRepository, workScheduleRepository, userRepository);
            var doctor = TestDataFactory.CreateDoctor();
            var doctor2 = TestDataFactory.CreateDoctor();
            await doctorRepository.AddAsync(doctor);
            await doctorRepository.AddAsync(doctor2);
            var now = DateTime.UtcNow;
            var schedule1 = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), 5, "");
            var schedule2 = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(10)), TimeOnly.FromDateTime(now.AddDays(10)), TimeOnly.FromDateTime(now.AddDays(10).AddHours(1)), 5, "");
            doctor.AddShiftRequest(schedule1);
            doctor.AddShiftRequest(schedule2);
            var receptionist = TestDataFactory.CreateEmployee(role: "Receptionist");
            await userRepository.AddAsync(receptionist.Person.User);
            await workScheduleRepository.AddShiftRequestAsync(schedule1);
            await workScheduleRepository.AddShiftRequestAsync(schedule2);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            await userRepository.AddAsync(doctor2.Employee.Person.User);
            var query = new GetDoctorRequestedShiftsQuery(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), doctor.Id);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(query, CancellationToken.None));

            // Not a doctor
            var query2 = new GetDoctorRequestedShiftsQuery(receptionist.Person.User.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), doctor.Id);
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(query2, CancellationToken.None));

            // Not the same doctor
            var query3 = new GetDoctorRequestedShiftsQuery(doctor2.Employee.Person.User.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), doctor.Id);
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(query3, CancellationToken.None));
        }
    }
}
