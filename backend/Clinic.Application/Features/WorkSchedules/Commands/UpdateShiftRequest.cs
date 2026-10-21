using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Doctor updates an existing shift request
    public record UpdateShiftRequestCommand(
        Guid Id,
        Guid? UserId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        int RecommendedCapacity,
        string Reason
    ) : IRequest<Guid>;
    public class UpdateShiftRequestCommandHandler : IRequestHandler<UpdateShiftRequestCommand, Guid>
    {
        private readonly IPersonRepository _personRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateShiftRequestCommandHandler(IPersonRepository personRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _personRepository = personRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(UpdateShiftRequestCommand request, CancellationToken cancellationToken)
        {
            var person = request.UserId is Guid userId ? await _personRepository.GetByIdentityUserIdAsync(userId) : null;
            if (person == null)
            {
                throw new UnauthorizedAccessException();
            }
            var shiftRequest = await _workScheduleRepository.GetShiftRequestByIdAsync(request.Id);
            if (shiftRequest == null)
            {
                throw new NotFoundException("Shift request not found");
            }
            if (person.Employee?.Doctor == null || shiftRequest.DoctorId != person.Employee.Doctor.Id)
            {
                throw new ForbiddenException("You are not authorized to update this shift request");
            }
            shiftRequest.UpdateShift(request.Date, request.StartTime, request.EndTime, request.RecommendedCapacity, request.Reason);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shiftRequest.Id;
        }
    }
}
