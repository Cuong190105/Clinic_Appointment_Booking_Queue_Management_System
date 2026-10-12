using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Patients.Commands
{
    public record UpdatePatientCommand(
        Guid UserId,
        string? Email,
        Gender Gender,
        string Address,
        string? InsuranceNumber,
        string? EmergencyContact,
        Guid? PatientId = null
    ) : IRequest<Guid>;
    public class UpdatePatientCommandHandler : IRequestHandler<UpdatePatientCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePatientCommandHandler(IUserRepository userRepository, IPatientRepository patientRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _patientRepository = patientRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found.");
            }
            if (!user.UserRoles.Any(ur => ur.Role.Name == "Receptionist") && request.PatientId != null)
            {
                throw new ForbiddenException("You are not authorized to update patient information.");
            }
            var patient = request.PatientId != null ? await _patientRepository.GetByIdAsync(request.PatientId) : user.Person.Patient;
            if (patient == null)
            {
                throw new NotFoundException("Patient not found.");
            }
            patient.Person.UpdateDetails(
                request.Email,
                request.Gender,
                request.Address
            );
            patient.Update(request.InsuranceNumber, request.EmergencyContact);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return patient.Id;
        }
    }
}
