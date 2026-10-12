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
    public class RejectRequestTests
    {
        [Fact]
        public async Task TestRejectRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new RejectRequestCommandHandler(workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);

            // Act
            var command = new RejectRequestCommand(shiftRequest.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(shiftRequest.Id, result);
            Assert.Equal(ShiftRequestStatus.Rejected, shiftRequest.Status);
        }

        [Fact]
        public async Task TestRejectNonExistingRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new RejectRequestCommandHandler(workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0,0,0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);

            // Act
            var command = new RejectRequestCommand(Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });

            shiftRequest.Delete(); // Mark the shift request as deleted
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(new RejectRequestCommand(shiftRequest.Id), CancellationToken.None);
            });
        }
    }
}
