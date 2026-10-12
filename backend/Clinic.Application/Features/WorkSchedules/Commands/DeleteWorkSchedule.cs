using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin deletes a work schedule for a doctor
    public record DeleteWorkScheduleCommand(
        Guid Id
    ) : IRequest<Guid>;
    public class DeleteWorkScheduleCommandHandler : IRequestHandler<DeleteWorkScheduleCommand, Guid>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public DeleteWorkScheduleCommandHandler(IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(DeleteWorkScheduleCommand request, CancellationToken cancellationToken)
        {
            var workSchedule = await _workScheduleRepository.GetWorkScheduleByIdAsync(request.Id);
            if (workSchedule == null)
            {
                throw new NotFoundException("Work schedule not found");
            }
            workSchedule.Delete();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return workSchedule.Id;
        }
    }
}
