using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Employees.Commands
{
    // Use-case: Create a new employee (not doctor)
    public record CreateEmployeeCommand(
        string Username,
        string Password,
        string FullName,
        string PhoneNumber,
        string Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address,
        DateOnly HireDate,
        List<string> Roles
    ) : IRequest<EmployeeSummaryDto>;

    public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, EmployeeSummaryDto>
    {
        private readonly IUserService _userService;
        private readonly IPersonService _personService;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CreateEmployeeCommandHandler(
            IUserService userService,
            IPersonService personService,
            IEmployeeRepository employeeRepository,
            IRoleRepository roleRepository,
            IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _personService = personService;
            _employeeRepository = employeeRepository;
            _roleRepository = roleRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<EmployeeSummaryDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var person = await _personService.CreateOrGetPersonAsync(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address);

            var user = await _userService.CreateUserAsync(request.Username, request.Password, person);
            if (request.Roles == null || !request.Roles.Any())
            {
                throw new ArgumentException("At least one role must be specified.");
            }
            foreach (var roleName in request.Roles)
            {
                Role role = await _roleRepository.GetByNameAsync(roleName.Trim());
                if (role == null)
                {
                    throw new ArgumentException($"Role '{roleName}' does not exist.");
                }
                if (role.Name == "Doctor" || role.Name == "Patient")
                {
                    throw new ArgumentException($"Role '{roleName}' is not allowed in this endpoint. For creating doctor, use POST /doctors instead. For registering an account for patient, use POST /register instead.");
                }
                user.AssignRole(role);
            }
            var employee = new Employee(person, request.HireDate);
            if (person.Patient == null)
            {
                var patient = new Patient(person, null, null);
                var patientRole = await _roleRepository.GetByNameAsync("Patient");
                if (patientRole == null)
                {
                    throw new ArgumentException("Role 'Patient' does not exist.");
                }
                user.AssignRole(patientRole);
                person.Patient = patient;
            }
            await _employeeRepository.AddAsync(employee);
            await _unitOfWork.SaveChangesAsync();
            return new EmployeeSummaryDto
            {
                Id = employee.Id,
                FullName = employee.Person.FullName,
                Email = employee.Person.Email,
                PhoneNumber = employee.Person.PhoneNumber,
                Gender = employee.Person.Gender,
                DateOfBirth = employee.Person.DateOfBirth,
                Status = employee.Status,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
            };
        }
    }
}
