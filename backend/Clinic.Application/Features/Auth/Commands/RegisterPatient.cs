using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Application.Services;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands
{
    public record RegisterCommand(
        string Username,
        string Password,
        string FullName,
        string PhoneNumber,
        string? Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address
    ) : IRequest<Guid>;
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Guid>
    {
        private readonly IUserService _userService;
        private readonly IPersonService _personService;
        private readonly IRoleRepository _roleRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IUnitOfWork _unitOfWork;

        public RegisterCommandHandler(
            IUserService userService,
            IPersonService personService,
            IRoleRepository roleRepository,
            IPatientRepository patientRepository,
            IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _personService = personService;
            _roleRepository = roleRepository;
            _patientRepository = patientRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(RegisterCommand command, CancellationToken cancellationToken)
        {
            // Phone number is required for patient registration via mobile app
            if (string.IsNullOrWhiteSpace(command.PhoneNumber))
            {
                throw new ArgumentException("Phone number is required.");
            }

            // Create a new person entity
            var newPerson = await _personService.CreateOrGetPersonAsync(command.FullName, command.PhoneNumber, command.Email, command.DateOfBirth, command.Gender, command.Address);

            // Create a new user entity and associate it with the person
            var existingUser = await _userService.CreateUserAsync(command.Username, command.Password, newPerson);

            // Assign the "Patient" role to the new user
            var patientRole = await _roleRepository.GetByNameAsync("Patient");
            if (patientRole == null)
            {
                throw new NotFoundException("Role 'Patient' not found.");
            }
            existingUser.AssignRole(patientRole);

            // Tạo hồ sơ Patient gắn với Person vừa tạo/tìm được. CreateOrGetPersonAsync có thể trả về
            // 1 Person đã tồn tại từ trước (khách vãng lai từng khám nhưng chưa có tài khoản) - trường hợp
            // đó Person có thể đã có Patient, nên phải kiểm tra tồn tại trước để tránh vi phạm UNIQUE (PersonId).
            if (newPerson.Patient is null)
            {
                var newPatient = new Patient(newPerson, null, null);
                await _patientRepository.AddAsync(newPatient);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return existingUser.Id;
        }
    }
}