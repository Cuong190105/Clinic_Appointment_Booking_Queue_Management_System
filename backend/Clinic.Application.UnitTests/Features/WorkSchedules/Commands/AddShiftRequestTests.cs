using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using MediatR.Wrappers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Commands
{
    public class AddShiftRequestTests
    {
        [Fact]
        public async Task TestAddDoctorShiftRequest()
        {
            var userRepository = new FakeUserRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new AddDoctorShiftRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            await userRepository.AddAsync(doctor.Employee.Person.User);
            var now = DateTime.UtcNow;
            var schedule1 = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            doctor.AddShiftRequest(schedule1);
            await workScheduleRepository.AddShiftRequestAsync(schedule1);

            var command = new AddDoctorShiftRequestCommand(
                doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 5, "");
            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(workScheduleRepository.GetShiftRequestByIdAsync(result.Id));
            Assert.Equal(2, doctor.ShiftRequests.Count);

            // Test duplicate shift request
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var overlappingCommand = new AddDoctorShiftRequestCommand(
                    doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
                await handler.Handle(overlappingCommand, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestAddNonExistentDoctorSchedule()
        {
            var userRepository = new FakeUserRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new AddDoctorShiftRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var now = DateTime.UtcNow;
            var command = new AddDoctorShiftRequestCommand(
                Guid.NewGuid(), DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 5, "");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestNotADoctorAddSchedule()
        {
            var userRepository = new FakeUserRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new AddDoctorShiftRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var employee = TestDataFactory.CreateEmployee();
            await userRepository.AddAsync(employee.Person.User);
            var now = DateTime.UtcNow;
            var command = new AddDoctorShiftRequestCommand(
                employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 5, "");
            await Assert.ThrowsAsync<ForbiddenException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
            var command2 = new AddDoctorShiftRequestCommand(
                Guid.NewGuid(), DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 5, "");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await handler.Handle(command2, CancellationToken.None);
            });
        }
    }
}
