using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin updates an existing work schedule for a doctor
    public record UpdateWorkScheduleCommand(
        Guid Id,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        int PatientLimit
    ) : IRequest<Guid>;
    public class UpdateWorkScheduleCommandHandler : IRequestHandler<UpdateWorkScheduleCommand, Guid>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateWorkScheduleCommandHandler(IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(UpdateWorkScheduleCommand request, CancellationToken cancellationToken)
        {
            var workSchedule = await _workScheduleRepository.GetWorkScheduleByIdAsync(request.Id);
            if (workSchedule == null)
            {
                throw new NotFoundException("Work schedule not found");
            }
            var hasOverlappingSchedules = await _workScheduleRepository.HasOverlappingWorkSchedule(workSchedule.DoctorId, request.Date, request.StartTime, request.EndTime, workSchedule.Id);
            if (hasOverlappingSchedules)
            {
                throw new InvalidOperationException("The updated schedule overlaps with another existing schedule.");
            }
            workSchedule.UpdateShift(request.Date, request.StartTime, request.EndTime, request.PatientLimit);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return workSchedule.Id;
        }
    }
}
