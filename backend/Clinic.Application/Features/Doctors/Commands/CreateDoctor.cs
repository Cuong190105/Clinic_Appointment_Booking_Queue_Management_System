using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Doctors.Commands
{
    // Use-case: Create a new doctor
    public record CreateDoctorCommand(
        string Username,
        string Password,
        string FullName,
        string PhoneNumber,
        string Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address,
        DateOnly HireDate,
        string LicenseNumber,
        string Qualification,
        string? Biography,
        int ExperienceYears,
        DoctorStatus Status,
        Guid SpecialtyId) : IRequest<DoctorSummaryDto>;
    public class CreateDoctorCommandHandler : IRequestHandler<CreateDoctorCommand, DoctorSummaryDto>
    {
        private readonly IUserService _userService;
        private readonly IPersonService _personService;
        private readonly IDoctorRepository _doctorRepository;
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CreateDoctorCommandHandler(IUserService userService, IPersonService personService, IDoctorRepository doctorRepository, ISpecialtyRepository specialtyRepository, IRoleRepository roleRepository, IEmployeeRepository employeeRepository, IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _personService = personService;
            _doctorRepository = doctorRepository;
            _specialtyRepository = specialtyRepository;
            _roleRepository = roleRepository;
            _unitOfWork = unitOfWork;
            _employeeRepository = employeeRepository;
        }
        public async Task<DoctorSummaryDto> Handle(CreateDoctorCommand request, CancellationToken cancellationToken)
        {
            var person = await _personService.CreateOrGetPersonAsync(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address);
            var user = await _userService.CreateUserAsync(request.Username, request.Password, person);
            var role = await _roleRepository.GetByNameAsync("Doctor");
            if (role == null)
            {
                throw new ArgumentException("Role 'Doctor' does not exist.");
            }
            user.AssignRole(role);
            var employee = new Employee(person, request.HireDate);
            if (person.Patient == null)
            {
                var patient = new Patient(person, null, null);
                var patientRole = await _roleRepository.GetByNameAsync("Patient");
                if (patientRole == null) {
                    throw new ArgumentException("Role 'Patient' does not exist.");
                }
                user.AssignRole(patientRole);
                person.Patient = patient;
            }

            var specialty = await _specialtyRepository.GetByIdAsync(request.SpecialtyId);
            if (specialty == null)
            {
                throw new ArgumentException($"Specialty with ID {request.SpecialtyId} does not exist.");
            }
            var doctor = new Doctor(employee, request.LicenseNumber, request.Qualification, specialty, request.ExperienceYears, request.Biography, request.Status);
            await _employeeRepository.AddAsync(employee);
            await _doctorRepository.AddAsync(doctor);
            await _unitOfWork.SaveChangesAsync();
            return new DoctorSummaryDto
            {
                Id = doctor.Id,
                FullName = person.FullName,
                PhoneNumber = person.PhoneNumber,
                Email = person.Email,
                LicenseNumber = doctor.LicenseNumber,
                Qualification = doctor.Qualification,
                CurrentSpecialty = specialty.Name,
                ExperienceYears = doctor.ExperienceYears,
                Status = doctor.Status
            };
        }
    }
}
