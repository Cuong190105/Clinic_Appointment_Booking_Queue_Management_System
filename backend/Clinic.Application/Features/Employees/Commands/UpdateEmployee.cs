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
        private readonly ICurrentUser _currentUser;
        private readonly IPersonRepository _personRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateEmployeeCommandHandler(
            ICurrentUser currentUser,
            IPersonRepository personRepository,
            IEmployeeRepository employeeRepository,
            IUnitOfWork unitOfWork)
        {
            _currentUser = currentUser;
            _personRepository = personRepository;
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId != request.UserId)
            {
                throw new UnauthorizedAccessException("User not found.");
            }
            var employeeId = request.EmployeeId ?? _currentUser.EmployeeId;
            var employee = employeeId == null ? null : await _employeeRepository.GetByIdAsync(employeeId.Value);
            if (employee == null)
            {
                throw new NotFoundException("Employee not found.");
            }
            if (!_currentUser.HasRole("Admin") && request.EmployeeId != null)
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

            // Role updates are sent to Identity by the API layer after the profile save.
            await _unitOfWork.SaveChangesAsync();
            return employee.Id;
        }
    }
}
