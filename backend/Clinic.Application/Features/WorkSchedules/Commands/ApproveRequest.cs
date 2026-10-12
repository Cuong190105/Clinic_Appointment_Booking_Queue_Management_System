using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin approves a shift request
    public record ApproveRequestCommand(
        Guid Id
    ) : IRequest<Guid>;
    public class ApproveRequestCommandHandler : IRequestHandler<ApproveRequestCommand, Guid>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public ApproveRequestCommandHandler(IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
        {
            var shiftRequest = await _workScheduleRepository.GetShiftRequestByIdAsync(request.Id);
            if (shiftRequest == null)
            {
                throw new NotFoundException("Shift request not found");
            }
            if (await _workScheduleRepository.HasOverlappingWorkSchedule(shiftRequest.DoctorId, shiftRequest.Date, shiftRequest.ShiftStart, shiftRequest.ShiftEnd))
            {
                throw new ConflictException("The shift request overlaps with an existing work schedule.");
            }
            var workSchedule = shiftRequest.Approve();
            await _workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shiftRequest.Id;
        }
    }
}

