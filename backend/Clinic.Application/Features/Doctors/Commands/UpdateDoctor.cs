using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Doctors.Commands
{
    // Use-case: Update an existing doctor
    public record UpdateDoctorCommand(
        Guid? UserId,
        string FullName,
        string PhoneNumber,
        string Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address,
        string LicenseNumber,
        string Qualification,
        int ExperienceYears,
        string? Biography,
        Guid? DoctorId = null) : IRequest<Guid>;
    public class UpdateDoctorCommandHandler : IRequestHandler<UpdateDoctorCommand, Guid>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IPersonRepository _personRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateDoctorCommandHandler(IDoctorRepository doctorRepository, IPersonRepository personRepository, ICurrentUser currentUser, IUnitOfWork unitOfWork)
        {
            _doctorRepository = doctorRepository;
            _personRepository = personRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(UpdateDoctorCommand request, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId != request.UserId || request.UserId == null)
            {
                throw new UnauthorizedAccessException();
            }
            if (!_currentUser.HasRole("Admin") && request.DoctorId != null)
            {
                throw new ForbiddenException("You are not authorized to update this information.");
            }
            var doctor = request.DoctorId == null
                ? (_currentUser.DoctorId == null ? null : await _doctorRepository.GetInfoByIdAsync(_currentUser.DoctorId.Value))
                : await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new NotFoundException("Doctor not found.");
            }
            if (request.PhoneNumber != doctor.Employee.Person.PhoneNumber && await _personRepository.GetByPhoneNumberAsync(request.PhoneNumber) != null)
            {
                throw new ArgumentException("Phone number already exists.");
            }
            if (request.Email != doctor.Employee.Person.Email && await _personRepository.GetByEmailAsync(request.Email) != null)
            {
                throw new ArgumentException("Email already exists.");
            }
            // Update the doctor's properties
            doctor.UpdateInfo(request.LicenseNumber, request.Qualification, request.ExperienceYears, request.Biography);

            doctor.Employee.Person.UpdateAdvancedDetails(request.FullName, request.PhoneNumber, request.Email, request.Gender, request.DateOfBirth, request.Address);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return doctor.Id;
        }
    }
}
