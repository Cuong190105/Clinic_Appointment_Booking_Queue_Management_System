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
        private readonly ICurrentUser _currentUser;
        private readonly IPatientRepository _patientRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePatientCommandHandler(ICurrentUser currentUser, IPatientRepository patientRepository, IUnitOfWork unitOfWork)
        {
            _currentUser = currentUser;
            _patientRepository = patientRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId != request.UserId)
            {
                throw new UnauthorizedAccessException("User not found.");
            }
            if (!_currentUser.HasRole("Receptionist") && request.PatientId != null && request.PatientId != _currentUser.PatientId)
            {
                throw new ForbiddenException("You are not authorized to update patient information.");
            }
            var patientId = request.PatientId ?? _currentUser.PatientId;
            var patient = patientId != null ? await _patientRepository.GetByIdAsync(patientId.Value) : null;
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
