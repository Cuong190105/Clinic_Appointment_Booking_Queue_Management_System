using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Employees.Commands
{
    public record UpdateEmployeeCommand(
        Guid UserId,
        string FullName,
        string PhoneNumber,
        string Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address,
        List<string>? Roles,
        Guid? EmployeeId = null
    ) : IRequest<Guid>;
    public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IPersonRepository _personRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateEmployeeCommandHandler(
            IUserRepository userRepository,
            IPersonRepository personRepository,
            IEmployeeRepository employeeRepository,
            IRoleRepository roleRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _personRepository = personRepository;
            _employeeRepository = employeeRepository;
            _roleRepository = roleRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found.");
            }
            var employee = request.EmployeeId != null ? await _employeeRepository.GetByIdAsync(request.EmployeeId.Value) : user.Person.Employee;
            if (employee == null)
            {
                throw new NotFoundException("Employee not found.");
            }
            if (!user.UserRoles.Any(r => r.Role.Name == "Admin") && request.EmployeeId != null)
            {
                throw new ForbiddenException("You do not have permission to update this employee.");
            }
            if (request.Roles != null && request.Roles.Any() && request.EmployeeId == null)
            {
                throw new ForbiddenException("You do not have permission to assign roles.");
            }
            if (request.Roles != null && !request.Roles.Any())
            {
                throw new ArgumentException("At least one role must be assigned.");
            }
            if (request.PhoneNumber != employee.Person.PhoneNumber && await _personRepository.GetByPhoneNumberAsync(request.PhoneNumber) != null)
            {
                throw new ArgumentException("Phone number already exists.");
            }
            if (request.Email != employee.Person.Email && await _personRepository.GetByEmailAsync(request.Email) != null)
            {
                throw new ArgumentException("Email already exists.");
            }

            // Update the employee's properties
            employee.Person.UpdateAdvancedDetails(request.FullName, request.PhoneNumber, request.Email, request.Gender, request.DateOfBirth, request.Address);

            // Only update roles that are not "Doctor" or "Patient"
            if (request.Roles != null && request.Roles.Count > 0)
            {
                foreach (var roleName in request.Roles)
                {
                    if ((new[] { "Doctor", "Patient" }).Contains(roleName))
                    {
                        continue;
                    } 
                    var role = await _roleRepository.GetByNameAsync(roleName);
                    if (role == null)
                    {
                        throw new ArgumentException($"Role '{roleName}' not found.");
                    }
                    if (!employee.Person.User.UserRoles.Any(r => r.Role.Name == roleName))
                    {
                        employee.Person.User.AssignRole(role);
                    }
                }
                var existingRoles = employee.Person.User!.UserRoles.Select(ur => ur.Role).ToList();
                foreach (var role in existingRoles)
                {
                    if ((new[] { "Doctor", "Patient" }).Contains(role.Name))
                    {
                        continue;
                    }
                    if (!request.Roles.Contains(role.Name))
                    {
                        employee.Person.User.RemoveRole(role);
                    }
                }
            }
            await _unitOfWork.SaveChangesAsync();
            return employee.Id;
        }
    }
}
