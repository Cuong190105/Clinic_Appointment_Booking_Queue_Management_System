using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.WorkSchedules.Commands
{
    public class ApproveRequestTests
    {
        [Fact]
        public async Task TestApproveRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new ApproveRequestCommandHandler(workScheduleRepository, unitOfWork);
            var doctor = TestDataFactory.CreateDoctor();
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var shiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1)), 5, "");
            var overlappingShiftRequest = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1).AddMinutes(30)), TimeOnly.FromDateTime(now.AddDays(1).AddMinutes(30)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(1).AddMinutes(30)), 5, "");
            //doctor.AddShiftRequest(shiftRequest);
            //doctor.AddShiftRequest(overlappingShiftRequest);
            await workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await workScheduleRepository.AddShiftRequestAsync(overlappingShiftRequest);

            // Act
            var command = new ApproveRequestCommand(shiftRequest.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(shiftRequest.Id, result);
            Assert.Equal(ShiftRequestStatus.Approved, shiftRequest.Status);
            doctor.ShiftRequests.Clear();

            // Overlapping shift request should not be approved
            await Assert.ThrowsAsync<ConflictException>(async () =>
            {
                var overlappingCommand = new ApproveRequestCommand(overlappingShiftRequest.Id);
                await handler.Handle(overlappingCommand, CancellationToken.None);
            });
        }

        [Fact]
        public async Task TestApproveNonExistingRequest()
        {
            // Arrange
            var workScheduleRepository = new FakeWorkScheduleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new ApproveRequestCommandHandler(workScheduleRepository, unitOfWork);

            // Act
            var command = new ApproveRequestCommand(Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await handler.Handle(command, CancellationToken.None);
            });
        }
    }
}
