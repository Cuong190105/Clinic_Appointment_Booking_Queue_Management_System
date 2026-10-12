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
    public class DeleteScheduleTests
    {
        [Fact]
        public async Task TestDeleteWorkSchedule()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new DeleteWorkScheduleCommandHandler(workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 6, 0, 0);
            var workSchedule = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5);
            await workScheduleRepository.AddWorkScheduleAsync(workSchedule);

            // Act
            var command = new DeleteWorkScheduleCommand(workSchedule.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(workSchedule.Id, result);
            Assert.True(workSchedule.IsDeleted);
        }

        [Fact]
        public async Task TestDeleteNonExistingSchedule()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new DeleteWorkScheduleCommandHandler(workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 6, 0, 0);
            var workSchedule = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            await workScheduleRepository.AddShiftRequestAsync(workSchedule);

            // Act
            var command = new DeleteWorkScheduleCommand(Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });

            workSchedule.Delete(); // Mark the work schedule as deleted
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(new DeleteWorkScheduleCommand(workSchedule.Id), CancellationToken.None);
            });
        }
    }
}
