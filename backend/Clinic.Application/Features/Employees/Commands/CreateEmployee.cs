using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Employees.Commands
{
    // Use-case: Create a new employee (not doctor)
    public record CreateEmployeeCommand(
        string FullName,
        string PhoneNumber,
        string Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address,
        DateOnly HireDate,
        List<string> Roles,
        Guid? IdentityUserId = null,
        Guid? IdentityPatientId = null
    ) : IRequest<EmployeeSummaryDto>;

    public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, EmployeeSummaryDto>
    {
        private readonly IPersonService _personService;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CreateEmployeeCommandHandler(
            IPersonService personService,
            IPatientRepository patientRepository,
            IEmployeeRepository employeeRepository,
            IUnitOfWork unitOfWork)
        {
            _personService = personService;
            _patientRepository = patientRepository;
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<EmployeeSummaryDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var person = await _personService.CreateOrGetPersonAsync(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address);

            if (request.IdentityUserId is not Guid identityUserId || identityUserId == Guid.Empty)
                throw new ArgumentException("An Identity account ID is required to create an employee profile.");
            person.LinkIdentityUser(identityUserId);
            if (request.Roles == null || !request.Roles.Any())
            {
                throw new ArgumentException("At least one role must be specified.");
            }
            var employee = new Employee(person, request.HireDate);
            if (person.Patient == null)
            {
                var patient = request.IdentityPatientId is Guid patientId
                    ? new Patient(patientId, person.Id, null, null, DateTime.UtcNow, null, false)
                    : new Patient(person, null, null);
                person.Patient = patient;
                await _patientRepository.AddAsync(patient);
            }
            await _employeeRepository.AddAsync(employee);
            await _unitOfWork.SaveChangesAsync();
            return new EmployeeSummaryDto
            {
                Id = employee.Id,
                PatientId = person.Patient?.Id,
                FullName = employee.Person.FullName,
                Email = employee.Person.Email,
                PhoneNumber = employee.Person.PhoneNumber,
                Gender = employee.Person.Gender,
                DateOfBirth = employee.Person.DateOfBirth,
                Status = employee.Status,
                Roles = request.Roles.Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }
    }
}
