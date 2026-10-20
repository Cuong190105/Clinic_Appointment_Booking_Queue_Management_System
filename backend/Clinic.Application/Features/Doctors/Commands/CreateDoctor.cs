using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Doctors.Commands
{
    // Use-case: Create a new doctor
    public record CreateDoctorCommand(
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
        Guid SpecialtyId,
        Guid? IdentityUserId = null,
        Guid? IdentityPatientId = null) : IRequest<DoctorSummaryDto>;
    public class CreateDoctorCommandHandler : IRequestHandler<CreateDoctorCommand, DoctorSummaryDto>
    {
        private readonly IPersonService _personService;
        private readonly IDoctorRepository _doctorRepository;
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CreateDoctorCommandHandler(IPersonService personService, IDoctorRepository doctorRepository, ISpecialtyRepository specialtyRepository, IEmployeeRepository employeeRepository, IPatientRepository patientRepository, IUnitOfWork unitOfWork)
        {
            _personService = personService;
            _doctorRepository = doctorRepository;
            _specialtyRepository = specialtyRepository;
            _unitOfWork = unitOfWork;
            _employeeRepository = employeeRepository;
            _patientRepository = patientRepository;
        }
        public async Task<DoctorSummaryDto> Handle(CreateDoctorCommand request, CancellationToken cancellationToken)
        {
            var person = await _personService.CreateOrGetPersonAsync(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address);
            if (request.IdentityUserId is not Guid identityUserId || identityUserId == Guid.Empty)
                throw new ArgumentException("An Identity account ID is required to create a doctor profile.");
            person.LinkIdentityUser(identityUserId);
            var employee = new Employee(person, request.HireDate);
            if (person.Patient == null)
            {
                var patient = request.IdentityPatientId is Guid patientId
                    ? new Patient(patientId, person.Id, null, null, DateTime.UtcNow, null, false)
                    : new Patient(person, null, null);
                person.Patient = patient;
                await _patientRepository.AddAsync(patient);
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
                EmployeeId = employee.Id,
                PatientId = person.Patient?.Id,
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
