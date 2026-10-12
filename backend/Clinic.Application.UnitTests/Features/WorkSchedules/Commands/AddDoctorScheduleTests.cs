using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.Features.WorkSchedules.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Commands
{
    public class AddDoctorScheduleTests
    {
        [Fact]
        public async Task TestAddDoctorSchedule()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new AddDoctorScheduleCommandHandler(doctorRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0,0,0, DateTimeKind.Utc);
            await doctorRepository.AddAsync(doctor);
            var schedule1 = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), 5);
            doctor.AddWorkSchedule(schedule1);

            await workScheduleRepository.AddWorkScheduleAsync(schedule1);
            var command = new AddDoctorScheduleCommand(
                doctor.Id, DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5);
            var result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(workScheduleRepository.GetWorkScheduleByIdAsync(result.Id));
            Assert.Equal(2, doctor.WorkSchedules.Count);

            // Test overlapping schedule
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var overlappingCommand = new AddDoctorScheduleCommand(
                    doctor.Id, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddMinutes(30)), TimeOnly.FromDateTime(now.AddHours(1).AddMinutes(30)), 5);
                await handler.Handle(overlappingCommand, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestAddNonExistentDoctorSchedule()
        {
            var doctorRepository = new FakeDoctorRepository();
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new AddDoctorScheduleCommandHandler(doctorRepository, workScheduleRepository, unitOfWork);
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var command = new AddDoctorScheduleCommand(
                Guid.NewGuid(), DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(2)), 5);
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }
    }
}
