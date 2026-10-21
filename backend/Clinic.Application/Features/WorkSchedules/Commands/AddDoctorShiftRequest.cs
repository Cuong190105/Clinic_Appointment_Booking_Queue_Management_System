using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Doctor adds a new shift request
    public record AddDoctorShiftRequestCommand(Guid? UserId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int RecommendedCapacity, string reason) : IRequest<RequestedShiftDto>;

    public class AddDoctorShiftRequestCommandHandler : IRequestHandler<AddDoctorShiftRequestCommand, RequestedShiftDto>
    {
        private readonly IPersonRepository _personRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public AddDoctorShiftRequestCommandHandler(IPersonRepository personRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _personRepository = personRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<RequestedShiftDto> Handle(AddDoctorShiftRequestCommand request, CancellationToken cancellationToken)
        {
            var person = request.UserId is Guid userId ? await _personRepository.GetByIdentityUserIdAsync(userId) : null;
            if (person == null)
            {
                throw new UnauthorizedAccessException("User not found.");
            }
            var doctor = person.Employee?.Doctor;
            if (doctor == null)
            {
                throw new ForbiddenException("The user is not a doctor and cannot add shift requests.");
            }
            if (await _workScheduleRepository.HasDuplicateShiftRequest(doctor.Id, request.Date, request.StartTime, request.EndTime))
            {
                throw new InvalidOperationException("The doctor has a duplicate shift request in the specified time range.");
            }
            var shiftRequest = new ShiftRequest
            (
                doctor: doctor,
                date: request.Date,
                shiftStart: request.StartTime,
                shiftEnd: request.EndTime,
                RecommendedCapacity: request.RecommendedCapacity,
                reason: request.reason
            );
            doctor.AddShiftRequest(shiftRequest);
            await _workScheduleRepository.AddShiftRequestAsync(shiftRequest);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new RequestedShiftDto
            {
                Id = shiftRequest.Id,
                DoctorId = doctor.Id,
                Date = request.Date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                RecommendedCapacity = request.RecommendedCapacity,
                Reason = request.reason,
                Status = shiftRequest.Status
            };
        }
    }
}
