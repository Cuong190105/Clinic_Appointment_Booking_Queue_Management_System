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
        private readonly IPersonRepository _personRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CancelRequestCommandHandler(IPersonRepository personRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _personRepository = personRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(CancelRequestCommand request, CancellationToken cancellationToken)
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
                throw new ForbiddenException("You are not authorized to cancel this shift request");
            }
            shiftRequest.Cancel();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return shiftRequest.Id;
        }
    }
}
