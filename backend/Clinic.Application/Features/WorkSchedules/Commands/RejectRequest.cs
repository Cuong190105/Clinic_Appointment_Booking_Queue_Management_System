using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin rejects a shift request
    public record RejectRequestCommand(
        Guid Id
    ) : IRequest<Guid>;
    public class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, Guid>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public RejectRequestCommandHandler(IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
        {
            var shiftRequest = await _workScheduleRepository.GetShiftRequestByIdAsync(request.Id);
            if (shiftRequest == null)
            {
                throw new NotFoundException("Shift request not found");
            }
            shiftRequest.Reject();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shiftRequest.Id;
        }
    }
}
