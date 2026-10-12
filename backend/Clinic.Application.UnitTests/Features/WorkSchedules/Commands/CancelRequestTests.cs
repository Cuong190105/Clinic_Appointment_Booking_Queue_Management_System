using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Commands
{
    public class CancelRequestTests
    {
        [Fact]
        public async Task TestCancelRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var userRepository = new FakeUserRepository();
            var handler = new CancelRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            // Act
            var command = new CancelRequestCommand(shiftRequest.Id, doctor.Employee.Person.User.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(shiftRequest.Id, result);
            Assert.Equal(ShiftRequestStatus.Cancelled, shiftRequest.Status);

            // Cancel already cancelled request
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestCancelNonExistingRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var userRepository = new FakeUserRepository();
            var handler = new CancelRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddHours(7).AddDays(1)), TimeOnly.FromDateTime(now.AddHours(7).AddDays(1)), TimeOnly.FromDateTime(now.AddHours(7).AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            // Cancel non-existing request
            var command = new CancelRequestCommand(Guid.NewGuid(), doctor.Employee.Person.User.Id);
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestCancelUnauthorizedRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var userRepository = new FakeUserRepository();
            var handler = new CancelRequestCommandHandler(userRepository, workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var doctor2 = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddHours(7).AddDays(1)), TimeOnly.FromDateTime(now.AddHours(7).AddDays(1)), TimeOnly.FromDateTime(now.AddHours(7).AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await userRepository.AddAsync(doctor.Employee.Person.User);
            await userRepository.AddAsync(doctor2.Employee.Person.User);
            // Cancel unauthorized request
            var command = new CancelRequestCommand(shiftRequest.Id, doctor2.Employee.Person.User.Id);
            await Assert.ThrowsAsync<ForbiddenException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }
    }
}
