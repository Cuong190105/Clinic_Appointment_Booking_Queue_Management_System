using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly ICurrentUser? _currentUser;

        public UpdatePatientCommandHandler(
            IUserRepository userRepository,
            IPatientRepository patientRepository,
            IUnitOfWork unitOfWork,
            ICurrentUser? currentUser = null)
        {
            _userRepository = userRepository;
            _patientRepository = patientRepository;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
        {
            var isStaff = _currentUser != null && (_currentUser.IsInRole("Receptionist") || _currentUser.IsInRole("Admin"));

            User? user = null;
            if (request.PatientId != null && !isStaff)
            {
                user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    throw new UnauthorizedAccessException("User not found.");
                }
                if (!user.UserRoles.Any(ur => ur.Role.Name == "Receptionist" || ur.Role.Name == "Admin"))
                {
                    throw new ForbiddenException("You are not authorized to update patient information.");
                }
            }

            Patient? patient = null;
            if (request.PatientId != null)
            {
                patient = await _patientRepository.GetByIdAsync(request.PatientId);
            }
            else
            {
                if (_currentUser?.PatientId.HasValue == true)
                {
                    patient = await _patientRepository.GetByIdAsync(_currentUser.PatientId.Value);
                }
                else
                {
                    user ??= await _userRepository.GetByIdAsync(request.UserId);
                    patient = user?.Person?.Patient;
                }
            }

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
