using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Commands
{
    public class UpdateRequestTests
    {
        [Fact]
        public async Task TestUpdateRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var userRepository = new FakeUserRepository();
            var handler = new UpdateShiftRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            // Act
            var command = new UpdateShiftRequestCommand(shiftRequest.Id, doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 10, "");
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(shiftRequest.Id, result);
            Assert.Equal(10, shiftRequest.PatientLimit);
            Assert.True(shiftRequest.Date > DateOnly.FromDateTime(now.AddDays(1)));
        }

        [Fact]
        public async Task TestUpdateNonExistingRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var userRepository = new FakeUserRepository();
            var handler = new UpdateShiftRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0,0,0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            // Update non-existing request
            var command = new UpdateShiftRequestCommand(Guid.NewGuid(), doctor.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 10, "");
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestUpdateUnauthorizedRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var userRepository = new FakeUserRepository();
            var handler = new UpdateShiftRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var doctor2 = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0,0,0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            await userRepository.AddAsync(doctor2.Employee.Person.User);

            // Update unauthorized request
            var command = new UpdateShiftRequestCommand(shiftRequest.Id, doctor2.Employee.Person.User.Id, DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 10, "");
            await Assert.ThrowsAsync<ForbiddenException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
            var command2 = new UpdateShiftRequestCommand(shiftRequest.Id, Guid.NewGuid(), DateOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2)), TimeOnly.FromDateTime(now.AddDays(2).AddHours(1)), 10, "");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await handler.Handle(command2, CancellationToken.None);
            });
        }
    }
}
