using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser? _currentUser;

        public UpdateDoctorCommandHandler(
            IDoctorRepository doctorRepository,
            IPersonRepository personRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            ICurrentUser? currentUser = null)
        {
            _doctorRepository = doctorRepository;
            _personRepository = personRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(UpdateDoctorCommand request, CancellationToken cancellationToken)
        {
            if (request.UserId == null && (_currentUser == null || !_currentUser.IsAuthenticated || _currentUser.DoctorId == null))
            {
                throw new UnauthorizedAccessException();
            }

            var isAdmin = _currentUser?.IsInRole("Admin") == true;

            User? user = null;
            if (request.UserId != null)
            {
                user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null && (_currentUser == null || !_currentUser.IsAuthenticated))
                {
                    throw new UnauthorizedAccessException();
                }
            }

            if (!isAdmin && request.DoctorId != null)
            {
                if (user == null || user.UserRoles.All(ur => ur.Role.Name != "Admin"))
                {
                    throw new ForbiddenException("You are not authorized to update this information.");
                }
            }

            Doctor? doctor = null;
            if (request.DoctorId == null)
            {
                if (_currentUser?.DoctorId.HasValue == true)
                {
                    doctor = await _doctorRepository.GetInfoByIdAsync(_currentUser.DoctorId.Value);
                }
                else
                {
                    user ??= await _userRepository.GetByIdAsync(request.UserId);
                    doctor = user?.Person?.Employee?.Doctor;
                }
            }
            else
            {
                doctor = await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            }

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

            doctor.UpdateInfo(request.LicenseNumber, request.Qualification, request.ExperienceYears, request.Biography);
            doctor.Employee.Person.UpdateAdvancedDetails(request.FullName, request.PhoneNumber, request.Email, request.Gender, request.DateOfBirth, request.Address);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return doctor.Id;
        }
    }
}
