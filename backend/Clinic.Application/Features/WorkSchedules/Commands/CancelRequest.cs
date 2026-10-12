using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Doctor cancels a shift request
    public record CancelRequestCommand(
        Guid Id,
        Guid? UserId
    ) : IRequest<Guid>;
    public class CancelRequestCommandHandler : IRequestHandler<CancelRequestCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CancelRequestCommandHandler(IUserRepository userRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(CancelRequestCommand request, CancellationToken cancellationToken)
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
            if (shiftRequest.DoctorId != user.Person.Employee.Doctor.Id)
            {
                throw new ForbiddenException("You are not authorized to cancel this shift request");
            }
            shiftRequest.Cancel();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shiftRequest.Id;
        }
    }
}
