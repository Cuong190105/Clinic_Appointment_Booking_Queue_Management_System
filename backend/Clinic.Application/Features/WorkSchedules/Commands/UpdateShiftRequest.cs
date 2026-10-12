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
        int PatientLimit,
        string Reason
    ) : IRequest<Guid>;
    public class UpdateShiftRequestCommandHandler : IRequestHandler<UpdateShiftRequestCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateShiftRequestCommandHandler(IUserRepository userRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(UpdateShiftRequestCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            var shiftRequest = await _workScheduleRepository.GetShiftRequestByIdAsync(request.Id);
            if (shiftRequest == null)
            {
                throw new NotFoundException("Shift request not found");
            }
            if (user.Person.Employee?.Doctor == null || shiftRequest.DoctorId != user.Person.Employee.Doctor.Id)
            {
                throw new ForbiddenException("You are not authorized to update this shift request");
            }
            shiftRequest.UpdateShift(request.Date, request.StartTime, request.EndTime, request.PatientLimit, request.Reason);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shiftRequest.Id;
        }
    }
}
